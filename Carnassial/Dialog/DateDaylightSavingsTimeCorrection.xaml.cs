using Carnassial.Data;
using Carnassial.Images;
using Carnassial.Util;
using System;
using System.Windows;

namespace Carnassial.Dialog
{
    /// <summary>
    /// This dialog lets a user enter a time change correction of +/-1 hour, which is propagated backwards or forwards.
    /// </summary>
    public partial class DateDaylightSavingsTimeCorrection : WindowWithSystemMenu
    {
        private readonly int currentFileIndex;
        private readonly FileDatabase fileDatabase;
        private readonly ImageCache imageCache;
        private readonly DateTimeOffset orignalDateTimeOffset;

        public DateDaylightSavingsTimeCorrection(FileDatabase fileDatabase, ImageCache imageCache, Window owner)
        {
            this.InitializeComponent();
            this.currentFileIndex = imageCache.CurrentRow;
            this.fileDatabase = fileDatabase;
            this.imageCache = imageCache;
            this.Message.SetVisibility();
            this.orignalDateTimeOffset = imageCache.Current.DateTimeOffset;
            this.Owner = owner;

            // display file properties
            this.FileName.Content = this.imageCache.Current.FileName;
            this.OriginalDate.Content = this.imageCache.Current.GetDisplayDateTime();
            this.HourButton_Checked(this, null);

            // hook event handlers
            this.AddHour.Checked += this.HourButton_Checked;
            this.SubtractHour.Checked += this.HourButton_Checked;
        }

        // add/subtract an hour propagated forwards/backwards as specified
        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            int startRow = this.currentFileIndex;
            int endRow = this.currentFileIndex;
            if (this.PropagateForward.IsChecked ?? false)
            {
                endRow = this.fileDatabase.CurrentlySelectedFileCount - 1;
            }
            else
            {
                startRow = 0;
            }

            // update the database
            int hours = (this.AddHour.IsChecked ?? false) ? 1 : -1;
            TimeSpan daylightSavingsAdjustment = new(hours, 0, 0);
            this.fileDatabase.AdjustFileTimes(daylightSavingsAdjustment, startRow, endRow); // For all rows...
            this.DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }

        private void HourButton_Checked(object sender, RoutedEventArgs e)
        {
            int hours = (this.AddHour.IsChecked ?? false) ? 1 : -1;
            TimeSpan daylightSavingsAdjustment = new(hours, 0, 0);
            DateTime dateTime = this.orignalDateTimeOffset.DateTime.Add(daylightSavingsAdjustment);
            this.NewDate.Content = DateTimeHandler.ToDisplayDateTimeString(dateTime);
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            CommonUserInterface.SetDefaultDialogPosition(this);
            CommonUserInterface.TryFitWindowInWorkingArea(this);

            await this.FileDisplay.DisplayAsync(this.fileDatabase.FolderPath, this.imageCache).ConfigureAwait(true);
        }
    }
}
