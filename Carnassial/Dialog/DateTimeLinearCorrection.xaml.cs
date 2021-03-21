using Carnassial.Control;
using Carnassial.Data;
using Carnassial.Util;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Windows;

namespace Carnassial.Dialog
{
    /// <summary>
    /// This dialog lets the user specify a corrected date and time of a file. All other dates and times are then corrected by the same amount.
    /// This is useful if (say) the camera was not initialized to the correct date and time.
    /// </summary>
    public partial class DateTimeLinearCorrection : WindowWithSystemMenu
    {
        private bool displayingPreview;
        private readonly DateTimeOffset earliestFileDateTimeUncorrected;
        private readonly ImageRow? earliestFile;
        private readonly FileDatabase fileDatabase;
        private readonly DateTimeOffset latestFileDateTimeUncorrected;
        private readonly ImageRow? latestFile;

        public bool Abort { get; private set; }

        public DateTimeLinearCorrection(FileDatabase fileDatabase, Window owner)
        {
            this.InitializeComponent();
            this.Abort = false;
            this.Message.SetVisibility();
            this.Owner = owner;

            this.displayingPreview = false;
            this.earliestFileDateTimeUncorrected = DateTime.MaxValue.ToUniversalTime();
            this.latestFileDateTimeUncorrected = DateTime.MinValue.ToUniversalTime();
            this.fileDatabase = fileDatabase;

            this.earliestFile = null;
            this.latestFile = null;
            bool foundEarliest = false;
            bool foundLatest = false;
            foreach (ImageRow file in fileDatabase.Files)
            {
                DateTimeOffset fileDateTime = file.DateTimeOffset;

                if (fileDateTime >= this.latestFileDateTimeUncorrected)
                {
                    this.latestFile = file;
                    this.latestFileDateTimeUncorrected = fileDateTime;
                    foundLatest = true;
                }

                if (fileDateTime <= this.earliestFileDateTimeUncorrected)
                {
                    this.earliestFile = file;
                    this.earliestFileDateTimeUncorrected = fileDateTime;
                    foundEarliest = true;
                }
            }

            if ((foundEarliest == false) || (foundLatest == false))
            {
                this.Abort = true;
                return;
            }
            Debug.Assert((this.earliestFile != null) && (this.latestFile != null));

            // configure earliest and latest images
            // Images proper are loaded in Window_Loaded().
            this.EarliestImageFileName.Content = this.earliestFile.FileName;
            this.EarliestImageFileName.ToolTip = this.EarliestImageFileName.Content;

            this.LatestImageFileName.Content = this.latestFile.FileName;
            this.LatestImageFileName.ToolTip = this.LatestImageFileName.Content;

            // configure interval
            this.ClockLastCorrect.Maximum = this.earliestFileDateTimeUncorrected;
            this.ClockLastCorrect.Value = this.earliestFileDateTimeUncorrected;
            this.ClockLastCorrect.ValueChanged += this.Interval_ValueChanged;

            this.ClockDriftMeasured.Minimum = this.latestFileDateTimeUncorrected;
            this.ClockDriftMeasured.Value = this.latestFileDateTimeUncorrected;
            this.ClockDriftMeasured.ValueChanged += this.Interval_ValueChanged;

            // configure adjustment
            this.ClockDrift.Value = this.ClockDrift.Value;
            this.ClockDrift.ValueChanged += this.ClockDrift_ValueChanged;

            // show image times
            this.RefreshImageTimes();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }

        private void ClockDrift_ValueChanged(TimeSpanPicker sender, TimeSpan newTimeSpan)
        {
            this.RefreshImageTimes();

            // the faster the camera's clock the more likely it is the latest image is in the future relative to when it should be
            // relax the measurement time constraint accordingly
            this.ClockDriftMeasured.Minimum = this.latestFileDateTimeUncorrected.DateTime;
            if (this.ClockDrift.Value < TimeSpan.Zero)
            {
                this.ClockDriftMeasured.Minimum += this.ClockDrift.Value;
            }

            // Enable the start button when there's a correction to apply
            this.ChangesButton.IsEnabled = this.ClockDrift.Value != TimeSpan.Zero;
        }

        private void Interval_ValueChanged(DateTimeOffsetPicker sender, DateTimeOffset newDateTime)
        {
            this.RefreshImageTimes();
        }

        private TimeSpan GetAdjustment(TimeSpan intervalFromCorrectToMeasured, DateTimeOffset imageDateTime)
        {
            Debug.Assert(intervalFromCorrectToMeasured >= TimeSpan.Zero, "Interval cannot be negative.");
            if (intervalFromCorrectToMeasured == TimeSpan.Zero)
            {
                return this.ClockDrift.Value;
            }

            double imagePositionInInterval = (double)(imageDateTime - this.ClockLastCorrect.Value).Ticks / (double)intervalFromCorrectToMeasured.Ticks;
            Debug.Assert((-0.0000001 < imagePositionInInterval) && (imagePositionInInterval < 1.0000001), String.Format(CultureInfo.InvariantCulture, "Interval position {0} is not between 0.0 and 1.0.", imagePositionInInterval));

            TimeSpan adjustment = TimeSpan.FromTicks((long)(imagePositionInInterval * this.ClockDrift.Value.Ticks + 0.5));
            // check the duration of the adjustment as slow clocks have negative adjustments
            Debug.Assert((TimeSpan.Zero <= adjustment.Duration()) && (adjustment.Duration() <= this.ClockDrift.Value.Duration()), String.Format(CultureInfo.InvariantCulture, "Expected adjustment {0} to be within [{1} {2}].", adjustment, TimeSpan.Zero, this.ClockDrift.Value));
            return adjustment;
        }

        private TimeSpan GetInterval()
        {
            return this.ClockDriftMeasured.Value - this.ClockLastCorrect.Value;
        }

        private void PreviewDateTimeChanges()
        {
            TimeSpan intervalFromCorrectToMeasured = this.GetInterval();
            foreach (ImageRow file in this.fileDatabase.Files)
            {
                string newDateTime = String.Empty;
                string difference = String.Empty;
                DateTimeOffset imageDateTime = file.DateTimeOffset;

                TimeSpan adjustment = this.GetAdjustment(intervalFromCorrectToMeasured, imageDateTime);
                string status;
                if (adjustment.Duration() >= Constant.Time.DateTimeDatabaseResolution)
                {
                    difference = DateTimeHandler.ToDisplayTimeSpanString(adjustment);
                    status = "Changed";
                    newDateTime = DateTimeHandler.ToDisplayDateTimeString(imageDateTime + adjustment);
                }
                else
                {
                    status = "Unchanged";
                }

                this.DateTimeChangeFeedback.AddFeedbackRow(file.FileName, status, file.GetDisplayDateTime(), newDateTime, difference);
            }

            this.AdjustmentEntryPanel.Visibility = Visibility.Collapsed;
            this.FeedbackPanel.Visibility = Visibility.Visible;
        }

        private void RefreshImageTimes()
        {
            TimeSpan intervalFromCorrectToMeasured = this.GetInterval();

            DateTimeOffset earliestImageDateTime = this.earliestFileDateTimeUncorrected + this.GetAdjustment(intervalFromCorrectToMeasured, this.earliestFileDateTimeUncorrected);
            this.EarliestImageDateTime.Content = DateTimeHandler.ToDisplayDateTimeString(earliestImageDateTime);

            DateTimeOffset latestImageDateTime = this.latestFileDateTimeUncorrected + this.GetAdjustment(intervalFromCorrectToMeasured, this.latestFileDateTimeUncorrected);
            this.LatestImageDateTime.Content = DateTimeHandler.ToDisplayDateTimeString(latestImageDateTime);

            this.ClockDriftMeasured.Minimum = this.latestFileDateTimeUncorrected.DateTime;
        }

        private void ChangesButton_Click(object sender, RoutedEventArgs e)
        {
            // 1st click: show the preview before actually making any changes.
            if (this.displayingPreview == false)
            {
                this.PreviewDateTimeChanges();
                this.displayingPreview = true;
                this.ChangesButton.Content = "_Apply Changes";
                return;
            }

            // 2nd click
            // We've shown the preview, which means the user actually wants to do the changes. 
            // Calculate the date/time difference and Update the database

            // In the single image case the the oldest and newest times will be the same
            // since Carnassial has only whole seconds resolution it's also possible with small selections from fast cameras that multiple images have the same time
            TimeSpan intervalFromCorrectToMeasured = this.GetInterval();
            if (intervalFromCorrectToMeasured == TimeSpan.Zero)
            {
                this.fileDatabase.AdjustFileTimes(this.ClockDrift.Value);
            }
            else
            {
                this.fileDatabase.AdjustFileTimes(
                    (DateTimeOffset imageDateTime) =>
                    {
                        return imageDateTime + this.GetAdjustment(intervalFromCorrectToMeasured, imageDateTime);
                    },
                    0,
                    this.fileDatabase.CurrentlySelectedFileCount - 1);
            }
            this.DialogResult = true;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            CommonUserInterface.SetDefaultDialogPosition(this);
            CommonUserInterface.TryFitWindowInWorkingArea(this);

            Debug.Assert((this.earliestFile != null) && (this.latestFile != null));
            await this.EarliestFileDisplay.DisplayAsync(this.fileDatabase.FolderPath, this.earliestFile).ConfigureAwait(true);
            await this.LatestFileDisplay.DisplayAsync(this.fileDatabase.FolderPath, this.latestFile).ConfigureAwait(true);
            this.ClockDrift.TimeSpanDisplay.Focus();
        }
    }
}
