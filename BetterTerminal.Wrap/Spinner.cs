namespace BetterTerminal.Wrap
{
    /// <summary>
    /// The character to show in place of a wait. Two shapes, because a turning circle and a
    /// pulsing star read differently: the circle says "this is still going", the star says
    /// "something is happening here right now".
    /// </summary>
    public sealed class Spinner
    {
        private const int FrameMilliseconds = 110;

        private readonly string _frames;

        private Spinner(string frames)
        {
            _frames = frames;
        }

        public static Spinner Circle()
        {
            return new Spinner("|/-\\");
        }

        public static Spinner Star()
        {
            return new Spinner("+x*x");
        }

        /// <summary>The frame for a point in time, so the caller keeps no animation state.</summary>
        public char Frame(long milliseconds)
        {
            long index = milliseconds / FrameMilliseconds % _frames.Length;
            return _frames[(int)index];
        }
    }
}
