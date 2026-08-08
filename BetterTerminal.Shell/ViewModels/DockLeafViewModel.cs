using System.Windows;
using System.Windows.Input;

namespace BetterTerminal.Shell.ViewModels
{
    /// <summary>
    /// Anything that can be a leaf of the pane tree: a terminal session, or a tool such as the
    /// connection list or the file explorer. The tree, the splitter branches and all of the docking
    /// code work in terms of this type, so a tool pane tears off and docks exactly the way a session
    /// does and none of that code has to know which it is holding.
    /// </summary>
    public abstract class DockLeafViewModel : ObservableObject
    {
        private bool _isFocused;

        public bool IsFocused
        {
            get { return _isFocused; }

            set
            {
                if (Set(ref _isFocused, value))
                {
                    OnFocusChanged();
                }
            }
        }

        public ICommand CloseCommand { get; set; }

        /// <summary>What the pane header shows, and what a torn-off window is titled.</summary>
        public abstract string HeaderText { get; }

        /// <summary>
        /// The element that moves between the grid and a floating window. Docking reparents this
        /// exact instance - never a rebuilt one - which is what keeps a session's process, its
        /// pseudo console and its scrollback alive across a tear-off.
        /// </summary>
        public abstract FrameworkElement Content { get; }

        /// <summary>
        /// False for a leaf that cannot survive being moved to another top-level window. The
        /// hosted-console fallback backend is a real console window parented into this one, and
        /// re-parenting that across windows loses the child; such a pane stays in the grid.
        /// </summary>
        public virtual bool CanFloat
        {
            get { return true; }
        }

        /// <summary>Why <see cref="CanFloat"/> is false, said in the user's terms.</summary>
        public virtual string FloatRefusal
        {
            get { return "This pane cannot be moved out of the window."; }
        }

        protected virtual void OnFocusChanged()
        {
        }
    }
}
