﻿using Carnassial.Database;
using Carnassial.Images;
using Carnassial.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Xceed.Wpf.Toolkit;
using MessageBox = Carnassial.Dialog.MessageBox;

namespace Carnassial.Controls
{
    /// <summary>
    /// The code in here propagates values of a control across the various images in various ways.
    /// Note that this is control-type specific, which means this code would have to be modified to handle new control types
    /// </summary>
    public class DataEntryHandler : IDisposable
    {
        private const int CopyForwardIndex = 1;
        private const int PropagateFromLastValueIndex = 0;

        private bool disposed;

        public FileDatabase FileDatabase { get; private set; }
        public ImageCache ImageCache { get; private set; }
        public bool IsProgrammaticControlUpdate { get; set; }

        public DataEntryHandler(FileDatabase fileDatabase)
        {
            this.disposed = false;
            this.ImageCache = new ImageCache(fileDatabase);
            this.FileDatabase = fileDatabase;  // We need a reference to the database if we are going to update it.
            this.IsProgrammaticControlUpdate = false;
        }

        public static void Configure(DateTimePicker dateTimePicker, Nullable<DateTime> defaultValue)
        {
            dateTimePicker.AutoCloseCalendar = true;
            dateTimePicker.Format = DateTimeFormat.Custom;
            dateTimePicker.FormatString = Constants.Time.DateTimeDisplayFormat;
            dateTimePicker.TimeFormat = DateTimeFormat.Custom;
            dateTimePicker.TimeFormatString = Constants.Time.TimeFormat;
            dateTimePicker.Value = defaultValue;
        }

        /// <summary>Propagate the current value of this control forward from this point across the current selection.</summary>
        public void CopyForward(string dataLabel, bool checkForZero)
        {
            int imagesAffected = this.FileDatabase.CurrentlySelectedImageCount - this.ImageCache.CurrentRow - 1;
            if (imagesAffected == 0)
            {
                // Nothing to propagate. Note that we shouldn't really see this, as the menu shouldn't be highlit if we are on the last image
                // But just in case...
                MessageBox messageBox = new MessageBox("Nothing to copy forward.", Application.Current.MainWindow);
                messageBox.Message.Icon = MessageBoxImage.Exclamation;
                messageBox.Message.Reason = "As you are on the last file, there are no files after this.";
                messageBox.ShowDialog();
                return;
            }

            string valueToCopy = this.ImageCache.Current.GetValueDisplayString(dataLabel);
            if (this.ConfirmCopyForward(valueToCopy, imagesAffected, checkForZero) != true)
            {
                return;
            }

            // Update. Note that we start on the next row, as we are copying from the current row.
            this.FileDatabase.UpdateImages(this.ImageCache.Current, dataLabel, this.ImageCache.CurrentRow + 1, this.FileDatabase.CurrentlySelectedImageCount - 1);
        }

        /// <summary>
        /// Copy the last non-empty value in this control preceding this image up to the current image
        /// </summary>
        public string CopyFromLastNonEmptyValue(DataEntryControl control)
        {
            bool checkForZero = control is DataEntryCounter;
            bool isFlag = control is DataEntryFlag;

            int indexToCopyFrom = -1;
            ImageRow valueSource = null;
            string valueToCopy = checkForZero ? "0" : String.Empty;
            for (int previousIndex = this.ImageCache.CurrentRow - 1; previousIndex >= 0; previousIndex--)
            {
                // Search for the row with some value in it, starting from the previous row
                ImageRow image = this.FileDatabase.Files[previousIndex];
                valueToCopy = image.GetValueDatabaseString(control.DataLabel);
                if (valueToCopy == null)
                {
                    continue;
                }

                valueToCopy = valueToCopy.Trim();
                if (valueToCopy.Length > 0)
                {
                    if ((checkForZero && !valueToCopy.Equals("0")) ||             // Skip over non-zero values for counters
                        (isFlag && !valueToCopy.Equals(Constants.Boolean.False, StringComparison.OrdinalIgnoreCase)) || // Skip over false values for flags
                        (!checkForZero && !isFlag))
                    {
                        indexToCopyFrom = previousIndex;    // We found a non-empty value
                        valueSource = image;
                        break;
                    }
                }
            }

            if (indexToCopyFrom < 0)
            {
                // Nothing to propagate. Note that we shouldn't see this, as the menu item should be deactivated if this is the case.
                // But just in case.
                MessageBox messageBox = new MessageBox("Nothing to Propagate to Here.", Application.Current.MainWindow);
                messageBox.Message.Icon = MessageBoxImage.Exclamation;
                messageBox.Message.Reason = "None of the earlier files have anything in this field, so there are no values to propagate.";
                messageBox.ShowDialog();
                return this.FileDatabase.Files[this.ImageCache.CurrentRow].GetValueDisplayString(control.DataLabel); // No change, so return the current value
            }

            int imagesAffected = this.ImageCache.CurrentRow - indexToCopyFrom;
            if (this.ConfirmPropagateFromLastValue(valueToCopy, imagesAffected) != true)
            {
                return this.FileDatabase.Files[this.ImageCache.CurrentRow].GetValueDisplayString(control.DataLabel); // No change, so return the current value
            }

            // Update. Note that we start on the next row, as we are copying from the current row.
            this.FileDatabase.UpdateImages(valueSource, control.DataLabel, indexToCopyFrom + 1, this.ImageCache.CurrentRow);
            return valueToCopy;
        }

        /// <summary>Copy the current value of this control to all images</summary>
        public void CopyToAll(DataEntryControl control)
        {
            bool checkForZero = control is DataEntryCounter;
            int imagesAffected = this.FileDatabase.CurrentlySelectedImageCount;
            string displayValueToCopy = this.ImageCache.Current.GetValueDisplayString(control.DataLabel);
            if (this.ConfirmCopyCurrentValueToAll(displayValueToCopy, imagesAffected, checkForZero) != true)
            {
                return;
            }
            this.FileDatabase.UpdateImages(this.ImageCache.Current, control.DataLabel);
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
                if (this.FileDatabase != null)
                {
                    this.FileDatabase.Dispose();
                }
            }

            this.disposed = true;
        }

        public bool IsCopyForwardPossible(DataEntryControl control)
        {
            if (this.ImageCache.Current == null || control.Copyable == false)
            {
                return false;
            }

            int imagesAffected = this.FileDatabase.CurrentlySelectedImageCount - this.ImageCache.CurrentRow - 1;
            return (imagesAffected > 0) ? true : false;
        }

        // Return true if there is a non-empty value available
        public bool IsCopyFromLastNonEmptyValuePossible(DataEntryControl control)
        {
            bool checkForZero = control is DataEntryCounter;
            int nearestRowWithCopyableValue = -1;
            for (int image = this.ImageCache.CurrentRow - 1; image >= 0; image--)
            {
                // Search for the row with some value in it, starting from the previous row
                string valueToCopy = this.FileDatabase.Files[image].GetValueDatabaseString(control.DataLabel);
                if (String.IsNullOrWhiteSpace(valueToCopy) == false)
                {
                    if ((checkForZero && !valueToCopy.Equals("0")) || !checkForZero)
                    {
                        nearestRowWithCopyableValue = image;    // We found a non-empty value
                        break;
                    }
                }
            }
            return (nearestRowWithCopyableValue >= 0) ? true : false;
        }

        /// <summary>
        /// Add data event handler callbacks for (possibly invisible) controls
        /// </summary>
        public void SetDataEntryCallbacks(Dictionary<string, DataEntryControl> controlsByDataLabel)
        {
            // Add data entry callbacks to all editable controls. When the user changes an image's attribute using a particular control,
            // the callback updates the matching field for that image in the database.
            foreach (KeyValuePair<string, DataEntryControl> pair in controlsByDataLabel)
            {
                if (pair.Value.ContentReadOnly)
                {
                    continue;
                }

                string controlType = this.FileDatabase.FileTableColumnsByDataLabel[pair.Key].ControlType;
                switch (controlType)
                {
                    case Constants.Control.Note:
                    case Constants.DatabaseColumn.File:
                    case Constants.DatabaseColumn.Folder:
                    case Constants.DatabaseColumn.RelativePath:
                        DataEntryNote note = (DataEntryNote)pair.Value;
                        note.ContentControl.TextAutocompleted += this.NoteControl_TextAutocompleted;
                        if (controlType == Constants.Control.Note)
                        {
                            this.SetContextMenuCallbacks(note);
                        }
                        break;
                    case Constants.DatabaseColumn.DateTime:
                        DataEntryDateTime dateTime = (DataEntryDateTime)pair.Value;
                        dateTime.ContentControl.ValueChanged += this.DateTimeControl_ValueChanged;
                        break;
                    case Constants.DatabaseColumn.UtcOffset:
                        DataEntryUtcOffset utcOffset = (DataEntryUtcOffset)pair.Value;
                        utcOffset.ContentControl.ValueChanged += this.UtcOffsetControl_ValueChanged;
                        break;
                    case Constants.DatabaseColumn.DeleteFlag:
                    case Constants.Control.Flag:
                        DataEntryFlag flag = (DataEntryFlag)pair.Value;
                        flag.ContentControl.Checked += this.FlagControl_CheckedChanged;
                        flag.ContentControl.Unchecked += this.FlagControl_CheckedChanged;
                        if (controlType == Constants.Control.Flag)
                        {
                            this.SetContextMenuCallbacks(flag);
                        }
                        break;
                    case Constants.DatabaseColumn.ImageQuality:
                    case Constants.Control.FixedChoice:
                        DataEntryChoice choice = (DataEntryChoice)pair.Value;
                        choice.ContentControl.SelectionChanged += this.ChoiceControl_SelectionChanged;
                        if (controlType == Constants.Control.FixedChoice)
                        {
                            this.SetContextMenuCallbacks(choice);
                        }
                        break;
                    case Constants.Control.Counter:
                        DataEntryCounter counter = (DataEntryCounter)pair.Value;
                        counter.ContentControl.TextChanged += this.CounterControl_TextChanged;
                        this.SetContextMenuCallbacks(counter);
                        break;
                    default:
                        break;
                }
            }
        }

        public static bool TryFindFocusedControl(IInputElement focusedElement, out DataEntryControl focusedControl)
        {
            if (focusedElement is FrameworkElement)
            {
                FrameworkElement focusedFrameworkElement = (FrameworkElement)focusedElement;
                focusedControl = (DataEntryControl)focusedFrameworkElement.Tag;
                if (focusedControl != null)
                {
                    return true;
                }

                // for complex controls which dynamic generate child controls, such as date time pickers, the tag of the focused element can't be set
                // so try to locate a parent of the focused element with a tag indicating the control
                FrameworkElement parent = null;
                if (focusedFrameworkElement.Parent != null && focusedFrameworkElement.TemplatedParent is FrameworkElement)
                {
                    parent = (FrameworkElement)focusedFrameworkElement.Parent;
                }
                else if (focusedFrameworkElement.TemplatedParent != null && focusedFrameworkElement.TemplatedParent is FrameworkElement)
                {
                    parent = (FrameworkElement)focusedFrameworkElement.TemplatedParent;
                }

                if (parent != null)
                {
                    return DataEntryHandler.TryFindFocusedControl(parent, out focusedControl);
                }
            }

            focusedControl = null;
            return false;
        }

        // Ask the user to confirm value propagation from the last value
        private bool? ConfirmCopyForward(string text, int imagesAffected, bool checkForZero)
        {
            text = text.Trim();

            MessageBox messageBox = new MessageBox("Please confirm 'Copy Forward' for this field...", Application.Current.MainWindow, MessageBoxButton.YesNo);
            messageBox.Message.Icon = MessageBoxImage.Question;
            messageBox.Message.What = "The Copy Forward operation is not undoable, and can overwrite existing values.";
            messageBox.Message.Result = "If you select yes, this operation will:" + Environment.NewLine;
            if (!checkForZero && text.Equals(String.Empty))
            {
                messageBox.Message.Result += "\u2022 copy the (empty) value \u00AB" + text + "\u00BB in this field from here to the last of the selected files.";
            }
            else
            {
                messageBox.Message.Result += "\u2022 copy the value \u00AB" + text + "\u00BB in this field from here to the last of the selected files.";
            }
            messageBox.Message.Result += Environment.NewLine + "\u2022 over-write any existing data values in those fields";
            messageBox.Message.Result += Environment.NewLine + "\u2022 will affect " + imagesAffected.ToString() + " files.";
            return messageBox.ShowDialog();
        }

        // Ask the user to confirm value propagation
        private bool? ConfirmCopyCurrentValueToAll(String text, int filesAffected, bool checkForZero)
        {
            text = text.Trim();

            MessageBox messageBox = new MessageBox("Please confirm 'Copy to All' for this field...", Application.Current.MainWindow, MessageBoxButton.YesNo);
            messageBox.Message.Icon = MessageBoxImage.Question;
            messageBox.Message.What = "The Copy to All operation is not undoable, and can overwrite existing values.";
            messageBox.Message.Result = "If you select yes, this operation will:" + Environment.NewLine;
            if (!checkForZero && text.Equals(String.Empty))
            {
                messageBox.Message.Result += "\u2022 clear this field across all " + filesAffected.ToString() + " selected files.";
            }
            else
            {
                messageBox.Message.Result += "\u2022 set this field to \u00AB" + text + "\u00BB across all " + filesAffected.ToString() + " selected files.";
            }
            messageBox.Message.Result += Environment.NewLine + "\u2022 over-write any existing data values in those fields";
            return messageBox.ShowDialog();
        }

        // Ask the user to confirm value propagation from the last value
        private bool? ConfirmPropagateFromLastValue(String text, int imagesAffected)
        {
            text = text.Trim();
            MessageBox messageBox = new MessageBox("Please confirm 'Propagate to Here' for this field.", Application.Current.MainWindow, MessageBoxButton.YesNo);
            messageBox.Message.Icon = MessageBoxImage.Question;
            messageBox.Message.What = "The 'Propagate to Here' operation is not undoable, and can overwrite existing values.";
            messageBox.Message.Reason = "\u2022 The last non-empty value \u00AB" + text + "\u00BB was seen " + imagesAffected.ToString() + " files back." + Environment.NewLine;
            messageBox.Message.Reason += "\u2022 That field's value will be copied across all files between that file and this one in the selection";
            messageBox.Message.Result = "If you select yes: " + Environment.NewLine;
            messageBox.Message.Result = "\u2022 " + imagesAffected.ToString() + " files will be affected.";
            return messageBox.ShowDialog();
        }

        // A callback allowing us to enable or disable particular context menu items
        protected virtual void Container_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            StackPanel stackPanel = (StackPanel)sender;
            DataEntryControl control = (DataEntryControl)stackPanel.Tag;

            MenuItem menuItemCopyForward = (MenuItem)stackPanel.ContextMenu.Items[DataEntryHandler.CopyForwardIndex];
            menuItemCopyForward.IsEnabled = this.IsCopyForwardPossible(control);
            MenuItem menuItemPropagateFromLastValue = (MenuItem)stackPanel.ContextMenu.Items[DataEntryHandler.PropagateFromLastValueIndex];
            menuItemPropagateFromLastValue.IsEnabled = this.IsCopyFromLastNonEmptyValuePossible(control);
        }

        // Whenever the text in a particular counter box changes, update the particular counter field in the database
        private void CounterControl_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (this.IsProgrammaticControlUpdate)
            {
                return;
            }

            TextBox textBox = (TextBox)sender;
            // Remove any characters that are not numbers
            Regex rgx = new Regex("[^0-9]");
            textBox.Text = rgx.Replace(textBox.Text, String.Empty);

            // In this version of Carnassial, we allow the field tp be either a number or empty. We do allow the field to be empty (i.e., blank).
            // If we change our minds about this, uncomment the code below and replace the regexp expression above with the Trim. 
            // However, users have asked for empty counters, as they treat it differently from a 0.
            // If the field is now empty, make the text a 0.  But, as this can make editing awkward, we select the 0 so that further editing will overwrite it.
            // textBox.Text = textBox.Text.Trim();  // Don't allow leading or trailing spaces in the counter
            // if (textBox.Text == String.Empty)
            // {
            // textBox.Text = "0";
            // textBox.Text = String.Empty;
            // textBox.SelectAll();
            // }

            // Get the key identifying the control, and then add its value to the database
            DataEntryControl control = (DataEntryControl)textBox.Tag;
            control.SetContentAndTooltip(textBox.Text);
            this.FileDatabase.UpdateImage(this.ImageCache.Current.ID, control.DataLabel, control.Content);
            return;
        }

        // Whenever the text in a particular fixedChoice box changes, update the particular choice field in the database
        private void ChoiceControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (this.IsProgrammaticControlUpdate)
            {
                return;
            }

            ComboBox comboBox = (ComboBox)sender;
            // Make sure an item was actually selected (it could have been cancelled)
            if (comboBox.SelectedItem == null)
            {
                return;
            }

            // Get the key identifying the control, and then add its value to the database
            DataEntryControl control = (DataEntryControl)comboBox.Tag;
            control.SetContentAndTooltip(comboBox.SelectedItem.ToString());
            this.FileDatabase.UpdateImage(this.ImageCache.Current.ID, control.DataLabel, control.Content);
        }

        private void DateTimeControl_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (this.IsProgrammaticControlUpdate)
            {
                return;
            }

            DateTimePicker dateTimePicker = (DateTimePicker)sender;
            if (dateTimePicker.Value.HasValue == false)
            {
                return;
            }

            // update image data table and write the new DateTime to the database
            this.ImageCache.Current.SetDateTimeOffset(new DateTimeOffset(dateTimePicker.Value.Value, this.ImageCache.Current.UtcOffset));
            dateTimePicker.ToolTip = DateTimeHandler.ToDisplayDateTimeString(dateTimePicker.Value.Value);

            List<ColumnTuplesWithWhere> imageToUpdate = new List<ColumnTuplesWithWhere>() { this.ImageCache.Current.GetDateTimeColumnTuples() };
            this.FileDatabase.UpdateImages(imageToUpdate);
        }

        // Whenever the checked state in a Flag  changes, update the particular choice field in the database
        private void FlagControl_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (this.IsProgrammaticControlUpdate)
            {
                return;
            }

            CheckBox checkBox = (CheckBox)sender;
            // Get the key identifying the control, and then add its value to the database
            string value = ((bool)checkBox.IsChecked) ? Constants.Boolean.True : Constants.Boolean.False;
            DataEntryControl control = (DataEntryControl)checkBox.Tag;
            control.SetContentAndTooltip(value);
            this.FileDatabase.UpdateImage(this.ImageCache.Current.ID, control.DataLabel, control.Content);
            return;
        }

        // Menu selections for propagating or copying the current value of this control to all images
        protected virtual void MenuItemPropagateFromLastValue_Click(object sender, RoutedEventArgs e)
        {
            DataEntryControl control = (DataEntryControl)((MenuItem)sender).Tag;
            control.SetContentAndTooltip(this.CopyFromLastNonEmptyValue(control));
        }

        // Copy the current value of this control to all images
        protected virtual void MenuItemCopyCurrentValue_Click(object sender, RoutedEventArgs e)
        {
            DataEntryControl control = (DataEntryControl)((MenuItem)sender).Tag;
            this.CopyToAll(control);
        }

        // Propagate the current value of this control forward from this point across the current selection
        protected virtual void MenuItemPropagateForward_Click(object sender, RoutedEventArgs e)
        {
            DataEntryControl control = (DataEntryControl)((MenuItem)sender).Tag;
            this.CopyForward(control.DataLabel, control is DataEntryCounter);
        }

        // Whenever the text in a particular note box changes, update the particular note field in the database 
        private void NoteControl_TextAutocompleted(object sender, TextChangedEventArgs e)
        {
            if (this.IsProgrammaticControlUpdate)
            {
                return;
            }

            // update control state write current value to the database
            DataEntryNote control = (DataEntryNote)((TextBox)sender).Tag;
            control.ContentChanged = true;

            this.FileDatabase.UpdateImage(this.ImageCache.Current.ID, control.DataLabel, control.Content);
            this.IsProgrammaticControlUpdate = false;
        }

        private void SetContextMenuCallbacks(DataEntryControl control)
        {
            MenuItem menuItemPropagateFromLastValue = new MenuItem();
            menuItemPropagateFromLastValue.IsCheckable = false;
            menuItemPropagateFromLastValue.Header = "Propagate from the _last non-empty value to here";
            if (control is DataEntryCounter)
            {
                menuItemPropagateFromLastValue.Header = "Propagate from the _last non-zero value to here";
            }
            menuItemPropagateFromLastValue.Click += this.MenuItemPropagateFromLastValue_Click;
            menuItemPropagateFromLastValue.Tag = control;

            MenuItem menuItemCopyForward = new MenuItem();
            menuItemCopyForward.IsCheckable = false;
            menuItemCopyForward.Header = "Copy forward to _end";
            menuItemCopyForward.ToolTip = "The value of this field will be copied forward from this image to the last image in this set";
            menuItemCopyForward.Click += this.MenuItemPropagateForward_Click;
            menuItemCopyForward.Tag = control;

            MenuItem menuItemCopyCurrentValue = new MenuItem();
            menuItemCopyCurrentValue.IsCheckable = false;
            menuItemCopyCurrentValue.Header = "Copy to _all";
            menuItemCopyCurrentValue.Click += this.MenuItemCopyCurrentValue_Click;
            menuItemCopyCurrentValue.Tag = control;

            // DataEntrHandler.PropagateFromLastValueIndex and CopyForwardIndex must be kept in sync with the add order here
            ContextMenu menu = new ContextMenu();
            menu.Items.Add(menuItemPropagateFromLastValue);
            menu.Items.Add(menuItemCopyForward);
            menu.Items.Add(menuItemCopyCurrentValue);

            control.Container.ContextMenu = menu;
            control.Container.PreviewMouseRightButtonDown += this.Container_PreviewMouseRightButtonDown;

            if (control is DataEntryCounter)
            {
                DataEntryCounter counter = (DataEntryCounter)control;
                counter.ContentControl.ContextMenu = menu;
            }
            else if (control is DataEntryNote)
            {
                DataEntryNote note = (DataEntryNote)control;
                note.ContentControl.ContextMenu = menu;
            }
            else if (control is DataEntryChoice)
            {
                DataEntryChoice choice = (DataEntryChoice)control;
                choice.ContentControl.ContextMenu = menu;
            }
            else if (control is DataEntryFlag)
            {
                DataEntryFlag flag = (DataEntryFlag)control;
                flag.ContentControl.ContextMenu = menu;
            }
            else
            {
                throw new NotSupportedException(String.Format("Unhandled control type {0}.", control.GetType().Name));
            }
        }

        private void UtcOffsetControl_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (this.IsProgrammaticControlUpdate)
            {
                return;
            }

            UtcOffsetUpDown utcOffsetPicker = (UtcOffsetUpDown)sender;
            if (utcOffsetPicker.Value.HasValue == false)
            {
                return;
            }

            DateTimeOffset currentImageDateTime = this.ImageCache.Current.GetDateTime();
            DateTimeOffset newImageDateTime = currentImageDateTime.SetOffset(utcOffsetPicker.Value.Value);
            this.ImageCache.Current.SetDateTimeOffset(newImageDateTime);
            utcOffsetPicker.ToolTip = DateTimeHandler.ToDisplayUtcOffsetString(utcOffsetPicker.Value.Value);

            List<ColumnTuplesWithWhere> imageToUpdate = new List<ColumnTuplesWithWhere>() { this.ImageCache.Current.GetDateTimeColumnTuples() };
            this.FileDatabase.UpdateImages(imageToUpdate);  // write the new UtcOffset to the database
        }
    }
}
