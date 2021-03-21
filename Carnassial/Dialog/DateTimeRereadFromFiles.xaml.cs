using Carnassial.Data;
using Carnassial.Util;
using System;
using System.Windows;

namespace Carnassial.Dialog
{
    public partial class DateTimeRereadFromFiles : WindowWithSystemMenu
    {
        private readonly TimeSpan desiredStatusInterval;
        private readonly FileDatabase fileDatabase;

        public DateTimeRereadFromFiles(FileDatabase fileDatabase, TimeSpan desiredStatusInterval, Window owner)
        {
            this.InitializeComponent();
            this.desiredStatusInterval = desiredStatusInterval;
            this.fileDatabase = fileDatabase;
            this.Message.SetVisibility();
            this.Owner = owner;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }

        private void DoneButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }

        private void ReportStatus(ObservableStatus<DateTimeRereadResult> status)
        {
            status.FeedbackRows.SendElementsCreatedEvents(status.CurrentFileIndex);
            int currentIndex = Math.Max(status.CurrentFileIndex - 1, 0);
            this.FeedbackGrid.ScrollIntoView(this.FeedbackGrid.Items[currentIndex]);
        }

        private async void StartButton_Click(object sender, RoutedEventArgs e)
        {
            // switch start button to done button
            this.CancelButton.IsEnabled = false;
            this.StartDoneButton.Content = "_Done";
            this.StartDoneButton.Click -= this.StartButton_Click;
            this.StartDoneButton.Click += this.DoneButton_Click;
            this.StartDoneButton.IsEnabled = false;

            ObservableArray<DateTimeRereadResult> feedbackRows = new(this.fileDatabase.Files.RowCount, DateTimeRereadResult.Default);
            this.FeedbackGrid.ItemsSource = feedbackRows;

            using (DateTimeRereadIOComputeTransactionManager rereadDateTimes = new(this.ReportStatus, feedbackRows, this.desiredStatusInterval))
            {
                await rereadDateTimes.RereadDateTimesAsync(this.fileDatabase).ConfigureAwait(true);
            }

            // enable done button so user can exit dialog
            this.StartDoneButton.IsEnabled = true;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            CommonUserInterface.SetDefaultDialogPosition(this);
            CommonUserInterface.TryFitWindowInWorkingArea(this);
        }
    }
}
