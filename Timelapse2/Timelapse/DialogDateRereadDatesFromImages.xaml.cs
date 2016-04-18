﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Media.Imaging;
using Timelapse.Database;
using Timelapse.Images;
using Timelapse.Util;

namespace Timelapse
{
    /// <summary>
    /// Interaction logic for DialogDateRereadDatesFromImages.xaml
    /// </summary>
    public partial class DialogDateRereadDatesFromImages : Window
    {
        private ImageDatabase database;

        public DialogDateRereadDatesFromImages(ImageDatabase database)
        {
            this.InitializeComponent();
            this.database = database;
        }

        #region Callbacks

        // If the user click ok, re-read the dates 
        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            // this.CancelButton.IsEnabled = false; // We won't allow the operation to be cancelled, as I am concerned about the database getting corrupted.
            this.RescanDates();
            this.CancelButton.IsEnabled = true;
            this.CancelButton.Content = "Done";
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = ((string)CancelButton.Content == "Cancel") ? false : true;
        }
        #endregion

        private void RescanDates()
        {
            // This list will hold key / value pairs that will be bound to the datagrid feedback, 
            // which is the way to make those pairs appear in the data grid during background worker progress updates
            ObservableCollection<MyFeedbackPair> feedbackPairList = new ObservableCollection<MyFeedbackPair>();
            this.dgFeedback.ItemsSource = feedbackPairList;

            BackgroundWorker bgw = new BackgroundWorker()
            {
                WorkerReportsProgress = true
            };

            bgw.DoWork += (ow, ea) =>
            {   // this runs on the background thread; its written as an anonymous delegate
                // We need to invoke this to allow updates on the UI
                this.Dispatcher.Invoke(new Action(() =>
                {
                    // First, change the UIprovide some feedback
                    bgw.ReportProgress(0, new FeedbackMessage("Pass 1: Examining all images...", "Checking if dates/time differ"));
                }));

                // Pass 1. Check to see what dates/times need updating.
                List<ImageProperties> imagePropertiesList = new List<ImageProperties>();
                int count = database.ImageCount;
                int j = 1;
                for (int i = 0; i < count; i++)
                {
                    // We will store the various times here
                    ImageProperties imageProperties = new ImageProperties(database.ImageDataTable.Rows[i]);
                    string message = String.Empty;
                    try
                    {
                        // Get the image (if its there), get the new dates/times, and add it to the list of images to be updated 
                        // Note that if the image can't be created, we will just to the catch.
                        BitmapSource bmap = imageProperties.LoadImage(database.FolderPath);

                        // First we try to see if  can get a valid and parsable metadata date and time
                        BitmapMetadata meta = (BitmapMetadata)bmap.Metadata;        // Get the data from the metadata
                        if (null != meta.DateTaken)
                        {
                            DateTime dtDate;
                            // all the different formats used by cameras, including ambiguities in month/day vs day/month orders.
                            if (DateTime.TryParse(meta.DateTaken, CultureInfo.InvariantCulture, DateTimeStyles.None, out dtDate))
                            {
                                imageProperties.Date = DateTimeHandler.StandardDateString(dtDate);
                                imageProperties.Time = DateTimeHandler.StandardTimeString(dtDate);
                                message += " Using metadata timestamp";
                            }
                        }
                        else  // Fallback as no meta data: We have to use the file date
                        {
                            // For some reason, different versions of Windows treat creation time and modification time differently, 
                            // giving inconsistent values. So I just check both and take the lesser of the two.
                            FileInfo fileInfo = imageProperties.GetFileInfo(database.FolderPath);
                            DateTime creationTime = File.GetCreationTime(fileInfo.FullName);
                            DateTime writeTime = File.GetLastWriteTime(fileInfo.FullName);
                            DateTime fileTime = (DateTime.Compare(creationTime, writeTime) < 0) ? creationTime : writeTime;
                            imageProperties.Date = DateTimeHandler.StandardDateString(fileTime);
                            imageProperties.Time = DateTimeHandler.StandardTimeString(fileTime);
                            message += " Using File timestamp";
                        }
                        if (imageProperties.Date.Equals(database.ImageDataTable.Rows[i][Constants.DatabaseColumn.Date].ToString()))
                        {
                            message += ", same date";
                            imageProperties.Date = String.Empty; // If its the same, we won't copy it
                        }
                        else
                        {
                            message += ", different date";
                        }
                        if (imageProperties.Time.Equals(database.ImageDataTable.Rows[i][Constants.DatabaseColumn.Time].ToString()))
                        {
                            message += ", same time";
                            imageProperties.Time = String.Empty; // If its the same, we won't copy it
                        }
                        else
                        {
                            message += ", different time";
                        }
                        imagePropertiesList.Add(imageProperties);
                    }
                    catch // Image isn't there
                    {
                        message += " , skipping as cannot open image.";
                    }
                    j++;
                    bgw.ReportProgress(0, new FeedbackMessage(imageProperties.FileName, message));
                    if (i % 100 == 0)
                    {
                        Thread.Sleep(25); // Put in a delay every now and then, as otherwise the UI won't update.
                    }
                }

                // Pass 2. Update each date as needed 
                string msg = String.Empty;
                bgw.ReportProgress(0, new FeedbackMessage("Pass 2: For selected images", "Updating only when dates or times differ..."));

                // This tuple list will hold the id, key and value that we will want to update in the database
                List<Tuple<long, string, string>> list_to_update_db = new List<Tuple<long, string, string>>();
                for (int i = 0; i < imagePropertiesList.Count; i++)
                {
                    if (!imagePropertiesList[i].Date.Equals(String.Empty) && !imagePropertiesList[i].Time.Equals(String.Empty))
                    {
                        // Both date and time need updating
                        list_to_update_db.Add(new Tuple<long, string, string>(imagePropertiesList[i].ID, Constants.DatabaseColumn.Date, imagePropertiesList[i].Date));
                        list_to_update_db.Add(new Tuple<long, string, string>(imagePropertiesList[i].ID, Constants.DatabaseColumn.Time, imagePropertiesList[i].Time));
                        msg = "Date / Time updated to: " + imagePropertiesList[i].Date + " " + imagePropertiesList[i].Time;
                    }
                    else if (!imagePropertiesList[i].Date.Equals(String.Empty))
                    {
                        // Only date needs updating
                        list_to_update_db.Add(new Tuple<long, string, string>(imagePropertiesList[i].ID, Constants.DatabaseColumn.Date, imagePropertiesList[i].Date));
                        msg = "Date updated to: " + imagePropertiesList[i].Date;
                    }
                    else if (!imagePropertiesList[i].Time.Equals(String.Empty))
                    {
                        list_to_update_db.Add(new Tuple<long, string, string>(imagePropertiesList[i].ID, Constants.DatabaseColumn.Time, imagePropertiesList[i].Time));
                        // dbData.RowSetValueFromID(Constants.TIME, imgprop_list[i].FinalTime, imgprop_list[i].ID); // OLD WAY: ONE ROW AT A TIME. Can DELETE THIS
                        msg = "Time updated to: " + imagePropertiesList[i].Time;
                    }
                    else
                    {
                        msg = "Updating not required";
                    }
                    bgw.ReportProgress(0, new FeedbackMessage(imagePropertiesList[i].FileName, msg));
                    if (i % 100 == 0)
                    {
                        Thread.Sleep(25); // Put in a delay every now and then, as otherwise the UI won't update.
                    }
                }
                bgw.ReportProgress(0, new FeedbackMessage("Writing to database...", "Please wait"));
                Thread.Sleep(25);
                database.UpdateImages(list_to_update_db);  // Write the updates to the database
                bgw.ReportProgress(0, new FeedbackMessage("Done", "Done"));
            };
            bgw.ProgressChanged += (o, ea) =>
            {
                FeedbackMessage message = (FeedbackMessage)ea.UserState;
                feedbackPairList.Add(new MyFeedbackPair { Image = message.ImageName, Message = message.Message });
                this.dgFeedback.ScrollIntoView(dgFeedback.Items[dgFeedback.Items.Count - 1]);
            };
            bgw.RunWorkerCompleted += (o, ea) =>
            {
                this.OkButton.IsEnabled = false;
                this.CancelButton.IsEnabled = true;
            };
            bgw.RunWorkerAsync();
        }

        // Used to label the datagrid feedback columns with the appropriate headers
        private void DatagridFeedback_AutoGeneratedColumns(object sender, EventArgs e)
        {
            this.dgFeedback.Columns[0].Header = "Image Name";
            this.dgFeedback.Columns[1].Header = "Date and Time Changes";
        }

        private class MyFeedbackPair
        {
            public string Image { get; set; }
            public string Message { get; set; }
        }
    }
}
