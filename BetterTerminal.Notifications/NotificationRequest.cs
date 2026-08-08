using System;
using System.Collections.Generic;
using System.Globalization;

namespace BetterTerminal.Notifications
{
    /// <summary>
    /// A notification described on a command line, so the library can be loaded and driven with
    /// plain arguments by any host:
    ///
    ///     --title "..."      the bold first line
    ///     --message "..."    the body under it
    ///     --appname "..."    the header text (defaults to BetterTerminal)
    ///     --duration N       seconds on screen before it fades (0 keeps it open)
    ///     -btn1 ACTION       up to three buttons, left to right; none are required
    ///     -btn2 ACTION
    ///     -btn3 ACTION
    ///
    /// ACTION is a named function the library knows how to perform (see NotificationActions), or the
    /// form LABEL=action to give the button a caption of its own. Unknown switches are ignored, so a
    /// host may pass its own flags on the same line.
    /// </summary>
    public sealed class NotificationRequest
    {
        public NotificationRequest()
        {
            AppName = "BetterTerminal";
            Buttons = new List<ButtonSpec>();
        }

        public string Title { get; set; }

        public string Message { get; set; }

        public string AppName { get; set; }

        public TimeSpan? Duration { get; set; }

        public List<ButtonSpec> Buttons { get; private set; }

        public static NotificationRequest Parse(string[] args)
        {
            NotificationRequest request = new NotificationRequest();
            if (args == null)
            {
                return request;
            }

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                if (string.IsNullOrEmpty(arg))
                {
                    continue;
                }

                string next = i + 1 < args.Length ? args[i + 1] : null;

                switch (arg.ToLowerInvariant())
                {
                    case "--title":
                        request.Title = next;
                        i++;
                        break;
                    case "--message":
                        request.Message = next;
                        i++;
                        break;
                    case "--appname":
                        request.AppName = next;
                        i++;
                        break;
                    case "--duration":
                        int seconds;
                        if (next != null && int.TryParse(next, NumberStyles.Integer, CultureInfo.InvariantCulture, out seconds) && seconds >= 0)
                        {
                            request.Duration = TimeSpan.FromSeconds(seconds);
                        }

                        i++;
                        break;
                    case "-btn1":
                    case "-btn2":
                    case "-btn3":
                        ButtonSpec spec = ButtonSpec.ParseValue(next);
                        if (spec != null)
                        {
                            request.Buttons.Add(spec);
                        }

                        i++;
                        break;
                }
            }

            return request;
        }

        /// <summary>A button as it arrived on the command line: which action, and an optional caption.</summary>
        public sealed class ButtonSpec
        {
            public string Action { get; private set; }

            /// <summary>The caption to show, or null to use the action's own default.</summary>
            public string Label { get; private set; }

            public static ButtonSpec ParseValue(string value)
            {
                if (string.IsNullOrEmpty(value))
                {
                    return null;
                }

                // LABEL=action gives the button a caption of its own; a bare token is the action, and
                // the action's default caption is used.
                int split = value.IndexOf('=');
                if (split > 0 && split < value.Length - 1)
                {
                    return new ButtonSpec
                    {
                        Label = value.Substring(0, split).Trim(),
                        Action = value.Substring(split + 1).Trim()
                    };
                }

                return new ButtonSpec { Action = value.Trim() };
            }
        }
    }
}
