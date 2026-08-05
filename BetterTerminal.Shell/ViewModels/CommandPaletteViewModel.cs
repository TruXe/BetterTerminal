using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;

namespace BetterTerminal.Shell.ViewModels
{
    public class CommandPaletteViewModel : ObservableObject
    {
        private string _query = string.Empty;
        private CommandItemViewModel _selectedResult;

        public CommandPaletteViewModel()
        {
            All = new ObservableCollection<CommandItemViewModel>();
            Results = new ObservableCollection<CommandItemViewModel>();

            if (DesignerProperties.GetIsInDesignMode(new DependencyObject()))
            {
                SampleData.Populate(this);
            }
        }

        public ObservableCollection<CommandItemViewModel> All { get; private set; }

        public ObservableCollection<CommandItemViewModel> Results { get; private set; }

        public CommandItemViewModel SelectedResult
        {
            get { return _selectedResult; }
            set { Set(ref _selectedResult, value); }
        }

        public string Query
        {
            get { return _query; }

            set
            {
                if (Set(ref _query, value))
                {
                    Filter();
                }
            }
        }

        public string ResultSummary
        {
            get { return Results.Count + " of " + All.Count + " commands"; }
        }

        /// <summary>
        /// Rebuilds the entry list and resets the query, so the palette always opens on a
        /// current view of what the shell can do.
        /// </summary>
        public void Reset(IEnumerable<CommandItemViewModel> commands)
        {
            All.Clear();
            foreach (CommandItemViewModel command in commands)
            {
                All.Add(command);
            }

            _query = string.Empty;
            Raise("Query");
            Filter();
        }

        private void Filter()
        {
            Results.Clear();

            IEnumerable<CommandItemViewModel> matches = string.IsNullOrWhiteSpace(_query)
                ? All
                : All.Where(Matches);

            foreach (CommandItemViewModel item in matches)
            {
                Results.Add(item);
            }

            SelectedResult = Results.FirstOrDefault();
            Raise("ResultSummary");
        }

        private bool Matches(CommandItemViewModel item)
        {
            return item.Name.IndexOf(_query, StringComparison.CurrentCultureIgnoreCase) >= 0
                || item.Group.IndexOf(_query, StringComparison.CurrentCultureIgnoreCase) >= 0;
        }
    }
}
