namespace BetterTerminal.Shell.Services
{
    /// <summary>
    /// Where a dragged leaf lands relative to what it was dropped on. Center is a swap rather than
    /// a tab group: this shell's pane tree is binary splits with no tabbed groups inside a pane, so
    /// there is nothing to tab into, and exchanging the two leaves is the one center-drop meaning
    /// that is useful and cannot be expressed by any of the four sides.
    /// </summary>
    public enum DockSide
    {
        Left,
        Right,
        Top,
        Bottom,
        Center,
    }
}
