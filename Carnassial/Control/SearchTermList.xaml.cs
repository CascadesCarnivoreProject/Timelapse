using Carnassial.Data;
using Carnassial.Database;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace Carnassial.Control
{
    public partial class SearchTermList : UserControl
    {
        private readonly Dictionary<string, ControlRow> controlsByLabel;
        private FileDatabase? fileDatabase;

        public event Action? QueryChanged;

        public SearchTermList()
        {
            this.InitializeComponent();

            this.controlsByLabel = new Dictionary<string, ControlRow>();
            this.fileDatabase = null;
        }

        private void AndOrRadioButton_Checked(object sender, RoutedEventArgs args)
        {
            Debug.Assert((this.fileDatabase != null) && (this.fileDatabase.CustomSelection != null));

            RadioButton radioButton = (RadioButton)sender;
            LogicalOperator termCombiningOperator = (radioButton == this.TermCombiningAnd) ? LogicalOperator.And : LogicalOperator.Or;
            if (termCombiningOperator != this.fileDatabase.CustomSelection.TermCombiningOperator)
            {
                this.fileDatabase.CustomSelection.TermCombiningOperator = termCombiningOperator;
                foreach (SearchTermPicker termPicker in this.SearchTerms.Items)
                {
                    termPicker.UpdateDisplayQuery();
                }
                this.QueryChanged?.Invoke();
            }
        }

        public ControlRow FindControlByLabel(string label)
        {
            return this.controlsByLabel[label];
        }

        public LogicalOperator? GetCombiningOperatorForTerm(int termIndex)
        {
            int lastEnabledTermIndex = -1;
            for (int index = this.SearchTerms.Items.Count - 1; index >= 0; --index)
            {
                SearchTermPicker termPicker = (SearchTermPicker)this.SearchTerms.Items[index];
                if (termPicker.SearchTerm.UseForSearching)
                {
                    lastEnabledTermIndex = index;
                    break;
                }
            }

            if (lastEnabledTermIndex == termIndex)
            {
                return null;
            }

            Debug.Assert((this.fileDatabase != null) && (this.fileDatabase.CustomSelection != null));
            return this.fileDatabase.CustomSelection.TermCombiningOperator;
        }

        private void ListViewItem_GotFocus(object sender, RoutedEventArgs e)
        {
            // by default, WPF's ListView decouples focus and selection and implements tabbing at the ListViewItem level
            // This often produces awkward user behavior and is something of a FAQ in cases where user controls are placed in 
            // ListViews. There doesn't appear to be a well defined best practice but the approach used here is
            // 
            // - set the ListView's KeyboardNavigation.TabNavigation to Continue in XAML
            //   Oddly, this approach seems seldom used. It behaves as expected, though, producing continuous tabbing into
            //   the ListView, through all tab stops within the view, and out of the view. With this setting alone, ListView 
            //   selection follows keyboard focus but does not behave as desired in the reverse (shift+tab) direction. As of
            //   .NET 4.7.1 it appears the selected item of ListViews with SelectionMode = single changes when a ListViewItem
            //   receives focus. When reverse tabbing, the sequence is to step through the tab stops within a ListViewItem and
            //   then to the ListViewItem itself. This produces an odd experience where the selection moves to the ListViewItem
            //   the user is tabbing out of, rather than the one containing the control which has keyboard focus.
            // - disable ListViewItems as tab stops in XAML
            //   This turns off the redundant tab stops on ListViewItems but, per the above, also disables select item tracking
            //   as ListViewItems no longer receive keyboard focus.  While often reported as breaking tab navigation, it seems 
            //   this is due to the approach seldom being combined with changes to the tab navigation setting.
            // - re-enable selection tracking with this callback
            //   Approaches such as moving focus to the content of a ListViewItem when it receives focus or sending additional 
            //   tab keys to automatically move past ListViewItem tab stops result in StackOverflowExceptions. Moving the selection,
            //   however, is decoupled from both focus changes and the tabbing sequence and appears to be safe.
            // 
            // ListView responds to ctrl+tab and ctrl+shift+tab by moving between list view items. Behavior in this configuration
            // is generally desirable as focus stays within the column of controls formed by the SearchTermPickers. The exception
            // is the initial ctrl+tab jumps out of the ListView rather than moving through the items. Workarounds for this ListView
            // bug haven't been explored.
            ListViewItem item = (ListViewItem)sender;
            item.IsSelected = true;
        }

        public void Populate(FileDatabase fileDatabase)
        {
            if (fileDatabase.CustomSelection == null)
            {
                throw new ArgumentOutOfRangeException(nameof(fileDatabase));
            }
            this.fileDatabase = fileDatabase;

            this.controlsByLabel.Clear();
            foreach (ControlRow control in this.fileDatabase.Controls)
            {
                this.controlsByLabel.Add(control.Label, control);
            }

            this.TermCombiningAnd.Checked -= this.AndOrRadioButton_Checked;
            this.TermCombiningOr.Checked -= this.AndOrRadioButton_Checked;
            this.TermCombiningAnd.IsChecked = this.fileDatabase.CustomSelection.TermCombiningOperator == LogicalOperator.And;
            this.TermCombiningOr.IsChecked = !this.TermCombiningAnd.IsChecked;
            this.TermCombiningAnd.Checked += this.AndOrRadioButton_Checked;
            this.TermCombiningOr.Checked += this.AndOrRadioButton_Checked;

            // labels tend to be unique at the control level but will be duplicated when multiple terms apply to a column
            // So access terms by index.
            int initialSelectionIndex = 0;
            List<TextBlock> termLabels = new();
            foreach (SearchTerm searchTerm in this.fileDatabase.CustomSelection.SearchTerms)
            {
                TextBlock labelBlock = new(new Run(searchTerm.Label))
                {
                    Tag = searchTerm.Label
                };
                termLabels.Add(labelBlock);
            }

            for (int index = 0; index < this.fileDatabase.CustomSelection.SearchTerms.Count; ++index)
            {
                // create and wire control for search term
                SearchTerm searchTerm = this.fileDatabase.CustomSelection.SearchTerms[index];
                SearchTermPicker termPicker = new(searchTerm, this, termLabels, index, this.fileDatabase.AutocompletionCache)
                {
                    TabIndex = index
                };
                this.SearchTerms.Items.Add(termPicker);

                if (searchTerm.ImportanceHint && (initialSelectionIndex == 0))
                {
                    initialSelectionIndex = index;
                }
            }

            if (this.fileDatabase.CustomSelection.SearchTerms.Count > 0)
            {
                this.SearchTerms.SelectedIndex = initialSelectionIndex;
            }
        }
    }
}
