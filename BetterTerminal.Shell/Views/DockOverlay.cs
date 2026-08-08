using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using BetterTerminal.Shell.Services;

namespace BetterTerminal.Shell.Views
{
    /// <summary>
    /// The docking targets drawn over the pane area while a leaf is being dragged: a rosette of five
    /// buttons over the pane under the pointer, four more on the edges of the whole area, and an
    /// outline of where the leaf would land. It sits in the same cell as the pane host so its
    /// coordinate space is the pane host's own - no screen coordinates, no DPI arithmetic.
    ///
    /// It never takes input. The drag owns the mouse from the moment it starts, so the controller
    /// hit-tests the site list itself and only tells this element what to light up.
    /// </summary>
    public sealed class DockOverlay : FrameworkElement
    {
        internal const double ButtonSize = 38;
        internal const double ButtonGap = 5;

        private static readonly Typeface IconFace = new Typeface(
            new FontFamily("Segoe MDL2 Assets"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

        private IList<DockSite> _sites = new List<DockSite>();
        private DockSite _active;

        public DockOverlay()
        {
            IsHitTestVisible = false;
            Visibility = Visibility.Collapsed;
        }

        public void Begin(IList<DockSite> sites)
        {
            _sites = sites ?? new List<DockSite>();
            _active = null;
            Visibility = Visibility.Visible;
            InvalidateVisual();
        }

        public void SetActive(DockSite site)
        {
            if (ReferenceEquals(_active, site))
            {
                return;
            }

            _active = site;
            InvalidateVisual();
        }

        public void End()
        {
            _sites = new List<DockSite>();
            _active = null;
            Visibility = Visibility.Collapsed;
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext context)
        {
            base.OnRender(context);

            if (_sites.Count == 0)
            {
                return;
            }

            Color accent = Themed("Bt.AccentFillDefaultBrush", Color.FromRgb(0x60, 0xCD, 0xFF));
            Color surface = Themed("Bt.ElevatedBackgroundBrush", Color.FromRgb(0x2B, 0x2B, 0x2B));
            Color stroke = Themed("Bt.StrokeDefaultBrush", Color.FromRgb(0x45, 0x45, 0x45));

            DrawPreview(context, accent);

            foreach (DockSite site in _sites)
            {
                DrawButton(context, site, accent, surface, stroke);
            }
        }

        private void DrawPreview(DrawingContext context, Color accent)
        {
            if (_active == null || _active.Preview.IsEmpty)
            {
                return;
            }

            SolidColorBrush fill = new SolidColorBrush(Color.FromArgb(0x38, accent.R, accent.G, accent.B));
            fill.Freeze();

            Pen edge = new Pen(new SolidColorBrush(accent), 2);
            edge.Brush.Freeze();
            edge.Freeze();

            // Inset by half the pen so the stroke lands inside the rectangle it describes.
            Rect outline = Deflate(_active.Preview, 1);
            context.DrawRoundedRectangle(fill, edge, outline, 4, 4);
        }

        private void DrawButton(DrawingContext context, DockSite site, Color accent, Color surface, Color stroke)
        {
            bool isActive = ReferenceEquals(site, _active);

            SolidColorBrush background = new SolidColorBrush(isActive
                ? accent
                : Color.FromArgb(0xF0, surface.R, surface.G, surface.B));
            background.Freeze();

            Pen border = new Pen(new SolidColorBrush(isActive ? accent : stroke), 1);
            border.Brush.Freeze();
            border.Freeze();

            Rect box = Deflate(site.Button, 0.5);
            context.DrawRoundedRectangle(background, border, box, 6, 6);

            Color glyphColor = isActive
                ? Themed("Bt.TextOnAccentBrush", Colors.Black)
                : Themed("Bt.TextFillPrimaryBrush", Colors.White);

            FormattedText text = new FormattedText(
                site.Glyph,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                IconFace,
                site.Side == DockSide.Center ? 16 : 14,
                new SolidColorBrush(glyphColor),
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            context.DrawText(text, new Point(
                site.Button.X + ((site.Button.Width - text.Width) / 2),
                site.Button.Y + ((site.Button.Height - text.Height) / 2)));
        }

        private static Rect Deflate(Rect rect, double amount)
        {
            double width = rect.Width - (amount * 2);
            double height = rect.Height - (amount * 2);
            if (width <= 0 || height <= 0)
            {
                return rect;
            }

            return new Rect(rect.X + amount, rect.Y + amount, width, height);
        }

        private Color Themed(string key, Color fallback)
        {
            object value = TryFindResource(key);

            SolidColorBrush brush = value as SolidColorBrush;
            if (brush != null)
            {
                return brush.Color;
            }

            return value is Color ? (Color)value : fallback;
        }
    }
}
