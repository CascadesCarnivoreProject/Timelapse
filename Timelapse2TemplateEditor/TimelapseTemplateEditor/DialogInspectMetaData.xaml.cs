﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Timelapse.Images;
using Timelapse.Util;

namespace Timelapse.Editor
{
    /// <summary>
    /// Interaction logic for DialogInspectMetaData.xaml
    /// This dialog displays a list of metadata found in a selected image. 
    /// </summary>
    // Note: There are lots of commonalities between this dialog and DialogPopulate, but its not clear if its worth the effort of factoring the two.
    public partial class DialogInspectMetaData : Window
    {
        private Dictionary<string, string> dataLabelFromLabel = new Dictionary<string, string>();  // DataLabel, Label
        private bool disposed;
        private ExifToolWrapper exifTool;
        private string metaDataName = String.Empty;
        private string noteLabel = String.Empty;
        private string noteDataLabel = String.Empty;

        private string imageFilePath;
        private string folderPath = String.Empty;

        public DialogInspectMetaData(string folderPath)
        {
            this.InitializeComponent();
            this.folderPath = folderPath;
            this.disposed = false;
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (this.disposed)
            {
                return;
            }

            if (disposing)
            {
                if (this.exifTool != null)
                {
                    this.exifTool.Dispose();
                }
            }

            this.disposed = true;
        }

        // After the interface is loaded, 
        // - Load the Exif data into the data grid
        // - Load the names of the note controls into the listbox
        // TODOSAUL: ERROR CHECK CORRUPTED, ETC.. that the exiftool exists, AND THAT WE CAN OPEN THE FILE AND GET THE EXIF AND 
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Utilities.SetDefaultDialogPosition(this);
            Utilities.TryFitWindowInWorkingArea(this);

            this.lblImageName.Content = "--";
            this.LoadExif();
        }

        #region Loading EXIF into the data grid
        // Use the ExifToolWrapper to load all the metadata
        // Note that this requires the exiftool(-k).exe to be available in the executables folder 
        private void LoadExif()
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
            // Select the first row
            if (dg.Items.Count > 0)
            { 
                dg.SelectedIndex = 0;
                dg.Focus();
            }
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
            this.lblMetaData.Text = this.metaDataName;
        }
        #endregion

        #region UI Button Callbacks

        private void InspectImage_Click(object sender, RoutedEventArgs e)
        {
            string filePath;
            if (Utilities.TryGetFileFromUser("Select a typical image file to inspect", ".", String.Format("Image files ({0})|*{0}", Constants.File.JpgFileExtension), out filePath) == true)
            {
                this.imageFilePath = filePath;
                this.lblImageName.Content = Path.GetFileName(this.imageFilePath);
                this.LoadExif();
            }
        }

        private void OkayButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
            return;
        }
        #endregion
    }
}