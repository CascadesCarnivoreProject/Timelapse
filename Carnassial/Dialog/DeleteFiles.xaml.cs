﻿using Carnassial.Data;
using Carnassial.Util;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Carnassial.Dialog
{
    /// <summary>
    /// This dialog box asks the user if he/she wants to delete the files (and possibly the data) of file data rows.  Files are soft deleted and the no
    /// longer available placeholder used.  Data is hard deleted.
    /// </summary>
    public partial class DeleteFiles : Window
    {
        // these variables will hold the values of the passed in parameters
        private bool deleteFileAndData;
        private FileDatabase fileDatabase;
        private List<ImageRow> filesToDelete;

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
            if (deleteCurrentFileOnly)
            {
                string imageOrVideo = filesToDelete[0].IsVideo ? "video" : "image";
                if (deleteFileAndData == false)
                {
                    // delete the current file, but not its data
                    this.Message.Title = String.Format("Delete the current {0} but not its data?", imageOrVideo);
                    this.Message.What = String.Format("Deletes the current {0} (shown below) but not its data.", imageOrVideo);
                    this.Message.Result = String.Format("\u2022 The deleted {0} will be backed up in a subfolder named {1}.{2}", imageOrVideo, Constant.File.DeletedFilesFolder, Environment.NewLine);
                    this.Message.Result += String.Format("\u2022 A placeholder {0} will be shown when you try to view a deleted {0}.", imageOrVideo);
                    this.Message.Hint = String.Format("\u2022 Restore the deleted {0} by manually moving it back to its original location, or{1}", imageOrVideo, Environment.NewLine);
                    this.Message.Hint += String.Format("\u2022 Permanently delete the {0} by deleting it from the {1} folder.", imageOrVideo, Constant.File.DeletedFilesFolder);
                }
                else
                {
                    // delete the current file and its data
                    this.Message.Title = String.Format("Delete the current {0} and its data", imageOrVideo);
                    this.Message.What = String.Format("Deletes the current {0} (shown below) and the data associated with that {0}.", imageOrVideo);
                    this.Message.Result = String.Format("\u2022 The deleted {0} will be backed up in a subfolder named {1}.{2}", imageOrVideo, Constant.File.DeletedFilesFolder, Environment.NewLine);
                    this.Message.Result += String.Format("\u2022 However, the data associated with that {0} will be permanently deleted.", imageOrVideo);
                    this.Message.Hint = String.Format("You can permanently delete the {0} by deleting it from the {1} folder.", imageOrVideo, Constant.File.DeletedFilesFolder);
                }
            }
            else
            {
                int numberOfFilesToDelete = this.filesToDelete.Count;
                this.Message.Title = "Delete " + numberOfFilesToDelete.ToString() + " files marked for deletion in this selection?";
                this.Message.Result = String.Empty;
                if (numberOfFilesToDelete > Constant.LargeNumberOfFilesToDelete)
                {
                    this.Message.Result += "\u2022 Deleting " + numberOfFilesToDelete.ToString() + " files will take a few moments. Please be patient." + Environment.NewLine;
                }
                this.Message.Result += String.Format("\u2022 The deleted files will be backed up in a subfolder named {0}.{1}", Constant.File.DeletedFilesFolder, Environment.NewLine);

                if (deleteFileAndData == false)
                {
                    // delete files which the delete flag set but not their data
                    this.Message.What = "Deletes the " + numberOfFilesToDelete.ToString() + " files marked for deletion (shown below) in this selection, but not the data entered for them.";
                    this.Message.Result += "\u2022 A placeholder image will be shown when you view a deleted file.";
                    this.Message.Hint = "\u2022 Restore deleted files by manually copying or moving them back to their original location, or" + Environment.NewLine;
                    this.Message.Hint += String.Format("\u2022 Permanently delete the files by deleting them from the {0} folder", Constant.File.DeletedFilesFolder);
                }
                else
                {
                    // delete files which have the delete flag set and their data
                    this.Message.Title = "Delete " + numberOfFilesToDelete.ToString() + " images and videos marked for deletion";
                    this.Message.What = "Deletes images and videos marked for deletion (shown below), along with the data entered for them.";
                    this.Message.Result += "\u2022 The data for these files will be permanently deleted.";
                    this.Message.Hint = String.Format("You can permanently delete the files by deleting the {0} folder.", Constant.File.DeletedFilesFolder);
                }
            }
            this.Message.Result += "\u2022 The changes cannot be undone.";
            this.Title = this.Message.Title;
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
            this.OkButton.IsEnabled = (bool)this.Confirm.IsChecked;
        }

        private GridView CreateFileGridDataBindings()
        {
            List<string> dataLabels = this.fileDatabase.GetDataLabelsExceptIDInSpreadsheetOrder();
            dataLabels.Remove(Constant.DatabaseColumn.UtcOffset);

            GridView gridView = new GridView();
            foreach (string dataLabel in dataLabels)
            {
                GridViewColumn column = new GridViewColumn();
                column.DisplayMemberBinding = new Binding(ImageRow.GetDataBindingPath(dataLabel));
                column.Header = ImageRow.GetPropertyName(dataLabel);
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
            Utilities.SetDefaultDialogPosition(this);
            Utilities.TryFitWindowInWorkingArea(this);
        }
    }
}
