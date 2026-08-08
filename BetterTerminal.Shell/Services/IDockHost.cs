using System.Collections.Generic;
using System.Windows;
using BetterTerminal.Shell.ViewModels;
using BetterTerminal.Shell.Views;

namespace BetterTerminal.Shell.Services
{
    /// <summary>
    /// What the docking controller needs from whoever owns the pane tree. Kept as a contract rather
    /// than a direct reference to <see cref="TerminalWorkspace"/> so the tree surgery stays in one
    /// place and the controller can be driven by a probe that owns no sessions.
    /// </summary>
    public interface IDockHost
    {
        /// <summary>The element the pane tree fills. Every docking rectangle is in its coordinates.</summary>
        FrameworkElement PaneHost { get; }

        /// <summary>The targets drawn while a drag is running.</summary>
        DockOverlay Overlay { get; }

        Window Owner { get; }

        /// <summary>Every leaf of the tab on screen right now, in no particular order.</summary>
        IEnumerable<DockLeafViewModel> VisibleLeaves { get; }

        /// <summary>
        /// Takes the leaf out of the tree and collapses the split that held it, leaving whatever it
        /// was sharing that split with in its place. The leaf keeps its content and its session.
        /// </summary>
        void RemoveLeaf(DockLeafViewModel leaf);

        /// <summary>Splits <paramref name="target"/> and puts the leaf on the given side of it.</summary>
        void InsertBeside(DockLeafViewModel leaf, DockLeafViewModel target, DockSide side);

        /// <summary>Splits the whole tab and puts the leaf against that edge of it.</summary>
        void InsertAtEdge(DockLeafViewModel leaf, DockSide side);

        /// <summary>
        /// Puts <paramref name="leaf"/> where <paramref name="target"/> was and takes the target
        /// out of the tree. The target keeps its content and its session; the caller decides where
        /// it goes, which for a center drop is the window the leaf just vacated.
        /// </summary>
        void Replace(DockLeafViewModel leaf, DockLeafViewModel target);

        /// <summary>Ends the leaf for good - closes its session and drops it.</summary>
        void CloseLeaf(DockLeafViewModel leaf);

        void FocusLeaf(DockLeafViewModel leaf);

        /// <summary>Says something in the status strip; empty clears it.</summary>
        void Report(string message);
    }
}
