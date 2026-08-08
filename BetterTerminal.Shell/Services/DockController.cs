using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using BetterTerminal.Interop;
using BetterTerminal.Shell.ViewModels;
using BetterTerminal.Shell.Views;

namespace BetterTerminal.Shell.Services
{
    /// <summary>
    /// Moves leaves between the pane grid and windows of their own. Everything here is a move of one
    /// live element: a pane that is torn off keeps its session, its process and its scrollback,
    /// because the element the grid was holding is the element the new window is given.
    ///
    /// The drag is tracked in physical screen pixels and converted through the pane host, so it is
    /// correct across monitors of different scale without any DPI arithmetic of its own.
    /// </summary>
    public sealed class DockController
    {
        private const double EdgeInset = 22;

        private readonly IDockHost _host;
        private readonly List<FloatingPaneWindow> _floating = new List<FloatingPaneWindow>();

        private FloatingPaneWindow _dragged;
        private List<DockSite> _sites = new List<DockSite>();
        private Dictionary<DockLeafViewModel, Rect> _bounds = new Dictionary<DockLeafViewModel, Rect>();
        private DockSite _active;

        public DockController(IDockHost host)
        {
            _host = host;
        }

        /// <summary>Every window a leaf has been torn off into, oldest first.</summary>
        public IList<FloatingPaneWindow> FloatingWindows
        {
            get { return _floating; }
        }

        public bool IsDragging
        {
            get { return _dragged != null; }
        }

        /// <summary>The window showing this element, or null when it is docked or gone.</summary>
        public FloatingPaneWindow WindowFor(FrameworkElement content)
        {
            foreach (FloatingPaneWindow window in _floating)
            {
                if (window.Leaf != null && ReferenceEquals(window.Leaf.Content, content))
                {
                    return window;
                }
            }

            return null;
        }

        /// <summary>
        /// Pulls a leaf out of the grid into a window under the pointer and hands the drag to that
        /// window, so the gesture that started on the pane header carries on without a break.
        /// </summary>
        public void TearOffAndDrag(DockLeafViewModel leaf, Point32 cursor)
        {
            if (leaf == null || _dragged != null)
            {
                return;
            }

            if (!leaf.CanFloat)
            {
                _host.Report(leaf.FloatRefusal);
                return;
            }

            Rect bounds = BoundsOf(leaf);
            FloatingPaneWindow window = TearOff(leaf, cursor, bounds);
            if (window == null)
            {
                return;
            }

            window.BeginDragFromCursor();
        }

        /// <summary>
        /// Takes the leaf out of the grid and gives it a window. Sized from the pane it came from so
        /// nothing jumps, and placed so the pointer lands on the new header where it already was.
        /// </summary>
        public FloatingPaneWindow TearOff(DockLeafViewModel leaf, Point32 cursor, Rect paneBounds)
        {
            FrameworkElement content = leaf.Content;
            if (content == null)
            {
                return null;
            }

            _host.RemoveLeaf(leaf);

            // The old container still owns the element as a logical child until it is told
            // otherwise, and adding it anywhere else before that throws.
            Detach(content);

            FloatingPaneWindow window = new FloatingPaneWindow();
            window.Owner = _host.Owner;
            window.Attach(this, leaf);
            _floating.Add(window);

            Size size = SizeFor(paneBounds);
            window.Show();
            window.MoveTo(new Rect32
            {
                Left = cursor.X - (int)(size.Width / 2),
                Top = cursor.Y - 18,
                Right = cursor.X - (int)(size.Width / 2) + (int)size.Width,
                Bottom = cursor.Y - 18 + (int)size.Height,
            });

            _host.Report(string.Empty);
            return window;
        }

        public void BeginFloatingDrag(FloatingPaneWindow window)
        {
            _dragged = window;
            _bounds = CollectLeafBounds();
            _active = null;
            _sites = new List<DockSite>();
            _host.Overlay.Begin(_sites);

            // Show the targets on the grab rather than waiting for the pointer to travel: a user who
            // has picked something up needs to see where it can go before deciding where to move it.
            Point32 cursor;
            if (NativeMethods.GetCursorPos(out cursor))
            {
                UpdateDrag(cursor);
            }
        }

        /// <summary>
        /// Starts a drag from a tool window - the connection list, the file explorer. The window is
        /// given up and its panel carries on inside a floating pane, so the gesture that began on
        /// the tool's own header continues as an ordinary dock drag with the targets already up.
        /// </summary>
        public void BeginToolDrag(DockLeafViewModel leaf, Rect32 bounds)
        {
            if (leaf == null || leaf.Content == null || _dragged != null)
            {
                return;
            }

            Detach(leaf.Content);
            FloatingPaneWindow window = Float(leaf, bounds);
            window.BeginDragFromCursor();
        }

        public void UpdateDrag(Point32 cursor)
        {
            if (_dragged == null)
            {
                return;
            }

            Point point;
            if (!TryPointInHost(cursor, out point))
            {
                _sites = new List<DockSite>();
                _active = null;
                _host.Overlay.End();
                _host.Report(string.Empty);
                return;
            }

            _sites = SitesFor(point);
            _active = null;
            foreach (DockSite site in _sites)
            {
                if (site.Contains(point))
                {
                    _active = site;
                    break;
                }
            }

            _host.Overlay.Begin(_sites);
            _host.Overlay.SetActive(_active);
            _host.Report(_active == null ? string.Empty : _active.Description);
        }

        public void CommitDrag(Point32 cursor)
        {
            FloatingPaneWindow window = _dragged;
            DockSite site = _active;

            EndDrag();

            if (window == null || site == null)
            {
                return;
            }

            Dock(window, site);
        }

        public void CancelDrag()
        {
            EndDrag();
        }

        /// <summary>
        /// Puts a floating window back without a drag - the header's dock button, or a double click
        /// on it. It lands beside the focused pane, or fills the tab when there is nothing there.
        /// </summary>
        public void DockBack(FloatingPaneWindow window)
        {
            DockLeafViewModel target = FirstVisibleLeaf();
            DockSite site = target == null
                ? new DockSite(DockSide.Center, null, true, Rect.Empty, Rect.Empty)
                : new DockSite(DockSide.Right, target, false, Rect.Empty, Rect.Empty);

            Dock(window, site);
        }

        /// <summary>The floating window is closing for real: the leaf and its session end with it.</summary>
        public void FloatingWindowClosed(FloatingPaneWindow window)
        {
            DockLeafViewModel leaf = window.Leaf;
            _floating.Remove(window);

            if (_dragged == window)
            {
                EndDrag();
            }

            if (leaf != null)
            {
                _host.CloseLeaf(leaf);
            }
        }

        public void CloseAllFloating()
        {
            foreach (FloatingPaneWindow window in _floating.ToArray())
            {
                window.Close();
            }

            _floating.Clear();
        }

        private void Dock(FloatingPaneWindow window, DockSite site)
        {
            DockLeafViewModel leaf = window.Leaf;
            if (leaf == null)
            {
                return;
            }

            // Taken before the window goes: a center drop hands this rectangle to the pane that
            // gets displaced, so the two simply change places on screen.
            Rect32 vacated = window.Bounds;

            window.ReleaseContent();
            Detach(leaf.Content);

            _floating.Remove(window);
            window.Close();

            if (site.Target == null || site.IsOuter)
            {
                _host.InsertAtEdge(leaf, site.Target == null && !site.IsOuter ? DockSide.Right : site.Side);
            }
            else if (site.Side == DockSide.Center)
            {
                DockLeafViewModel displaced = site.Target;
                _host.Replace(leaf, displaced);
                Detach(displaced.Content);
                Float(displaced, vacated);
            }
            else
            {
                _host.InsertBeside(leaf, site.Target, site.Side);
            }

            _host.FocusLeaf(leaf);
            _host.Report(string.Empty);
        }

        /// <summary>
        /// Puts a leaf built from the saved layout straight into a window, without it ever having
        /// been in the grid. The rectangle is clamped onto a screen that exists: a window restored
        /// onto a monitor that has since been unplugged would be unreachable.
        /// </summary>
        public FloatingPaneWindow Restore(DockLeafViewModel leaf, Rect32 bounds)
        {
            if (leaf == null || leaf.Content == null)
            {
                return null;
            }

            Detach(leaf.Content);
            return Float(leaf, OnScreen(bounds));
        }

        private static Rect32 OnScreen(Rect32 bounds)
        {
            if (bounds.Width < 200 || bounds.Height < 120)
            {
                return new Rect32 { Left = 120, Top = 120, Right = 1020, Bottom = 680 };
            }

            int left = (int)SystemParameters.VirtualScreenLeft;
            int top = (int)SystemParameters.VirtualScreenTop;
            int right = left + (int)SystemParameters.VirtualScreenWidth;
            int bottom = top + (int)SystemParameters.VirtualScreenHeight;

            // Enough of the header has to remain reachable to grab it again.
            bool visible = bounds.Left < right - 80 && bounds.Right > left + 80
                           && bounds.Top < bottom - 40 && bounds.Bottom > top + 40;

            if (visible)
            {
                return bounds;
            }

            return new Rect32
            {
                Left = left + 120,
                Top = top + 120,
                Right = left + 120 + bounds.Width,
                Bottom = top + 120 + bounds.Height,
            };
        }

        /// <summary>
        /// Opens a leaf that has never been in the grid as a window - how the tools first appear.
        /// It is the same window a torn-off pane gets, so its header docks by being dragged and
        /// nothing has to be taught about tools specifically.
        /// </summary>
        public FloatingPaneWindow Open(DockLeafViewModel leaf, int width, int height)
        {
            if (leaf == null || leaf.Content == null)
            {
                return null;
            }

            Detach(leaf.Content);

            Rect32 owner = OwnerBounds();
            int left = owner.Left + ((owner.Width - width) / 2);
            int top = owner.Top + ((owner.Height - height) / 3);

            return Float(leaf, OnScreen(new Rect32
            {
                Left = left,
                Top = top,
                Right = left + width,
                Bottom = top + height,
            }));
        }

        private Rect32 OwnerBounds()
        {
            Rect32 bounds;
            IntPtr handle = new System.Windows.Interop.WindowInteropHelper(_host.Owner).Handle;
            if (handle != IntPtr.Zero && NativeMethods.GetWindowRect(handle, out bounds))
            {
                return bounds;
            }

            return new Rect32 { Left = 0, Top = 0, Right = 1280, Bottom = 800 };
        }

        /// <summary>Gives a leaf that is already out of the tree a window at a known rectangle.</summary>
        private FloatingPaneWindow Float(DockLeafViewModel leaf, Rect32 bounds)
        {
            FloatingPaneWindow window = new FloatingPaneWindow();
            window.Owner = _host.Owner;
            window.Attach(this, leaf);
            _floating.Add(window);

            window.Show();
            window.MoveTo(bounds);
            return window;
        }

        private void EndDrag()
        {
            _dragged = null;
            _active = null;
            _sites = new List<DockSite>();
            _host.Overlay.End();
            _host.Report(string.Empty);
        }

        /// <summary>
        /// The four edge targets, plus a rosette over whichever pane the pointer is on. Showing every
        /// pane's rosette at once would put more buttons on screen than anyone can aim at.
        /// </summary>
        private List<DockSite> SitesFor(Point point)
        {
            List<DockSite> sites = new List<DockSite>();

            double width = _host.PaneHost.ActualWidth;
            double height = _host.PaneHost.ActualHeight;
            if (width <= 0 || height <= 0)
            {
                return sites;
            }

            Rect area = new Rect(0, 0, width, height);
            double size = DockOverlay.ButtonSize;

            sites.Add(Edge(DockSide.Left, area, new Rect(EdgeInset, (height - size) / 2, size, size)));
            sites.Add(Edge(DockSide.Right, area, new Rect(width - EdgeInset - size, (height - size) / 2, size, size)));
            sites.Add(Edge(DockSide.Top, area, new Rect((width - size) / 2, EdgeInset, size, size)));
            sites.Add(Edge(DockSide.Bottom, area, new Rect((width - size) / 2, height - EdgeInset - size, size, size)));

            DockLeafViewModel hovered = LeafAt(point);
            if (hovered != null)
            {
                sites.AddRange(Rosette(hovered, _bounds[hovered]));
            }

            return sites;
        }

        private static DockSite Edge(DockSide side, Rect area, Rect button)
        {
            return new DockSite(side, null, true, button, Half(area, side));
        }

        private static IEnumerable<DockSite> Rosette(DockLeafViewModel leaf, Rect pane)
        {
            double size = DockOverlay.ButtonSize;
            double step = size + DockOverlay.ButtonGap;
            double cx = pane.X + (pane.Width / 2) - (size / 2);
            double cy = pane.Y + (pane.Height / 2) - (size / 2);

            return new[]
            {
                new DockSite(DockSide.Center, leaf, false, new Rect(cx, cy, size, size), pane),
                new DockSite(DockSide.Left, leaf, false, new Rect(cx - step, cy, size, size), Half(pane, DockSide.Left)),
                new DockSite(DockSide.Right, leaf, false, new Rect(cx + step, cy, size, size), Half(pane, DockSide.Right)),
                new DockSite(DockSide.Top, leaf, false, new Rect(cx, cy - step, size, size), Half(pane, DockSide.Top)),
                new DockSite(DockSide.Bottom, leaf, false, new Rect(cx, cy + step, size, size), Half(pane, DockSide.Bottom)),
            };
        }

        private static Rect Half(Rect rect, DockSide side)
        {
            switch (side)
            {
                case DockSide.Left:
                    return new Rect(rect.X, rect.Y, rect.Width / 2, rect.Height);
                case DockSide.Right:
                    return new Rect(rect.X + (rect.Width / 2), rect.Y, rect.Width / 2, rect.Height);
                case DockSide.Top:
                    return new Rect(rect.X, rect.Y, rect.Width, rect.Height / 2);
                case DockSide.Bottom:
                    return new Rect(rect.X, rect.Y + (rect.Height / 2), rect.Width, rect.Height / 2);
                default:
                    return rect;
            }
        }

        private DockLeafViewModel LeafAt(Point point)
        {
            foreach (KeyValuePair<DockLeafViewModel, Rect> entry in _bounds)
            {
                if (entry.Value.Contains(point))
                {
                    return entry.Key;
                }
            }

            return null;
        }

        private DockLeafViewModel FirstVisibleLeaf()
        {
            foreach (DockLeafViewModel leaf in _host.VisibleLeaves)
            {
                return leaf;
            }

            return null;
        }

        private Rect BoundsOf(DockLeafViewModel leaf)
        {
            Dictionary<DockLeafViewModel, Rect> bounds = CollectLeafBounds();
            return bounds.ContainsKey(leaf) ? bounds[leaf] : Rect.Empty;
        }

        /// <summary>
        /// Where each leaf sits inside the pane host. Found by walking the visual tree rather than
        /// by asking the templates, because the outermost element carrying a leaf as its data
        /// context is that leaf's container whatever the template does inside it.
        /// </summary>
        private Dictionary<DockLeafViewModel, Rect> CollectLeafBounds()
        {
            Dictionary<DockLeafViewModel, Rect> found = new Dictionary<DockLeafViewModel, Rect>();
            Collect(_host.PaneHost, found);
            return found;
        }

        private void Collect(DependencyObject node, Dictionary<DockLeafViewModel, Rect> found)
        {
            FrameworkElement element = node as FrameworkElement;
            if (element != null && element.ActualWidth > 0 && element.ActualHeight > 0)
            {
                DockLeafViewModel leaf = element.DataContext as DockLeafViewModel;
                if (leaf != null && !found.ContainsKey(leaf))
                {
                    found[leaf] = element.TransformToAncestor(_host.PaneHost)
                        .TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));
                }
            }

            int count = VisualTreeHelper.GetChildrenCount(node);
            for (int index = 0; index < count; index++)
            {
                Collect(VisualTreeHelper.GetChild(node, index), found);
            }
        }

        private bool TryPointInHost(Point32 cursor, out Point point)
        {
            point = new Point();

            FrameworkElement host = _host.PaneHost;
            if (host == null || !host.IsVisible || _host.Owner.WindowState == WindowState.Minimized)
            {
                return false;
            }

            try
            {
                point = host.PointFromScreen(new Point(cursor.X, cursor.Y));
            }
            catch (InvalidOperationException)
            {
                // No presentation source yet - the window is not on screen, so nothing to aim at.
                return false;
            }

            return point.X >= 0 && point.Y >= 0
                   && point.X <= host.ActualWidth && point.Y <= host.ActualHeight;
        }

        private static Size SizeFor(Rect pane)
        {
            double width = pane.IsEmpty || pane.Width < 360 ? 900 : pane.Width + 24;
            double height = pane.IsEmpty || pane.Height < 220 ? 560 : pane.Height + 60;
            return new Size(width, height);
        }

        /// <summary>
        /// Releases an element from whatever is holding it, so it can be given to something else.
        /// A content host keeps its child as a logical child until it is told to let go, and adding
        /// an element that still has a parent throws.
        /// </summary>
        internal static void Detach(FrameworkElement element)
        {
            if (element == null)
            {
                return;
            }

            DependencyObject parent = LogicalTreeHelper.GetParent(element);

            System.Windows.Controls.ContentPresenter presenter = parent as System.Windows.Controls.ContentPresenter;
            if (presenter != null)
            {
                System.Windows.Data.BindingOperations.ClearBinding(
                    presenter, System.Windows.Controls.ContentPresenter.ContentProperty);
                presenter.Content = null;
                return;
            }

            System.Windows.Controls.ContentControl control = parent as System.Windows.Controls.ContentControl;
            if (control != null)
            {
                System.Windows.Data.BindingOperations.ClearBinding(
                    control, System.Windows.Controls.ContentControl.ContentProperty);
                control.Content = null;
                return;
            }

            System.Windows.Controls.Decorator decorator = parent as System.Windows.Controls.Decorator;
            if (decorator != null)
            {
                decorator.Child = null;
                return;
            }

            System.Windows.Controls.Panel panel = parent as System.Windows.Controls.Panel;
            if (panel != null)
            {
                panel.Children.Remove(element);
            }
        }
    }
}
