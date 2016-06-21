﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Timelapse.Database;

namespace Timelapse
{
    // TODOSAUL: Modify this so it tells the user that they can't delete an image that is not there.
    // TODOSAUL: Here and elsewhere, show the placeholder images scaled to the correct size. May have to keep width/height of original image to do this.

    /// <summary>
    /// This dialog box asks the user if he/she wants to delete the following images
    /// What actually happens is that the image is replaced by a 'dummy' placeholder image,
    /// and the original image is copied into a subfolder called Deleted.
    /// </summary>
    public partial class DialogDeleteImages : Window
    {
        // these variables will hold the values of the passed in parameters
        private string imageFolderPath; // the full folder path where that image is located
        private DataTableBackedList<ImageRow> deletedImageTable;
        private bool deleteData;
        private ImageDatabase database;

        #region Public methods
        /// <summary>
        /// Ask the user if he/she wants to delete one or more images and (depending on whether deleteData is set) the data associated with those images.
        /// Other parameters indicate various specifics of that image that we will use to display and delete it.
        /// deleteData is true when the associated data should be deleted.
        /// useDeleteFlags is true when the user is trying to delete images with the deletion flag set, otherwise its the current image being deleted
        /// </summary>
        public DialogDeleteImages(ImageDatabase database, DataTableBackedList<ImageRow> deletedImageTable, bool deleteData, bool useDeleteFlags)
        {
            this.InitializeComponent();
            Mouse.OverrideCursor = Cursors.Wait;
            this.deletedImageTable = deletedImageTable;
            this.imageFolderPath = database.FolderPath;
            this.deleteData = deleteData;
            this.database = database;

            this.ImageFilesRemovedByID = new List<long>();

            if (this.deleteData)
            {
                this.OkButton.IsEnabled = false;
                this.chkboxConfirm.Visibility = Visibility.Visible;
            }
            else
            {
                this.OkButton.IsEnabled = true;
                this.chkboxConfirm.Visibility = Visibility.Collapsed;
            }
            this.GridGallery.RowDefinitions.Clear();

            // Construct the dialog's text based on the state of the flags
            if (useDeleteFlags == false)
            {
                if (deleteData == false)
                {
                    // Case 1: Delete the current image, but not its data - This is the default and is coded in the XAML
                    this.Message.MessageWhat = "Deletes the current image (shown below) but not its data.";
                    this.Message.MessageResult = "\u2022 The deleted image file will be backed up in a sub-folder named DeletedImages." + Environment.NewLine;
                    this.Message.MessageResult += "\u2022 A placeholder image will be shown when you try to view a deleted image.";
                    this.Message.MessageHint = "\u2022 Restore deleted images by manually copying or moving them back to their original location, or" + Environment.NewLine;
                    this.Message.MessageHint += "\u2022 Delete your image backups by deleting the DeletedImages folder.";
                }
                else
                {
                    // Case 2: Delete the current image and its data
                    this.Message.MessageTitle = "Delete the current image and its data";
                    this.Message.MessageWhat = "Deletes the current image (shown below) and the data associated with that image.";
                    this.Message.MessageResult = "\u2022 The deleted image file will be backed up in a sub-folder named DeletedImages." + Environment.NewLine;
                    this.Message.MessageResult += "\u2022 However, the data associated with that image will be permanently deleted.";
                    this.Message.MessageHint = "You can delete your image backups by deleting the DeletedImages folder.";
                }
            }
            else
            {
                if (deleteData == false)
                {
                    // Case 3: Delete the images that have the delete flag set, but not their data
                    this.Message.MessageTitle = "Delete all images marked for deletion";
                    this.Message.MessageWhat = "\u2022 Deletes all images marked for deletion (shown below) but not the data associated with those images.";
                    this.Message.MessageResult = "\u2022 The deleted image file will be backed up in a sub-folder named DeletedImages." + Environment.NewLine;
                    this.Message.MessageResult += "\u2022 A placeholder image will be shown when you try to view a deleted image.";
                    this.Message.MessageHint = "\u2022 Restore deleted images by manually copying or moving them back to their original location, or" + Environment.NewLine;
                    this.Message.MessageHint += "\u2022 Delete your image backups by deleting the DeletedImages folder";
                }
                else
                {
                    // Case 4: Delete the images that have the delete flag set, and their data
                    this.Message.MessageTitle = "Delete all images marked for deletion and their data";
                    this.Message.MessageWhat = "Deletes all images marked for deletion (shown below) along with the data associated with those images.";
                    this.Message.MessageResult = "\u2022 The deleted image file will be backed up in a sub-folder named DeletedImages" + Environment.NewLine;
                    this.Message.MessageResult += "\u2022 However, the data associated with those images will be permanently deleted.";
                    this.Message.MessageHint = "You can delete your image backups by deleting the DeletedImages folder. ";
                }
            }
            this.Title = this.Message.MessageTitle;

            // Set the local variables to the passed in parameters
            int column = 0;
            int row = 0;

            GridLength gridlength200 = new GridLength(1, GridUnitType.Auto);
            GridLength gridlength20 = new GridLength(1, GridUnitType.Auto);

            // SAULTODO: If the number of images to delete is really large, then:
            // - bitmap loading is very slow
            // - the eventual deletion is slow, as we are deleting tons of files
            // SAULTODO: Need to warn the user, and perhaps see if we can make it more efficient, or if we can alter the user interface. 
            foreach (ImageRow imageProperties in deletedImageTable)
            {
                ImageSource bitmap = imageProperties.LoadBitmapThumbnail(database.FolderPath, 400);

                if (column == 0)
                {
                    this.GridGallery.RowDefinitions.Add(new RowDefinition() { Height = gridlength20 });
                    this.GridGallery.RowDefinitions.Add(new RowDefinition() { Height = gridlength200 });
                }

                Label imageLabel = new Label();
                imageLabel.Content = imageProperties.FileName;
                imageLabel.Height = 25;
                imageLabel.VerticalAlignment = VerticalAlignment.Top;

                Image imageControl = new Image();
                imageControl.Source = bitmap;

                Grid.SetRow(imageLabel, row);
                Grid.SetRow(imageControl, row + 1);
                Grid.SetColumn(imageLabel, column);
                Grid.SetColumn(imageControl, column);
                this.GridGallery.Children.Add(imageLabel);
                this.GridGallery.Children.Add(imageControl);
                column++;
                if (column == 5)
                {
                    // A new row is started every five columns
                    column = 0;
                    row += 2;
                }
            }

            this.scroller.CanContentScroll = true;
            Mouse.OverrideCursor = null;
        }
        #endregion

        public List<long> ImageFilesRemovedByID { get; private set; }

        #region Private methods
        /// <summary>
        /// Cancel button selected
        /// </summary>
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }

        /// <summary>
        /// Ok button selected
        /// </summary>
        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            // Don't allow the user to delete ALL their data. 
            // TODOSAUL: Need a general fix to this throughout, where we allow for an empty dataset 
            if (this.deleteData && (this.deletedImageTable.RowCount >= this.database.GetImageCount()))
            {
                DialogMessageBox dlgMsg = new DialogMessageBox();
                dlgMsg.IconType = MessageBoxImage.Error;
                dlgMsg.MessageTitle = "You can't delete all your images";
                dlgMsg.MessageProblem = "You can't delete all your images";
                dlgMsg.MessageReason = "Timelapse must have at least one image to display.";
                dlgMsg.MessageSolution = "Select only a subset of images to delete.";
                dlgMsg.ShowDialog();
                this.DialogResult = false;
                return;
            }

            List<long> imageIDsToDeleteFromDatabase = new List<long>();
            Mouse.OverrideCursor = Cursors.Wait;
            foreach (ImageRow imageProperties in this.deletedImageTable)
            {
                string markForDeletionDataLabel = this.database.DataLabelFromStandardControlType[Constants.Control.DeleteFlag];
                this.database.UpdateImage(imageProperties.ID, markForDeletionDataLabel, Constants.Boolean.False);
                if (this.deleteData)
                {
                    imageIDsToDeleteFromDatabase.Add(imageProperties.ID);
                }
                else
                {
                    // as only the image file was deleted, change its image quality to missing in the database
                    string imageQualityDataLabel = this.database.DataLabelFromStandardControlType[Constants.DatabaseColumn.ImageQuality];
                    this.database.UpdateImage(imageProperties.ID, imageQualityDataLabel, ImageQualityFilter.Missing.ToString());
                    this.ImageFilesRemovedByID.Add(imageProperties.ID);
                }
                this.TryMoveImageToDeletedImagesFolder(this.imageFolderPath, imageProperties);
            }

            if (this.deleteData)
            {
                this.database.DeleteImages(imageIDsToDeleteFromDatabase);
            }

            this.DialogResult = true;
            Mouse.OverrideCursor = null;
        }

        /// <summary>
        /// Create a backup of the current image file in the backup folder
        /// </summary>
        private bool TryMoveImageToDeletedImagesFolder(string folderPath, ImageRow imageProperties)
        {
            string sourceFilePath = imageProperties.GetImagePath(folderPath);
            if (!File.Exists(sourceFilePath))
            {
                return false;  // If there is no source file, its a missing file so we can't back it up
            }

            // Create a new target folder, if necessary.
            string destinationFolder = Path.Combine(folderPath, Constants.File.DeletedImagesFolder);
            if (!Directory.Exists(destinationFolder))
            {
                Directory.CreateDirectory(destinationFolder);
            }

            // Move the image file to the backup location.           
            string destinationFilePath = Path.Combine(destinationFolder, imageProperties.FileName);
            if (File.Exists(destinationFilePath))
            {
                try
                {
                    // Becaue move doesn't allow overwriting, delete the destination file if it already exists.
                    File.Delete(sourceFilePath);
                    return true;
                }
                catch (IOException e)
                {
                    Debug.Print(e.Message);
                    return false;
                }
            }
            try
            {
                File.Move(sourceFilePath, destinationFilePath);
                return true;
            }
            catch (IOException e)
            {
                Debug.Print(e.Message);
                return false;
            }
        }
        #endregion

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Make sure the title bar of the dialog box is on the screen. For small screens it may default to being off the screen
            if (this.Left < 10 || this.Top < 10)
            {
                this.Left = this.Owner.Left + (this.Owner.Width - this.ActualWidth) / 2; // Center it horizontally
                this.Top = this.Owner.Top + 20; // Offset it from the windows'top by 20 pixels downwards
            }
        }

        private void ConfirmBox_Checked(object sender, RoutedEventArgs e)
        {
            this.OkButton.IsEnabled = (bool)this.chkboxConfirm.IsChecked;
        }
    }
}
