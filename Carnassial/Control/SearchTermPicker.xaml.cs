using Carnassial.Data;
using Carnassial.Database;
using Carnassial.Util;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using WpfControl = System.Windows.Controls.Control;

namespace Carnassial.Control
{
    public partial class SearchTermPicker : UserControl
    {
        private const int DatabaseValueColumn = 3;

        private readonly AutocompletionCache autocompletionCache;
        private WpfControl? databaseValueControl;
        private readonly SearchTermList parentSearchTermList;
        private readonly SearchTermPicker? previousSearchTermPicker;
        private readonly int termIndex;

        public SearchTerm SearchTerm { get; private set; }

        public event Action<bool> SubsequentSearchTermEnabledOrDisabled;

        public SearchTermPicker(SearchTerm searchTerm, SearchTermList parent, List<TextBlock> termLabels, int termIndex, AutocompletionCache autocompletionCache)
        {
            this.InitializeComponent();

            this.autocompletionCache = autocompletionCache;
            this.parentSearchTermList = parent;
            this.previousSearchTermPicker = null;
            if (termIndex > 0)
            {
                this.previousSearchTermPicker = (SearchTermPicker)parent.SearchTerms.Items[termIndex - 1];
            }
            this.SearchTerm = searchTerm;
            this.SubsequentSearchTermEnabledOrDisabled += this.UpdateDisplayQuery;
            this.termIndex = termIndex;
            this.databaseValueControl = null;

            this.LabelBox.ItemsSource = termLabels;
            this.LabelBox.SelectedIndex = termIndex;
            this.UseCheckBox.DataContext = this.SearchTerm;
        }

        private void LabelBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string selectedLabel = (string)((FrameworkElement)this.LabelBox.SelectedItem).Tag;
            if (String.Equals(this.SearchTerm.Label, selectedLabel, StringComparison.Ordinal) == false)
            {
                // don't reset search term values if the search term is already configured for the control
                // This event fires in two cases
                //   1) During dialog initialization, when the selected item changes from null to the label of the data bound control
                //   2) When the user changes the selected item.
                // SetControl() is desirable only in the second case.  In the first, the SearchTerms's values should flow to the
                // picker controls.
                this.SearchTerm.SetControl(this.parentSearchTermList.FindControlByLabel(selectedLabel));
            }

            // update operator collection with changes
            this.OperatorBox.SelectionChanged -= this.OperatorBox_SelectionChanged;
            List<string> operators = this.SearchTerm.GetOperators();
            int existingOperatorBoxCount = this.OperatorBox.Items.Count;
            for (int index = 0; index < existingOperatorBoxCount; ++index)
            {
                this.OperatorBox.Items[index] = operators[index];
            }
            for (int index = existingOperatorBoxCount; index < operators.Count; ++index)
            {
                this.OperatorBox.Items.Add(operators[index]);
            }
            while (operators.Count < this.OperatorBox.Items.Count)
            {
                this.OperatorBox.Items.RemoveAt(operators.Count);
            }
            this.OperatorBox.SelectedItem = this.SearchTerm.Operator;
            this.OperatorBox.SelectionChanged += this.OperatorBox_SelectionChanged;

            // update database value control
            this.databaseValueControl = this.SearchTerm.CreateValueControl(this.autocompletionCache);
            this.databaseValueControl.IsEnabled = this.UseCheckBox.IsChecked ?? false;
            this.Grid.ReplaceOrAddChild(0, SearchTermPicker.DatabaseValueColumn, this.databaseValueControl);

            // update shortcut key
            if (String.IsNullOrWhiteSpace(this.SearchTerm.Label))
            {
                this.Shortcut.Content = null;
            }
            else
            {
                this.Shortcut.Content = "_" + this.SearchTerm.Label.Substring(0, 1);
            }

            this.UpdateDisplayQuery();
            e.Handled = true;
        }

        private void OperatorBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            this.SearchTerm.Operator = (string)this.OperatorBox.SelectedItem;

            this.UpdateDisplayQuery();
        }

        public void UpdateDisplayQuery()
        {
            if (this.SearchTerm.UseForSearching)
            {
                string query = this.SearchTerm.ToString();
                LogicalOperator? termCombiningOperator = this.parentSearchTermList.GetCombiningOperatorForTerm(this.termIndex);
                if (termCombiningOperator.HasValue)
                {
                    query += " " + termCombiningOperator.ToString();
                }
                this.Query.Content = query;
            }
            else
            {
                this.Query.Content = null;
            }
        }

        private void UpdateDisplayQuery(bool subsequentSearchTermIsEnabled)
        {
            if (this.SearchTerm.UseForSearching)
            {
                this.UpdateDisplayQuery();
            }
            else
            {
                this.previousSearchTermPicker?.SubsequentSearchTermEnabledOrDisabled?.Invoke(subsequentSearchTermIsEnabled);
            }
        }

        private void UseCheckBox_CheckedOrUnchecked(object sender, RoutedEventArgs e)
        {
            bool isEnabled = this.UseCheckBox.IsChecked ?? false;
            this.OperatorBox.IsEnabled = isEnabled;
            if (this.databaseValueControl != null)
            {
                this.databaseValueControl.IsEnabled = isEnabled;
            }

            this.UpdateDisplayQuery();

            this.previousSearchTermPicker?.SubsequentSearchTermEnabledOrDisabled?.Invoke(isEnabled);
        }
    }
}
