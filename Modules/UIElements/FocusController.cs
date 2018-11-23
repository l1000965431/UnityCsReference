// Unity C# reference source
// Copyright (c) Unity Technologies. For terms of use, see
// https://unity3d.com/legal/licenses/Unity_Reference_Only_License

using System;
using System.Collections.Generic;

namespace UnityEngine.UIElements
{
    public abstract class Focusable : CallbackEventHandler
    {
        protected Focusable()
        {
            m_Focusable = true;
            m_TabIndex = 0;
        }

        public abstract FocusController focusController { get; }

        bool m_Focusable;
        public virtual bool focusable
        {
            get { return m_Focusable;}
            set { m_Focusable = value; }
        }

        int m_TabIndex;
        // See http://w3c.github.io/html/editing.html#the-tabindex-attribute
        public virtual int tabIndex
        {
            get { return m_TabIndex;}
            set { m_TabIndex = value; }
        }

        public virtual bool canGrabFocus
        {
            get { return focusable; }
        }

        public virtual void Focus()
        {
            if (focusController != null)
            {
                focusController.SwitchFocus(canGrabFocus ? this : null);
            }
        }

        public virtual void Blur()
        {
            if (focusController != null && focusController.focusedElement == this)
            {
                focusController.SwitchFocus(null);
            }
        }

        protected internal override void ExecuteDefaultAction(EventBase evt)
        {
            base.ExecuteDefaultAction(evt);

            if (evt?.eventTypeId == MouseDownEvent.TypeId())
            {
                Focus();
            }

            focusController?.SwitchFocusOnEvent(evt);
        }
    }

    public class FocusChangeDirection
    {
        static readonly FocusChangeDirection s_Unspecified = new FocusChangeDirection(-1);

        public static FocusChangeDirection unspecified
        {
            get { return s_Unspecified; }
        }

        static readonly FocusChangeDirection s_None = new FocusChangeDirection(0);

        public static FocusChangeDirection none
        {
            get { return s_None; }
        }

        protected static FocusChangeDirection lastValue { get { return s_None; } }

        readonly int m_Value;

        protected FocusChangeDirection(int value)
        {
            m_Value = value;
        }

        public static implicit operator int(FocusChangeDirection fcd)
        {
            return fcd?.m_Value ?? 0;
        }
    }

    public interface IFocusRing
    {
        FocusChangeDirection GetFocusChangeDirection(Focusable currentFocusable, EventBase e);

        Focusable GetNextFocusable(Focusable currentFocusable, FocusChangeDirection direction);
    }

    public class FocusController
    {
        // https://w3c.github.io/uievents/#interface-focusevent

        public FocusController(IFocusRing focusRing)
        {
            this.focusRing = focusRing;
            focusedElement = null;
            imguiKeyboardControl = 0;
        }

        IFocusRing focusRing { get; }

        public Focusable focusedElement
        {
            get;
            private set;
        }

        internal void DoFocusChange(Focusable f)
        {
            focusedElement = f;
        }

        void AboutToReleaseFocus(Focusable focusable, Focusable willGiveFocusTo, FocusChangeDirection direction)
        {
            using (FocusOutEvent e = FocusOutEvent.GetPooled(focusable, willGiveFocusTo, direction, this))
            {
                focusable.SendEvent(e);
            }
        }

        void ReleaseFocus(Focusable focusable, Focusable willGiveFocusTo, FocusChangeDirection direction)
        {
            using (BlurEvent e = BlurEvent.GetPooled(focusable, willGiveFocusTo, direction, this))
            {
                focusable.SendEvent(e);
            }
        }

        void AboutToGrabFocus(Focusable focusable, Focusable willTakeFocusFrom, FocusChangeDirection direction)
        {
            using (FocusInEvent e = FocusInEvent.GetPooled(focusable, willTakeFocusFrom, direction, this))
            {
                focusable.SendEvent(e);
            }
        }

        void GrabFocus(Focusable focusable, Focusable willTakeFocusFrom, FocusChangeDirection direction)
        {
            using (FocusEvent e = FocusEvent.GetPooled(focusable, willTakeFocusFrom, direction, this))
            {
                focusable.SendEvent(e);
            }
        }

        internal void SwitchFocus(Focusable newFocusedElement)
        {
            SwitchFocus(newFocusedElement, FocusChangeDirection.unspecified);
        }

        void SwitchFocus(Focusable newFocusedElement, FocusChangeDirection direction)
        {
            if (newFocusedElement == focusedElement)
            {
                return;
            }

            var oldFocusedElement = focusedElement;

            if (newFocusedElement == null || !newFocusedElement.canGrabFocus)
            {
                if (oldFocusedElement != null)
                {
                    AboutToReleaseFocus(oldFocusedElement, newFocusedElement, direction);
                    ReleaseFocus(oldFocusedElement, newFocusedElement, direction);
                }
            }
            else if (newFocusedElement != oldFocusedElement)
            {
                if (oldFocusedElement != null)
                {
                    AboutToReleaseFocus(oldFocusedElement, newFocusedElement, direction);
                }

                AboutToGrabFocus(newFocusedElement, oldFocusedElement, direction);

                if (oldFocusedElement != null)
                {
                    ReleaseFocus(oldFocusedElement, newFocusedElement, direction);
                }

                GrabFocus(newFocusedElement, oldFocusedElement, direction);
            }
        }

        internal Focusable SwitchFocusOnEvent(EventBase e)
        {
            FocusChangeDirection direction = focusRing.GetFocusChangeDirection(focusedElement, e);
            if (direction != FocusChangeDirection.none)
            {
                Focusable f = focusRing.GetNextFocusable(focusedElement, direction);
                SwitchFocus(f, direction);
                // f does not have the focus yet. It will when the series of focus events will have been handled.
                return f;
            }

            return focusedElement;
        }

        /// <summary>
        /// This property contains the actual keyboard id of the element being focused in the case of an IMGUIContainer
        /// </summary>
        internal int imguiKeyboardControl { get; set; }

        internal void SyncIMGUIFocus(int imguiKeyboardControlID, Focusable imguiContainerHavingKeyboardControl, bool forceSwitch)
        {
            imguiKeyboardControl = imguiKeyboardControlID;

            if (forceSwitch || imguiKeyboardControl != 0)
            {
                SwitchFocus(imguiContainerHavingKeyboardControl, FocusChangeDirection.unspecified);
            }
            else
            {
                SwitchFocus(null, FocusChangeDirection.unspecified);
            }
        }
    }
}
