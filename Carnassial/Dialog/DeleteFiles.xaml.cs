using Carnassial.Data;
using Carnassial.Util;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Carnassial.Dialog
{
    /// <summary>
    /// This dialog box asks the user if he/she wants to delete the files (and possibly the data) of file data rows.  Files are soft deleted and the no
    /// longer available placeholder used.  Data is hard deleted.
    /// </summary>
    public partial class DeleteFiles : WindowWithSystemMenu
    {
        private readonly bool deleteFileAndData;
        private readonly FileDatabase fileDatabase;
        private readonly List<ImageRow> filesToDelete;

        /// <summary>
        /// Ask the user if he/she wants to delete one or more files and (depending on whether deleteData is set) the data associated with those files.
        /// Other parameters indicate various specifics of how the deletion was specified, which also determines what is displayed in the interface:
        /// </summary>
        public DeleteFiles(FileDatabase database, List<ImageRow> filesToDelete, bool deleteFileAndData, bool deleteCurrentFileOnly, Window owner)
        {
            this.InitializeComponent();
            this.deleteFileAndData = deleteFileAndData;
            this.fileDatabase = database;
            this.filesToDelete = filesToDelete;
            this.Message.SetVisibility();
            this.Owner = owner;
            this.ThumbnailList.ItemsSource = this.filesToDelete;
            this.ThumbnailList.View = this.CreateFileGridDataBindings();

            if (this.deleteFileAndData)
            {
                this.OkButton.IsEnabled = false;
            }
            else
            {
                this.OkButton.IsEnabled = true;
                this.Confirm.Visibility = Visibility.Collapsed;
            }

            // construct the dialog's text based on the state of the flags
            string messageArgument;
            string messageKey;
            if (deleteCurrentFileOnly)
            {
                messageArgument = filesToDelete[0].IsVideo ? "video" : "image";
                messageKey = deleteFileAndData ? Constant.ResourceKey.DeleteFilesMessageCurrentFileAndData : Constant.ResourceKey.DeleteFilesMessageCurrentFileOnly;
            }
            else
            {
                messageArgument = this.filesToDelete.Count.ToString(CultureInfo.CurrentCulture);
                messageKey = deleteFileAndData ? Constant.ResourceKey.DeleteFilesMessageFilesAndData : Constant.ResourceKey.DeleteFilesMessageFilesOnly;
            }

            this.Message.Initialize(App.FindResource<Message>(messageKey), messageArgument);
        }

        /// <summary>
        /// Cancel button selected
        /// </summary>
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }

        private void ConfirmBox_Checked(object sender, RoutedEventArgs e)
        {
            this.OkButton.IsEnabled = this.Confirm.IsChecked ?? false;
        }

        private GridView CreateFileGridDataBindings()
        {
            List<ControlRow> controlsExceptUtcOffset = this.fileDatabase.Controls.InSpreadsheetOrder().Where(control => String.Equals(control.DataLabel, Constant.FileColumn.UtcOffset, StringComparison.Ordinal) == false).ToList();
            GridView gridView = new();
            foreach (ControlRow control in controlsExceptUtcOffset)
            {
                GridViewColumn column = new()
                {
                    DisplayMemberBinding = new Binding(ImageRow.GetDataBindingPath(control)),
                    Header = ImageRow.GetPropertyName(control.DataLabel)
                };
                gridView.Columns.Add(column);
            }
            return gridView;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            CommonUserInterface.SetDefaultDialogPosition(this);
            CommonUserInterface.TryFitWindowInWorkingArea(this);
        }
    }
}
