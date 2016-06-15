﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using Timelapse.Database;
using Timelapse.Images;

namespace Timelapse
{
    /// <summary>
    /// Interaction logic for DialogPopulateFieldWithMetadata.xaml
    /// This dialog displays a list of available data fields (currently the note, date and time fields), and 
    /// a list of metadata found in the current image. It asks the user to select one from each.
    /// The user can then populate the selected data field with the corresponding metadata value from that image for all images.
    /// </summary>
    public partial class DialogPopulateFieldWithMetadata : Window, IDisposable
    {
        private Dictionary<string, string> dataLabelFromLabel = new Dictionary<string, string>();  // DataLabel, Label
        private bool disposed;
        private ExifToolWrapper exifTool;
        private string metaDataName = String.Empty;
        private string noteLabel = String.Empty;
        private string noteDataLabel = String.Empty;

        private ImageDatabase database;
        private string imageFilePath;
        private bool isSelectedDataField = false;
        private bool isSelectedMetaData = false;
        private bool isClearIfNoMetaData = false;

        public DialogPopulateFieldWithMetadata(ImageDatabase database, string imageFilePath)
        {
            this.imageFilePath = imageFilePath;
            this.database = database;
            this.InitializeComponent();
        }

        public void Dispose()
        {
            if (this.disposed == false)
            {
                if (this.exifTool != null)
                {
                    this.exifTool.Dispose();
                }
            }

            GC.SuppressFinalize(this);
            this.disposed = true;
        }

        // After the interface is loaded, 
        // - Load the Exif data into the data grid
        // - Load the names of the note controls into the listbox
        // TODOSAUL: ERROR CHECK CORRUPTED, ETC.. that the exiftool exists, AND THAT WE CAN OPEN THE FILE AND GET THE EXIF AND 
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Make sure the title bar of the dialog box is on the screen. For small screens it may default to being off the screen
            if (this.Left < 10 || this.Top < 10)
            {
                this.Left = this.Owner.Left + (this.Owner.Width - this.ActualWidth) / 2; // Center it horizontally
                this.Top = this.Owner.Top + 20; // Offset it from the windows'top by 20 pixels downwards
            }

            this.lblImageName.Content = this.imageFilePath;
            this.LoadExif();
            this.LoadDataFieldLabels();
        }

        #region Loading EXIF into the data grid
        // Use the ExifToolWrapper to load all the metadata
        // Note that this requires the exiftool(-k).exe to be available in the executables folder 
        internal void LoadExif()
        {
            this.exifTool = new ExifToolWrapper();
            this.exifTool.Start();

            Dictionary<string, string> exifData = this.exifTool.FetchExifFrom(this.imageFilePath);
            this.dg.ItemsSource = exifData; // Bind the dictionary to the data grid. For some reason, I couldn't do this in  xaml
        }

        #endregion

        #region Configuring the data grid appearance
        // Label the column headers
        private void Datagrid_AutoGeneratedColumns(object sender, EventArgs e)
        {
            dg.Columns[0].Header = "MetaData Name";
            dg.Columns[1].Header = "Example Value";
            this.SortDataGrid();
        }

        // Sort the DataGrid by the metadata names (similar to clicking the first column header)
        public void SortDataGrid()
        {
            var column = this.dg.Columns[0];
            ListSortDirection sortDirection = ListSortDirection.Ascending;

            // Clear current sort descriptions
            dg.Items.SortDescriptions.Clear();

            // Add the new sort description
            dg.Items.SortDescriptions.Add(new SortDescription(column.SortMemberPath, sortDirection));

            // Apply sort
            foreach (var col in dg.Columns)
            {
                col.SortDirection = null;
            }
            column.SortDirection = sortDirection;

            // Refresh items to display sort
            dg.Items.Refresh();
        }
        #endregion

        #region Datagrid callbacks
        // The user has selected a row. Get the metadata from that row, and make it the selected metadata.
        private void Datagrid_SelectedCellsChanged(object sender, SelectedCellsChangedEventArgs e)
        {
            IList<DataGridCellInfo> selectedcells = e.AddedCells;

            // Make sure there are actually some selected cells
            if (selectedcells == null || selectedcells.Count == 0)
            {
                return;
            }

            // We should only have a single selected cell, so just grab the first one
            DataGridCellInfo di = selectedcells[0];

            // the selected item is the entire row, where the format returned is [MetadataName , MetadataValue] 
            // Parse out the metadata name
            String[] s = di.Item.ToString().Split(',');  // Get the "[Metadataname" portion before the ','
            this.metaDataName = s[0].Substring(1);              // Remove the leading '['
            this.lblMetaData.Content = this.metaDataName;

            // Note that metadata name may still has spaces in it. We will have to strip it out and check it to make sure its an acceptable data label
            this.isSelectedMetaData = true;
            this.btnPopulate.IsEnabled = this.isSelectedDataField && this.isSelectedMetaData;
        }
        #endregion

        #region Notefields callbacks
        public void LoadDataFieldLabels()
        {
            for (int i = 0; i < this.database.TemplateTable.Rows.Count; i++)
            {
                // Get the values for each control
                DataRow row = this.database.TemplateTable.Rows[i];
                string type = row[Constants.Control.Type].ToString();

                if (type == Constants.Control.Note || type == Constants.DatabaseColumn.Date || type == Constants.DatabaseColumn.Time)
                {
                    string datalabel = row.GetStringField(Constants.Control.DataLabel);
                    string label = row.GetStringField(Constants.Control.Label);
                    this.dataLabelFromLabel.Add(label, datalabel);
                    // this.NoteID = Convert.ToInt32(row[Constants.ID].ToString()); // TODOSAUL: Need to use this ID to pass between controls and data
                    this.lboxNoteFields.Items.Add(label);
                }
            }
        }

        // Listbox Callback indicating the user has selected a data field. 
        private void NoteFieldsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lboxNoteFields.SelectedItem != null)
            {
                this.lblNoteField.Content = lboxNoteFields.SelectedItem as String;
                this.noteLabel = lboxNoteFields.SelectedItem as String;
                this.isSelectedDataField = true;
            }
            // If both the 
            this.btnPopulate.IsEnabled = this.isSelectedDataField && this.isSelectedMetaData;
        }
        #endregion

        #region Populate the database with the metadata for that note field
        // Populate the database with the metadata for that note field
        private void Populate()
        {
            // This list will hold key / value pairs that will be bound to the datagrid feedback, 
            // which is the way to make those pairs appear in the data grid during background worker progress updates
            ObservableCollection<KeyValuePair<string, string>> keyValueList = new ObservableCollection<KeyValuePair<string, string>>();
            this.dgFeedback.ItemsSource = keyValueList;

            // Update the UI to show the feedback datagrid, 
            this.tbPopulatingMessage.Text = "Populating the data field '" + this.noteDataLabel + "' from each image's '" + this.metaDataName + "' metadata ";
            btnPopulate.Visibility = Visibility.Collapsed; // Hide the populate button, as we are now in the act of populating things
            cbClearIfNoMetada.Visibility = Visibility.Collapsed; // Hide the checkbox button for the same reason
            this.PrimaryPanel.Visibility = Visibility.Collapsed;  // Hide the various panels to reveal the feedback datagrid
            this.lboxNoteFields.Visibility = Visibility.Collapsed;
            this.FeedbackPanel.Visibility = Visibility.Visible;
            this.PanelHeader.Visibility = Visibility.Collapsed;

            BackgroundWorker backgroundWorker = new BackgroundWorker() { WorkerReportsProgress = true };
            backgroundWorker.DoWork += (ow, ea) =>
            {  
                // this runs on the background thread; its written as an anonymous delegate
                // We need to invoke this to allow updates on the UI
                this.Dispatcher.Invoke(new Action(() =>
                {
                }));

                // For each row in the database, get the image filename and try to extract the chosen metatag value.
                // If we can't decide if we want to leave the data field alone or to clear it depending on the state of the isClearIfNoMetaData (set via the checkbox)
                // Report progress as needed.
                // This tuple list will hold the id, key and value that we will want to update in the database
                List<Tuple<long, string, string>> imageIDKeyValue = new List<Tuple<long, string, string>>();
                for (int image = 0; image < database.CurrentlySelectedImageCount; image++)
                {
                    ImageProperties imageProperties = database.GetImageByRow(image);
                    string[] tags = { this.metaDataName };
                    Dictionary<string, string> exifData = this.exifTool.FetchExifFrom(imageProperties.GetImagePath(database.FolderPath), tags);
                    if (exifData.Count <= 0)
                    {
                        if (this.isClearIfNoMetaData)
                        {
                            imageIDKeyValue.Add(new Tuple<long, string, string>(imageProperties.ID, this.dataLabelFromLabel[this.noteLabel], String.Empty)); // Clear the data field if there is no metadata...
                            backgroundWorker.ReportProgress(0, new FeedbackMessage(imageProperties.FileName, "No metadata found - data field is cleared"));
                        }
                        else
                        {
                            backgroundWorker.ReportProgress(0, new FeedbackMessage(imageProperties.FileName, "No metadata found - data field remains unaltered"));
                        }
                        continue;
                    }
                    string value = exifData[this.metaDataName];
                    backgroundWorker.ReportProgress(0, new FeedbackMessage(imageProperties.FileName, value));
                    if (image % 250 == 0)
                    {
                        Thread.Sleep(25); // Put in a short delay every now and then, as otherwise the UI may not update.
                    }
                    imageIDKeyValue.Add(new Tuple<long, string, string>(imageProperties.ID, this.dataLabelFromLabel[this.noteLabel], value));
                }

                backgroundWorker.ReportProgress(0, new FeedbackMessage("Writing the data...", "Please wait..."));
                database.UpdateImages(imageIDKeyValue);
                backgroundWorker.ReportProgress(0, new FeedbackMessage("Done", "Done"));
            };
            backgroundWorker.ProgressChanged += (o, ea) =>
            {
                // Get the message and add it to the data structure 
                FeedbackMessage message = (FeedbackMessage)ea.UserState;
                keyValueList.Add(new KeyValuePair<string, string>(message.FileName, message.MetadataValue));

                // Scrolls so the last object added is visible
                this.dgFeedback.ScrollIntoView(dgFeedback.Items[dgFeedback.Items.Count - 1]);
            };
            backgroundWorker.RunWorkerCompleted += (o, ea) =>
            {
                btnCancel.Content = "Done"; // Change the Cancel button to Done, but inactivate it as we don't want the operation to be cancellable (due to worries about database corruption)
                btnCancel.IsEnabled = true;
            };
            backgroundWorker.RunWorkerAsync();
        }
        #endregion

        #region Datagrid appearance
        // Ensures that the columns will have appropriate header names. Can't be set directly in code otherwise
        private void FeedbackDatagrid_AutoGeneratedColumns(object sender, EventArgs e)
        {
            this.dgFeedback.Columns[0].Header = "Image Name";
            this.dgFeedback.Columns[1].Header = "The Metadata Value for " + this.metaDataName;
        }
        #endregion

        #region UI Button Callbacks
        private void PoplulateButton_Click(object sender, RoutedEventArgs e)
        {
            this.Populate();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = ((string)btnCancel.Content == "Cancel") ? false : true;
        }

        // This checkbox sets the state as to whether the data field should be cleared or left alone if there is no metadata
        private void ClearIfNoMetadata_Checked(object sender, RoutedEventArgs e)
        {
            this.isClearIfNoMetaData = (cbClearIfNoMetada.IsChecked == true) ? true : false;
        }
        #endregion

        // Classes that tracks our progress as we load the images
        // These are needed to make the background worker update correctly.
        private class FeedbackMessage
        {
            public string FileName { get; set; }
            public string MetadataValue { get; set; }

            public FeedbackMessage(string fileName, string metadataValue)
            {
                this.FileName = fileName;
                this.MetadataValue = metadataValue;
            }
        }
    }
}
