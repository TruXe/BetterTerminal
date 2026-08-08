using System;

namespace BetterTerminal.Notifications
{
    /// <summary>One action button in the toast footer. Up to three sit in a row.</summary>
    public sealed class ToastAction
    {
        public ToastAction(string label, Action<ToastNotification> invoke = null)
        {
            Label = label;
            Invoke = invoke;
        }

        public string Label { get; private set; }

        /// <summary>What the button does. Null simply dismisses the toast.</summary>
        public Action<ToastNotification> Invoke { get; private set; }

        /// <summary>When true the toast stays open after the button is clicked.</summary>
        public bool KeepOpen { get; set; }
    }
}
