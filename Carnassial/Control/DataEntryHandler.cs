using Carnassial.Data;
using Carnassial.Images;
using Carnassial.Interop;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MessageBox = Carnassial.Dialog.MessageBox;

namespace Carnassial.Control
{
    /// <summary>
    /// Callbacks for data entry or and context menus for propagation.
    /// </summary>
    public class DataEntryHandler : IDisposable
    {
        private bool disposed;
        private readonly Lazy<FileFindReplace> findCriteria;

        public event EventHandler? BulkEdit;
        public FileDatabase FileDatabase { get; private set; }
        public ImageCache ImageCache { get; private set; }
        public bool IsProgrammaticUpdate { get; set; }

        public DataEntryHandler(FileDatabase fileDatabase)
        {
            this.disposed = false;

            this.ImageCache = new ImageCache(fileDatabase);
            this.FileDatabase = fileDatabase;
            this.IsProgrammaticUpdate = false;

            this.findCriteria = new Lazy<FileFindReplace>(() =>
            {
                return new FileFindReplace(this.FileDatabase);
            });
        }

        public FileFindReplace FindReplace
        {
            get { return this.findCriteria.Value; }
        }

        // ask the user to confirm value propagation from the last value
        private static bool? ConfirmCopyForward(string? value, int filesAffected)
        {
            MessageBox messageBox = MessageBox.FromResource(Constant.ResourceKey.DataEntryHandlerConfirmCopyForward, App.Current.MainWindow,
                                                            value,
                                                            filesAffected.ToString(CultureInfo.CurrentCulture));
            return messageBox.ShowDialog();
        }

        // ask the user to confirm propagation to all selected files
        private static bool? ConfirmCopyCurrentValueToAll(string? value, int filesAffected)
        {
            MessageBox messageBox = MessageBox.FromResource(Constant.ResourceKey.DataEntryHandlerConfirmCopyAll, App.Current.MainWindow,
                                                            value,
                                                            filesAffected.ToString(CultureInfo.CurrentCulture));
            return messageBox.ShowDialog();
        }

        // ask the user to confirm value propagation from the last value
        private static bool? ConfirmCopyFromLastNonEmptyValue(string? value, int filesAffected)
        {
            MessageBox messageBox = MessageBox.FromResource(Constant.ResourceKey.DataEntryHandlerConfirmPropagateToHere, App.Current.MainWindow,
                                                            value,
                                                            filesAffected.ToString(CultureInfo.CurrentCulture));
            return messageBox.ShowDialog();
        }

        // enable or disable context menu items
        protected virtual void Container_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            DataEntryControl control = (DataEntryControl)((FrameworkElement)sender).Tag;
            ContextMenu contextMenu = control.ContextMenu;
            if (contextMenu != null && contextMenu.Items != null)
            {
                foreach (object menuObject in contextMenu.Items)
                {
                    if (menuObject is MenuItem == false)
                    {
                        continue;
                    }

                    MenuItem menuItem = (MenuItem)menuObject;
                    DataEntryControlContextMenuItemType menuItemType = (DataEntryControlContextMenuItemType)menuItem.Tag;
                    switch (menuItemType)
                    {
                        case DataEntryControlContextMenuItemType.CopyForward:
                            menuItem.IsEnabled = this.IsCopyForwardPossible(control);
                            break;
                        case DataEntryControlContextMenuItemType.CopyToAll:
                            // nothing to do
                            break;
                        case DataEntryControlContextMenuItemType.PropagateFromLastValue:
                            menuItem.IsEnabled = this.IsCopyFromLastNonEmptyValuePossible(control);
                            break;
                        default:
                            throw new NotSupportedException(String.Format(CultureInfo.CurrentCulture, "Unhandled context menu item type {0}.", menuItemType));
                    }
                }
            }
        }

        [SupportedOSPlatform(Constant.Platform.Windows)]
        public void DeleteFiles(IEnumerable<ImageRow> filesToDelete, bool deleteFilesAndData)
        {
            this.IsProgrammaticUpdate = true;

            List<ImageRow> filesToUpdate = new();
            List<long> fileIDsToDropFromDatabase = new();
            using (Recycler fileOperation = new())
            {
                foreach (ImageRow file in filesToDelete)
                {
                    string filePath = file.GetFilePath(this.FileDatabase.FolderPath);
                    if (File.Exists(filePath))
                    {
                        fileOperation.MoveToRecycleBin(filePath);
                    }

                    // if this file is cached, invalidate it so FileNoLongerAvailable placeholder will be displayed instead of the
                    // cached image
                    this.ImageCache.TryInvalidate(file.ID);

                    if (deleteFilesAndData)
                    {
                        // mark the file row for dropping
                        fileIDsToDropFromDatabase.Add(file.ID);
                    }
                    else
                    {
                        // clear the delete flag
                        // It is potentially desirable to change the classification to FileNoLongerAvailable but doing so is also
                        // potentially undesirable.  For now, prefer not to blindly override color/greyscale/dark classifications.
                        file.DeleteFlag = false;
                        filesToUpdate.Add(file);
                    }
                }
            }

            int filesDroppedOrUpdated;
            if (deleteFilesAndData)
            {
                // drop files
                Debug.Assert(fileIDsToDropFromDatabase.Count > 0, "No files are being deleted.");
                Debug.Assert(filesToUpdate.Count == 0, "Files to update unexpectedly present.");
                filesDroppedOrUpdated = this.FileDatabase.DeleteFiles(fileIDsToDropFromDatabase);
            }
            else
            {
                // update file properties
                Debug.Assert(fileIDsToDropFromDatabase.Count > 0, "Files to drop from database unexpectedly present.");
                Debug.Assert(filesToUpdate.Count > 0, "No files are being to be deleted.");
                using UpdateFileColumnTransactionSequence updateFiles = this.FileDatabase.CreateUpdateFileColumnTransaction(Constant.FileColumn.DeleteFlag);
                updateFiles.UpdateFiles(filesToUpdate);
                updateFiles.Commit();
                filesDroppedOrUpdated = updateFiles.RowsCommitted;
            }

            if (filesDroppedOrUpdated > 0)
            {
                this.BulkEdit?.Invoke(this, EventArgs.Empty);
            }
            this.IsProgrammaticUpdate = false;
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
                this.TrySyncCurrentFileToDatabase();
                this.ImageCache.Dispose();
                this.FileDatabase.Dispose();
            }

            this.disposed = true;
        }

        public static Dictionary<string, object?> GetCopyableFields(ImageRow file, List<DataEntryControl> controls)
        {
            Dictionary<string, object?> copyableFields = new(StringComparer.Ordinal);
            foreach (DataEntryControl control in controls)
            {
                if (control.Copyable)
                {
                    copyableFields.Add(control.PropertyName, file[control.PropertyName]);
                }
            }
            return copyableFields;
        }

        public Dictionary<string, object?> GetCopyableFieldsFromCurrentFile(List<DataEntryControl> controls)
        {
            Debug.Assert(this.ImageCache.IsFileAvailable, "Attempt to copy from nonexistent file");
            return DataEntryHandler.GetCopyableFields(this.ImageCache.Current, controls);
        }

        public bool IsCopyForwardPossible(DataEntryControl control)
        {
            if (this.ImageCache.Current == null || control.Copyable == false)
            {
                return false;
            }

            int filesAffected = this.FileDatabase.CurrentlySelectedFileCount - this.ImageCache.CurrentRow - 1;
            return filesAffected > 0;
        }

        // return true if there is a non-empty value available
        public bool IsCopyFromLastNonEmptyValuePossible(DataEntryControl control)
        {
            Debug.Assert(control.Type != ControlType.DateTime, "Propagate context menu unexpectedly enabled on DateTime control.");
            Debug.Assert(control.Type != ControlType.UtcOffset, "Propagate context menu unexpectedly enabled on UTC offset control.");
            for (int fileIndex = this.ImageCache.CurrentRow - 1; fileIndex >= 0; --fileIndex)
            {
                // search for a file with a value assigned for this field, starting from the previous file
                object? valueToTest = this.FileDatabase.Files[fileIndex][control.PropertyName];
                if (control.IsCopyableValue(valueToTest))
                {
                    return true;
                }
            }
            return false;
        }

        private void MenuContextFilePath_Click(object sender, RoutedEventArgs e)
        {
            Debug.Assert(this.ImageCache.Current != null, "Context menu unexpectedly reached without a file displayed.");
            string filePath = this.ImageCache.Current.GetFilePath(this.FileDatabase.FolderPath);
            Clipboard.SetText(filePath);
        }

        // copy the current value of this control to all files
        private void MenuContextCopyToAll_Click(object sender, RoutedEventArgs e)
        {
            DataEntryControl control = (DataEntryControl)((ContextMenu)((MenuItem)sender).Parent).Tag;
            this.TryCopyToAll(control);
        }

        // propagate the current value of this control forward from this point across the current selection
        private void MenuContextPropagateForward_Click(object sender, RoutedEventArgs e)
        {
            DataEntryControl control = (DataEntryControl)((ContextMenu)((MenuItem)sender).Parent).Tag;
            this.TryCopyForward(control);
        }

        private void MenuContextPropagateFromLastValue_Click(object sender, RoutedEventArgs e)
        {
            DataEntryControl control = (DataEntryControl)((ContextMenu)((MenuItem)sender).Parent).Tag;
            if (this.TryCopyFromLastNonEmptyValue(control, out object? valueToCopy))
            {
                Debug.Assert(this.ImageCache.Current != null);
                this.ImageCache.Current[control.PropertyName] = valueToCopy;
            }
        }

        public int ReplaceAll()
        {
            int filesUpdated = this.FileDatabase.ReplaceAllInFiles(this.FindReplace);
            if (filesUpdated > 0)
            {
                this.BulkEdit?.Invoke(this, EventArgs.Empty);
            }
            return filesUpdated;
        }

        public void SelectFiles(FileSelection selection)
        {
            this.FileDatabase.SelectFiles(selection);
            if (this.ImageCache.IsFileAvailable)
            {
                this.ImageCache.TryInvalidate(this.ImageCache.Current.ID);
            }
        }

        /// <summary>
        /// Add data event handler callbacks for (possibly invisible) controls.
        /// </summary>
        public void SetDataEntryCallbacks(List<DataEntryControl> dataEntryControls)
        {
            // Adds
            // - copy file path option to file and relative path controls
            // - data entry callbacks to editable controls
            // - propagate context menu to copyable controls
            foreach (DataEntryControl dataEntryControl in dataEntryControls)
            {
                ControlRow control = this.FileDatabase.Controls[dataEntryControl.DataLabel];
                if (control.IsFilePathComponent())
                {
                    Debug.Assert(dataEntryControl.ContentReadOnly, "File name and relative path are expected to be read only fields.");
                    Debug.Assert(dataEntryControl.Copyable == false, "File name and relative path are not expected to be copyable fields.");
                    MenuItem filePathItem = new() { Header = "Copy file's path" };
                    filePathItem.Click += this.MenuContextFilePath_Click;
                    dataEntryControl.AppendToContextMenu(filePathItem);
                }

                // add propagate context menu to copyable fields
                if (dataEntryControl.Copyable)
                {
                    Debug.Assert(control.ControlType != ControlType.DateTime, "Propagate context menu unexpectedly enabled on DateTime control.");
                    Debug.Assert(control.ControlType != ControlType.UtcOffset, "Propagate context menu unexpectedly enabled on UTC offset control.");

                    MenuItem menuItemPropagateFromLastValue = new()
                    {
                        Header = "Propagate from the _last non-empty value to here...",
                        Tag = DataEntryControlContextMenuItemType.PropagateFromLastValue
                    };
                    menuItemPropagateFromLastValue.Click += this.MenuContextPropagateFromLastValue_Click;
                    if (dataEntryControl.Type == ControlType.Counter)
                    {
                        menuItemPropagateFromLastValue.Header = "Propagate from the _last non-zero value to here...";
                    }

                    MenuItem menuItemCopyForward = new()
                    {
                        Header = "Copy forward to _end...",
                        ToolTip = "The value of this field will be copied forward from this file to the last file in this set.",
                        Tag = DataEntryControlContextMenuItemType.CopyForward
                    };
                    menuItemCopyForward.Click += this.MenuContextPropagateForward_Click;

                    MenuItem menuItemCopyToAll = new()
                    {
                        Header = "Copy to _all...",
                        ToolTip = "Copy the value of this field to all selected files.",
                        Tag = DataEntryControlContextMenuItemType.CopyToAll
                    };
                    menuItemCopyToAll.Click += this.MenuContextCopyToAll_Click;

                    dataEntryControl.AppendToContextMenu(menuItemPropagateFromLastValue, menuItemCopyForward, menuItemCopyToAll);
                }
            }
        }

        /// <summary>Propagate the current value of this control forward from this point across the current selection.</summary>
        public bool TryCopyForward(DataEntryControl control)
        {
            int filesAffected = this.FileDatabase.CurrentlySelectedFileCount - this.ImageCache.CurrentRow - 1;
            if (filesAffected < 1)
            {
                // should be unreachable as the menu shouldn't be be enabled on the last file
                MessageBox messageBox = MessageBox.FromResource(Constant.ResourceKey.DataEntryHandlerNothingToCopyForward, App.Current.MainWindow);
                messageBox.ShowDialog();
                return false;
            }

            Debug.Assert(this.ImageCache.Current != null);
            string? displayValueForConfirm = this.ImageCache.Current.GetDisplayString(control);
            if (DataEntryHandler.ConfirmCopyForward(displayValueForConfirm, filesAffected) != true)
            {
                return false;
            }

            // update starts on the next row since copying from the current row
            int filesUpdated = this.FileDatabase.UpdateFiles(this.ImageCache.Current, control, this.ImageCache.CurrentRow + 1, this.FileDatabase.CurrentlySelectedFileCount - 1);
            if (filesUpdated > 0)
            {
                this.BulkEdit?.Invoke(this, EventArgs.Empty);
            }
            return true;
        }

        /// <summary>
        /// Copy the closest, non-empty value in a file preceding this one to all intervening files and the current file.
        /// </summary>
        private bool TryCopyFromLastNonEmptyValue(DataEntryControl control, out object? valueToCopy)
        {
            // search for a previous file with a value assigned for this field, starting from the previous row
            ImageRow? fileWithLastNonEmptyValue = null;
            int indexToCopyFrom = Constant.Database.InvalidRow;
            string? displayValueForConfirm = null;
            valueToCopy = null;
            for (int previousIndex = this.ImageCache.CurrentRow - 1; previousIndex >= 0; previousIndex--)
            {
                ImageRow file = this.FileDatabase.Files[previousIndex];
                valueToCopy = file[control.PropertyName];
                if (control.IsCopyableValue(valueToCopy))
                {
                    indexToCopyFrom = previousIndex;
                    fileWithLastNonEmptyValue = file;

                    Debug.Assert(this.ImageCache.Current != null);
                    displayValueForConfirm = this.ImageCache.Current.GetDisplayString(control);
                    break;
                }
            }

            if (indexToCopyFrom == Constant.Database.InvalidRow)
            {
                // Nothing to propagate.  If the menu item is deactivated as expected this shouldn't be reachable.
                MessageBox messageBox = MessageBox.FromResource(Constant.ResourceKey.DataEntryHandlerNothingToPropagate, App.Current.MainWindow);
                messageBox.ShowDialog();
                return false;
            }

            int filesAffected = this.ImageCache.CurrentRow - indexToCopyFrom;
            if (DataEntryHandler.ConfirmCopyFromLastNonEmptyValue(displayValueForConfirm, filesAffected) != true)
            {
                return false;
            }

            Debug.Assert(fileWithLastNonEmptyValue != null);
            int filesUpdated = this.FileDatabase.UpdateFiles(fileWithLastNonEmptyValue, control, indexToCopyFrom + 1, this.ImageCache.CurrentRow);
            if (filesUpdated > 0)
            {
                this.BulkEdit?.Invoke(this, EventArgs.Empty);
            }
            return true;
        }

        /// <summary>Copy the current value of this control to all files.</summary>
        private bool TryCopyToAll(DataEntryControl control)
        {
            Debug.Assert(this.ImageCache.Current != null);

            int filesAffected = this.FileDatabase.CurrentlySelectedFileCount;
            string? displayValueForConfirm = this.ImageCache.Current.GetDisplayString(control);
            if (DataEntryHandler.ConfirmCopyCurrentValueToAll(displayValueForConfirm, filesAffected) != true)
            {
                return false;
            }

            this.TrySyncCurrentFileToDatabase();
            int filesUpdated = this.FileDatabase.UpdateFiles(this.ImageCache.Current, control);
            if (filesUpdated > 0)
            {
                this.BulkEdit?.Invoke(this, EventArgs.Empty);
            }
            return true;
        }

        public static bool TryFindFocusedControl(IInputElement focusedElement, [NotNullWhen(true)] out DataEntryControl? focusedControl)
        {
            if (focusedElement is FrameworkElement focusedFrameworkElement)
            {
                focusedControl = (DataEntryControl)focusedFrameworkElement.Tag;
                if (focusedControl != null)
                {
                    return true;
                }

                // for complex controls which dynamic generate child controls, such as date time pickers, the tag of the focused element can't be set
                // so try to locate a parent of the focused element with a tag indicating the control
                FrameworkElement? parent = null;
                if (focusedFrameworkElement.Parent != null && focusedFrameworkElement.Parent is FrameworkElement parentElement)
                {
                    parent = parentElement;
                }
                else if (focusedFrameworkElement.TemplatedParent != null && focusedFrameworkElement.TemplatedParent is FrameworkElement frameworkElement)
                {
                    parent = frameworkElement;
                }

                if (parent != null)
                {
                    return DataEntryHandler.TryFindFocusedControl(parent, out focusedControl);
                }
            }

            focusedControl = null;
            return false;
        }

        public bool TryFindNext(out int fileIndex)
        {
            int startRow = this.ImageCache.CurrentRow + 1;
            for (int row = startRow; row < this.FileDatabase.Files.RowCount; ++row)
            {
                ImageRow file = this.FileDatabase.Files[row];
                if (this.FindReplace.Matches(file))
                {
                    fileIndex = row;
                    return true;
                }
            }
            for (int row = 0; row < startRow; ++row)
            {
                ImageRow file = this.FileDatabase.Files[row];
                if (this.FindReplace.Matches(file))
                {
                    fileIndex = row;
                    return true;
                }
            }

            fileIndex = Constant.Database.InvalidRow;
            return false;
        }

        public bool TryFindPrevious(out int fileIndex)
        {
            int startRow = this.ImageCache.CurrentRow - 1;
            for (int row = startRow; row >= 0; --row)
            {
                ImageRow file = this.FileDatabase.Files[row];
                if (this.FindReplace.Matches(file))
                {
                    fileIndex = row;
                    return true;
                }
            }
            for (int row = this.FileDatabase.Files.RowCount - 1; row > startRow; --row)
            {
                ImageRow file = this.FileDatabase.Files[row];
                if (this.FindReplace.Matches(file))
                {
                    fileIndex = row;
                    return true;
                }
            }

            fileIndex = Constant.Database.InvalidRow;
            return false;
        }

        public bool TrySyncCurrentFileToDatabase()
        {
            if (this.ImageCache.IsFileAvailable == false)
            {
                return false;
            }

            return this.FileDatabase.TrySyncFileToDatabase(this.ImageCache.Current);
        }
    }
}
