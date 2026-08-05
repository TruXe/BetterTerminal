using System.Windows;
using System.Windows.Media.Animation;

namespace BetterTerminal.Shell.Services
{
    /// <summary>
    /// Single reduced-motion gate (MO-08). Callers ask <see cref="Enabled"/> and, when it is
    /// false, apply the final value with zero duration instead of skipping the state change.
    /// </summary>
    internal static class MotionPolicy
    {
        public static bool Enabled
        {
            get { return SystemParameters.ClientAreaAnimation; }
        }

        public static void Begin(FrameworkElement host, Storyboard storyboard)
        {
            if (storyboard == null)
            {
                return;
            }

            if (!Enabled)
            {
                storyboard.Begin(host, HandoffBehavior.SnapshotAndReplace, true);
                storyboard.SeekAlignedToLastTick(host, storyboard.Duration.TimeSpan, TimeSeekOrigin.BeginTime);
                storyboard.Pause(host);
                return;
            }

            storyboard.Begin(host, HandoffBehavior.SnapshotAndReplace, true);
        }
    }
}
