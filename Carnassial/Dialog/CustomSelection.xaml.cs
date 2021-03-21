using Carnassial.Data;
using Carnassial.Util;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Windows;

namespace Carnassial.Dialog
{
    /// <summary>
    /// A dialog allowing a user to create a custom selection by setting conditions on data fields.
    /// </summary>
    public partial class CustomSelection : WindowWithSystemMenu
    {
        private readonly FileDatabase fileDatabase;

        public CustomSelection(FileDatabase fileDatabase, Window owner)
        {
            this.InitializeComponent();
            this.Message.SetVisibility();

            this.fileDatabase = fileDatabase;
            this.Owner = owner;

            // populate list of search terms
            this.SearchTerms.Populate(this.fileDatabase);

            // hook change notifications for search
            this.SearchTerms.QueryChanged += this.UpdateFileCount;
            Debug.Assert(this.fileDatabase.CustomSelection != null);
            foreach (SearchTerm term in this.fileDatabase.CustomSelection.SearchTerms)
            {
                term.PropertyChanged += this.UpdateFileCount;
            }

            this.UpdateFileCount();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }

        private void OkButton_Click(object sender, RoutedEventArgs args)
        {
            this.DialogResult = true;
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            Debug.Assert(this.fileDatabase.CustomSelection != null);
            // disable all search terms
            foreach (SearchTerm term in this.fileDatabase.CustomSelection.SearchTerms)
            {
                term.UseForSearching = false;
            }
        }

        private void UpdateFileCount()
        {
            int count = this.fileDatabase.GetFileCount(FileSelection.Custom);
            this.OkButton.IsEnabled = count > 0;
            this.QueryMatches.Text = count > 0 ? count.ToString(CultureInfo.CurrentCulture) : "0";
        }

        private void UpdateFileCount(object? sender, PropertyChangedEventArgs e)
        {
            this.UpdateFileCount();
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            Debug.Assert(this.fileDatabase.CustomSelection != null);
            // search terms are durable so unhook their property changed events
            for (int index = 0; index < this.fileDatabase.CustomSelection.SearchTerms.Count; ++index)
            {
                SearchTerm searchTerm = this.fileDatabase.CustomSelection.SearchTerms[index];
                searchTerm.PropertyChanged -= this.UpdateFileCount;
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            CommonUserInterface.SetDefaultDialogPosition(this);
            CommonUserInterface.TryFitWindowInWorkingArea(this);

            this.SearchTerms.SearchTerms.Focus();
        }
    }
}
