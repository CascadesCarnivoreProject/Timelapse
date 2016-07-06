﻿using System;
using System.Windows;
using Timelapse.Database;
using Timelapse.Util;

namespace Timelapse
{
    /// <summary>
    /// Interaction logic for DialogDateSwapDayMonth.xaml
    /// </summary>
    public partial class DialogDateSwapDayMonth : Window
    {
        private ImageDatabase database;
        private int rangeStart = 0;
        private int rangeEnd = -1;

        public DialogDateSwapDayMonth(ImageDatabase database)
        {
            this.InitializeComponent();
            this.database = database;

            // We add this in code behind as we don't want to invoke these callbacks when the interface is created.
            // TODOSAUL: both combo boxes route to the same handler which doesn't vary its action depending on the sending control; is this a bug?
            this.cboxOriginalDate.Checked += this.DateBox_Checked;
            this.cboxNewDate.Checked += this.DateBox_Checked;

            // Find the first ambiguous date
            this.NextAmbiguousDate();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Utilities.SetDefaultDialogPosition(this);
            Utilities.TryFitWindowInWorkingArea(this);
        }

        private bool NextAmbiguousDate()
        {
            // The search will search inclusively from the given image number, and will return the first image number that is ambiguous, else -1
            this.rangeStart = this.GetNextAmbiguousDate(this.rangeStart);
            ImageRow imageProperties;
            if (this.rangeStart >= 0)
            {
                // We found an ambiguous date; provide appropriate feedback
                imageProperties = this.database.ImageDataTable[this.rangeStart];
                this.lblOriginalDate.Content = imageProperties.Date;
                // If we can't swap the date, we just return the original unaltered date
                string swappedDate;
                this.lblNewDate.Content = DateTimeHandler.TrySwapSingleDayMonth(imageProperties.Date, out swappedDate) ? swappedDate : imageProperties.Date;

                this.rangeEnd = this.GetLastImageOnSameDay(this.rangeStart);
                this.lblNumberOfImagesWithSameDate.Content = " Images and videos from the same day: ";
                this.lblNumberOfImagesWithSameDate.Content += (this.rangeEnd - this.rangeStart + 1).ToString();
            }
            else
            {
                // No dates are ambiguous; provide appropriate feedback
                this.btnNext.IsEnabled = false; // Disable the 'Next' button

                // Hide date-specific items so they are no longer visible on the screen
                this.lblOriginalDate.Visibility = Visibility.Hidden;
                this.lblNewDate.Visibility = Visibility.Hidden;
                this.cboxOriginalDate.Visibility = Visibility.Hidden;
                this.cboxNewDate.Visibility = Visibility.Hidden;
                this.lblImageName.Visibility = Visibility.Hidden;
                this.spImageArea.Visibility = Visibility.Hidden;
                this.label2.Visibility = Visibility.Hidden;
                this.label3.Visibility = Visibility.Hidden;
                this.lblNumberOfImagesWithSameDate.Content = "No ambiguous dates left";
                this.imgDateImage.Source = null;
                return false;
            }

            // Display the image. While we should be on a valid image (our assumption), we can still show a missing or corrupted file if needed
            this.imgDateImage.Source = imageProperties.LoadBitmap(this.database.FolderPath);
            this.lblImageName.Content = imageProperties.FileName;
            return true;
        }

        #region Callbacks
        private void DateBox_Checked(object sender, RoutedEventArgs e)
        {
            this.database.ExchangeDayAndMonthInImageDates(this.rangeStart, this.rangeEnd);
        }

        // If the user click ok, then exit
        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            this.rangeStart = this.rangeEnd + 1; // Go to the next image
            this.NextAmbiguousDate();

            // Set the radio button back to the orginal date (default)
            // As we do this, unlink and then relink the callback as we don't want to invoke the data update
            this.cboxOriginalDate.Checked -= this.DateBox_Checked;
            this.cboxOriginalDate.IsChecked = true;
            this.cboxOriginalDate.Checked += this.DateBox_Checked;
        }
        #endregion

        private int GetNextAmbiguousDate(int startIndex)
        {
            // Starting from the index, get the date from successive rows and see if the date is ambiguous
            // Note that if the index is out of range, it will return -1, so that's ok.
            for (int index = startIndex; index < this.database.CurrentlySelectedImageCount; index++)
            {
                ImageRow imageProperties = this.database.ImageDataTable[index];
                DateTime imageDateTime;
                if (imageProperties.TryGetDateTime(out imageDateTime) == false)
                {
                    continue; // if we can't get a valid DateTime, skip over this image i.e., don't consider it ambiguous as we can't alter it anyways.
                }
                if (imageDateTime.Day <= Constants.MonthsInYear)
                {
                    return index; // If the date is ambiguous, return the row index. 
                }
            }
            return -1; // -1 means all dates are unambiguous
        }

        // Given a starting index, find its date and then go through the successive images until the date differs.
        // That is, return the final image that is dated the same date as this image
        private int GetLastImageOnSameDay(int startIndex)
        {
            if (startIndex >= this.database.CurrentlySelectedImageCount)
            {
                return -1;   // Make sure index is in range.
            }

            // Parse the provided starting date. Note that this should never fail at this point, but just in case, put out a debug message
            ImageRow imageProperties = this.database.ImageDataTable[startIndex];
            DateTime desiredDateTime;
            if (imageProperties.TryGetDateTime(out desiredDateTime) == false)
            {
                return -1;  // The starting date is not a valid date
            }
            for (int index = startIndex + 1; index < this.database.CurrentlySelectedImageCount; index++)
            {
                // Parse the date for the given record.
                imageProperties = this.database.ImageDataTable[index];
                DateTime imageDateTime;
                if (imageProperties.TryGetDateTime(out imageDateTime) == false)
                {
                    // TODOSAUL: code and comment are inconsistent; which is the desired behaviour?
                    continue; // if we get to an invalid date, return the prior index
                }

                if (desiredDateTime.Date == imageDateTime.Date)
                {
                    continue;
                }
                return index - 1;
            }
            return this.database.CurrentlySelectedImageCount - 1; // if we got here, it means that we arrived at the end of the records
        }
    }
}
