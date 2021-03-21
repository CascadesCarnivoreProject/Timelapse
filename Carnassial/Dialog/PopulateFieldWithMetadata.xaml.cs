using Carnassial.Data;
using Carnassial.Images;
using Carnassial.Util;
using MetadataExtractor;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Carnassial.Dialog
{
    /// <summary>
    /// This dialog displays a list of file database columns using note controls and a list of metadata found in the current file.  It 
    /// asks the user to select one from each.  The user can then populate the selected data field with the corresponding metadata value
    /// for all selected files.
    /// </summary>
    /// <remarks>
    /// The <see cref="DateTimeRereadFromFiles"/> dialog performs a similar but more restricted function, setting files' date and time to
    /// that found in their metadata.  This dialog is also capable of reading dates and times into a note control.  It may, for example,
    /// be desired to compare a file's original metadata date time to its DateTimeOffset after clock corrections have been performed.
    /// </remarks>
    public partial class PopulateFieldWithMetadata : WindowWithSystemMenu
    {
        private bool clearIfNoMetadata;
        private string dataFieldLabel;
        private bool dataFieldSelected;
        private readonly Dictionary<string, string> dataLabelByLabel;
        private readonly TimeSpan desiredStatusInterval;
        private readonly FileDatabase fileDatabase;
        private readonly string filePath;
        private Tag? metadataField;
        private bool metadataFieldSelected;

        public PopulateFieldWithMetadata(FileDatabase fileDatabase, string filePath, TimeSpan desiredStatusInterval, Window owner)
        {
            this.InitializeComponent();
            this.clearIfNoMetadata = false;
            this.dataFieldLabel = String.Empty;
            this.dataFieldSelected = false;
            this.dataLabelByLabel = new Dictionary<string, string>(StringComparer.Ordinal);
            this.desiredStatusInterval = desiredStatusInterval;
            this.fileDatabase = fileDatabase;
            this.filePath = filePath;
            this.metadataField = null;
            this.metadataFieldSelected = false;

            this.Message.SetVisibility();

            if (JpegImage.IsJpeg(filePath))
            {
                this.DataGrid.ItemsSource = JpegImage.LoadMetadata(this.filePath).SelectMany(directory => directory.Tags);
                this.DataGrid.SortByFirstTwoColumnsAscending();
                this.DataGrid.Items.Refresh();
            }
            this.FileName.Content = Path.GetFileName(this.filePath);
            this.Owner = owner;

            foreach (ControlRow control in this.fileDatabase.Controls)
            {
                if (control.ControlType == ControlType.Note)
                {
                    this.dataLabelByLabel.Add(control.Label, control.DataLabel);
                    this.DataFields.Items.Add(control.Label);
                }
            }
        }

        private void CancelDoneButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = (string)this.CancelDone.Content != "Cancel";
        }

        private void ClearIfNoMetadata_Checked(object sender, RoutedEventArgs e)
        {
            this.clearIfNoMetadata = this.ClearIfNoMetadata.IsChecked == true;
        }

        private void NoteFieldsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (this.DataFields.SelectedItem != null)
            {
                this.DataField.Content = this.DataFields.SelectedItem as string;
                this.dataFieldLabel = this.DataFields.SelectedItem as string;
                this.dataFieldSelected = true;
            }

            this.PopulateButton.IsEnabled = this.dataFieldSelected && this.metadataFieldSelected;
        }

        // populate the database with the metadata for the selected field
        private async void PopulateButton_Click(object sender, RoutedEventArgs e)
        {
            // key/value pairs that will be bound to the datagrid feedback so they appear during background worker progress updates
            ObservableArray<MetadataFieldResult> feedbackRows = new(this.fileDatabase.Files.RowCount, MetadataFieldResult.Default);
            this.FeedbackGrid.ItemsSource = feedbackRows;

            // switch UI to feedback datagrid
            this.PopulatingMessage.Text = "Populating the data field '" + this.dataFieldLabel + "' from each file's '" + this.metadataField.Name + "' field.";
            this.PopulateButton.Visibility = Visibility.Collapsed;
            this.ClearIfNoMetadata.Visibility = Visibility.Collapsed;
            this.PrimaryPanel.Visibility = Visibility.Collapsed;
            this.DataFields.Visibility = Visibility.Collapsed;
            this.FeedbackPanel.Visibility = Visibility.Visible;
            this.PanelHeader.Visibility = Visibility.Collapsed;

            string dataLabel = this.dataLabelByLabel[this.dataFieldLabel];
            MetadataIOComputeTransactionManager readMetadata = new(this.ReportStatus, feedbackRows, this.desiredStatusInterval);
            await readMetadata.ReadFieldAsync(this.fileDatabase, dataLabel, this.metadataField, this.clearIfNoMetadata).ConfigureAwait(true);

            this.CancelDone.Content = "Done";
            this.CancelDone.IsEnabled = true;
        }

        private void ReportStatus(ObservableStatus<MetadataFieldResult> status)
        {
            status.FeedbackRows.SendElementsCreatedEvents(status.CurrentFileIndex);
            int currentIndex = Math.Max(status.CurrentFileIndex - 1, 0);
            this.FeedbackGrid.ScrollIntoView(this.FeedbackGrid.Items[currentIndex]);
        }

        private void SelectionGrid_SelectedCellsChanged(object sender, SelectedCellsChangedEventArgs e)
        {
            if (e.AddedCells == null || e.AddedCells.Count == 0)
            {
                return;
            }

            // user selected a row; get the indicated tag
            this.metadataField = (Tag)e.AddedCells[0].Item;
            this.Metadata.Content = this.metadataField.Name;

            this.metadataFieldSelected = true;
            this.PopulateButton.IsEnabled = this.dataFieldSelected && this.metadataFieldSelected;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            CommonUserInterface.SetDefaultDialogPosition(this);
            CommonUserInterface.TryFitWindowInWorkingArea(this);
        }
    }
}
