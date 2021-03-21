using Carnassial.Control;
using Carnassial.Data;
using Carnassial.Dialog;
using Carnassial.Editor.Dialog;
using Carnassial.Editor.Util;
using Carnassial.Github;
using Carnassial.Util;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;
using System.Windows.Threading;
using MessageBox = Carnassial.Dialog.MessageBox;
using SaveFileDialog = System.Windows.Forms.SaveFileDialog;

namespace Carnassial.Editor
{
    public partial class EditorWindow : ApplicationWindow, IDisposable
    {
        // state tracking
        private readonly EditorUserRegistrySettings userSettings;

        private bool controlDataGridBeingUpdatedByCode;
        private bool controlDataGridCellEditForcedByCode;
        private bool disposed;

        // database where the controls and image set defaults are stored
        private TemplateDatabase templateDatabase;

        [SupportedOSPlatform(Constant.Platform.Windows)]
        public EditorWindow()
        {
            App.Current.DispatcherUnhandledException += this.OnUnhandledException;
            this.InitializeComponent();
            this.AddCounterButton.Tag = ControlType.Counter;
            this.AddFixedChoiceButton.Tag = ControlType.FixedChoice;
            this.AddFlagButton.Tag = ControlType.Flag;
            this.AddNoteButton.Tag = ControlType.Note;
            this.DataEntryControls.AllowDrop = true;
            this.Title = EditorConstant.MainWindowBaseTitle;
            CommonUserInterface.TryFitWindowInWorkingArea(this);

            this.controlDataGridBeingUpdatedByCode = false;
            this.controlDataGridCellEditForcedByCode = false;
            this.disposed = false;

            this.MenuOptionsShowAllColumns_Click(this.MenuOptionsShowAllColumns, null);

            // recall state from prior sessions
            this.userSettings = new EditorUserRegistrySettings();

            // populate the most recent databases list
            this.MenuFileRecentTemplates_Refresh();

            if (this.InstructionsScrollViewer.Tag == null)
            {
                Hyperlink tutorialLink = (Hyperlink)LogicalTreeHelper.FindLogicalNode(this.InstructionsScrollViewer.Document, Constant.DialogControlName.InstructionsTutorialLink);
                tutorialLink.NavigateUri = CarnassialConfigurationSettings.GetTutorialBrowserAddress();
                tutorialLink.ToolTip = tutorialLink.NavigateUri.AbsoluteUri;

                this.InstructionsScrollViewer.Tag = true;
            }
        }

        /// <summary>
        /// Adds a row to the table. The row type is decided by the button tags.
        /// Default values are set for the added row, differing depending on type.
        /// </summary>
        private void AddControlButton_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            ControlType controlType = (ControlType)button.Tag;

            this.controlDataGridBeingUpdatedByCode = true;

            this.templateDatabase.AppendUserDefinedControl(controlType);
            this.ControlDataGrid.DataContext = this.templateDatabase.Controls;
            this.ControlDataGrid.ScrollIntoView(this.ControlDataGrid.Items[^1]);

            this.RebuildControlPreview();
            this.SynchronizeSpreadsheetOrderPreview();

            this.controlDataGridBeingUpdatedByCode = false;
        }

        private void ControlDataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.Column.Header.Equals(EditorConstant.ColumnHeader.DataLabel))
            {
                this.ValidateDataLabel(e);
            }
            if ((e.EditAction == DataGridEditAction.Commit) && (this.controlDataGridCellEditForcedByCode == false))
            {
                // flush changes in each cell as user exits it to database and trigger a redraw to update control display
                // Generating a call to ControlDataGrid_RowEditEnding() isn't the most elegant approach but it avoids separate commit
                // pathways for cell and row edits.  Since this is an ending event (rather than an ended devent, which DataGrid lacks)
                // first comitting the cell edit is necessary to flush the change to the ControlRow so that it's visible to the row
                // edit ending handler.
                this.controlDataGridCellEditForcedByCode = true;
                this.ControlDataGrid.CommitEdit(DataGridEditingUnit.Cell, false);
                this.ControlDataGrid.CommitEdit(DataGridEditingUnit.Row, false);
                this.controlDataGridCellEditForcedByCode = false;
            }
        }

        private void ControlDataGridCopyable_Changed(object sender, RoutedEventArgs e)
        {
            CheckBox checkBox = (CheckBox)sender;
            int rowIndex = (int)checkBox.Tag;
            if (checkBox.IsChecked.HasValue)
            {
                int analysisLabelIndex = -1;
                for (int column = 0; column < this.ControlDataGrid.Columns.Count; ++column)
                {
                    string columnHeader = (string)this.ControlDataGrid.Columns[column].Header;
                    if (String.Equals(columnHeader, EditorConstant.ColumnHeader.AnalysisLabel, StringComparison.Ordinal))
                    {
                        analysisLabelIndex = column;
                        break;
                    }
                }
                Debug.Assert(analysisLabelIndex != -1, "Failed to find analysis label column.");

                DataGridRow row = (DataGridRow)this.ControlDataGrid.ItemContainerGenerator.ContainerFromIndex(rowIndex);
                DataGridCellsPresenter presenter = EditorWindow.GetVisualChild<DataGridCellsPresenter>(row);
                DataGridCell analysisLabelCell = (DataGridCell)presenter.ItemContainerGenerator.ContainerFromIndex(analysisLabelIndex);

                // immediately update enable/disable state of analysis label cell
                ControlRow control = this.templateDatabase.Controls[rowIndex];
                if (checkBox.IsChecked.Value)
                {
                    analysisLabelCell.IsEnabled = true;
                }
                else
                {
                    analysisLabelCell.IsEnabled = false;
                    control.AnalysisLabel = false;
                }
            }
        }

        /// <summary>
        /// Sets cell enable/disable and colors when rows are added, moved, or deleted.
        /// </summary>
        private void ControlDataGrid_LayoutUpdated(object sender, EventArgs e)
        {
            for (int rowIndex = 0; rowIndex < this.ControlDataGrid.Items.Count; rowIndex++)
            {
                // for ItemContainerGenerator to work the DataGrid must have VirtualizingStackPanel.IsVirtualizing="False"
                // the following may be more efficient for large grids but are not used as more than dozen or so controls is unlikely
                // this.ControlDataGrid.UpdateLayout();
                // this.ControlDataGrid.ScrollIntoView(rowIndex + 1);
                DataGridRow row = (DataGridRow)this.ControlDataGrid.ItemContainerGenerator.ContainerFromIndex(rowIndex);
                if (row == null)
                {
                    continue;
                }

                // grid cells are editable by default
                // disable cells which should not be editable
                ControlRow control = this.templateDatabase.Controls[rowIndex];
                DataGridCellsPresenter presenter = EditorWindow.GetVisualChild<DataGridCellsPresenter>(row);
                for (int column = 0; column < this.ControlDataGrid.Columns.Count; column++)
                {
                    DataGridCell cell = (DataGridCell)presenter.ItemContainerGenerator.ContainerFromIndex(column);
                    if (cell == null)
                    {
                        // cell will be null for columns with Visibility = Hidden
                        continue;
                    }

                    string columnHeader = (string)this.ControlDataGrid.Columns[column].Header;
                    bool disableCell = DataGridCellExtensions.ShouldDisableCell(control, columnHeader);
                    if (disableCell)
                    {
                        cell.IsEnabled = false;
                    }
                    else
                    {
                        cell.IsEnabled = true;

                        if (String.Equals(columnHeader, EditorConstant.ColumnHeader.MaxWidth, StringComparison.Ordinal))
                        {
                            // set up text boxes in the width column for immediate data binding so user sees the control preview
                            // update promptly
                            // A guard is required as above.  When the DataGrid is first instantiated TextBlocks are used for the
                            // cell content; these are changed to TextBoxes when the user initiates an edit. Therefore, it's OK if
                            // TryGetControl() returns false.
                            if (cell.TryGetControl(out TextBox textBox))
                            {
                                if (textBox.Tag == null)
                                {
                                    textBox.TextChanged += this.ControlDataGrid_WidthChanged;
                                    textBox.Tag = rowIndex;
                                }
                            }
                        }
                        else if (String.Equals(columnHeader, Constant.ControlColumn.Visible, StringComparison.Ordinal))
                        {
                            // set up check boxes in the visible column for immediate data binding so user sees the control preview
                            // update promptly
                            // The LayoutUpdated event fires many times so a guard is required to set the callbacks only once.
                            if (cell.TryGetControl(out CheckBox checkBox))
                            {
                                if (checkBox.Tag == null)
                                {
                                    checkBox.Checked += this.ControlDataGrid_VisibleChanged;
                                    checkBox.Unchecked += this.ControlDataGrid_VisibleChanged;
                                    checkBox.Tag = rowIndex;
                                }
                            }
                            else
                            {
                                Debug.Fail("Could not find check box associated with Visible column for immediate data binding.");
                            }
                        }
                    }

                    if (String.Equals(columnHeader, Constant.ControlColumn.Copyable, StringComparison.Ordinal))
                    {
                        if (cell.TryGetControl(out CheckBox checkBox))
                        {
                            if (checkBox.Tag == null)
                            {
                                checkBox.Checked += this.ControlDataGridCopyable_Changed;
                                checkBox.Unchecked += this.ControlDataGridCopyable_Changed;
                                checkBox.Tag = rowIndex;
                            }
                        }
                        else
                        {
                            Debug.Fail("Could not find check box associated with Copyable column for immediate data binding.");
                        }
                    }
                }
            }
        }

        private void ControlDataGrid_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            switch ((string)this.ControlDataGrid.CurrentColumn.Header)
            {
                // EditorConstant.Control.ControlOrder is not editable
                case EditorConstant.ColumnHeader.DefaultValue:
                    if (this.ControlDataGrid.SelectedIndex < 0)
                    {
                        e.Handled = true;
                        return;
                    }
                    DataGridRow currentRow = (DataGridRow)this.ControlDataGrid.ItemContainerGenerator.ContainerFromIndex(this.ControlDataGrid.SelectedIndex);
                    ControlRow control = (ControlRow)currentRow.Item;
                    switch (control.ControlType)
                    {
                        case ControlType.Counter:
                            e.Handled = !Utilities.IsDigits(e.Text);
                            break;
                        case ControlType.Flag:
                            bool syncControl = false;
                            if (String.Equals(e.Text, Constant.Sql.FalseString, StringComparison.Ordinal) ||
                                String.Equals(e.Text, Constant.Sql.TrueString, StringComparison.Ordinal))
                            {
                                control.DefaultValue = e.Text;
                                syncControl = true;
                            }
                            else if (Boolean.FalseString.StartsWith(e.Text, StringComparison.OrdinalIgnoreCase))
                            {
                                control.DefaultValue = Constant.Sql.FalseString;
                            }
                            else if (Boolean.TrueString.StartsWith(e.Text, StringComparison.OrdinalIgnoreCase))
                            {
                                control.DefaultValue = Constant.Sql.TrueString;
                            }
                            if (syncControl)
                            {
                                this.SyncControlToDatabaseAndPreviews(control);
                            }
                            e.Handled = true;
                            break;
                        case ControlType.FixedChoice:
                            // no restrictions for now
                            // The default value should eventially be limited to one of the well known values defined, however.
                            break;
                        case ControlType.DateTime:
                        case ControlType.Note:
                        case ControlType.UtcOffset:
                            // no restrictions on note or time controls
                            break;
                        default:
                            throw new NotSupportedException(String.Format(CultureInfo.InvariantCulture, "Unhandled control type {0}.", control.ControlType));
                    }
                    break;
                // EditorConstant.Control.ID is not editable
                // EditorConstant.Control.SpreadsheetOrder is not editable
                // Type is not editable
                case EditorConstant.ColumnHeader.MaxWidth:
                    // only allow digits in widths as they must be parseable as integers
                    e.Handled = !Utilities.IsDigits(e.Text);
                    break;
                default:
                    // no restrictions on analysis label, copyable, data label, label, tooltip, or visible columns
                    break;
            }
        }

        private void ControlDataGrid_RowEditEnding(object sender, DataGridRowEditEndingEventArgs e)
        {
            if (!this.controlDataGridBeingUpdatedByCode)
            {
                this.SyncControlToDatabaseAndPreviews((ControlRow)e.Row.Item);
            }
        }

        /// <summary>
        /// Enable or disable the remove control button as appropriate.
        /// </summary>
        private void ControlDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // depending on the bindings used DataGrid may not fire cell edit or row edit ending events
            // In such cases, changes to the previous selection might not be synchronized.
            if (e.RemovedItems != null)
            {
                foreach (object removedItem in e.RemovedItems)
                {
                    if (removedItem is ControlRow previouslySelectedControl)
                    {
                        if (previouslySelectedControl.HasChanges)
                        {
                            this.SyncControlToDatabaseAndPreviews(previouslySelectedControl);
                        }
                    }
                }
            }

            ControlRow control = (ControlRow)this.ControlDataGrid.SelectedItem;
            if (control == null)
            {
                this.RemoveControlButton.IsEnabled = false;
                return;
            }

            this.RemoveControlButton.IsEnabled = !Constant.Control.StandardControls.Contains(control.DataLabel, StringComparer.Ordinal);
        }

        private void ControlDataGrid_VisibleChanged(object sender, RoutedEventArgs e)
        {
            CheckBox checkBox = (CheckBox)sender;
            int rowIndex = (int)checkBox.Tag;
            if (checkBox.IsChecked.HasValue)
            {
                // immediately propagate change in check to underlying data table so user sees control appear or disappear
                // Data grids don't particularly support immediate data binding (though they can be coerced into it by providing
                // unknown template cell types at the expense of other state management issues) so a change in cell focus is,
                // within their model, needed to trigger an update of the control layout preview for Width and Visible at the data
                // grid level.
                ControlRow control = this.templateDatabase.Controls[rowIndex];
                control.Visible = checkBox.IsChecked.Value;
                this.SyncControlToDatabaseAndPreviews(control);
            }
        }

        private void ControlDataGrid_WidthChanged(object sender, RoutedEventArgs e)
        {
            TextBox textBox = (TextBox)sender;
            int rowIndex = (int)textBox.Tag;
            if (Int32.TryParse(textBox.Text, NumberStyles.None, CultureInfo.CurrentCulture, out int newWidth))
            {
                // immediately propagate change in width to underlying data table so user sees control width adjust as they type
                ControlRow control = this.templateDatabase.Controls[rowIndex];
                control.MaxWidth = newWidth;
                this.SyncControlToDatabaseAndPreviews(control);
            }
        }

        private void DataEntryControls_ControlOrderChangedByDragDrop(DataEntryControl controlBeingDragged, DataEntryControl dropTarget)
        {
            Dictionary<string, int> newControlOrderByDataLabel = new(StringComparer.Ordinal);
            int controlOrder = 1;
            foreach (ControlRow control in this.templateDatabase.Controls)
            {
                if (String.Equals(control.DataLabel, controlBeingDragged.DataLabel, StringComparison.Ordinal))
                {
                    continue;
                }

                newControlOrderByDataLabel.Add(control.DataLabel, controlOrder);
                ++controlOrder;
                if (String.Equals(control.DataLabel, dropTarget.DataLabel, StringComparison.Ordinal))
                {
                    newControlOrderByDataLabel.Add(controlBeingDragged.DataLabel, controlOrder);
                    ++controlOrder;
                }
            }

            this.templateDatabase.UpdateDisplayOrder(Constant.ControlColumn.ControlOrder, newControlOrderByDataLabel);
            this.RebuildControlPreview();
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

            if (disposing && (this.templateDatabase != null))
            {
                this.templateDatabase.Dispose();
            }
            this.disposed = true;
        }

        // raise a dialog box that lets the user edit the list of choices or note control default autocompletions
        private void EditWellKnownValues_Click(object sender, RoutedEventArgs e)
        {
            // the button's tag is bound to the ControlOrder of the ControlRow the button is associated with in xaml; find the
            // control with the same control order
            Button button = (Button)sender;
            ControlRow choiceOrNote = this.templateDatabase.Controls.FirstOrDefault(control => control.ControlOrder == (int)button.Tag);
            Debug.Assert(choiceOrNote != null, String.Format(CultureInfo.InvariantCulture, "Control with tag {0} not found.", button.Tag));

            EditWellKnownValues wellKnownValuesDialog = new(button, choiceOrNote.GetWellKnownValues(), this);
            if (wellKnownValuesDialog.ShowDialog() == true)
            {
                choiceOrNote.SetWellKnownValues(wellKnownValuesDialog.Values);
                this.SyncControlToDatabaseAndPreviews(choiceOrNote);
            }
        }

        private void EnableOrDisableMenusAndControls(bool templateLoaded)
        {
            this.AddCounterButton.IsEnabled = templateLoaded;
            this.AddFixedChoiceButton.IsEnabled = templateLoaded;
            this.AddNoteButton.IsEnabled = templateLoaded;
            this.AddFlagButton.IsEnabled = templateLoaded;

            this.MenuFileCloseTemplate.IsEnabled = templateLoaded;
            this.MenuFileNewTemplate.IsEnabled = templateLoaded;
            this.MenuFileOpenTemplate.IsEnabled = !templateLoaded;
            this.MenuFileRecentTemplates.IsEnabled = !templateLoaded;
            this.MenuOptions.IsEnabled = templateLoaded;
            this.MenuView.IsEnabled = templateLoaded;

            if (templateLoaded)
            {
                this.ControlDataGrid.ItemsSource = this.templateDatabase.Controls;

                this.Tabs.SelectedIndex = 1;
                this.Title = Path.GetFileName(this.templateDatabase.FilePath) + " - " + EditorConstant.MainWindowBaseTitle;
                this.userSettings.MostRecentTemplates.SetMostRecent(this.templateDatabase.FilePath);
                this.MenuFileRecentTemplates_Refresh();

                // populate controls interface in UX
                this.RebuildControlPreview();
                this.SynchronizeSpreadsheetOrderPreview();
            }
            else
            {
                this.ControlDataGrid.ItemsSource = null;
                this.DataEntryControls.Clear();
                this.SpreadsheetOrderPreview.Columns.Clear();

                this.Tabs.SelectedIndex = 0;
                this.Title = EditorConstant.MainWindowBaseTitle;
            }
        }

        /// <summary>
        /// Used in this code to get the child of a DataGridRows, DataGridCellsPresenter. This can be used to get the DataGridCell.
        /// WPF does not make it easy to get to the actual cells.
        /// </summary>
        // Code from: http://techiethings.blogspot.com/2010/05/get-wpf-datagrid-row-and-cell.html
        private static T GetVisualChild<T>(Visual parent) where T : Visual
        {
            T child = default;
            int numVisuals = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < numVisuals; i++)
            {
                Visual v = (Visual)VisualTreeHelper.GetChild(parent, i);
                child = v as T;
                if (child == null)
                {
                    child = EditorWindow.GetVisualChild<T>(v);
                }
                if (child != null)
                {
                    break;
                }
            }
            return child;
        }

        private void InitializeDataGrid(string templateDatabasePath)
        {
            // flush any pending edit which might exist
            this.ControlDataGrid.CommitEdit();

            // create a new template file if one does not exist or load a DB file if there is one.
            bool templateLoaded = TemplateDatabase.TryCreateOrOpen(templateDatabasePath, out this.templateDatabase);
            if (templateLoaded == false)
            {
                // notify the user the template couldn't be loaded
                MessageBox messageBox = MessageBox.FromResource(EditorConstant.ResourceKey.EditorWindowTemplateLoadFailed, this, Path.GetFileName(templateDatabasePath));
                messageBox.ShowDialog();
            }

            // update UI
            this.EnableOrDisableMenusAndControls(templateLoaded);
        }

        private void Instructions_Drop(object sender, DragEventArgs dropEvent)
        {
            if (ApplicationWindow.IsSingleTemplateFileDrag(dropEvent, out string templateDatabaseFilePath))
            {
                this.InitializeDataGrid(templateDatabaseFilePath);
            }
        }

        private void MenuFileCloseTemplate_Click(object sender, RoutedEventArgs e)
        {
            // apply any pending edits, including those DataGrid may not fire cell or row edit ending events for
            this.ControlDataGrid.CommitEdit();
            if (this.ControlDataGrid.SelectedItem is ControlRow selectedControl)
            {
                if (selectedControl != null)
                {
                    this.templateDatabase.TrySyncControlToDatabase(selectedControl);
                }
            }

            this.EnableOrDisableMenusAndControls(false);
        }

        /// <summary>
        /// Exits the application.
        /// </summary>
        private void MenuFileExit_Click(object sender, RoutedEventArgs e)
        {
            this.MenuFileCloseTemplate_Click(sender, e);
            Application.Current.Shutdown();
        }

        /// <summary>
        /// Creates a new template of a user chosen name in a user chosen location.
        /// </summary>
        private void MenuFileNewTemplate_Click(object sender, RoutedEventArgs e)
        {
            using SaveFileDialog saveFileDialog = new();
            saveFileDialog.AddExtension = true;
            saveFileDialog.AutoUpgradeEnabled = true;
            saveFileDialog.CheckPathExists = true;
            saveFileDialog.CreatePrompt = false;
            saveFileDialog.DefaultExt = Constant.File.TemplateFileExtension;
            saveFileDialog.FileName = Path.GetFileNameWithoutExtension(Constant.File.DefaultTemplateDatabaseFileName);
            saveFileDialog.Filter = App.FindResource<string>(EditorConstant.ResourceKey.EditorWindowTemplateFileFilter);
            saveFileDialog.OverwritePrompt = true;
            saveFileDialog.Title = App.FindResource<string>(EditorConstant.ResourceKey.EditorWindowTemplateFileSaveNew);

            if (saveFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                this.InitializeDataGrid(saveFileDialog.FileName);
            }
        }

        /// <summary>
        /// Open an existing template.
        /// </summary>
        private void MenuFileOpenTemplate_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new()
            {
                FileName = Path.GetFileNameWithoutExtension(Constant.File.DefaultTemplateDatabaseFileName),
                DefaultExt = Constant.File.TemplateFileExtension,
                Filter = App.FormatResource(EditorConstant.ResourceKey.EditorWindowTemplateFileOpenExistingFilter, Constant.File.TemplateFileExtension),
                Title = App.FindResource<string>(EditorConstant.ResourceKey.EditorWindowTemplateFileOpenExisting)
            };

            if (openFileDialog.ShowDialog() == true)
            {
                this.InitializeDataGrid(openFileDialog.FileName);
            }
        }

        // open a recently used template
        private void MenuFileRecentTemplate_Click(object sender, RoutedEventArgs e)
        {
            string recentTemplatePath = (string)((MenuItem)sender).ToolTip;
            this.InitializeDataGrid(recentTemplatePath);
        }

        /// <summary>
        /// Update the list of recent databases displayed under File -> Recent Databases.
        /// </summary>
        private void MenuFileRecentTemplates_Refresh()
        {
            this.MenuFileRecentTemplates.IsEnabled = this.userSettings.MostRecentTemplates.Count > 0;
            this.MenuFileRecentTemplates.Items.Clear();

            int index = 1;
            foreach (string recentTemplatePath in this.userSettings.MostRecentTemplates)
            {
                MenuItem recentImageSetItem = new();
                recentImageSetItem.Click += this.MenuFileRecentTemplate_Click;
                recentImageSetItem.Header = String.Format(CultureInfo.CurrentCulture, "_{0} {1}", index, recentTemplatePath);
                recentImageSetItem.ToolTip = recentTemplatePath;
                this.MenuFileRecentTemplates.Items.Add(recentImageSetItem);
                ++index;
            }
        }

        private void MenuHelpAbout_Click(object sender, RoutedEventArgs e)
        {
            AboutEditor about = new(this);
            if ((about.ShowDialog() == true) && about.MostRecentCheckForUpdate.HasValue)
            {
                this.userSettings.MostRecentCheckForUpdates = about.MostRecentCheckForUpdate.Value;
            }
        }

        private void MenuOptionsAdvancedImageSetOptions_Click(object sender, RoutedEventArgs e)
        {
            AdvancedImageSetOptions advancedImageSetOptions = new(this.templateDatabase, this);
            advancedImageSetOptions.ShowDialog();
        }

        /// <summary>
        /// Depending on the menu's checkbox state, show all columns or hide selected columns.
        /// </summary>
        private void MenuOptionsShowAllColumns_Click(object sender, RoutedEventArgs e)
        {
            Visibility visibility = this.MenuOptionsShowAllColumns.IsChecked ? Visibility.Visible : Visibility.Collapsed;
            foreach (DataGridColumn column in this.ControlDataGrid.Columns)
            {
                if (column.Header.Equals(EditorConstant.ColumnHeader.ID) ||
                    column.Header.Equals(EditorConstant.ColumnHeader.ControlOrder) ||
                    column.Header.Equals(EditorConstant.ColumnHeader.SpreadsheetOrder))
                {
                    column.Visibility = visibility;
                }
            }
        }

        /// <summary>
        /// Show the dialog that allows a user to inspect image metadata.
        /// </summary>
        private void MenuViewInspectMetadata_Click(object sender, RoutedEventArgs e)
        {
            InspectMetadata inspectMetadata = new(this);
            inspectMetadata.ShowDialog();
        }

        private void OnSpreadsheetOrderChanged(object sender, DataGridColumnEventArgs e)
        {
            DataGrid dataGrid = (DataGrid)sender;
            Dictionary<string, int> spreadsheetOrderByDataLabel = new(StringComparer.Ordinal);
            for (int control = 0; control < dataGrid.Columns.Count; control++)
            {
                string dataLabelFromColumnHeader = (string)dataGrid.Columns[control].Header;
                int newSpreadsheetOrder = dataGrid.Columns[control].DisplayIndex + 1;
                spreadsheetOrderByDataLabel.Add(dataLabelFromColumnHeader, newSpreadsheetOrder);
            }

            this.templateDatabase.UpdateDisplayOrder(Constant.ControlColumn.SpreadsheetOrder, spreadsheetOrderByDataLabel);
        }

        [SupportedOSPlatform(Constant.Platform.Windows)]
        private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            string databasePath = null;
            if (this.templateDatabase != null)
            {
                databasePath = this.templateDatabase.FilePath;
            }
            this.ShowExceptionReportingDialog(App.FindResource<string>(EditorConstant.ResourceKey.EditorWindowException), databasePath, e);
        }

        private void RebuildControlPreview()
        {
            this.DataEntryControls.CreateControls(this.templateDatabase, null, (string dataLabel) => { return this.templateDatabase.Controls[dataLabel].GetWellKnownValues(); });
        }

        private void SyncControlToDatabaseAndPreviews(ControlRow control)
        {
            this.controlDataGridBeingUpdatedByCode = true;
            this.templateDatabase.TrySyncControlToDatabase(control);
            this.controlDataGridBeingUpdatedByCode = false;

            if ((this.DataEntryControls.ControlsByDataLabel.TryGetValue(control.DataLabel, out DataEntryControl controlPreview) == false) ||
                (control.Visible == false))
            {
                // rebuild the controls preview if data label or visibility changed
                // DataEntryControls does not create UI objects for controls which aren't visible, so the TryGetValue() check above triggers when a control is
                // becoming visible.  The Visible == false check handles the case where the control is becoming invisible.
                // Control order is not checked here as the basic comparison
                //   this.DataEntryControls.Controls.IndexOf(controlPreview) != control.ControlOrder
                // has to be adjusted for controls which aren't visible and for the DateTime and UtcOffset ControlRows often mapping to the same DataEntryDateTime.
                // This is complex and not needed as drop events call RebuildControlPreview() rather than this function.
                this.RebuildControlPreview();
            }
            else
            {
                // if the control's data label isn't changing just update preview for control
                // This is a UX responsiveness optimization for simple changes.
                // control.ControlOrder changes are handled elsewhere
                // changes to control.AnalysisLabel have no effect on the preview
                // changes to control.Copyable have no effect on the preview
                // control.DataLabel changes are handled above
                // control.DefaultValue is used to initialize the preview but changes not propagated as the user may have configured the preview differently
                // Default value changes are included in choice updates.
                // control.ID is immutable
                if (control.Label != controlPreview.Label)
                {
                    controlPreview.Label = control.Label;
                }
                // control.List is handled via choice comparison
                List<string> currentValues = control.GetWellKnownValues();
                List<string> previewValues = controlPreview.GetWellKnownValues();
                bool wellKnonwValuesNeedSynchronization = currentValues.Count != previewValues.Count;
                if (wellKnonwValuesNeedSynchronization == false)
                {
                    List<string> allValues = currentValues.Union(previewValues).ToList();
                    wellKnonwValuesNeedSynchronization = allValues.Count != previewValues.Count;
                }
                if (wellKnonwValuesNeedSynchronization)
                {
                    controlPreview.SetWellKnownValues(currentValues);
                }
                // control.SpreadsheetOrder is handled below
                if (control.Tooltip != controlPreview.LabelTooltip)
                {
                    controlPreview.LabelTooltip = control.Tooltip;
                }
                // control.Type is immutable
                // control.Visible changes are handled above
                if (control.MaxWidth != controlPreview.ContentMaxWidth)
                {
                    controlPreview.ContentMaxWidth = control.MaxWidth;
                }
            }

            this.SynchronizeSpreadsheetOrderPreview();
        }

        // incrementally update spreadsheet order preview
        // Incremental updates are used to limit WPF UI tree rebuilds and improve responsiveness.
        private void SynchronizeSpreadsheetOrderPreview()
        {
            List<ControlRow> controlsInSpreadsheetOrder = this.templateDatabase.Controls.OrderBy(control => control.SpreadsheetOrder).ToList();

            // synchronize number of preview columns if number of controls changed
            while (this.SpreadsheetOrderPreview.Columns.Count < controlsInSpreadsheetOrder.Count)
            {
                this.SpreadsheetOrderPreview.Columns.Add(new DataGridTextColumn());
            }
            while (this.SpreadsheetOrderPreview.Columns.Count > controlsInSpreadsheetOrder.Count)
            {
                this.SpreadsheetOrderPreview.Columns.RemoveAt(this.SpreadsheetOrderPreview.Columns.Count - 1);
            }

            // update existing column headers if needed
            for (int controlIndex = 0; controlIndex < controlsInSpreadsheetOrder.Count; ++controlIndex)
            {
                ControlRow control = controlsInSpreadsheetOrder[controlIndex];
                Debug.Assert(String.IsNullOrEmpty(control.DataLabel) == false, "Database constructors should guarantee data labels are not null.");

                DataGridColumn column = this.SpreadsheetOrderPreview.Columns[controlIndex];
                if ((column.Header == null) || (String.Equals((string)column.Header, control.DataLabel, StringComparison.Ordinal) == false))
                {
                    column.Header = control.DataLabel;
                }
            }
        }

        /// <summary>
        /// Removes a row from the table and shifts up the ids on the remaining rows.
        /// Standard controls can't be deleted.
        /// </summary>
        private void RemoveControlButton_Click(object sender, RoutedEventArgs e)
        {
            ControlRow control = (ControlRow)this.ControlDataGrid.SelectedItem;
            if (control == null)
            {
                // nothing to do
                return;
            }

            if (Constant.Control.StandardControls.Contains(control.DataLabel, StringComparer.Ordinal))
            {
                // standard controls cannot be removed
                return;
            }

            this.controlDataGridBeingUpdatedByCode = true;
            this.templateDatabase.RemoveUserDefinedControl(control);

            // update the control panel so it reflects the current values in the database
            this.RebuildControlPreview();
            this.SynchronizeSpreadsheetOrderPreview();

            this.controlDataGridBeingUpdatedByCode = false;
        }

        private void TutorialLink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            if (e.Uri != null)
            {
                Process.Start(e.Uri.AbsoluteUri);
                e.Handled = true;
            }
        }

        private void ValidateDataLabel(DataGridCellEditEndingEventArgs e)
        {
            TextBox textBox = e.EditingElement as TextBox;
            string dataLabel = textBox.Text;

            // if the data label is empty replace it and notify the user
            if (String.IsNullOrWhiteSpace(dataLabel))
            {
                MessageBox messageBox = MessageBox.FromResource(EditorConstant.ResourceKey.EditorWindowDataLabelEmpty, this);
                messageBox.ShowDialog();
                textBox.Text = this.templateDatabase.GetNextUniqueDataLabel(Constant.ControlColumn.DataLabel);
            }

            // if data label is not unique, derive a unique one and notify the user
            for (int row = 0; row < this.templateDatabase.Controls.RowCount; row++)
            {
                ControlRow control = this.templateDatabase.Controls[row];
                if ((this.ControlDataGrid.SelectedIndex != row) && dataLabel.Equals(control.DataLabel, StringComparison.Ordinal))
                {
                    MessageBox messageBox = MessageBox.FromResource(EditorConstant.ResourceKey.EditorWindowDataLabelNotUnique, this, textBox.Text);
                    messageBox.ShowDialog();
                    textBox.Text = this.templateDatabase.GetNextUniqueDataLabel(textBox.Text);
                    break;
                }
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // check for updates
            if (DateTime.UtcNow - this.userSettings.MostRecentCheckForUpdates > Constant.CheckForUpdateInterval)
            {
                Uri latestVersionAddress = CarnassialConfigurationSettings.GetLatestReleaseApiAddress();
                if (latestVersionAddress == null)
                {
                    return;
                }

                GithubReleaseClient updater = new(EditorConstant.ApplicationName, latestVersionAddress);
                updater.TryGetAndParseRelease(false, out Version _);
                this.userSettings.MostRecentCheckForUpdates = DateTime.UtcNow;
            }

            // if a file was passed on the command line, try to open it
            // args[0] is the .exe
            string[] args = Environment.GetCommandLineArgs();
            if (args != null && args.Length > 1)
            {
                string filePath = args[1];
                string fileExtension = Path.GetExtension(filePath);
                if (String.Equals(fileExtension, Constant.File.TemplateFileExtension, StringComparison.OrdinalIgnoreCase))
                {
                    this.InitializeDataGrid(filePath);
                }
            }
        }

        [SupportedOSPlatform(Constant.Platform.Windows)]
        private void Window_Closing(object sender, CancelEventArgs e)
        {
            this.MenuFileCloseTemplate_Click(this, null);

            // persist state to registry
            this.userSettings.WriteToRegistry();
        }
    }
}