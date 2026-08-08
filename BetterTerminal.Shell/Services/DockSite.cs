using System.Windows;
using BetterTerminal.Shell.ViewModels;

namespace BetterTerminal.Shell.Services
{
    /// <summary>
    /// One place a dragged leaf can be dropped: the button the user aims at, and the outline of
    /// where the leaf would end up if they let go there. Both rectangles are in the pane host's own
    /// coordinates - the overlay draws in that space and the hit test runs in it, so neither has to
    /// know about screen coordinates or DPI.
    /// </summary>
    public sealed class DockSite
    {
        public DockSite(DockSide side, DockLeafViewModel target, bool isOuter, Rect button, Rect preview)
        {
            Side = side;
            Target = target;
            IsOuter = isOuter;
            Button = button;
            Preview = preview;
        }

        public DockSide Side { get; private set; }

        /// <summary>The leaf being aimed at, or null when this is an edge of the whole tab.</summary>
        public DockLeafViewModel Target { get; private set; }

        /// <summary>True for the four targets on the edges of the pane area.</summary>
        public bool IsOuter { get; private set; }

        /// <summary>The target the pointer has to be inside for this site to win.</summary>
        public Rect Button { get; private set; }

        /// <summary>What gets outlined while this site is the candidate.</summary>
        public Rect Preview { get; private set; }

        /// <summary>
        /// The icon drawn in the button - an arrow for a side, a swap mark for center. Built from a
        /// code point rather than written as a literal: the range is private use and does not
        /// survive every editor, diff and console this file passes through.
        /// </summary>
        public string Glyph
        {
            get { return char.ConvertFromUtf32(GlyphCode); }
        }

        /// <summary>Spoken by screen readers and shown as the drag hint in the status strip.</summary>
        public string Description
        {
            get
            {
                if (IsOuter)
                {
                    switch (Side)
                    {
                        case DockSide.Left: return "Dock to the left edge";
                        case DockSide.Right: return "Dock to the right edge";
                        case DockSide.Top: return "Dock to the top edge";
                        default: return "Dock to the bottom edge";
                    }
                }

                switch (Side)
                {
                    case DockSide.Left: return "Dock left of this pane";
                    case DockSide.Right: return "Dock right of this pane";
                    case DockSide.Top: return "Dock above this pane";
                    case DockSide.Bottom: return "Dock below this pane";
                    default: return "Swap with this pane";
                }
            }
        }

        /// <summary>Segoe MDL2 Assets: ChevronLeft, ChevronRight, ChevronUp, ChevronDown, Switch.</summary>
        private int GlyphCode
        {
            get
            {
                switch (Side)
                {
                    case DockSide.Left: return 0xE76B;
                    case DockSide.Right: return 0xE76C;
                    case DockSide.Top: return 0xE70E;
                    case DockSide.Bottom: return 0xE70D;
                    default: return 0xE8AB;
                }
            }
        }

        public bool Contains(Point point)
        {
            return Button.Contains(point);
        }
    }
}
