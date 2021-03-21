using Carnassial.Command;
using Carnassial.Control;
using Carnassial.Data;
using Carnassial.Data.Spreadsheet;
using Carnassial.Database;
using Carnassial.Dialog;
using Carnassial.Github;
using Carnassial.Images;
using Carnassial.Interop;
using Carnassial.Util;
using Microsoft.WindowsAPICodePack.Dialogs;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Speech.Synthesis;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using MessageBox = Carnassial.Dialog.MessageBox;
using SaveFileDialog = System.Windows.Forms.SaveFileDialog;

namespace Carnassial
{
    public partial class CarnassialWindow : ApplicationWindow, IDisposable
    {
        private bool disposed;
        private readonly Lazy<SpeechSynthesizer> speechSynthesizer;

        public DataEntryHandler? DataHandler { get; private set; }
        public CarnassialState State { get; private set; }

        [SupportedOSPlatform(Constant.Platform.Windows)]
        public CarnassialWindow()
        {
            App.Current.DispatcherUnhandledException += this.OnUnhandledException;
            this.InitializeComponent();

            this.speechSynthesizer = new Lazy<SpeechSynthesizer>(() => new SpeechSynthesizer());
            this.State = new CarnassialState();
            this.Title = Constant.MainWindowBaseTitle;

            // recall user's state from prior sessions
            this.ControlGrid.Width = this.State.ControlGridWidth;
            this.FileView.ColumnDefinitions[2].Width = new GridLength(this.ControlGrid.Width);

            this.MenuOptionsAudioFeedback.IsChecked = this.State.AudioFeedback;
            this.MenuOptionsEnableImportPrompt.IsChecked = !this.State.SuppressImportPrompt;
            this.MenuOptionsOrderFilesByDateTime.IsChecked = this.State.OrderFilesByDateTime;
            this.MenuOptionsSkipFileClassification.IsChecked = this.State.SkipFileClassification;

            this.State.BackupTimer.Tick += this.Backup_TimerTick;
            this.State.FileNavigatorSliderTimer.Tick += this.FileNavigatorSlider_TimerTick;
            this.State.Throttles.FilePlayTimer.Tick += this.FilePlay_TimerTick;

            // populate lists of menu items
            CultureInfo keyboardCulture = NativeMethods.GetKeyboardCulture();
            string keyboardControlPrefix = App.FindResource<string>(Constant.ResourceKey.KeyboardControl, keyboardCulture);
            for (int analysisSlot = 0; analysisSlot < Constant.AnalysisSlots; ++analysisSlot)
            {
                int displaySlot = analysisSlot + 1;
                string displaySlotKeyName = displaySlot.ToString(keyboardCulture);
                string analysisHeader = App.FormatResource(Constant.ResourceKey.AnalysisHeader, displaySlotKeyName);

                MenuItem copyToAnalysisSlot = new();
                copyToAnalysisSlot.Click += this.MenuEditCopyValuesToAnalysis_Click;
                copyToAnalysisSlot.Header = analysisHeader;
                copyToAnalysisSlot.InputGestureText = keyboardControlPrefix + displaySlotKeyName;
                copyToAnalysisSlot.Tag = analysisSlot;
                copyToAnalysisSlot.ToolTip = App.FormatResource(Constant.ResourceKey.AnalysisAssignToolTip, displaySlot);
                this.MenuEditCopyValuesToAnalysis.Items.Add(copyToAnalysisSlot);

                MenuItem pasteFromAnalysisSlot = new();
                pasteFromAnalysisSlot.Click += this.MenuEditPasteFromAnalysis_Click;
                pasteFromAnalysisSlot.Icon = new Image() { Source = Constant.Images.Paste.Value };
                pasteFromAnalysisSlot.InputGestureText = displaySlotKeyName;
                pasteFromAnalysisSlot.IsEnabled = false;
                pasteFromAnalysisSlot.Header = analysisHeader;
                pasteFromAnalysisSlot.Tag = analysisSlot;
                pasteFromAnalysisSlot.ToolTip = App.FormatResource(Constant.ResourceKey.AnalysisPasteToolTip, displaySlot);
                this.MenuEditPasteValuesFromAnalysis.Items.Add(pasteFromAnalysisSlot);
            }
            this.MenuFileRecentImageSets_Refresh();

            this.Top = this.State.CarnassialWindowPosition.Y;
            this.Left = this.State.CarnassialWindowPosition.X;
            this.Height = this.State.CarnassialWindowPosition.Height;
            this.Width = this.State.CarnassialWindowPosition.Width;
            CommonUserInterface.TryFitWindowInWorkingArea(this);

            this.FileDisplay.Display(Constant.Images.NoSelectableFileMessage);
        }

        private string FolderPath
        {
            get
            {
                Debug.Assert(this.IsFileDatabaseAvailable(), "State management failure: attempt to obtain folder path when database is unavailable.");
                Debug.Assert(this.DataHandler != null);
                return this.DataHandler.FileDatabase.FolderPath;
            }
        }

        private void AddCommand(UndoableCommand<CarnassialWindow> command)
        {
            this.State.UndoRedoChain.AddCommand(command);
            this.MenuEditRedo.IsEnabled = this.State.UndoRedoChain.CanRedo;
            this.MenuEditUndo.IsEnabled = this.State.UndoRedoChain.CanUndo;
        }

        private async void Backup_TimerTick(object? sender, EventArgs e)
        {
            if (this.IsFileDatabaseAvailable())
            {
                Debug.Assert(this.DataHandler != null);
                if ((this.DataHandler.FileDatabase.BackupTask == null) || this.DataHandler.FileDatabase.BackupTask.IsCompleted)
                {
                    await this.DataHandler.FileDatabase.TryBackupAsync().ConfigureAwait(true);
                }
            }
        }

        private void ClearStatusMessage()
        {
            this.MessageBar.Text = String.Empty;
        }

        private async Task CloseImageSetAsync()
        {
            if (this.IsFileDatabaseAvailable())
            {
                // stop any backup timer
                this.State.BackupTimer.Stop();

                // persist image set properties if an image set has been opened
                Debug.Assert(this.DataHandler != null);
                if (this.DataHandler.FileDatabase.CurrentlySelectedFileCount > 0)
                {
                    // revert to custom selections to all 
                    if (this.DataHandler.FileDatabase.ImageSet.FileSelection == FileSelection.Custom)
                    {
                        this.DataHandler.FileDatabase.ImageSet.FileSelection = FileSelection.All;
                    }

                    // sync image set properties
                    if (this.FileDisplay != null)
                    {
                        this.DataHandler.FileDatabase.ImageSet.Options.SetFlag(ImageSetOptions.Magnifier, this.FileDisplay.MagnifyingGlassEnabled);
                    }

                    if (this.IsFileAvailable())
                    {
                        this.DataHandler.FileDatabase.ImageSet.MostRecentFileID = this.DataHandler.ImageCache.Current.ID;
                    }

                    // write image set properties to the database
                    this.DataHandler.FileDatabase.TrySyncImageSetToDatabase();

                    // ensure custom filter operator is synchronized in state for writing to user's registry
                    Debug.Assert((this.DataHandler != null) && (this.DataHandler.FileDatabase.CustomSelection != null));
                    this.State.CustomSelectionTermCombiningOperator = this.DataHandler.FileDatabase.CustomSelection.TermCombiningOperator;
                }

                // discard the image set and reset UX for no open image set/no selected files
                // Controls are cleared after the call to ShowFileAsync() so that the show file can clear any data context set on the controls before they're
                // discarded.  The dispose chain under DataEntryHandler blocks until any running backup is complete.
                this.DataHandler.Dispose();
                this.DataHandler = null;
                this.EnableOrDisableMenusAndControls();
                await this.ShowFileAsync(Constant.Database.InvalidRow, false).ConfigureAwait(true);
                this.DataEntryControls.Clear();

                // return to instuction tab
                this.MenuViewShowInstructions_Click(this, null);
            }

            this.State.ResetImageSetRelatedState();
            // reset undo/redo after reset of main state object as it touches some of the same state but also updates the enable state of undo/rendo menu items
            this.ResetUndoRedoState();
            this.SetStatusMessage(Constant.ResourceKey.CarnassialWindowStatusImageSetNone);
            this.Title = Constant.MainWindowBaseTitle;
        }

        /// <summary>When the user selects a counter update the color and emphasis of its markers.</summary>
        private void DataEntryCounter_LabelClick(object sender, RoutedEventArgs e)
        {
            this.RefreshDisplayedMarkers();
        }

        /// <summary>Highlight markers associated with a counter when the mouse enters the counter.</summary>
        private void DataEntryCounter_MouseEnter(object sender, MouseEventArgs e)
        {
            Panel panel = (Panel)sender;
            this.State.MouseOverCounter = ((DataEntryCounter)panel.Tag).DataLabel;
            this.RefreshDisplayedMarkers();
        }

        /// <summary>Remove marker highlighting when the mouse leaves a counter.</summary>
        private void DataEntryCounter_MouseLeave(object sender, MouseEventArgs e)
        {
            this.State.MouseOverCounter = null;
            this.RefreshDisplayedMarkers();
        }

        [SupportedOSPlatform(Constant.Platform.Windows)]
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        [SupportedOSPlatform(Constant.Platform.Windows)]
        protected virtual void Dispose(bool disposing)
        {
            if (this.disposed)
            {
                return;
            }

            if (disposing)
            {
                if (this.DataHandler != null)
                {
                    this.DataHandler.Dispose();
                }
                if (this.speechSynthesizer.IsValueCreated)
                {
                    this.speechSynthesizer.Value.Dispose();
                }
            }

            this.disposed = true;
        }

        private void EnableOrDisableMenusAndControls()
        {
            bool imageSetAvailable = this.IsFileDatabaseAvailable();
            bool filesSelected = false;
            if (imageSetAvailable)
            {
                Debug.Assert(this.DataHandler != null);
                filesSelected = this.DataHandler.FileDatabase.CurrentlySelectedFileCount > 0;
            }

            // enable/disable menus and menu items as appropriate depending upon whether files are selected
            Debug.Assert(this.DataHandler != null);
            // file menu
            this.MenuFileAddFilesToImageSet.IsEnabled = imageSetAvailable;
            this.MenuFileLoadImageSet.IsEnabled = !imageSetAvailable;
            this.MenuFileRecentImageSets.IsEnabled = !imageSetAvailable;
            this.MenuFileCloseImageSet.IsEnabled = imageSetAvailable;
            this.MenuFileCloneCurrent.IsEnabled = filesSelected;
            this.MenuFileExport.IsEnabled = filesSelected;
            this.MenuFileImport.IsEnabled = imageSetAvailable;
            this.MenuFileMoveFiles.IsEnabled = filesSelected;
            this.MenuFileRenameFileDatabase.IsEnabled = filesSelected;
            // edit menu
            this.MenuEdit.IsEnabled = filesSelected;
            // view menu
            this.MenuView.IsEnabled = filesSelected;
            this.MenuViewDisplayMagnifier.IsChecked = imageSetAvailable && this.DataHandler.FileDatabase.ImageSet.Options.HasFlag(ImageSetOptions.Magnifier);
            // select menu
            this.MenuSelect.IsEnabled = filesSelected;
            // options menu
            // always enable at top level when an image set exists so that image set advanced options are accessible
            this.MenuOptions.IsEnabled = imageSetAvailable;
            this.MenuOptionsAudioFeedback.IsEnabled = filesSelected;
            this.MenuOptionsDialogsOnOrOff.IsEnabled = filesSelected;
            this.MenuOptionsAdvancedCarnassialOptions.IsEnabled = filesSelected;

            // other UI components
            // If no files are selected there's nothing for the user to do with data entry.
            this.AnalysisButtons.EnableOrDisable(filesSelected, this.State.Analysis);
            this.DataEntryControls.IsEnabled = filesSelected;
            this.FileNavigatorSlider.IsEnabled = filesSelected;
            this.FileDisplay.IsEnabled = filesSelected;
            this.FileDisplay.MagnifyingGlassEnabled = filesSelected && this.DataHandler.FileDatabase.ImageSet.Options.HasFlag(ImageSetOptions.Magnifier);

            if (filesSelected == false)
            {
                this.SetStatusMessage(Constant.ResourceKey.CarnassialWindowStatusImageSetEmpty);
                this.SetFileCount(0);
            }
        }

        private async void FileNavigatorSlider_DragCompleted(object sender, DragCompletedEventArgs args)
        {
            this.State.FileNavigatorSliderDragging = false;
            await this.ShowFileAsync(this.FileNavigatorSlider).ConfigureAwait(true);
            this.State.FileNavigatorSliderTimer.Stop();
        }

        private void FileNavigatorSlider_DragStarted(object sender, DragStartedEventArgs args)
        {
            this.State.FileNavigatorSliderTimer.Start(); // The timer forces an image display update to the current slider position if the user pauses longer than the timer's interval. 
            this.State.FileNavigatorSliderDragging = true;
        }

        private void FileNavigatorSlider_EnableOrDisableValueChangedCallback(bool enableCallback)
        {
            if (enableCallback)
            {
                this.FileNavigatorSlider.ValueChanged += this.FileNavigatorSlider_ValueChanged;
            }
            else
            {
                this.FileNavigatorSlider.ValueChanged -= this.FileNavigatorSlider_ValueChanged;
            }
        }

        private void FileNavigatorSlider_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            this.FocusFileDisplay();
        }

        private async void FileNavigatorSlider_TimerTick(object? sender, EventArgs e)
        {
            // display the current file as the user drags the navigation slider 
            await this.ShowFileAsync(this.FileNavigatorSlider).ConfigureAwait(true);
            this.State.FileNavigatorSliderTimer.Stop();
        }

        private async void FileNavigatorSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> args)
        {
            // since the minimum value is 1 there's a value change event during InitializeComponent() to ignore
            if (this.State == null)
            {
                args.Handled = true;
                return;
            }

            DateTime utcNow = DateTime.UtcNow;
            if ((this.State.FileNavigatorSliderDragging == false) || (utcNow - this.State.MostRecentRender > this.State.FileNavigatorSliderTimer.Interval))
            {
                await this.ShowFileAsync(this.FileNavigatorSlider).ConfigureAwait(true);
                this.State.MostRecentRender = utcNow;
                args.Handled = true;
            }
        }

        private async void FilePlay_TimerTick(object? sender, EventArgs e)
        {
            await this.ShowFileWithoutSliderCallbackAsync(true, ModifierKeys.None).ConfigureAwait(true);

            Debug.Assert(this.DataHandler != null);
            if (this.DataHandler.ImageCache.CurrentRow == (this.DataHandler.FileDatabase.Files.RowCount - 1))
            {
                // stop playing files since the end of the image set's been reached
                this.MenuViewPlayFiles_Click(this, null);
            }
            else
            {
                this.DataHandler.FileDatabase.Files.TryGetPreviousFile(this.DataHandler.ImageCache.CurrentRow, out ImageRow? previousFile);
                this.State.Throttles.SetFilePlayInterval(previousFile, this.DataHandler.ImageCache.Current);
            }
        }

        private void FileViewGridSplitter_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            // WPF's GridSplitter doesn't modify the width of grid elements even when set to resize the previous and next columns
            // This is a quirk of GridSplitter's implementation, which is mainly intended to work with star sizing.  The below 
            // workaround simply completes the propagation of the size change.
            this.ControlGrid.Width -= e.HorizontalChange;
        }

        private void FocusFileDisplay()
        {
            Debug.Assert(this.FileDisplay.FileDisplay.Dock.Focusable, "FileDisplay isn't focusable.");
            this.FileDisplay.FileDisplay.Dock.Focus();
        }

        private void FolderSelectionDialog_FolderChanging(object? sender, CommonFileDialogFolderChangeEventArgs e)
        {
            // require folders to be loaded be either the same folder as the .tdb and .ddb or subfolders of it
            if ((e.Folder == null) || (e.Folder.StartsWith(this.FolderPath, StringComparison.OrdinalIgnoreCase) == false))
            {
                // rather than cancel the event, override the selected path
                // This produces a better experience in cases where the user selects a folder that's above the databases as there's a visible response
                // when the current folder is a subfolder.
                e.Folder = this.FolderPath;
            }
        }

        private List<Marker> GetDisplayMarkers()
        {
            List<Marker> markers = new();
            if (this.IsFileAvailable() == false)
            {
                return markers;
            }

            // if no counter is selected that just indicates no markers need to be highlighted at this time
            this.TryGetSelectedCounter(out DataEntryCounter? selectedCounter);
            foreach (DataEntryControl control in this.DataEntryControls.ControlsByDataLabel.Values)
            {
                if (control.Type != ControlType.Counter)
                {
                    continue;
                }
                Debug.Assert(this.DataHandler != null);
                MarkersForCounter markersForCounter = this.DataHandler.ImageCache.Current.GetMarkersForCounter(control.DataLabel);

                // on mouse hover over a counter, emphasize markers associated with it
                DataEntryCounter currentCounter = (DataEntryCounter)control;
                bool emphasize = String.Equals(markersForCounter.DataLabel, this.State.MouseOverCounter, StringComparison.Ordinal);
                bool highlight = (selectedCounter != null) && String.Equals(currentCounter.DataLabel, selectedCounter.DataLabel, StringComparison.Ordinal);
                foreach (Marker marker in markersForCounter.Markers)
                {
                    // label markers when they're first created, don't show a label afterwards
                    if (marker.ShowLabel && (marker.LabelShownPreviously == false))
                    {
                        marker.ShowLabel = true;
                        marker.LabelShownPreviously = true;
                    }
                    else
                    {
                        marker.ShowLabel = false;
                    }

                    marker.Emphasize = emphasize;
                    marker.Highlight = highlight;
                    marker.Tooltip = currentCounter.Label.Replace("_", String.Empty);
                    markers.Add(marker);
                }
            }
            return markers;
        }

        private void HideLongRunningOperationFeedback()
        {
            this.LongRunningFeedback.Visibility = Visibility.Collapsed;
            this.FileNavigationGrid.Visibility = Visibility.Visible;

            // reset state for the next long running operation
            this.LongRunningFeedback.ProgressBar.Value = 0.0;
            StatusBarItem statusMessage = (StatusBarItem)this.LongRunningFeedback.StatusMessage.Items[0];
            statusMessage.Content = null;
        }

        private async void Instructions_Drop(object sender, DragEventArgs dropEvent)
        {
            if (ApplicationWindow.IsSingleTemplateFileDrag(dropEvent, out string? templateDatabaseFilePath))
            {
                dropEvent.Handled = await this.TryOpenTemplateAndFileDatabaseAsync(templateDatabaseFilePath).ConfigureAwait(true);
            }
        }

        [MemberNotNullWhen(true, nameof(DataHandler))]
        public bool IsFileAvailable()
        {
            if ((this.DataHandler == null) || (this.DataHandler.ImageCache == null))
            {
                return false;
            }

            return this.DataHandler.ImageCache.IsFileAvailable;
        }

        private bool IsFileDatabaseAvailable()
        {
            if ((this.DataHandler == null) || (this.DataHandler.FileDatabase == null))
            {
                return false;
            }

            return true;
        }

        private void MaybeExecuteMultipleFieldEdit(FileMultipleFieldChange multipleChange)
        {
            if (multipleChange.Changes > 0)
            {
                if (multipleChange.Changes == 1)
                {
                    // in the case where the multiple change reduced to a single field convert to a single change for clarity 
                    FileSingleFieldChange singleChange = multipleChange.AsSingleChange();
                    singleChange.Execute(this);
                    this.AddCommand(singleChange);
                }
                else
                {
                    multipleChange.Execute(this);
                    this.AddCommand(multipleChange);
                }
            }
        }

        private void MaybeShowFileCountsDialog(bool onFileLoading)
        {
            if (onFileLoading && this.State.SuppressFileCountOnImportDialog)
            {
                return;
            }

            Debug.Assert(this.DataHandler != null);
            Dictionary<FileClassification, int> counts = this.DataHandler.FileDatabase.GetFileCountsByClassification();
            FileCountsByClassification imageStats = new(counts, this);
            if (onFileLoading)
            {
                imageStats.Message.Hint.Inlines.Add(new Run(App.FindResource<string>(Constant.ResourceKey.FileCountsByClassificationMessageHint)));
                imageStats.DontShowAgain.Visibility = Visibility.Visible;
            }

            bool? result = imageStats.ShowDialog();
            if (onFileLoading && (result == true) && imageStats.DontShowAgain.IsChecked.HasValue)
            {
                this.State.SuppressFileCountOnImportDialog = imageStats.DontShowAgain.IsChecked.Value;
                this.MenuOptionsEnableFileCountOnImportDialog.IsChecked = !this.State.SuppressFileCountOnImportDialog;
            }
        }

        [SupportedOSPlatform(Constant.Platform.Windows)]
        private void MaybeSpeak(string text)
        {
            if (this.State.AudioFeedback)
            {
                // cancel any speech in progress and say the given text
                this.speechSynthesizer.Value.SpeakAsyncCancelAll();
                this.speechSynthesizer.Value.SpeakAsync(text);
            }
        }

        // Event handler: A marker, as defined in e.Marker, has been either added (if e.IsNew is true) or deleted (if it is false)
        // Depending on which it is, add or delete the tag from the current counter control's list of tags 
        // If its deleted, remove the tag from the current counter control's list of tags
        // Every addition / deletion requires us to:
        // - update the contents of the counter control 
        // - update the data held by the image
        // - update the list of markers held by that counter
        // - regenerate the list of markers used by the markableCanvas
        [SupportedOSPlatform(Constant.Platform.Windows)]
        private void MarkableCanvas_MarkerCreatedOrDeleted(object sender, MarkerCreatedOrDeletedEventArgs e)
        {
            if (this.TryGetSelectedCounter(out DataEntryCounter? selectedCounter) == false)
            {
                // mouse logic in MarkableCanvas sends marker create events based on mouse action and has no way of knowing if a counter is selected
                // If no counter's selected there's no marker to create and the event can be ignored.
                return;
            }

            // if this is a newly created marker, fill in marker information not populated at creation time because it's unavailable to MarkableCanvas
            // The counter's label typically contains an underscore to set a hotkey, which should not be included in the marker's tooltip.
            if (e.IsCreation)
            {
                Debug.Assert(e.Marker.DataLabel == null, "Markable canvas unexpectedly sent new marker with data label set.");
                e.Marker.DataLabel = selectedCounter.DataLabel;
                e.Marker.Tooltip = selectedCounter.Label.Replace("_", String.Empty);
            }

            // add or remove the marker
            Debug.Assert(this.IsFileAvailable(), "Marker creation when no file is available.  Is the DataEntryCounter unexpectedly enabled?");
            FileMarkerChange markerChange = new(this.DataHandler.ImageCache.Current.ID, e);
            markerChange.Execute(this);
            this.AddCommand(markerChange);

            this.RefreshDisplayedMarkers();
            if (e.IsCreation)
            {
                this.MaybeSpeak(selectedCounter.Label);
            }
        }

        public void RefreshDisplayedMarkers()
        {
            this.FileDisplay.Markers = this.GetDisplayMarkers();
        }

        private void MenuEditCopy_Click(object sender, RoutedEventArgs e)
        {
            if (this.IsFileAvailable())
            {
                DataObject clipboardData = new(this.DataHandler.GetCopyableFieldsFromCurrentFile(this.DataEntryControls.Controls));
                Clipboard.SetDataObject(clipboardData);
            }
        }

        private void MenuEditCopyValuesToAnalysis_Click(object sender, RoutedEventArgs e)
        {
            this.TryCopyValuesToAnalysis((int)((MenuItem)sender).Tag);
        }

        // Correct ambiguous dates dialog (i.e. dates that could be read as either month/day or day/month
        private async void MenuEditCorrectAmbiguousDates_Click(object sender, RoutedEventArgs e)
        {
            Debug.Assert(this.DataHandler != null);
            DateCorrectAmbiguous ambiguousDateCorrection = new(this.DataHandler.FileDatabase, this);
            if (ambiguousDateCorrection.Abort)
            {
                MessageBox messageBox = MessageBox.FromResource(Constant.ResourceKey.CarnassialWindowNoAmbiguousDates, this);
                messageBox.Close();
                return;
            }
            await this.ShowBulkFileEditDialogAsync(ambiguousDateCorrection).ConfigureAwait(true);
        }

        /// <summary>Correct the date by specifying an offset.</summary>
        private async void MenuEditDateTimeFixedCorrection_Click(object sender, RoutedEventArgs e)
        {
            Debug.Assert(this.DataHandler != null);
            DateTimeFixedCorrection fixedDateCorrection = new(this.DataHandler.FileDatabase, this.DataHandler.ImageCache, this);
            await this.ShowBulkFileEditDialogAsync(fixedDateCorrection).ConfigureAwait(true);
        }

        /// <summary>Correct for drifting clock times. Correction applied only to selected files.</summary>
        private async void MenuEditDateTimeLinearCorrection_Click(object sender, RoutedEventArgs e)
        {
            Debug.Assert(this.DataHandler != null);
            DateTimeLinearCorrection linearDateCorrection = new(this.DataHandler.FileDatabase, this);
            if (linearDateCorrection.Abort)
            {
                MessageBox messageBox = MessageBox.FromResource(Constant.ResourceKey.CarnassialWindowClockDriftFailed, this);
                messageBox.ShowDialog();
                return;
            }
            await this.ShowBulkFileEditDialogAsync(linearDateCorrection).ConfigureAwait(true);
        }

        /// <summary>Correct for daylight savings time.</summary>
        private async void MenuEditDaylightSavingsTimeCorrection_Click(object sender, RoutedEventArgs e)
        {
            Debug.Assert(this.DataHandler != null);
            if (this.DataHandler.ImageCache.Current.IsDisplayable() == false)
            {
                // Just a corrupted image
                MessageBox messageBox = MessageBox.FromResource(Constant.ResourceKey.CarnassialWindowDaylightSavingsFailed, this);
                messageBox.ShowDialog();
                return;
            }

            DateDaylightSavingsTimeCorrection daylightSavingsCorrection = new(this.DataHandler.FileDatabase, this.DataHandler.ImageCache, this);
            await this.ShowBulkFileEditDialogAsync(daylightSavingsCorrection).ConfigureAwait(true);
        }

        private void MenuEditDelete_SubmenuOpened(object sender, RoutedEventArgs e)
        {
            if (this.IsFileAvailable())
            {
                this.MenuEditDeleteCurrentFile.IsEnabled = this.DataHandler.ImageCache.Current.Classification != FileClassification.NoLongerAvailable;
                this.MenuEditDeleteCurrentFileAndData.IsEnabled = true;
            }
            else
            {
                this.MenuEditDeleteCurrentFile.IsEnabled = false;
                this.MenuEditDeleteCurrentFileAndData.IsEnabled = false;
            }

            Debug.Assert(this.DataHandler != null);
            int deletedFiles = this.DataHandler.FileDatabase.Files.Count(file => file.DeleteFlag == true);
            this.MenuEditDeleteFiles.IsEnabled = deletedFiles > 0;
            this.MenuEditDeleteFilesAndData.IsEnabled = deletedFiles > 0;
        }

        /// <summary>Soft delete one or more files marked for deletion, and optionally the data associated with those files.</summary>
        [SupportedOSPlatform(Constant.Platform.Windows)]
        private async void MenuEditDeleteFiles_Click(object sender, RoutedEventArgs e)
        {
            MenuItem menuItem = (MenuItem)sender;
            Debug.Assert(this.DataHandler != null);

            // this callback is invoked by DeleteCurrentFile and DeleteFiles
            // The logic therefore branches for removing a single file versus all selected files marked for deletion.
            List<ImageRow> filesToDelete = new();
            bool deleteCurrentFileOnly;
            bool deleteFilesAndData;
            if (menuItem.Name.Equals(this.MenuEditDeleteFiles.Name, StringComparison.Ordinal) || menuItem.Name.Equals(this.MenuEditDeleteFilesAndData.Name, StringComparison.Ordinal))
            {
                deleteCurrentFileOnly = false;
                deleteFilesAndData = menuItem.Name.Equals(this.MenuEditDeleteFilesAndData.Name, StringComparison.Ordinal);
                // get files marked for deletion in the current seletion
                filesToDelete.AddRange(this.DataHandler.FileDatabase.Files.Where(file => file.DeleteFlag == true));
            }
            else
            {
                // delete current file
                deleteCurrentFileOnly = true;
                deleteFilesAndData = menuItem.Name.Equals(this.MenuEditDeleteCurrentFileAndData.Name, StringComparison.Ordinal);
                if (this.IsFileAvailable())
                {
                    filesToDelete.Add(this.DataHandler.ImageCache.Current);
                }
            }

            // notify the user if no files are selected for deletion
            // This should be unreachable as the invoking menu item should be disabled.
            if (filesToDelete == null || filesToDelete.Count < 1)
            {
                MessageBox messageBox = MessageBox.FromResource(Constant.ResourceKey.CarnassialWindowNoDeletableFiles, this);
                messageBox.ShowDialog();
                return;
            }

            DeleteFiles deleteFilesDialog = new(this.DataHandler.FileDatabase, filesToDelete, deleteFilesAndData, deleteCurrentFileOnly, this);
            if (deleteFilesDialog.ShowDialog() == true)
            {
                // cache the current ID and sync the current file to database as it may be invalidated
                long currentFileID = this.DataHandler.ImageCache.Current.ID;
                this.DataHandler.TrySyncCurrentFileToDatabase();
                // the current file might or might not be on the delete list but unlink it from change notification regardless
                // If it's not unlinked and it is deleted it remains on change tracking, resulting in OnFileFieldEdit() potentially receiving unexpected events 
                // during application shutdown as the controls panel is removed.  If the current file is on the delete list invalidating it from the cache sets
                // .Current to null, meaning event unregistry should be done before the TryInvalidate() call below.  C# ignores attempts to remove unregistered 
                // event callbacks, so there's no impact if the current file's not on the delete list; ShowFile() will repeat the remove without any effect and 
                // then restore the handler.
                this.DataHandler.ImageCache.Current.PropertyChanged -= this.OnFileFieldChanged;

                Mouse.OverrideCursor = Cursors.Wait;
                this.DataHandler.DeleteFiles(filesToDelete, deleteFilesAndData);
                if (deleteFilesAndData)
                {
                    // reload the file data table and find and show the file closest to the last one shown
                    await this.SelectFilesAndShowFileAsync(currentFileID, this.DataHandler.FileDatabase.ImageSet.FileSelection).ConfigureAwait(true);
                }
                else
                {
                    // display the updated properties on the current file or, if data for the current file was dropped, the next one
                    await this.ShowFileAsync(this.DataHandler.FileDatabase.GetFileOrNextFileIndex(currentFileID)).ConfigureAwait(true);
                }
                Mouse.OverrideCursor = null;
            }
        }

        private void MenuEditFind_Click(object sender, RoutedEventArgs? e)
        {
            FindReplace findReplace = new(this);
            findReplace.ShowDialog();
        }

        public async void MenuEditFindNext_Click(object sender, RoutedEventArgs? e)
        {
            Debug.Assert(this.DataHandler != null);
            if (this.DataHandler.TryFindNext(out int fileIndex))
            {
                await this.ShowFileAsync(fileIndex).ConfigureAwait(true);
            }
        }

        public async void MenuEditFindPrevious_Click(object sender, RoutedEventArgs? e)
        {
            Debug.Assert(this.DataHandler != null);
            if (this.DataHandler.TryFindPrevious(out int fileIndex))
            {
                await this.ShowFileAsync(fileIndex).ConfigureAwait(true);
            }
        }

        /// <summary>Edit text in the image set log.</summary>
        private void MenuEditLog_Click(object sender, RoutedEventArgs e)
        {
            Debug.Assert(this.DataHandler != null);
            EditLog editImageSetLog = new(this.DataHandler.FileDatabase.ImageSet.Log, this);
            if (editImageSetLog.ShowDialog() == true)
            {
                this.DataHandler.FileDatabase.ImageSet.Log = editImageSetLog.Log.Text;
                this.DataHandler.FileDatabase.TrySyncImageSetToDatabase();
            }
        }

        private void MenuEditPaste_Click(object sender, RoutedEventArgs e)
        {
            if (this.IsFileAvailable() == false)
            {
                return;
            }

            IDataObject clipboardData = Clipboard.GetDataObject();
            if (clipboardData == null || clipboardData.GetDataPresent(typeof(Dictionary<string, object?>)) == false)
            {
                return;
            }
            Dictionary<string, object?> valuesFromClipboard = (Dictionary<string, object?>)clipboardData.GetData(typeof(Dictionary<string, object?>));
            if (valuesFromClipboard == null)
            {
                return;
            }

            this.PasteValuesToCurrentFileWithUndo(new Dictionary<string, object?>(valuesFromClipboard, StringComparer.Ordinal));
        }

        private void MenuEditPasteFromAnalysis_Click(object sender, RoutedEventArgs e)
        {
            if (this.IsFileAvailable())
            {
                this.TryPasteValuesFromAnalysis((int)((MenuItem)sender).Tag);
            }
        }

        // populate a data field from metadata (example metadata displayed from the currently selected file)
        private async void MenuEditPopulateFieldFromMetadata_Click(object sender, RoutedEventArgs e)
        {
            Debug.Assert(this.DataHandler != null);
            if (this.DataHandler.ImageCache.Current.IsDisplayable() == false)
            {
                int firstFileDisplayable = this.DataHandler.FileDatabase.GetCurrentOrNextDisplayableFile(this.DataHandler.ImageCache.CurrentRow);
                if (firstFileDisplayable == -1)
                {
                    // there are no displayable files and thus no metadata to choose, so abort
                    MessageBox messageBox = MessageBox.FromResource(Constant.ResourceKey.CarnassialWindowNoMetadataAvailable, this);
                    messageBox.ShowDialog();
                    return;
                }
            }

            PopulateFieldWithMetadata populateField = new(this.DataHandler.FileDatabase, this.DataHandler.ImageCache.Current.GetFilePath(this.FolderPath), this.State.Throttles.GetDesiredProgressUpdateInterval(), this);
            await this.ShowBulkFileEditDialogAsync(populateField).ConfigureAwait(true);
        }

        private async void MenuEditReclassify_Click(object sender, RoutedEventArgs e)
        {
            Debug.Assert(this.DataHandler != null);
            using ReclassifyFiles reclassify = new(this.DataHandler.FileDatabase, this.DataHandler.ImageCache, this.State, this);
            await this.ShowBulkFileEditDialogAsync(reclassify).ConfigureAwait(true);
        }

        internal async void MenuEditRedo_Click(object sender, RoutedEventArgs e)
        {
            if (this.IsFileAvailable() == false)
            {
                return;
            }

            if (this.State.UndoRedoChain.TryMoveToNextRedo(out UndoableCommand<CarnassialWindow> stateToRedo))
            {
                if (stateToRedo.CanExecute(this) == false)
                {
                    throw new InvalidOperationException(String.Format(CultureInfo.CurrentCulture, "Cannot redo {0}.", stateToRedo));
                }

                if (stateToRedo.IsAsync)
                {
                    UndoableCommandAsync<CarnassialWindow> stateToUndoAsync = (UndoableCommandAsync<CarnassialWindow>)stateToRedo;
                    await stateToUndoAsync.ExecuteAsync(this).ConfigureAwait(true);
                }
                else
                {
                    stateToRedo.Execute(this);
                }

                this.MenuEditRedo.IsEnabled = this.State.UndoRedoChain.CanRedo;
                this.MenuEditUndo.IsEnabled = this.State.UndoRedoChain.CanUndo;
                this.SetStatusMessage(Constant.ResourceKey.CarnassialWindowStatusRedo, stateToRedo);
            }
        }

        private void MenuEditReplace_Click(object sender, RoutedEventArgs e)
        {
            FindReplace findReplace = new(this);
            findReplace.FindReplaceTabs.SelectedItem = findReplace.ReplaceTab;
            findReplace.ShowDialog();
        }

        private async void MenuEditRereadDateTimesFromFiles_Click(object sender, RoutedEventArgs e)
        {
            Debug.Assert(this.DataHandler != null);
            DateTimeRereadFromFiles rereadDates = new(this.DataHandler.FileDatabase, this.State.Throttles.GetDesiredProgressUpdateInterval(), this);
            await this.ShowBulkFileEditDialogAsync(rereadDates).ConfigureAwait(true);
        }

        private void MenuEditResetValues_Click(object sender, RoutedEventArgs e)
        {
            if (this.IsFileAvailable() == false)
            {
                return;
            }

            Dictionary<string, object?> defaultValues = ImageRow.GetDefaultValues(this.DataHandler.FileDatabase);
            this.MaybeExecuteMultipleFieldEdit(new FileMultipleFieldChange(this.DataHandler.ImageCache, defaultValues));
        }

        private async void MenuEditSetTimeZone_Click(object sender, RoutedEventArgs e)
        {
            Debug.Assert(this.DataHandler != null);
            DateTimeSetTimeZone setTimeZone = new(this.DataHandler.FileDatabase, this.DataHandler.ImageCache, this);
            await this.ShowBulkFileEditDialogAsync(setTimeZone).ConfigureAwait(true);
        }

        private async void MenuEditToggleCurrentFileDeleteFlag_Click(object sender, RoutedEventArgs e)
        {
            await this.ToggleCurrentFileDeleteFlagAsync().ConfigureAwait(true);
        }

        internal async void MenuEditUndo_Click(object sender, RoutedEventArgs e)
        {
            if (this.IsFileAvailable() == false)
            {
                return;
            }

            if (this.State.UndoRedoChain.TryMoveToNextUndo(out UndoableCommand<CarnassialWindow> stateToUndo))
            {
                if (stateToUndo.CanUndo(this) == false)
                {
                    throw new InvalidOperationException(String.Format(CultureInfo.CurrentCulture, "Cannot undo {0}.", stateToUndo));
                }

                if (stateToUndo.IsAsync)
                {
                    UndoableCommandAsync<CarnassialWindow> stateToUndoAsync = (UndoableCommandAsync<CarnassialWindow>)stateToUndo;
                    await stateToUndoAsync.UndoAsync(this).ConfigureAwait(true);
                }
                else
                {
                    stateToUndo.Undo(this);
                }

                this.MenuEditRedo.IsEnabled = this.State.UndoRedoChain.CanRedo;
                this.MenuEditUndo.IsEnabled = this.State.UndoRedoChain.CanUndo;
                this.SetStatusMessage(Constant.ResourceKey.CarnassialWindowStatusUndo, stateToUndo.ToString());
            }
        }

        private void MenuFile_SubmenuOpened(object sender, RoutedEventArgs e)
        {
            this.MenuFileRecentImageSets_Refresh();
        }

        private async void MenuFileAddFilesToImageSet_Click(object sender, RoutedEventArgs e)
        {
            if (this.ShowFolderSelectionDialog(out IEnumerable<string>? folderPaths))
            {
                await this.TryAddFilesAsync(folderPaths).ConfigureAwait(true);
            }
        }

        /// <summary>
        /// Make a copy of the current file in the folder selected by the user and provide feedback in the status.
        /// </summary>
        private void MenuFileCloneCurrent_Click(object sender, RoutedEventArgs e)
        {
            Debug.Assert(this.DataHandler != null);

            string sourcePath = this.DataHandler.ImageCache.Current.GetFilePath(this.FolderPath);
            if (File.Exists(sourcePath) == false)
            {
                MessageBox messageBox = MessageBox.FromResource(Constant.ResourceKey.CarnassialWindowCopyFileFailed, this, sourcePath);
                messageBox.ShowDialog();
                return;
            }

            string sourceFileName = this.DataHandler.ImageCache.Current.FileName;

            using SaveFileDialog saveFileDialog = new();
            saveFileDialog.Title = App.FindResource<string>(Constant.ResourceKey.CarnassialWindowCopyFile);
            saveFileDialog.Filter = String.Format(CultureInfo.CurrentCulture, "*{0}|*{0}", Path.GetExtension(this.DataHandler.ImageCache.Current.FileName));
            saveFileDialog.FileName = sourceFileName;
            saveFileDialog.OverwritePrompt = true;

            if (saveFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                // Set the source and destination file names, including the complete path
                string destinationPath = saveFileDialog.FileName;

                // Try to copy the source file to the destination, overwriting the destination file if it already exists.
                // And giving some feedback about its success (or failure) 
                try
                {
                    File.Copy(sourcePath, destinationPath, true);
                    this.SetStatusMessage(Constant.ResourceKey.CarnassialWindowStatusCopyFileCompleted, sourceFileName, destinationPath);
                }
                catch (IOException exception)
                {
                    Debug.Fail(String.Format(CultureInfo.InvariantCulture, "Copy of '{0}' to '{1}' failed.", sourceFileName, destinationPath), exception.ToString());
                    this.SetStatusMessage(Constant.ResourceKey.CarnassialWindowStatusCopyFileFailed, exception.GetType().Name);
                }
            }
        }

        private async void MenuFileCloseImageSet_Click(object sender, RoutedEventArgs e)
        {
            await this.CloseImageSetAsync().ConfigureAwait(true);
            this.MenuViewShowInstructions_Click(this, null);
        }

        private async void MenuFileLoadImageSet_Click(object sender, RoutedEventArgs e)
        {
            if (this.TryGetTemplatePath(out string templateDatabaseFilePath))
            {
                await this.TryOpenTemplateAndFileDatabaseAsync(templateDatabaseFilePath).ConfigureAwait(true);
            }
        }

        private async void MenuFileMoveFiles_Click(object sender, RoutedEventArgs e)
        {
            using CommonOpenFileDialog folderSelectionDialog = new();
            folderSelectionDialog.Title = "Select the folder to move files to...";
            folderSelectionDialog.DefaultDirectory = this.FolderPath;
            folderSelectionDialog.InitialDirectory = this.FolderPath;
            folderSelectionDialog.IsFolderPicker = true;
            folderSelectionDialog.FolderChanging += this.FolderSelectionDialog_FolderChanging;

            if (folderSelectionDialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                // flush any pending changes so MoveFilesToFolder() has clean state
                Debug.Assert(this.DataHandler != null);
                this.DataHandler.TrySyncCurrentFileToDatabase();

                // move files
                List<string> immovableFiles = this.DataHandler.FileDatabase.MoveSelectedFilesToFolder(folderSelectionDialog.FileName);
                this.SetStatusMessage(Constant.ResourceKey.CarnassialWindowStatusMoveFilesComplete, this.DataHandler.FileDatabase.CurrentlySelectedFileCount - immovableFiles.Count, this.DataHandler.FileDatabase.CurrentlySelectedFileCount, Path.GetFileName(folderSelectionDialog.FileName));
                if (immovableFiles.Count > 0)
                {
                    MessageBox messageBox = MessageBox.FromResource(Constant.ResourceKey.CarnassialWindowFileMoveIncomplete, this,
                                                                    this.DataHandler.FileDatabase.CurrentlySelectedFileCount - immovableFiles.Count,
                                                                    this.DataHandler.FileDatabase.CurrentlySelectedFileCount,
                                                                    immovableFiles.Count);
                    foreach (string fileName in immovableFiles)
                    {
                        messageBox.Message.Hint.Inlines.Add(new LineBreak());
                        messageBox.Message.Hint.Inlines.Add(new Run("  \u2022 " + fileName));
                    }
                    messageBox.ShowDialog();
                }

                // refresh the current file to show its new relative path field 
                await this.ShowFileAsync(this.DataHandler.ImageCache.CurrentRow, false).ConfigureAwait(true);

                // clear undo/redo state as bulk edits aren't undoable
                this.OnBulkEdit(this, null);
            }
        }

        /// <summary>Write the .csv or .xlsx file and maybe send an open command to the system.</summary>
        private async void MenuFileExportSpreadsheet_Click(object sender, RoutedEventArgs e)
        {
            Debug.Assert(this.DataHandler != null);

            MenuItem menuItem = (MenuItem)sender;
            bool exportXlsx = (sender == this.MenuFileExportXlsxAndOpen) || (sender == this.MenuFileExportXlsx);
            if (exportXlsx && (this.DataHandler.FileDatabase.Files.RowCount > Constant.Excel.MaximumRowsInWorksheet))
            {
                this.SetStatusMessage(Constant.ResourceKey.CarnassialWindowStatusSpreadsheetExportExcelLimitExceeded, this.DataHandler.FileDatabase.Files.RowCount, Constant.Excel.MaximumRowsInWorksheet);
                return;
            }
            string spreadsheetFileExtension = exportXlsx ? Constant.File.ExcelFileExtension : Constant.File.CsvFileExtension;
            string spreadsheetFileFilter = exportXlsx ? App.FindResource<string>(Constant.ResourceKey.ExcelFileFilter) : App.FindResource<string>(Constant.ResourceKey.CsvFileFilter);

            using SaveFileDialog saveFileDialog = new();
            saveFileDialog.AddExtension = true;
            saveFileDialog.AutoUpgradeEnabled = true;
            saveFileDialog.CheckPathExists = true;
            saveFileDialog.CreatePrompt = false;
            saveFileDialog.DefaultExt = spreadsheetFileExtension;
            saveFileDialog.FileName = Path.GetFileNameWithoutExtension(this.DataHandler.FileDatabase.FileName);
            saveFileDialog.InitialDirectory = this.FolderPath;
            saveFileDialog.Filter = spreadsheetFileFilter;
            saveFileDialog.OverwritePrompt = true;
            saveFileDialog.Title = App.FindResource<string>(Constant.ResourceKey.CarnassialWindowExportSpreadsheet);

            if (saveFileDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            {
                return;
            }

            Stopwatch stopwatch = new();
            stopwatch.Start();

            bool openFileAfterExport = (sender == this.MenuFileExportXlsxAndOpen) || (sender == this.MenuFileExportCsvAndOpen);
            string spreadsheetFileName = Path.GetFileName(saveFileDialog.FileName);
            string spreadsheetFilePath = saveFileDialog.FileName;
            this.SetStatusMessage(Constant.ResourceKey.CarnassialWindowStatusSpreadsheetExport, spreadsheetFileName);
            SpreadsheetReaderWriter spreadsheetWriter = new(this.UpdateImportOrExportProgress, this.State.Throttles.GetDesiredProgressUpdateInterval());
            try
            {
                await Task.Run(() =>
                {
                    if (exportXlsx)
                    {
                        spreadsheetWriter.ExportFileDataToXlsx(this.DataHandler.FileDatabase, spreadsheetFilePath);
                    }
                    else
                    {
                        spreadsheetWriter.ExportFileDataToCsv(this.DataHandler.FileDatabase, spreadsheetFilePath);
                    }
                    stopwatch.Stop();

                    if (openFileAfterExport)
                    {
                            // show the exported file in whatever program is associated with its extension
                            Process process = new();
                        process.StartInfo.UseShellExecute = true;
                        process.StartInfo.RedirectStandardOutput = false;
                        process.StartInfo.FileName = spreadsheetFilePath;
                        process.Start();
                    }
                }).ConfigureAwait(true);
            }
            catch (IOException exception)
            {
                MessageBox messageBox = MessageBox.FromResource(Constant.ResourceKey.CarnassialWindowExportSpreadsheetFailed, this,
                                                                spreadsheetFilePath,
                                                                exception.GetType().FullName,
                                                                exception.Message);
                messageBox.ShowDialog();
                return;
            }
            finally
            {
                this.HideLongRunningOperationFeedback();
            }

            this.SetStatusMessage(Constant.ResourceKey.CarnassialWindowStatusSpreadsheetExportCompleted, spreadsheetFileName, stopwatch.Elapsed.TotalSeconds, this.DataHandler.FileDatabase.CurrentlySelectedFileCount / stopwatch.Elapsed.TotalSeconds);
        }

        private async void MenuFileImport_Click(object sender, RoutedEventArgs e)
        {
            if (this.State.SuppressImportPrompt == false)
            {
                MessageBox messageBox = MessageBox.FromResource(Constant.ResourceKey.CarnassialWindowImport, this,
                    Constant.Time.DateTimeDatabaseFormat,
                    DateTimeHandler.ToDatabaseUtcOffsetString(TimeSpan.FromHours(Constant.Time.MinimumUtcOffsetInHours)),
                    DateTimeHandler.ToDatabaseUtcOffsetString(TimeSpan.FromHours(Constant.Time.MinimumUtcOffsetInHours)),
                    Constant.Excel.FileDataWorksheetName,
                    Constant.File.FileDatabaseFileExtension);

                if (messageBox.ShowDialog() != true)
                {
                    return;
                }

                if (messageBox.DontShowAgain.IsChecked.HasValue)
                {
                    this.State.SuppressImportPrompt = messageBox.DontShowAgain.IsChecked.Value;
                    this.MenuOptionsEnableImportPrompt.IsChecked = !this.State.SuppressImportPrompt;
                }
            }

            Debug.Assert(this.DataHandler != null);
            string defaultSpreadsheetFileName = Path.GetFileNameWithoutExtension(this.DataHandler.FileDatabase.FileName) + Constant.File.ExcelFileExtension;
            if (CommonUserInterface.TryGetFileFromUser("Select a data file to merge into the current image set",
                                                       Path.Combine(this.DataHandler.FileDatabase.FolderPath, defaultSpreadsheetFileName),
                                                       String.Format(CultureInfo.CurrentCulture, "Data files (*{0};*{1};*{2})|*{0};*{1};*{2}", Constant.File.FileDatabaseFileExtension, Constant.File.CsvFileExtension, Constant.File.ExcelFileExtension),
                                                       out string otherDataFilePath) == false)
            {
                return;
            }

            // ensure all files are selected
            // This prevents files which are in the database but not selected from being added a second time.
            FileSelection originalSelection = this.DataHandler.FileDatabase.ImageSet.FileSelection;
            if (originalSelection != FileSelection.All)
            {
                this.DataHandler.SelectFiles(FileSelection.All);
            }

            Stopwatch stopwatch = new();
            stopwatch.Start();
            this.SetStatusMessage(Constant.ResourceKey.CarnassialWindowStatusImport);

            // perform import
            // Both image set and spreadsheet rely on System.Progress to report status as imports proceed.  Progress objects have to 
            // be instantiated on the UI thread because System.Progress..ctor() binds to the available synchronization context which, 
            // in WPF, includes the dispatcher. If the Progress is created on a worker thread an InvalidOperationException results 
            // when the progress callback attempts to udpate UI because the UI thread dispatcher owns the objects.
            FileImportResult importResult;
            if (String.Equals(Path.GetExtension(otherDataFilePath), Constant.File.FileDatabaseFileExtension, StringComparison.OrdinalIgnoreCase))
            {
                importResult = await Task.Run(() =>
                {
                    DataImportProgress importStatus = new(this.UpdateImportOrExportProgress<DataImportProgress>, this.State.Throttles.GetDesiredProgressUpdateInterval());
                    this.DataHandler.IsProgrammaticUpdate = true;
                    FileImportResult result = this.DataHandler.FileDatabase.TryImportData(otherDataFilePath, importStatus);
                    this.DataHandler.IsProgrammaticUpdate = false;
                    return result;
                }).ConfigureAwait(true);
            }
            else
            {
                SpreadsheetReaderWriter spreadsheetReader = new(this.UpdateImportOrExportProgress<SpreadsheetReadWriteStatus>, this.State.Throttles.GetDesiredProgressUpdateInterval());
                importResult = await Task.Run(() =>
                {
                    this.DataHandler.IsProgrammaticUpdate = true;
                    FileImportResult result = spreadsheetReader.TryImportData(otherDataFilePath, this.DataHandler.FileDatabase);
                    this.DataHandler.IsProgrammaticUpdate = false;
                    return result;
                }).ConfigureAwait(true);
            }

            if (importResult.Exception != null)
            {
                stopwatch.Stop();
                this.SetStatusMessage(Constant.ResourceKey.CarnassialWindowStatusSpreadsheetImportFailed);

                MessageBox messageBox = MessageBox.FromResource(Constant.ResourceKey.CarnassialWindowImportFailed, this,
                                                                otherDataFilePath,
                                                                importResult.Exception.GetType().FullName,
                                                                importResult.Exception.Message);
                messageBox.ShowDialog();
            }
            else if (importResult.Errors.Count > 0)
            {
                stopwatch.Stop();

                MessageBox messageBox = MessageBox.FromResource(Constant.ResourceKey.CarnassialWindowImportIncomplete, this, otherDataFilePath);
                foreach (string importError in importResult.Errors)
                {
                    messageBox.Message.Hint.Inlines.Add(new LineBreak());
                    messageBox.Message.Hint.Inlines.Add(new Run(importError));
                }
                messageBox.ShowDialog();
            }

            if (importResult.FilesChanged > 0)
            {
                // reload the in memory file table to put files in the appropriate sort order
                // Also triggers an update to the enable/disable state of the user interface to match.
                await this.SelectFilesAndShowFileAsync(originalSelection).ConfigureAwait(true);

                // clear undo/redo state as bulk edits aren't undoable
                this.OnBulkEdit(this, null);
            }

            this.HideLongRunningOperationFeedback();
            if (stopwatch.IsRunning)
            {
                stopwatch.Stop();
            }

            this.SetStatusMessage(Constant.ResourceKey.CarnassialWindowStatusSpreadsheetImportCompleted, Path.GetFileName(otherDataFilePath), stopwatch.Elapsed.TotalSeconds, importResult.FilesAdded, importResult.FilesUpdated, importResult.FilesProcessed / stopwatch.Elapsed.TotalSeconds);
        }

        private async void MenuFileRecentImageSet_Click(object sender, RoutedEventArgs e)
        {
            await this.TryOpenTemplateAndFileDatabaseAsync((string)((MenuItem)sender).ToolTip).ConfigureAwait(true);
        }

        /// <summary>
        /// Update the list of recent databases displayed under File -> Recent Databases.
        /// </summary>
        private void MenuFileRecentImageSets_Refresh()
        {
            // remove image sets which are no longer present from the most recently used list
            // probably overkill to perform this check on every refresh rather than once at application launch, but it's not particularly expensive
            List<string> invalidPaths = new();
            foreach (string recentImageSetPath in this.State.MostRecentImageSets)
            {
                if (File.Exists(recentImageSetPath) == false)
                {
                    invalidPaths.Add(recentImageSetPath);
                }
            }

            foreach (string path in invalidPaths)
            {
                bool result = this.State.MostRecentImageSets.TryRemove(path);
                Debug.Assert(result, String.Format(CultureInfo.InvariantCulture, "Removal of image set '{0}' no longer present on disk unexpectedly failed.", path));
            }

            // Enable recent image sets only if there are recent sets and the parent menu is also enabled (indicating no image set has been loaded)
            this.MenuFileRecentImageSets.IsEnabled = this.MenuFileLoadImageSet.IsEnabled && this.State.MostRecentImageSets.Count > 0;
            this.MenuFileRecentImageSets.Items.Clear();

            // add menu items most recently used image sets
            int index = 1;
            foreach (string recentImageSetPath in this.State.MostRecentImageSets)
            {
                // Create a menu item for each path
                MenuItem recentImageSetItem = new();
                recentImageSetItem.Click += this.MenuFileRecentImageSet_Click;
                recentImageSetItem.Header = String.Format(CultureInfo.CurrentCulture, "_{0} {1}", index++, recentImageSetPath);
                recentImageSetItem.ToolTip = recentImageSetPath;
                this.MenuFileRecentImageSets.Items.Add(recentImageSetItem);
            }
        }

        private void MenuFileRenameFileDatabase_Click(object sender, RoutedEventArgs e)
        {
            Debug.Assert(this.DataHandler != null);
            RenameFileDatabaseFile renameFileDatabase = new(this.DataHandler.FileDatabase.FileName, this);
            if (renameFileDatabase.ShowDialog() == true)
            {
                this.DataHandler.FileDatabase.RenameDatabaseFile(renameFileDatabase.NewFileName);
            }
        }

        private void MenuFileExit_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
            Application.Current.Shutdown();
        }

        /// <summary>Display a message describing the version, etc.</summary> 
        private void MenuHelpAbout_Click(object sender, RoutedEventArgs e)
        {
            About about = new(this);
            if ((about.ShowDialog() == true) && about.MostRecentCheckForUpdate.HasValue)
            {
                this.State.MostRecentCheckForUpdates = about.MostRecentCheckForUpdate.Value;
            }
        }

        /// <summary>Show advanced Carnassial options.</summary>
        private void MenuOptionsAdvancedCarnassialOptions_Click(object sender, RoutedEventArgs e)
        {
            AdvancedCarnassialOptions advancedCarnassialOptions = new(this.State, this.FileDisplay, this);
            if (advancedCarnassialOptions.ShowDialog() == true)
            {
                // throttle may have changed; update rendering rate
                this.State.FileNavigatorSliderTimer.Interval = this.State.Throttles.DesiredIntervalBetweenRenders;
            }
        }

        /// <summary>Show advanced image set options.</summary>
        private void MenuOptionsAdvancedImageSetOptions_Click(object sender, RoutedEventArgs e)
        {
            Debug.Assert(this.DataHandler != null);
            AdvancedImageSetOptions advancedImageSetOptions = new(this.DataHandler.FileDatabase, this);
            advancedImageSetOptions.ShowDialog();
        }

        /// <summary>Toggle audio feedback on and off.</summary>
        private void MenuOptionsAudioFeedback_Click(object sender, RoutedEventArgs e)
        {
            this.State.AudioFeedback = !this.State.AudioFeedback;
            this.MenuOptionsAudioFeedback.IsChecked = this.State.AudioFeedback;
        }

        private void MenuOptionsEnableFileCountOnImportDialog_Click(object sender, RoutedEventArgs e)
        {
            this.State.SuppressFileCountOnImportDialog = !this.State.SuppressFileCountOnImportDialog;
            this.MenuOptionsEnableFileCountOnImportDialog.IsChecked = !this.State.SuppressFileCountOnImportDialog;
        }

        private void MenuOptionsEnableImportPrompt_Click(object sender, RoutedEventArgs e)
        {
            this.State.SuppressImportPrompt = !this.State.SuppressImportPrompt;
            this.MenuOptionsEnableImportPrompt.IsChecked = !this.State.SuppressImportPrompt;
        }

        internal async void MenuOptionsOrderFilesByDateTime_Click(object sender, RoutedEventArgs e)
        {
            if (this.IsFileDatabaseAvailable() == false)
            {
                return;
            }

            Debug.Assert(this.DataHandler != null);
            FileOrdering orderingCommand = new(this.DataHandler.ImageCache);
            await orderingCommand.ExecuteAsync(this).ConfigureAwait(true);
            this.State.UndoRedoChain.AddCommand(orderingCommand);
        }

        private void MenuOptionsSkipFileClassification_Click(object sender, RoutedEventArgs e)
        {
            this.State.SkipFileClassification = !this.State.SkipFileClassification;
            this.MenuOptionsSkipFileClassification.IsChecked = this.State.SkipFileClassification;
        }

        private async void MenuSelectCustom_Click(object sender, RoutedEventArgs e)
        {
            // the first time the custom selection dialog is launched update the DateTime and UtcOffset search terms to the time of the current file
            // Don't need to check CustomSelectionChange.HasChanges() as a change is guaranteed.
            Debug.Assert((this.DataHandler != null) && (this.DataHandler.FileDatabase.CustomSelection != null));
            SearchTerm? firstDateTimeSearchTerm = this.DataHandler.FileDatabase.CustomSelection.SearchTerms.FirstOrDefault(searchTerm => String.Equals(searchTerm.DataLabel, Constant.FileColumn.DateTime, StringComparison.Ordinal));
            if ((firstDateTimeSearchTerm != null) && ((DateTime)firstDateTimeSearchTerm.DatabaseValue == Constant.ControlDefault.DateTimeValue.UtcDateTime))
            {
                Data.CustomSelection customSelectionInitialSnapshot = new(this.DataHandler.FileDatabase.CustomSelection);
                DateTimeOffset defaultDate = this.DataHandler.ImageCache.Current.DateTimeOffset;
                this.DataHandler.FileDatabase.CustomSelection.SetDateTimesAndOffset(defaultDate);
                this.AddCommand(new CustomSelectionChange(customSelectionInitialSnapshot, this.DataHandler.FileDatabase.CustomSelection));
            }

            // show the dialog and process the results
            Data.CustomSelection customSelectionSnapshot = new(this.DataHandler.FileDatabase.CustomSelection);
            Dialog.CustomSelection customSelectionDialog = new(this.DataHandler.FileDatabase, this);
            if (customSelectionDialog.ShowDialog() == true)
            {
                CustomSelectionChange customSelectionChange = new(customSelectionSnapshot, this.DataHandler.FileDatabase.CustomSelection);
                if (customSelectionChange.HasChanges())
                {
                    this.AddCommand(customSelectionChange);
                }
                await this.SelectFilesAndShowFileAsync(FileSelection.Custom).ConfigureAwait(true);
            }
            else
            {
                // if needed, uncheck the custom selection menu item
                // It's checked automatically by WPF but, as the user cancelled out of custom selection, this isn't correct in cases where the selection 
                // wasn't already custom.
                this.MenuSelectCustom.IsChecked = this.DataHandler.FileDatabase.ImageSet.FileSelection == FileSelection.Custom;
            }
        }

        /// <summary>Show a dialog box telling the user how many images were loaded, etc.</summary>
        public void MenuSelectFileCounts_Click(object sender, RoutedEventArgs e)
        {
            this.MaybeShowFileCountsDialog(false);
        }

        /// <summary>Get the non-custom selection and update the view.</summary>
        private async void MenuSelectFiles_Click(object sender, RoutedEventArgs e)
        {
            // get selection 
            FileSelection selection;
            if (sender == this.MenuSelectAllFiles)
            {
                selection = FileSelection.All;
            }
            else if (sender == this.MenuSelectColorFiles)
            {
                selection = FileSelection.Color;
            }
            else if (sender == this.MenuSelectCorruptedFiles)
            {
                selection = FileSelection.Corrupt;
            }
            else if (sender == this.MenuSelectDarkFiles)
            {
                selection = FileSelection.Dark;
            }
            else if (sender == this.MenuSelectFilesMarkedForDeletion)
            {
                selection = FileSelection.MarkedForDeletion;
            }
            else if (sender == this.MenuSelectFilesNoLongerAvailable)
            {
                selection = FileSelection.NoLongerAvailable;
            }
            else if (sender == this.MenuSelectGreyscaleFiles)
            {
                selection = FileSelection.Greyscale;
            }
            else if (sender == this.MenuSelectVideoFiles)
            {
                selection = FileSelection.Video;
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(sender), String.Format(CultureInfo.CurrentCulture, "Unknown sender {0}.", sender));
            }

            await this.SelectFilesAndShowFileAsync(selection).ConfigureAwait(true);
        }

        private void MenuSelect_SubmenuOpening(object sender, RoutedEventArgs e)
        {
            Debug.Assert(this.DataHandler != null);
            Dictionary<FileClassification, int> counts = this.DataHandler.FileDatabase.GetFileCountsByClassification();

            this.MenuSelectColorFiles.IsEnabled = counts[FileClassification.Color] > 0;
            this.MenuSelectCorruptedFiles.IsEnabled = counts[FileClassification.Corrupt] > 0;
            this.MenuSelectDarkFiles.IsEnabled = counts[FileClassification.Dark] > 0;
            this.MenuSelectGreyscaleFiles.IsEnabled = counts[FileClassification.Greyscale] > 0;
            this.MenuSelectFilesNoLongerAvailable.IsEnabled = counts[FileClassification.NoLongerAvailable] > 0;
            this.MenuSelectFilesMarkedForDeletion.IsEnabled = this.DataHandler.FileDatabase.GetFileCount(FileSelection.MarkedForDeletion) > 0;
            this.MenuSelectVideoFiles.IsEnabled = counts[FileClassification.Video] > 0;
        }

        private void MenuViewApplyBookmark_Click(object sender, RoutedEventArgs e)
        {
            this.FileDisplay.ApplyBookmark();
        }

        /// <summary>Toggle the magnifier on and off.</summary>
        private void MenuViewDisplayMagnifier_Click(object sender, RoutedEventArgs e)
        {
            Debug.Assert(this.DataHandler != null);
            bool displayMagnifier = this.DataHandler.FileDatabase.ImageSet.Options.HasFlag(ImageSetOptions.Magnifier);
            displayMagnifier = !displayMagnifier;

            this.DataHandler.FileDatabase.ImageSet.Options = this.DataHandler.FileDatabase.ImageSet.Options.SetFlag(ImageSetOptions.Magnifier, displayMagnifier);
            this.MenuViewDisplayMagnifier.IsChecked = displayMagnifier;
            this.FileDisplay.MagnifyingGlassEnabled = displayMagnifier;
        }

        /// <summary>View the combined image differences.</summary>
        private async void MenuViewDifferencesCombined_Click(object sender, RoutedEventArgs e)
        {
            await this.TryViewCombinedDifferenceAsync().ConfigureAwait(true);
        }

        /// <summary>Increase the magnification of the magnifying glass by several keyboard steps.</summary>
        private void MenuViewMagnifierIncrease_Click(object sender, RoutedEventArgs e)
        {
            this.FileDisplay.MagnifierZoomIn();
            this.FileDisplay.MagnifierZoomIn();
            this.FileDisplay.MagnifierZoomIn();
        }

        private async void MenuViewGotoFile_Click(object sender, RoutedEventArgs e)
        {
            if (this.IsFileDatabaseAvailable() == false)
            {
                return;
            }

            Debug.Assert(this.DataHandler != null);
            GoToFile goToFile = new(this.DataHandler.ImageCache.CurrentRow, this.DataHandler.FileDatabase.CurrentlySelectedFileCount, this);
            if (goToFile.ShowDialog() == true)
            {
                await this.ShowFileWithoutSliderCallbackAsync(goToFile.FileIndex).ConfigureAwait(true);
            }
        }

        /// <summary>Decrease the magnification of the magnifying glass by several keyboard steps.</summary>
        private void MenuViewMagnifierDecrease_Click(object sender, RoutedEventArgs e)
        {
            this.FileDisplay.MagnifierZoomOut();
            this.FileDisplay.MagnifierZoomOut();
            this.FileDisplay.MagnifierZoomOut();
        }

        private void MenuViewPlayFiles_Click(object sender, RoutedEventArgs? e)
        {
            // if this event doesn't result from a button click, toggle the play files button's state
            if (sender != this.PlayFilesButton)
            {
                this.PlayFilesButton.IsChecked = !this.PlayFilesButton.IsChecked;
            }

            // switch from not playing files to playing files or vice versa
            if (this.PlayFilesButton.IsChecked == true)
            {
                Debug.Assert(this.DataHandler != null);
                if (this.DataHandler.FileDatabase.Files.TryGetPreviousFile(this.DataHandler.ImageCache.CurrentRow, out ImageRow? previousFile))
                {
                    this.State.Throttles.StartFilePlayTimer(previousFile, this.DataHandler.ImageCache.Current);
                }
            }
            else
            {
                this.State.Throttles.StopFilePlayTimer();
            }
        }

        private void MenuViewPlayVideo_Click(object sender, RoutedEventArgs e)
        {
            this.FileDisplay.TryPlayOrPauseVideo();
        }

        /// <summary>Cycle through next and previous image differences.</summary>
        private async void MenuViewPreviousOrNextDifference_Click(object sender, RoutedEventArgs e)
        {
            await this.TryViewPreviousOrNextDifferenceAsync().ConfigureAwait(true);
        }

        private void MenuViewSetBookmark_Click(object sender, RoutedEventArgs e)
        {
            this.FileDisplay.SetBookmark();
        }

        private async void MenuViewShowFirstFile_Click(object sender, RoutedEventArgs e)
        {
            await this.ShowFileWithoutSliderCallbackAsync(0).ConfigureAwait(true);
        }

        private void MenuViewShowFiles_Click(object sender, RoutedEventArgs? e)
        {
            this.Tabs.SelectedIndex = 1;
            this.FocusFileDisplay();
        }

        private void MenuViewShowInstructions_Click(object sender, RoutedEventArgs? e)
        {
            this.Tabs.SelectedIndex = 0;
        }

        private async void MenuViewShowLastFile_Click(object sender, RoutedEventArgs e)
        {
            if (this.IsFileDatabaseAvailable())
            {
                Debug.Assert(this.DataHandler != null);
                await this.ShowFileWithoutSliderCallbackAsync(this.DataHandler.FileDatabase.CurrentlySelectedFileCount - 1).ConfigureAwait(true);
            }
        }

        /// <summary>Navigate to the next file in this image set.</summary>
        private async void MenuViewShowNextFile_Click(object sender, RoutedEventArgs e)
        {
            await this.ShowFileWithoutSliderCallbackAsync(true, ModifierKeys.None).ConfigureAwait(true);
        }

        private async void MenuViewShowNextFileControl_Click(object sender, RoutedEventArgs e)
        {
            await this.ShowFileWithoutSliderCallbackAsync(true, ModifierKeys.Control).ConfigureAwait(true);
        }

        private async void MenuViewShowNextFileControlShift_Click(object sender, RoutedEventArgs e)
        {
            await this.ShowFileWithoutSliderCallbackAsync(true, ModifierKeys.Control | ModifierKeys.Shift).ConfigureAwait(true);
        }

        private async void MenuViewShowNextFileShift_Click(object sender, RoutedEventArgs e)
        {
            await this.ShowFileWithoutSliderCallbackAsync(true, ModifierKeys.Shift).ConfigureAwait(true);
        }

        private async void MenuViewShowNextFilePageDown_Click(object sender, RoutedEventArgs e)
        {
            if (this.IsFileDatabaseAvailable())
            {
                Debug.Assert(this.DataHandler != null);
                int increment = (int)(Constant.PageUpDownNavigationFraction * this.DataHandler.FileDatabase.CurrentlySelectedFileCount);
                await this.ShowFileWithoutSliderCallbackAsync(this.DataHandler.ImageCache.CurrentRow + increment, increment).ConfigureAwait(true);
            }
        }

        /// <summary>Navigate to the previous file in this image set.</summary>
        private async void MenuViewShowPreviousFile_Click(object sender, RoutedEventArgs e)
        {
            await this.ShowFileWithoutSliderCallbackAsync(false, ModifierKeys.None).ConfigureAwait(true);
        }

        private async void MenuViewShowPreviousFileControl_Click(object sender, RoutedEventArgs e)
        {
            await this.ShowFileWithoutSliderCallbackAsync(false, ModifierKeys.Control).ConfigureAwait(true);
        }

        private async void MenuViewShowPreviousFileControlShift_Click(object sender, RoutedEventArgs e)
        {
            await this.ShowFileWithoutSliderCallbackAsync(false, ModifierKeys.Control | ModifierKeys.Shift).ConfigureAwait(true);
        }

        private async void MenuViewShowPreviousFileShift_Click(object sender, RoutedEventArgs e)
        {
            await this.ShowFileWithoutSliderCallbackAsync(false, ModifierKeys.Shift).ConfigureAwait(true);
        }

        private async void MenuViewShowPreviousFilePageUp_Click(object sender, RoutedEventArgs e)
        {
            if (this.IsFileDatabaseAvailable())
            {
                Debug.Assert(this.DataHandler != null);
                int increment = -(int)(Constant.PageUpDownNavigationFraction * this.DataHandler.FileDatabase.CurrentlySelectedFileCount);
                await this.ShowFileWithoutSliderCallbackAsync(this.DataHandler.ImageCache.CurrentRow + increment, increment).ConfigureAwait(true);
            }
        }

        private void MenuViewZoomIn_Click(object sender, RoutedEventArgs e)
        {
            this.FileDisplay.ZoomIn();
        }

        private void MenuViewZoomOut_Click(object sender, RoutedEventArgs e)
        {
            this.FileDisplay.ZoomOut();
        }

        private void MenuViewZoomToFit_Click(object sender, RoutedEventArgs e)
        {
            this.FileDisplay.ZoomToFit();
        }

        private void MoveFocusToNextOrPreviousTabPosition(bool moveToPrevious)
        {
            // identify the currently selected control with the most recently selected control as a default if no control is selected
            // The defaulting causes tabbing to resume from the last control the user tabbed to.  This is desirable as pressing enter on a control
            // sets focus to the markable canvas, meaning if defaulting weren't used a user entering data and tabbing through controls would have
            // to tab through controls they've already entered for after each press of enter.  Defaulting lets tabbing pick up seamlessly instead.
            int currentControlIndex = this.State.MostRecentlyFocusedControlIndex;
            IInputElement focusedElement = Keyboard.FocusedElement;
            if (focusedElement != null)
            {
                if (DataEntryHandler.TryFindFocusedControl(focusedElement, out DataEntryControl? focusedControl))
                {
                    currentControlIndex = this.DataEntryControls.Controls.IndexOf(focusedControl);
                }
            }

            // if no control is selected and no default from previous tabbing is available then set up the loop below to move to the last control
            if ((currentControlIndex == -1) && moveToPrevious)
            {
                currentControlIndex = this.DataEntryControls.Controls.Count;
            }

            // move to the next or previous control as available
            Func<int, int> incrementOrDecrement;
            if (moveToPrevious)
            {
                incrementOrDecrement = (int index) => { return --index; };
            }
            else
            {
                incrementOrDecrement = (int index) => { return ++index; };
            }

            for (currentControlIndex = incrementOrDecrement(currentControlIndex);
                 currentControlIndex > -1 && currentControlIndex < this.DataEntryControls.Controls.Count;
                 currentControlIndex = incrementOrDecrement(currentControlIndex))
            {
                DataEntryControl control = this.DataEntryControls.Controls[currentControlIndex];
                if (control.ContentReadOnly == false)
                {
                    control.Focus(this);
                    this.State.MostRecentlyFocusedControlIndex = currentControlIndex;
                    return;
                }
            }

            // no control was found so set focus to the slider
            // this has also the desirable side effect of binding the controls into both next and previous loops so that keys can be used to cycle
            // continuously through them
            this.FocusFileDisplay();
            this.State.MostRecentlyFocusedControlIndex = -1;
        }

        public void OnBulkEdit(object? sender, EventArgs? e)
        {
            // clear undo/redo state as bulk edits aren't undoable
            this.ResetUndoRedoState();
        }

        private async Task OnFileDatabaseOpenedOrFilesAddedAsync(bool filesJustAdded)
        {
            // set the file displayed to the one 
            // - from the previous session with the image set if the .ddb was just opened
            // - displayed prior to adding files to an image set
            Debug.Assert(this.DataHandler != null);
            long mostRecentFileID = this.DataHandler.FileDatabase.ImageSet.MostRecentFileID;
            if (filesJustAdded)
            {
                if (this.IsFileAvailable())
                {
                    // if this is completion of an add to an existing image set stay on the file, ideally, shown before the import
                    mostRecentFileID = this.DataHandler.ImageCache.Current.ID;
                    // however, the cache doesn't know file loading changed the display image so invalidate to force a redraw
                    // This is heavier weight than desirable, but it occurs infrequently.
                    this.DataHandler.ImageCache.TryInvalidate(mostRecentFileID);
                }

                // reload the in memory copy of the files table
                // Adding files appends them in memory, which is consistent with FileSelection.All and sort by insertion order, but
                // for any other selection and sort the table needs to be rebuilt. For now, always reload all. It may eventually be
                // desirable to move this logic into AddFilesTransaction.Commit().
                await this.SelectFilesAndShowFileAsync(mostRecentFileID, this.DataHandler.FileDatabase.ImageSet.FileSelection, false).ConfigureAwait(true);
            }
            else
            {
                this.OnFileSelectionChanged();
                await this.ShowFileAsync(this.DataHandler.FileDatabase.GetFileOrNextFileIndex(mostRecentFileID), false).ConfigureAwait(true);
            }

            // clear undo/redo chain as opening a .ddb or adding files is not an undoable operation
            this.OnBulkEdit(this, null);

            // start backup timer
            this.State.BackupTimer.Start();
        }

        private void OnFileFieldChanged(object? sender, PropertyChangedEventArgs fileChange)
        {
            if ((this.DataHandler != null) && this.DataHandler.IsProgrammaticUpdate)
            {
                return;
            }

            string? propertyName = fileChange.PropertyName;
            if (fileChange is IndexedPropertyChangedEventArgs<string> indexedChange)
            {
                propertyName = indexedChange.Index;
            }
            else if (propertyName == null)
            {
                throw new ArgumentOutOfRangeException(nameof(fileChange), "Property name is not specified.");
            }

            Debug.Assert(this.DataHandler != null);
            object? previousValue = this.State.CurrentFileSnapshot[propertyName];
            object? newValue = this.DataHandler.ImageCache.Current[propertyName];
            FileSingleFieldChange fileEdit = new(this.DataHandler.ImageCache.Current.ID, ImageRow.GetDataLabel(propertyName), propertyName, previousValue, newValue, true);
            Debug.Assert((this.State.CurrentFileSnapshot.ContainsKey(propertyName) == false) || (this.State.CurrentFileSnapshot[propertyName] == fileEdit.PreviousValue), String.Format(CultureInfo.InvariantCulture, "Change tracking failure: previous value in file snapshot '{0}' does not match the previous value of the edit '{1}'.", this.State.CurrentFileSnapshot[propertyName], fileEdit.PreviousValue));

            if (fileEdit.HasChange())
            {
                this.AddCommand(fileEdit);
                this.State.CurrentFileSnapshot[propertyName] = fileEdit.NewValue;

                DataEntryControl control = this.DataEntryControls.ControlsByDataLabel[fileEdit.DataLabel];
                if (control.Type == ControlType.Note)
                {
                    DataEntryNote noteControl = (DataEntryNote)control;
                    if (noteControl.ContentControl.Autocompletions.Contains((string?)fileEdit.NewValue, StringComparer.Ordinal) == false)
                    {
                        // if needed, controls could be removed from the list in cases where a correction returns the field's value to one which is already
                        // a known autocomplete
                        // a closely related case is removal of the old value from the autocomplete list when this was the only file to use the value
                        this.State.NoteControlsWithNewValues.Add(noteControl);
                    }
                }
            }
        }
        private void OnFileSelectionChanged()
        {
            Debug.Assert(this.DataHandler != null);
            FileSelection selection = this.DataHandler.FileDatabase.ImageSet.FileSelection;

            // update status and menu state to reflect what ended up being selected
            this.SetFileCount(this.DataHandler.FileDatabase.CurrentlySelectedFileCount);
            this.SetSelection(App.FindResource<string>(nameof(FileSelection) + "." + selection.ToString()));

            this.EnableOrDisableMenusAndControls();
            this.MenuSelectAllFiles.IsChecked = selection == FileSelection.All;
            this.MenuSelectColorFiles.IsChecked = selection == FileSelection.Color;
            this.MenuSelectCorruptedFiles.IsChecked = selection == FileSelection.Corrupt;
            this.MenuSelectCustom.IsChecked = selection == FileSelection.Custom;
            this.MenuSelectDarkFiles.IsChecked = selection == FileSelection.Dark;
            this.MenuSelectGreyscaleFiles.IsChecked = selection == FileSelection.Greyscale;
            this.MenuSelectFilesNoLongerAvailable.IsChecked = selection == FileSelection.NoLongerAvailable;
            this.MenuSelectFilesMarkedForDeletion.IsChecked = selection == FileSelection.MarkedForDeletion;
            this.MenuSelectVideoFiles.IsChecked = selection == FileSelection.Video;

            // after a selection change update the file navigatior slider's range and tick space
            this.FileNavigatorSlider_EnableOrDisableValueChangedCallback(false);
            this.FileNavigatorSlider.Maximum = this.DataHandler.FileDatabase.CurrentlySelectedFileCount;  // slider is one based so no - 1 on the count
            CommonUserInterface.ConfigureNavigatorSliderTick(this.FileNavigatorSlider);

            this.FileNavigatorSlider_EnableOrDisableValueChangedCallback(true);
        }

        [SupportedOSPlatform(Constant.Platform.Windows)]
        private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            string? databasePath = null;
            if (this.IsFileDatabaseAvailable())
            {
                Debug.Assert(this.DataHandler != null);
                databasePath = this.DataHandler.FileDatabase.FilePath;
            }
            this.ShowExceptionReportingDialog(null, databasePath, e);
        }

        private void PasteAnalysis_Click(object sender, int analysisSlot)
        {
            this.TryPasteValuesFromAnalysis(analysisSlot);
        }

        /// <summary>
        /// Highlight controls for copyable fields when the mouse enters the paste button.
        /// </summary>
        private void PasteButton_MouseEnter(object sender, MouseEventArgs e)
        {
            foreach (DataEntryControl control in this.DataEntryControls.Controls)
            {
                control.HighlightIfCopyable();
            }
        }

        /// <summary>
        /// Remove highlights when the mouse leaves the paste button.
        /// </summary>
        private void PasteButton_MouseLeave(object sender, MouseEventArgs e)
        {
            foreach (DataEntryControl control in this.DataEntryControls.Controls)
            {
                control.RemoveHighlightIfCopyable();
            }
        }

        private void PasteNextValues_Click(object sender, RoutedEventArgs e)
        {
            Debug.Assert(this.DataHandler != null);
            int nextIndex = this.DataHandler.ImageCache.CurrentRow + 1;
            if (this.DataHandler.FileDatabase.IsFileRowInRange(nextIndex) == false)
            {
                // at last file, so nothing to copy
                return;
            }

            ImageRow nextFile = this.DataHandler.FileDatabase.Files[nextIndex];
            this.PasteValuesToCurrentFileWithUndo(DataEntryHandler.GetCopyableFields(nextFile, this.DataEntryControls.Controls));
        }

        private void PastePreviousValues_Click(object sender, RoutedEventArgs e)
        {
            Debug.Assert(this.DataHandler != null);
            int previousIndex = this.DataHandler.ImageCache.CurrentRow - 1;
            if (this.DataHandler.FileDatabase.IsFileRowInRange(previousIndex) == false)
            {
                // at first file, so nothing to copy
                return;
            }

            ImageRow previousFile = this.DataHandler.FileDatabase.Files[previousIndex];
            this.PasteValuesToCurrentFileWithUndo(DataEntryHandler.GetCopyableFields(previousFile, this.DataEntryControls.Controls));
        }

        private void PasteValuesToCurrentFileWithUndo(Dictionary<string, object?> values)
        {
            Debug.Assert(this.DataHandler != null);
            this.MaybeExecuteMultipleFieldEdit(new FileMultipleFieldChange(this.DataHandler.ImageCache, values));
            // remove any existing status message as it won't apply to the paste
            this.ClearStatusMessage();
        }

        private void ResetUndoRedoState()
        {
            if (this.IsFileAvailable())
            {
                this.State.CurrentFileSnapshot = this.DataHandler.ImageCache.Current.GetValues();
            }
            else
            {
                this.State.CurrentFileSnapshot.Clear();
            }
            this.State.NoteControlsWithNewValues.Clear();
            this.State.UndoRedoChain.Clear();
            this.MenuEditRedo.IsEnabled = this.State.UndoRedoChain.CanRedo;
            this.MenuEditUndo.IsEnabled = this.State.UndoRedoChain.CanUndo;
        }

        public async Task SelectFilesAndShowFileAsync()
        {
            Debug.Assert(this.DataHandler != null);
            Debug.Assert(this.IsFileDatabaseAvailable(), "Expected a file database to be available.");
            await this.SelectFilesAndShowFileAsync(this.DataHandler.FileDatabase.ImageSet.FileSelection).ConfigureAwait(true);
        }

        public async Task SelectFilesAndShowFileAsync(FileSelection selection)
        {
            Debug.Assert(this.DataHandler != null);
            await this.SelectFilesAndShowFileAsync(this.DataHandler.ImageCache.GetCurrentFileID(), selection).ConfigureAwait(true);
        }

        public async Task SelectFilesAndShowFileAsync(long fileID, FileSelection selection)
        {
            await this.SelectFilesAndShowFileAsync(fileID, selection, true).ConfigureAwait(true);
        }

        public async Task SelectFilesAndShowFileAsync(long fileID, FileSelection selection, bool generateUndoRedoCommands)
        {
            // record current selection for eventual insertion in undo/redo chain and change selection
            Debug.Assert(this.DataHandler != null);
            Debug.Assert(this.IsFileDatabaseAvailable(), "SelectFilesAndShowFile() should not be reachable with a null data handler or database.  Is a menu item wrongly enabled?");
            long previousFileID = this.DataHandler.ImageCache.GetCurrentFileID();
            FileSelection previousSelection = this.DataHandler.FileDatabase.ImageSet.FileSelection;
            this.DataHandler.TrySyncCurrentFileToDatabase();
            this.DataHandler.SelectFiles(selection);

            // if the selection has gone empty revert to all files
            if ((this.DataHandler.FileDatabase.CurrentlySelectedFileCount < 1) && (selection != FileSelection.All))
            {
                // This case is reached when 
                // 1) datetime modifications result in no files matching a custom selection
                // 2) all files which match the selection get deleted
                this.SetStatusMessage(Constant.ResourceKey.CarnassialWindowStatusSelectionReverted);
                this.DataHandler.SelectFiles(FileSelection.All);
            }

            // update UI for current selection
            this.OnFileSelectionChanged();

            // display the specified file or, if it's no longer selected, the next closest one
            // ShowFileAsync() handles empty image sets, so those don't need to be checked for here.
            // Undo/redo is handled in this function so that navigation triggered by selection changes is part of the selection undo/redo step.  Therefore, 
            // undo/redo generation is always suppressed in ShowFileAsync().
            await this.ShowFileAsync(this.DataHandler.FileDatabase.GetFileOrNextFileIndex(fileID), false).ConfigureAwait(true);
            if (generateUndoRedoCommands)
            {
                FileSelectionChange selectionChange = new(this.DataHandler, previousSelection, previousFileID);
                if (selectionChange.HasChange())
                {
                    this.AddCommand(selectionChange);
                }
            }
        }

        // set the current file index
        private void SetCurrentFile(int fileIndex)
        {
            StatusBarItem currentFile = (StatusBarItem)this.FileNavigation.Items[1];
            currentFile.Content = CarnassialWindow.ToDisplayIndex(fileIndex).ToString(CultureInfo.CurrentCulture);
        }

        // set the total number of files
        private void SetFileCount(int selectedFileCount)
        {
            StatusBarItem numberOfFiles = (StatusBarItem)this.FileNavigation.Items[3];
            numberOfFiles.Content = selectedFileCount.ToString(CultureInfo.CurrentCulture);
        }

        private void SetStatusMessage(string key, params object?[] args)
        {
            this.MessageBar.Text = App.FormatResource(key, args);
        }

        // display a view in the view portion of the status bar
        private void SetSelection(string selectionDescription)
        {
            StatusBarItem selection = (StatusBarItem)this.FileNavigation.Items[4];
            selection.Content = selectionDescription;
        }

        // various dialogs cam perform a bulk edit, after which the current file's data needs to be refreshed
        private async Task ShowBulkFileEditDialogAsync(Window dialog)
        {
            Debug.Assert((dialog.GetType() == typeof(DateCorrectAmbiguous)) ||
                         (dialog.GetType() == typeof(DateDaylightSavingsTimeCorrection)) ||
                         (dialog.GetType() == typeof(DateTimeFixedCorrection)) ||
                         (dialog.GetType() == typeof(DateTimeLinearCorrection)) ||
                         (dialog.GetType() == typeof(DateTimeRereadFromFiles)) ||
                         (dialog.GetType() == typeof(DateTimeSetTimeZone)) ||
                         (dialog.GetType() == typeof(ReclassifyFiles)) ||
                         (dialog.GetType() == typeof(PopulateFieldWithMetadata)),
                         String.Format(CultureInfo.InvariantCulture, "Unexpected dialog {0}.", dialog.GetType()));

            Debug.Assert(this.DataHandler != null);
            this.DataHandler.TrySyncCurrentFileToDatabase();
            this.DataHandler.IsProgrammaticUpdate = true;
            if (dialog.ShowDialog() == true)
            {
                // load the changes made through the current dialog
                // Often this won't be needed but it's nontrivial to determine if a bulk edit would affect which files are selected
                // or change files' sort order.
                long currentFileID = this.DataHandler.ImageCache.Current.ID;
                this.DataHandler.SelectFiles(this.DataHandler.FileDatabase.ImageSet.FileSelection);

                // show updated data for file
                // Delete doesn't go through this code path so none of the bulk edit dialogs can result in a change in the file which 
                // needs to be displayed.  Hence the image cache doesn't need to be invalidated.  However, the SelectFiles() call above 
                // might mean the currently displayed file is no longer part of the selection and hence GetFileOrNextFileIndex() needs 
                // to be called for a fully safe flow.
                await this.ShowFileAsync(this.DataHandler.FileDatabase.GetFileOrNextFileIndex(currentFileID)).ConfigureAwait(true);

                // clear undo/redo state as bulk edits aren't undoable
                this.OnBulkEdit(this, null);
            }
            this.DataHandler.IsProgrammaticUpdate = false;
        }

        private void ShowLongRunningOperationFeedback()
        {
            this.LongRunningFeedback.Visibility = Visibility.Visible;
            this.FileNavigationGrid.Visibility = Visibility.Collapsed;
        }

        public async Task ShowFileAsync(int fileIndex)
        {
            await this.ShowFileAsync(fileIndex, Constant.DefaultPrefetchStride, true).ConfigureAwait(true);
        }

        public async Task ShowFileAsync(int fileIndex, bool generateUndoRedoCommands)
        {
            await this.ShowFileAsync(fileIndex, Constant.DefaultPrefetchStride, generateUndoRedoCommands).ConfigureAwait(true);
        }

        public async Task ShowFileAsync(int fileIndex, int prefetchStride)
        {
            await this.ShowFileAsync(fileIndex, prefetchStride, true).ConfigureAwait(true);
        }

        public async Task ShowFileAsync(int fileIndex, int prefetchStride, bool generateUndoRedoCommands)
        {
            // if there is no file to show, then show an image indicating no image set or an empty image set
            if ((this.IsFileDatabaseAvailable() == false) || (this.DataHandler!.FileDatabase.CurrentlySelectedFileCount < 1))
            {
                this.FileDisplay.Display(Constant.Images.NoSelectableFileMessage);
                this.RefreshDisplayedMarkers();
                this.SetCurrentFile(Constant.Database.InvalidRow);

                // clear data context
                this.DataEntryControls.SetDataContext(null);

                // clear tracking state
                this.State.CurrentFileSnapshot.Clear();
                this.State.NoteControlsWithNewValues.Clear();
                return;
            }

            // detach from current file, if any
            if (this.IsFileAvailable())
            {
                if (this.DataHandler.ImageCache.Current.HasChanges)
                {
                    // persist any changes to the current file in the database and, if needed, include them in the undo/redo chain
                    // Changes to the file should already be captured to the undo/redo chain through data binding (single field edits) or from command 
                    // generation at multiple edits, so no new undo/redo state should be needed.
                    Debug.Assert(new FileMultipleFieldChange(this.State.CurrentFileSnapshot, this.DataHandler.ImageCache).Changes == 0, "Current file has unexpected changes which may not have been included in undo/redo state.");
                    this.DataHandler.TrySyncCurrentFileToDatabase();

                    // for note controls, update the autocomplete list if a new value was used
                    foreach (DataEntryNote noteControl in this.State.NoteControlsWithNewValues)
                    {
                        noteControl.MergeAutocompletions(this.DataHandler.FileDatabase.GetDistinctValuesInFileDataColumn(noteControl.DataLabel));
                    }
                }

                // unlink current file from change tracking
                this.DataHandler.ImageCache.Current.PropertyChanged -= this.OnFileFieldChanged;
                this.State.NoteControlsWithNewValues.Clear();
            }

            // move to new file, though it might be the same file if this show call is just to sync fields from the database
            // This should be the only place where code in CarnassialWindow moves the file enumerator
            // - database synchronization logic above depends on this
            // - image caching logic below depends on this
            if (this.State.FileNavigatorSliderDragging)
            {
                prefetchStride = 0;
            }
            int previousFileIndex = this.DataHandler.ImageCache.CurrentRow;

            MoveToFileResult moveToFile = await this.DataHandler.ImageCache.TryMoveToFileAsync(fileIndex, prefetchStride).ConfigureAwait(true);
            if (moveToFile.Succeeded == false)
            {
                throw new ArgumentOutOfRangeException(nameof(fileIndex), String.Format(CultureInfo.CurrentCulture, "{0} is not a valid index in the file table.", fileIndex));
            }

            // update each control with the data for the now current file
            // This is always done as it's assumed either the file being displayed changed or that a control refresh is required due to database changes
            // the call to TryMoveToFile() above refreshes the data stored under this.dataHandler.ImageCache.Current.
            // Note: The refresh here covers only the file table as there's no scenario for edits to the markers table which don't route through
            // MarkableCanvas.MarkerCreatedOrDeleted.
            this.State.CurrentFileSnapshot = this.DataHandler.ImageCache.Current.GetValues();
            this.DataHandler.IsProgrammaticUpdate = true;
            this.DataEntryControls.SetDataContext(this.DataHandler.ImageCache.Current);
            this.DataHandler.IsProgrammaticUpdate = false;
            this.DataHandler.ImageCache.Current.PropertyChanged += this.OnFileFieldChanged;

            // update status bar
            this.SetCurrentFile(fileIndex);
            this.ClearStatusMessage();

            // update nav slider thumb's position to the current file
            this.FileNavigatorSlider.Value = CarnassialWindow.ToDisplayIndex(fileIndex);

            // display new file and update menu item enables if the file changed
            // This avoids unnecessary image reloads and refreshes in cases where ShowFile() is just being called to refresh controls.
            if (moveToFile.NewFileToDisplay)
            {
                // show the file
                this.FileDisplay.Display(this.DataHandler.FileDatabase.FolderPath, this.DataHandler.ImageCache, this.GetDisplayMarkers());

                // add move to this file to the undo/redo chain if it's not already present
                if (generateUndoRedoCommands)
                {
                    this.AddCommand(new FileNavigation(this.DataHandler.ImageCache, previousFileIndex));
                }

                // enable or disable menu items whose availability depends on whether the file's an image or video
                bool isVideo = this.DataHandler.ImageCache.Current.IsVideo;
                bool isImage = !isVideo;
                this.MenuViewApplyBookmark.IsEnabled = isImage;
                this.MenuViewDifferencesCombined.IsEnabled = isImage;
                this.MenuViewDisplayMagnifier.IsEnabled = isImage;
                this.MenuViewMagnifierZoomIncrease.IsEnabled = isImage;
                this.MenuViewMagnifierZoomDecrease.IsEnabled = isImage;
                this.MenuViewNextOrPreviousDifference.IsEnabled = isImage;
                this.MenuViewSetBookmark.IsEnabled = isImage;
                this.MenuViewZoomIn.IsEnabled = isImage;
                this.MenuViewZoomOut.IsEnabled = isImage;
                this.MenuViewZoomToFit.IsEnabled = isImage;

                this.MenuViewPlayVideo.IsEnabled = isVideo;
            }
        }

        private async Task ShowFileAsync(Slider fileNavigatorSlider)
        {
            await this.ShowFileAsync((int)fileNavigatorSlider.Value - 1).ConfigureAwait(true);
        }

        internal async Task ShowFileWithoutSliderCallbackAsync(bool forward, ModifierKeys modifiers)
        {
            // determine how far to move and in which direction
            Debug.Assert(this.DataHandler != null);
            int increment = CommonUserInterface.GetIncrement(forward, modifiers);
            int newFileIndex = this.DataHandler.ImageCache.CurrentRow + increment;

            await this.ShowFileWithoutSliderCallbackAsync(newFileIndex, increment).ConfigureAwait(true);
        }

        private async Task ShowFileWithoutSliderCallbackAsync(int newFileIndex)
        {
            await this.ShowFileWithoutSliderCallbackAsync(newFileIndex, 0).ConfigureAwait(true);
        }

        private async Task ShowFileWithoutSliderCallbackAsync(int newFileIndex, int prefetchStride)
        {
            // if no change the file is already being displayed
            // For example, the end of the image set has been reached but key repeat means right arrow events are still coming in as the user hasn't
            // reacted yet.
            Debug.Assert(this.DataHandler != null);
            if (newFileIndex == this.DataHandler.ImageCache.CurrentRow)
            {
                return;
            }

            // clamp to the maximum or minimum row available
            if (newFileIndex >= this.DataHandler.FileDatabase.CurrentlySelectedFileCount)
            {
                newFileIndex = this.DataHandler.FileDatabase.CurrentlySelectedFileCount - 1;
            }
            else if (newFileIndex < 0)
            {
                newFileIndex = 0;
            }

            // show the new file
            this.FileNavigatorSlider_EnableOrDisableValueChangedCallback(false);
            await this.ShowFileAsync(newFileIndex, prefetchStride).ConfigureAwait(true);
            this.FileNavigatorSlider_EnableOrDisableValueChangedCallback(true);
        }

        private bool ShowFolderSelectionDialog([NotNullWhen(true)] out IEnumerable<string>? folderPaths)
        {
            using (CommonOpenFileDialog folderSelectionDialog = new())
            {
                folderSelectionDialog.Title = "Select one or more folders...";
                folderSelectionDialog.DefaultDirectory = this.State.MostRecentFileAddFolderPath;
                folderSelectionDialog.InitialDirectory = this.State.MostRecentFileAddFolderPath;
                folderSelectionDialog.IsFolderPicker = true;
                folderSelectionDialog.Multiselect = true;

                folderSelectionDialog.FolderChanging += this.FolderSelectionDialog_FolderChanging;

                if (folderSelectionDialog.ShowDialog() == CommonFileDialogResult.Ok)
                {
                    folderPaths = folderSelectionDialog.FileNames;

                    // remember the parent of the selected folder path to save the user clicks and scrolling in case files from additional 
                    // directories are added later
                    // Moves above the location of the template file are disallowed, however.
                    string? parentFolderPath = Path.GetDirectoryName(folderPaths.First());
                    if ((parentFolderPath != null) && parentFolderPath.StartsWith(this.FolderPath, StringComparison.OrdinalIgnoreCase))
                    {
                        this.State.MostRecentFileAddFolderPath = parentFolderPath;
                    }
                    return true;
                }
            }

            folderPaths = null;
            return false;
        }

        private static int ToDisplayIndex(int databaseIndex)
        {
            // +1 since database file indices are zero based but display file indices are ones based
            return databaseIndex + 1;
        }

        private async Task ToggleCurrentFileDeleteFlagAsync()
        {
            if (this.IsFileAvailable() == false)
            {
                return;
            }

            bool newDeleteValue = !this.DataHandler.ImageCache.Current.DeleteFlag;
            this.DataHandler.ImageCache.Current.DeleteFlag = newDeleteValue;

            // if the current file was just marked for deletion presumably the user is done with it and ready to move to the next
            // This autoadvance saves the user having to keep backing out of data entry and hitting the next arrow, so offers substantial savings when
            // working through large numbers of wind triggers or such but may not be desirable in all cases.  If needed an option can be added to disable
            // the behavior.
            if (newDeleteValue)
            {
                await this.ShowFileWithoutSliderCallbackAsync(true, ModifierKeys.None).ConfigureAwait(true);
            }
        }

        // out parameters can't be used in anonymous methods, so a separate pointer to backgroundWorker is required for return to the caller
        private async Task<bool> TryAddFilesAsync(IEnumerable<string> folderPaths)
        {
            using (AddFilesIOComputeTransactionManager folderLoad = new(this.UpdateFolderLoadProgress, this.State.Throttles.GetDesiredProgressUpdateInterval()))
            {
                folderLoad.FolderPaths.AddRange(folderPaths);
                folderLoad.FindFilesToLoad(this.FolderPath);
                if (folderLoad.FilesToLoad == 0)
                {
                    // no images were found in folder; see if user wants to try again
                    MessageBox messageBox = MessageBox.FromResource(Constant.ResourceKey.CarnassialWindowSelectFolder, this, this.FolderPath);
                    if (messageBox.ShowDialog() == false)
                    {
                        return false;
                    }

                    if (this.ShowFolderSelectionDialog(out IEnumerable<string>? folderPathFromDialog))
                    {
                        return await this.TryAddFilesAsync(folderPathFromDialog).ConfigureAwait(true);
                    }

                    // exit if user changed their mind about trying again
                    return false;
                }

                // update UI for import
                this.ShowLongRunningOperationFeedback();
                this.MenuOptions.IsEnabled = true;
                folderLoad.QueueProgressUpdate();
                if (this.State.SkipFileClassification)
                {
                    this.SetStatusMessage(Constant.ResourceKey.CarnassialWindowStatusImageSetLoadingFolders);
                }
                else
                {
                    this.SetStatusMessage(Constant.ResourceKey.CarnassialWindowStatusImageSetLoadingFoldersWithClassification);
                }

                // if it's not already being displayed, change to the files tab so the long running progress bar is visible
                this.MenuViewShowFiles_Click(this, null);

                // ensure all files are selected
                // This prevents files which are in the database but not selected from being added a second time.
                Stopwatch stopwatch = new();
                stopwatch.Start();
                Debug.Assert(this.DataHandler != null);
                FileSelection originalSelection = this.DataHandler.FileDatabase.ImageSet.FileSelection;
                if (originalSelection != FileSelection.All)
                {
                    this.DataHandler.SelectFiles(FileSelection.All);
                }

                // load all files found
                // Note: the UI thread is free during loading.  So if loading's going slow the user can switch off dark checking 
                // asynchronously in the middle of the load to speed it up.            
                int filesAddedToDatabase = await folderLoad.AddFilesAsync(this.DataHandler.FileDatabase, this.State, (int)this.Width).ConfigureAwait(true);

                // if needed, revert to original selection
                if (originalSelection != this.DataHandler.FileDatabase.ImageSet.FileSelection)
                {
                    this.DataHandler.SelectFiles(originalSelection);
                }

                // shift UI to normal, non-loading state
                // Stopwatch is stopped before OnFolderLoadingCompleteAsync() to exclude load and render time of the first image.
                // Status message is updated after OnFolderLoadingCompleteAsync() because loading an image clears the status message.
                this.HideLongRunningOperationFeedback();
                stopwatch.Stop();
                await this.OnFileDatabaseOpenedOrFilesAddedAsync(true).ConfigureAwait(true);
                this.SetStatusMessage(Constant.ResourceKey.CarnassialWindowStatusImageSetLoadingFoldersComplete, filesAddedToDatabase, folderLoad.FilesToLoad, stopwatch.Elapsed.TotalSeconds, folderLoad.FilesToLoad / stopwatch.Elapsed.TotalSeconds, folderLoad.IODuration.TotalSeconds, folderLoad.ComputeDuration.TotalSeconds, folderLoad.DatabaseDuration.TotalSeconds);

                // update the user as to what files are in the database
                this.MaybeShowFileCountsDialog(true);
            }
            return true;
        }

        private bool TryCopyValuesToAnalysis(int analysisSlot)
        {
            if (this.IsFileAvailable() == false)
            {
                return false;
            }

            Dictionary<string, object?> analysisValuesByDataLabel = this.DataHandler.GetCopyableFieldsFromCurrentFile(this.DataEntryControls.Controls);
            this.State.Analysis[analysisSlot] = analysisValuesByDataLabel;
            ((MenuItem)this.MenuEditPasteValuesFromAnalysis.Items[analysisSlot]).IsEnabled = true;

            HashSet<string> analysisLabelsByDataLabel = new(this.DataHandler.FileDatabase.Controls.Where(control => control.AnalysisLabel).Select(control => control.DataLabel));
            this.AnalysisButtons.SetAnalysis(analysisSlot, analysisValuesByDataLabel, analysisLabelsByDataLabel);
            return true;
        }

        private bool TryGetSelectedCounter([NotNullWhen(true)] out DataEntryCounter? selectedCounter)
        {
            foreach (DataEntryControl control in this.DataEntryControls.Controls)
            {
                if (control is DataEntryCounter counter)
                {
                    if (counter.IsSelected)
                    {
                        selectedCounter = counter;
                        return true;
                    }
                }
            }
            selectedCounter = null;
            return false;
        }

        private bool TryGetTemplatePath(out string templateDatabasePath)
        {
            // prompt user to select a template
            // default the template selection dialog to the most recently opened database
            this.State.MostRecentImageSets.TryGetMostRecent(out string defaultTemplateDatabasePath);
            if (CommonUserInterface.TryGetFileFromUser("Select a template file, which should be located in the root folder containing your images and videos",
                                                       defaultTemplateDatabasePath,
                                                       String.Format(CultureInfo.CurrentCulture, "Template files (*{0})|*{0}", Constant.File.TemplateFileExtension),
                                                       out templateDatabasePath) == false)
            {
                return false;
            }

            string? templateDatabaseDirectoryPath = Path.GetDirectoryName(templateDatabasePath);
            if (String.IsNullOrEmpty(templateDatabaseDirectoryPath))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Load the specified database template and then the associated file database.
        /// </summary>
        /// <param name="templateDatabasePath">Fully qualified path to the template database file.</param>
        /// <returns>true only if both the template and file database are loaded (regardless of whether any files were loaded), false otherwise.</returns>
        /// <remarks>This method doesn't particularly need to be public. But making it private imposes substantial complexity in invoking it via PrivateObject.
        /// in unit tests.</remarks>
        public async Task<bool> TryOpenTemplateAndFileDatabaseAsync(string templateDatabasePath)
        {
            Stopwatch imageSetSetupTime = new();
            imageSetSetupTime.Start();
            bool templateLoadedOrCreated = TemplateDatabase.TryCreateOrOpen(templateDatabasePath, out TemplateDatabase templateDatabase);
            imageSetSetupTime.Stop();
            if (templateLoadedOrCreated == false)
            {
                // notify the user the template couldn't be loaded
                MessageBox messageBox = MessageBox.FromResource(Constant.ResourceKey.CarnassialWindowTemplateLoadFailed, this, templateDatabasePath);
                messageBox.ShowDialog();

                this.State.MostRecentImageSets.TryRemove(templateDatabasePath);
                this.MenuFileRecentImageSets_Refresh();
                templateDatabase.Dispose();
                return false;
            }

            // try to get the file database file path
            // addFiles will be true if it's a new file database (meaning the user will be prompted import some files)
            if (this.TrySelectDatabaseFile(templateDatabasePath, out string? fileDatabaseFilePath, out bool tryAddFiles) == false)
            {
                // no file database was selected
                templateDatabase.Dispose();
                return false;
            }

            // update status and, if it's not already displayed, change to the image set tab
            // In the event opening the file database is a long running operation this provides visual feedback the user's 
            // request to open an image set is being processed since the user can see the tab change and controls render.
            imageSetSetupTime.Start();
            string? fileDatabaseFileName = Path.GetFileName(fileDatabaseFilePath);
            this.SetStatusMessage(Constant.ResourceKey.CarnassialWindowStatusImageSetOpening, fileDatabaseFileName);
            this.MenuViewShowFiles_Click(this, null);

            // open file database
            bool fileDatabaseCreatedOrOpened = FileDatabase.TryCreateOrOpen(fileDatabaseFilePath, templateDatabase, this.State.OrderFilesByDateTime, this.State.CustomSelectionTermCombiningOperator, out FileDatabase fileDatabase);
            templateDatabase.Dispose();
            if (fileDatabaseCreatedOrOpened == false)
            {
                imageSetSetupTime.Stop();

                // before running from an existing file database, verify the controls in the template database are compatible with those
                // of the file database
                if ((fileDatabase != null) && (fileDatabase.ControlSynchronizationIssues.Count > 0))
                {
                    // notify user the template and database are out of sync
                    TemplateSynchronization templatesNotCompatibleDialog = new(fileDatabase.ControlSynchronizationIssues, this);
                    if (templatesNotCompatibleDialog.ShowDialog() != true)
                    {
                        // user indicated not to update to the current template or cancelled out of the dialog
                        fileDatabase.Dispose();
                        Application.Current.Shutdown();
                        return false;
                    }

                    // user indicated to proceed with the stale copy of the template found in the file database
                    imageSetSetupTime.Start();
                }
                else
                {
                    // notify user the database couldn't be loaded
                    MessageBox messageBox = MessageBox.FromResource(Constant.ResourceKey.CarnassialWindowDatabaseLoadFailed, this, fileDatabaseFileName);
                    messageBox.ShowDialog();
                    if (fileDatabase != null)
                    {
                        fileDatabase.Dispose();
                    }
                    return false;
                }
            }

            // since database open was successful, update recent image set state
            this.State.MostRecentFileAddFolderPath = fileDatabase.FolderPath;
            this.State.MostRecentImageSets.SetMostRecent(templateDatabasePath);
            this.Title = fileDatabaseFileName + " - " + Constant.MainWindowBaseTitle;
            this.MenuFileRecentImageSets_Refresh();

            // generate and render the data entry controls regardless of whether there are files in the database
            // DataEntryHandler takes ownership of fileDatabase and is responsible for disposing it.  Control generation is done 
            // prior to selecting files so the user can see the controls while files are being read from the database if that is
            // a long running operation.  Also, startup latency is noticeably reduced as control rendering is somewhat expensive 
            // and can proceed in parallel on the UI thread during file loading.
            this.DataHandler = new DataEntryHandler(fileDatabase);
            this.DataHandler.BulkEdit += this.OnBulkEdit;
            this.DataEntryControls.CreateControls(fileDatabase, this.DataHandler, (string dataLabel) => { return fileDatabase.GetDistinctValuesInFileDataColumn(dataLabel); });

            // add event handlers for marker effects which can't be handled by DataEntryHandler
            foreach (DataEntryControl control in this.DataEntryControls.Controls)
            {
                if (control.Type == ControlType.Counter)
                {
                    DataEntryCounter counter = (DataEntryCounter)control;
                    counter.Container.MouseEnter += this.DataEntryCounter_MouseEnter;
                    counter.Container.MouseLeave += this.DataEntryCounter_MouseLeave;
                    counter.LabelControl.Click += this.DataEntryCounter_LabelClick;
                }
            }

            // load files from image set
            Stopwatch fileLoadTime = new();
            await Task.Run(() =>
            {
                fileLoadTime.Start();
                this.DataHandler.FileDatabase.SelectFiles(this.DataHandler.FileDatabase.ImageSet.FileSelection);
                fileLoadTime.Stop();
            }).ConfigureAwait(true);
            imageSetSetupTime.Stop();

            bool filesAdded = false;
            if (tryAddFiles)
            {
                // if this is a new file database, try to load files (if any) from the same folder
                filesAdded = await this.TryAddFilesAsync(new string[] { this.FolderPath }).ConfigureAwait(true);
            }

            if (filesAdded == false)
            {
                await this.OnFileDatabaseOpenedOrFilesAddedAsync(filesAdded).ConfigureAwait(true);
                this.SetStatusMessage(Constant.ResourceKey.CarnassialWindowStatusImageSetOpened, imageSetSetupTime.Elapsed.TotalSeconds, this.DataHandler.FileDatabase.CurrentlySelectedFileCount / fileLoadTime.Elapsed.TotalSeconds, fileLoadTime.Elapsed.TotalSeconds);
            }
            return true;
        }

        private bool TryPasteValuesFromAnalysis(int analysisSlot)
        {
            Dictionary<string, object?>? valuesFromAnalysis = this.State.Analysis[analysisSlot];
            if (valuesFromAnalysis == null)
            {
                // nothing to copy
                return false;
            }

            this.PasteValuesToCurrentFileWithUndo(new Dictionary<string, object?>(valuesFromAnalysis, StringComparer.Ordinal));
            return true;
        }

        // Given the location path of the template, return:
        // - true if a database file was specified
        // - databaseFilePath: the path to the data database file (or null if none was specified).
        // - addFiles: true when the database file has just been created, which means images still have to be imported.
        private bool TrySelectDatabaseFile(string templateDatabasePath, [NotNullWhen(true)] out string? databaseFilePath, out bool addFiles)
        {
            addFiles = false;

            string? directoryPath = Path.GetDirectoryName(templateDatabasePath);
            if (directoryPath == null)
            {
                throw new ArgumentOutOfRangeException(nameof(templateDatabasePath), String.Format(CultureInfo.CurrentCulture, "Failed to extract a directory from the template database path '{0}'. Is the file name of the template database missing?", templateDatabasePath));
            }
            List<string> fileDatabasePaths = Directory.GetFiles(directoryPath, "*" + Constant.File.FileDatabaseFileExtension).Where(databasePath => Path.GetFileNameWithoutExtension(databasePath).EndsWith(Constant.Database.BackupFileNameSuffix, StringComparison.Ordinal) == false).ToList();

            string databaseFileName;
            if (fileDatabasePaths.Count == 1)
            {
                databaseFileName = Path.GetFileName(fileDatabasePaths[0]);
            }
            else if (fileDatabasePaths.Count > 1)
            {
                ChooseFileDatabase chooseDatabaseFile = new(fileDatabasePaths, templateDatabasePath, this);
                if (chooseDatabaseFile.ShowDialog() == true)
                {
                    databaseFileName = chooseDatabaseFile.SelectedFile;
                }
                else
                {
                    // user cancelled .ddb selection
                    databaseFilePath = null;
                    return false;
                }
            }
            else
            {
                // there are no existing .ddb files
                string templateDatabaseFileName = Path.GetFileName(templateDatabasePath);
                if (String.Equals(templateDatabaseFileName, Constant.File.DefaultTemplateDatabaseFileName, StringComparison.OrdinalIgnoreCase))
                {
                    databaseFileName = Constant.File.DefaultFileDatabaseFileName;
                }
                else
                {
                    databaseFileName = Path.GetFileNameWithoutExtension(templateDatabasePath) + Constant.File.FileDatabaseFileExtension;
                }
                addFiles = true;
            }

            databaseFilePath = Path.Combine(directoryPath, databaseFileName);
            return true;
        }

        private async Task TryViewCombinedDifferenceAsync()
        {
            if ((this.IsFileAvailable() == false) || this.DataHandler.ImageCache.Current.IsVideo)
            {
                return;
            }

            // generate and cache difference image if needed
            ImageDifferenceResult result = await this.DataHandler.ImageCache.TryMoveToNextCombinedDifferenceImageAsync(this.State.DifferenceThreshold).ConfigureAwait(true);
            switch (result)
            {
                case ImageDifferenceResult.CurrentImageNotAvailable:
                    this.SetStatusMessage(Constant.ResourceKey.CarnassialWindowStatusCombinedDifferenceCurrentNotLoadable);
                    break;
                case ImageDifferenceResult.NextImageNotAvailable:
                    this.SetStatusMessage(Constant.ResourceKey.CarnassialWindowStatusCombinedDifferenceNextNotAvailable);
                    break;
                case ImageDifferenceResult.NoLongerValid:
                    // nothing to do
                    break;
                case ImageDifferenceResult.NotCalculable:
                    this.SetStatusMessage(Constant.ResourceKey.CarnassialWindowStatusCombinedDifferenceNotCalculable, this.DataHandler.ImageCache.Current.FileName);
                    break;
                case ImageDifferenceResult.PreviousImageNotAvailable:
                    this.SetStatusMessage(Constant.ResourceKey.CarnassialWindowStatusCombinedDifferencePreviousNotAvailable);
                    break;
                case ImageDifferenceResult.Success:
                    this.FileDisplay.Display(this.DataHandler.ImageCache.GetCurrentImage());
                    if (this.DataHandler.ImageCache.CurrentDifferenceState != ImageDifference.Combined)
                    {
                        this.ClearStatusMessage();
                    }
                    else
                    {
                        this.SetStatusMessage(Constant.ResourceKey.CarnassialWindowStatusCombinedDifference, 1000.0 * this.DataHandler.ImageCache.AverageCombinedDifferenceTimeInSeconds);
                    }
                    break;
                default:
                    throw new NotSupportedException(String.Format(CultureInfo.CurrentCulture, "Unhandled combined difference result {0}.", result));
            }
        }

        // Cycle through difference images in the order current, then previous and next differenced images.
        // Create and cache the differenced images.
        private async Task TryViewPreviousOrNextDifferenceAsync()
        {
            // generate and cache difference image if needed
            Debug.Assert(this.DataHandler != null);
            ImageDifferenceResult result = await this.DataHandler.ImageCache.TryMoveToNextDifferenceImageAsync(this.State.DifferenceThreshold).ConfigureAwait(true);
            switch (result)
            {
                case ImageDifferenceResult.CurrentImageNotAvailable:
                    this.SetStatusMessage(Constant.ResourceKey.CarnassialWindowStatusPreviousNextDifferenceCurrentNotLoadable);
                    break;
                case ImageDifferenceResult.NextImageNotAvailable:
                case ImageDifferenceResult.PreviousImageNotAvailable:
                    this.SetStatusMessage(Constant.ResourceKey.CarnassialWindowStatusPreviousNextDifferenceOtherNotLoadable, this.DataHandler.ImageCache.CurrentDifferenceState == ImageDifference.Previous ? "previous" : "next");
                    break;
                case ImageDifferenceResult.NoLongerValid:
                    // nothing to do
                    break;
                case ImageDifferenceResult.NotCalculable:
                    this.SetStatusMessage(Constant.ResourceKey.CarnassialWindowStatusPreviousNextDifferenceOtherNotCompatible, this.DataHandler.ImageCache.CurrentDifferenceState == ImageDifference.Previous ? "Previous" : "Next", this.DataHandler.ImageCache.Current.FileName);
                    break;
                case ImageDifferenceResult.Success:
                    // display the differenced image
                    // the magnifying glass always displays the original non-diferenced image so ImageToDisplay is updated and ImageToMagnify left unchnaged
                    // this allows the user to examine any particular differenced area and see what it really looks like in the non-differenced image. 
                    this.FileDisplay.Display(this.DataHandler.ImageCache.GetCurrentImage());
                    if (this.DataHandler.ImageCache.CurrentDifferenceState == ImageDifference.Unaltered)
                    {
                        this.ClearStatusMessage();
                    }
                    else
                    {
                        this.SetStatusMessage(Constant.ResourceKey.CarnassialWindowStatusPreviousNextDifference, this.DataHandler.ImageCache.CurrentDifferenceState == ImageDifference.Previous ? "previous" : "next", 1000.0 * this.DataHandler.ImageCache.AverageDifferenceTimeInSeconds);
                    }
                    break;
                default:
                    throw new NotSupportedException(String.Format(CultureInfo.CurrentCulture, "Unhandled difference result {0}.", result));
            }
        }

        private void UpdateFolderLoadProgress(FileLoadStatus progress)
        {
            StatusBarItem statusMessage = (StatusBarItem)this.LongRunningFeedback.StatusMessage.Items[0];
            statusMessage.Content = progress.GetMessage();
            this.LongRunningFeedback.ProgressBar.Value = progress.GetPercentage();

            if (progress.TryDetachImage(out CachedImage image))
            {
                progress.MaybeUpdateImageRenderWidth((int)this.Width);
                this.FileDisplay.Display(image, null);
                image.Dispose();
            }
        }

        private void UpdateImportOrExportProgress<TProgress>(DataImportExportProgress<TProgress> progress) where TProgress : class
        {
            this.ShowLongRunningOperationFeedback();

            StatusBarItem statusMessage = (StatusBarItem)this.LongRunningFeedback.StatusMessage.Items[0];
            statusMessage.Content = progress.GetMessage();
            this.LongRunningFeedback.ProgressBar.Value = progress.GetPercentage();
        }

        [SupportedOSPlatform(Constant.Platform.Windows)]
        private async void Window_Closing(object sender, CancelEventArgs e)
        {
            await this.CloseImageSetAsync().ConfigureAwait(true);

            HwndSource.FromHwnd(new WindowInteropHelper(this).Handle).RemoveHook(new HwndSourceHook(this.WndProc));

            // persist user specific state to the registry
            if (this.Top > -10 && this.Left > -10)
            {
                this.State.CarnassialWindowPosition = new Rect(new Point(this.Left, this.Top), new Size(this.Width, this.Height));
            }
            this.State.ControlGridWidth = (int)this.ControlGrid.Width;
            this.State.WriteToRegistry();
        }

        private async void Window_ContentRendered(object sender, EventArgs e)
        {
            // check for updates
            if (DateTime.UtcNow - this.State.MostRecentCheckForUpdates > Constant.CheckForUpdateInterval)
            {
                Uri latestVersionAddress = CarnassialConfigurationSettings.GetLatestReleaseApiAddress();
                if (latestVersionAddress == null)
                {
                    return;
                }

                GithubReleaseClient updater = new(Constant.ApplicationName, latestVersionAddress);
                updater.TryGetAndParseRelease(false, out Version _);
                this.State.MostRecentCheckForUpdates = DateTime.UtcNow;
            }

            // if a file was passed on the command line, try to open it
            // args[0] is the .exe
            string[] args = Environment.GetCommandLineArgs();
            if (args != null && args.Length > 1)
            {
                string filePath = args[1];
                if (filePath.EndsWith(Constant.File.TemplateFileExtension, StringComparison.OrdinalIgnoreCase))
                {
                    await this.TryOpenTemplateAndFileDatabaseAsync(filePath).ConfigureAwait(true);
                }
                else if (filePath.EndsWith(Constant.File.FileDatabaseFileExtension, StringComparison.OrdinalIgnoreCase))
                {
                    string? directoryPath = Path.GetDirectoryName(filePath);
                    if (directoryPath == null)
                    {
                        throw new NotSupportedException(String.Format(CultureInfo.CurrentCulture, "Unable to obtain directory from database file path '{0}'.", filePath));
                    }
                    string[] templatePaths = Directory.GetFiles(directoryPath, "*" + Constant.File.TemplateFileExtension);
                    if (templatePaths != null && templatePaths.Length == 1)
                    {
                        await this.TryOpenTemplateAndFileDatabaseAsync(templatePaths[0]).ConfigureAwait(true);
                    }
                }
            }
        }

        private async void Window_KeyDown(object sender, KeyEventArgs currentKey)
        {
            if (this.IsFileAvailable() == false)
            {
                // no file displayed so no special processing to do; let WPF drive menus as needed
                return;
            }

            // stop any file play in progress when any key press is received
            // For example, the user could click on a data entry control and start entering data.  This check is a best effort
            // as the content controls of data entry controls can trap keyboard events, meaning file play will continue as this
            // code is never reached.  If needed, stopping play when the selected data entry control changes would provide 
            // further mitigation.
            bool wasPlayingFiles = false;
            if (this.PlayFilesButton.IsChecked == true)
            {
                wasPlayingFiles = true;
                this.MenuViewPlayFiles_Click(sender, null);
            }

            // pass all keys to menus
            if (this.Menu.IsKeyboardFocusWithin)
            {
                return;
            }

            // pass all keys except keys which move focus off of data entry controls to data entry controls
            if (this.DataEntryControls.ControlsView.IsKeyboardFocusWithin)
            {
                // check if focus is actually within a control
                // This check is needed to prevent focus unexpectedly going to the data entry controls in the case where
                //   1) DataEntryControls, as is usually the case, doesn't fill the whole height above the analysis buttons
                //   2) the user switched input focus to another application
                //   3) the user clicks empty space near the controls to return input focus to Carnassial
                // Without this check, this sequence results in the controls view having keyboard focus even though no control has 
                // keyboard focus, which causes Carnassial to swallow keyboard input from the user and offer a confusing experience.
                // Numerous other solutions are possible but, in general, create auto layout difficulties as Grids do not resize
                // the height of auto rows or offer collapse priorities among star spacings.  Within Carnassial, probably the best
                // xaml based alternative is a row height multibinding, but this is substantially more complex than checking whether
                // a control is selected.
                if (this.DataEntryControls.ControlsView.SelectedItem != null)
                {
                    if ((currentKey.Key != Key.Escape) && (currentKey.Key != Key.Enter) && (currentKey.Key != Key.Tab))
                    {
                        return;
                    }
                }
            }

            // check if input key or chord is a shortcut key and dispatch appropriately if so
            // When dispatch to an async method occurs the key typically needs to be marked handled before the async call.  Otherwise the UI thread continues
            // event processing on the reasonable assumption the key's unhandled.  This is frequently benign but can have undesirable side effects, such as
            // navigation keys changing which control has focus in addition to their intended effect.
            int keyRepeatCount = this.State.GetKeyRepeatCount(currentKey);
            switch (currentKey.Key)
            {
                case Key.B:
                    if (Keyboard.Modifiers == ModifierKeys.None)
                    {
                        // apply the current bookmark
                        this.FileDisplay.ApplyBookmark();
                        currentKey.Handled = true;
                    }
                    else if (Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        // bookmark (save) the current pan / zoom of the display image
                        this.FileDisplay.SetBookmark();
                        currentKey.Handled = true;
                    }
                    break;
                case Key.C:
                    // copy whatever data fields are copyable from the previous file
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        this.MenuEditCopy_Click(this, currentKey);
                        currentKey.Handled = true;
                    }
                    break;
                case Key.D:
                    // decrease the magnifing glass zoom
                    this.FileDisplay.MagnifierZoomOut();
                    currentKey.Handled = true;
                    break;
                // return to full view of display image
                case Key.D0:
                case Key.NumPad0:
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        this.FileDisplay.ZoomToFit();
                        currentKey.Handled = true;
                    }
                    break;
                case Key.OemMinus:
                    this.FileDisplay.ZoomOut();
                    currentKey.Handled = true;
                    break;
                case Key.OemPlus:
                    this.FileDisplay.ZoomIn();
                    currentKey.Handled = true;
                    break;
                case Key.D1:
                case Key.D2:
                case Key.D3:
                case Key.D4:
                case Key.D5:
                case Key.D6:
                case Key.D7:
                case Key.D8:
                case Key.D9:
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        this.TryCopyValuesToAnalysis(currentKey.Key - Key.D1);
                        currentKey.Handled = true;
                    }
                    else if (Keyboard.Modifiers == ModifierKeys.None)
                    {
                        this.TryPasteValuesFromAnalysis(currentKey.Key - Key.D1);
                        currentKey.Handled = true;
                    }
                    break;
                case Key.NumPad1:
                case Key.NumPad2:
                case Key.NumPad3:
                case Key.NumPad4:
                case Key.NumPad5:
                case Key.NumPad6:
                case Key.NumPad7:
                case Key.NumPad8:
                case Key.NumPad9:
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        this.TryCopyValuesToAnalysis(currentKey.Key - Key.NumPad1);
                        currentKey.Handled = true;
                    }
                    else if (Keyboard.Modifiers == ModifierKeys.None)
                    {
                        this.TryPasteValuesFromAnalysis(currentKey.Key - Key.NumPad1);
                        currentKey.Handled = true;
                    }
                    break;
                // toggle the file's delete flag and, if set, move to the next file
                case Key.Delete:
                    currentKey.Handled = true;
                    await this.ToggleCurrentFileDeleteFlagAsync().ConfigureAwait(true);
                    break;
                // exit current control, if any
                case Key.Enter:
                case Key.Escape:
                    this.FocusFileDisplay();
                    currentKey.Handled = true;
                    break;
                case Key.F:
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        this.MenuEditFind_Click(this, null);
                    }
                    break;
                case Key.F3:
                    if (Keyboard.Modifiers == ModifierKeys.Shift)
                    {
                        this.MenuEditFindPrevious_Click(this, null);
                    }
                    else
                    {
                        this.MenuEditFindNext_Click(this, null);
                    }
                    break;
                case Key.G:
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        this.MenuViewGotoFile_Click(this, currentKey);
                        currentKey.Handled = true;
                    }
                    break;
                case Key.H:
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        this.MenuEditReplace_Click(this, currentKey);
                        currentKey.Handled = true;
                    }
                    break;
                // toggle the magnifying glass on and off
                case Key.M:
                    this.MenuViewDisplayMagnifier_Click(this, currentKey);
                    currentKey.Handled = true;
                    break;
                case Key.N:
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        this.PasteNextValues_Click(this, currentKey);
                        currentKey.Handled = true;
                    }
                    break;
                case Key.P:
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        this.PastePreviousValues_Click(this, currentKey);
                        currentKey.Handled = true;
                    }
                    break;
                case Key.R:
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        this.MenuEditResetValues_Click(this, currentKey);
                        currentKey.Handled = true;
                    }
                    break;
                case Key.Space:
                    if (Keyboard.Modifiers == ModifierKeys.None)
                    {
                        // since the check near the start of Window_KeyDown() stops any current play, only respond to this key if it's a request to
                        // start a play
                        // Without this check, MenuViewPlayFiles_Click() would be called twice if the user uses ctrl+space to stop a
                        // file play, resulting in the keystroke causing file play to continue.
                        if (wasPlayingFiles == false)
                        {
                            this.MenuViewPlayFiles_Click(this, null);
                            currentKey.Handled = true;
                            return;
                        }
                    }
                    else if (Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        // if the current file's a video allow the user to hit ctrl+space to start or stop playing the video
                        // This is desirable as the play or pause button doesn't necessarily have focus and it saves the user having to click the button with
                        // the mouse. If needed, video play and file play can be integrated but, for the moment, it seems most useful to distinguish between
                        // the two in the keyboard shortcuts.
                        if (this.FileDisplay.TryPlayOrPauseVideo() == false)
                        {
                            currentKey.Handled = true;
                            return;
                        }
                    }
                    break;
                case Key.U:
                    this.FileDisplay.MagnifierZoomIn();
                    currentKey.Handled = true;
                    break;
                case Key.V:
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        this.MenuEditPaste_Click(this, currentKey);
                        currentKey.Handled = true;
                    }
                    break;
                case Key.Y:
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        this.MenuEditRedo_Click(this, currentKey);
                        currentKey.Handled = true;
                    }
                    break;
                case Key.Z:
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        this.MenuEditUndo_Click(this, currentKey);
                        currentKey.Handled = true;
                    }
                    break;
                case Key.End:
                    if (this.IsFileDatabaseAvailable())
                    {
                        Debug.Assert(this.DataHandler != null);
                        currentKey.Handled = true;
                        await this.ShowFileWithoutSliderCallbackAsync(this.DataHandler.FileDatabase.CurrentlySelectedFileCount - 1).ConfigureAwait(true);
                    }
                    break;
                case Key.Left:              // previous file
                    currentKey.Handled = true;
                    if (keyRepeatCount % this.State.Throttles.RepeatedKeyAcceptanceInterval == 0)
                    {
                        await this.ShowFileWithoutSliderCallbackAsync(false, Keyboard.Modifiers).ConfigureAwait(true);
                    }
                    break;
                case Key.Home:
                    currentKey.Handled = true;
                    await this.ShowFileWithoutSliderCallbackAsync(0).ConfigureAwait(true);
                    break;
                case Key.PageDown:
                    if (this.IsFileDatabaseAvailable())
                    {
                        Debug.Assert(this.DataHandler != null);
                        currentKey.Handled = true;
                        await this.ShowFileWithoutSliderCallbackAsync(this.DataHandler.ImageCache.CurrentRow + (int)(Constant.PageUpDownNavigationFraction * this.DataHandler.FileDatabase.CurrentlySelectedFileCount)).ConfigureAwait(true);
                    }
                    break;
                case Key.PageUp:
                    if (this.IsFileDatabaseAvailable())
                    {
                        Debug.Assert(this.DataHandler != null);
                        currentKey.Handled = true;
                        await this.ShowFileWithoutSliderCallbackAsync(this.DataHandler.ImageCache.CurrentRow - (int)(Constant.PageUpDownNavigationFraction * this.DataHandler.FileDatabase.CurrentlySelectedFileCount)).ConfigureAwait(true);
                    }
                    break;
                case Key.Right:             // next file
                    currentKey.Handled = true;
                    if (keyRepeatCount % this.State.Throttles.RepeatedKeyAcceptanceInterval == 0)
                    {
                        await this.ShowFileWithoutSliderCallbackAsync(true, Keyboard.Modifiers).ConfigureAwait(true);
                    }
                    break;
                case Key.Tab:               // next or previous control
                    this.MoveFocusToNextOrPreviousTabPosition(Keyboard.Modifiers == ModifierKeys.Shift);
                    currentKey.Handled = true;
                    break;
                case Key.Up:                // show visual difference to next image
                    currentKey.Handled = true;
                    await this.TryViewPreviousOrNextDifferenceAsync().ConfigureAwait(true);
                    break;
                case Key.Down:              // show visual difference to previous image
                    currentKey.Handled = true;
                    await this.TryViewCombinedDifferenceAsync().ConfigureAwait(true);
                    break;
                default:
                    return;
            }
        }

        private void Window_SourceInitialized(object sender, EventArgs e)
        {
            // hook touchpad swipes
            HwndSource.FromHwnd(new WindowInteropHelper(this).Handle).AddHook(new HwndSourceHook(this.WndProc));
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            // handle left and right swipes
            // The maximum size of the horizontal scrolling increment accepted is limited as, particularly when swiping left, wParam can carry large
            // positive increments rather than the negative increment which is correct for the motion.  As it sometimes jumps large for swipe rights
            // as well the simplest solution is to declare the event out of range and drop it.  Some combining of drag events is done (MouseHWheelStep)
            // as moving to a new file is a relatively chunky operation and actioning every increment makes the user interface rather hyper.
            if (msg == Constant.Win32Messages.WM_MOUSEHWHEEL && this.FileDisplay.IsFocused)
            {
                long wheelIncrement = wParam.ToInt64() >> 16;
                if (Math.Abs(wheelIncrement) < Constant.Gestures.MaximumMouseHWheelIncrement)
                {
                    this.State.MouseHorizontalScrollDelta += wheelIncrement;
                    if (Math.Abs(this.State.MouseHorizontalScrollDelta) >= Constant.Gestures.MouseHWheelStep)
                    {
                        // if enough swipe distance has accumulated reset the accumulator
                        // This resembles a slider drag in that the rendering needs not to be spammed with events but differs as the touchpad driver
                        // likely keeps firing intertial events for quite some time if the user is doing swipe and throw.  For best responsiveness,
                        // the accumulator is allowed to build until the next render and the number of files traversed incremented (or decremented)
                        // accordingly.
                        DateTime utcNow = DateTime.UtcNow;
                        if (utcNow - this.State.MostRecentRender > this.State.FileNavigatorSliderTimer.Interval)
                        {
                            // awaiting an async function within WndProc() bricks the UI, so fire it asynchronously
                            Debug.Assert(this.DataHandler != null);
                            int increment = (int)(this.State.MouseHorizontalScrollDelta / Constant.Gestures.MouseHWheelStep);
                            int newFileIndex = this.DataHandler.ImageCache.CurrentRow + increment;
                            #pragma warning disable CS4014
                            this.ShowFileWithoutSliderCallbackAsync(newFileIndex, increment);
                            #pragma warning restore CS4014
                            this.State.MostRecentRender = utcNow;
                            this.State.MouseHorizontalScrollDelta = 0;
                        }
                    }
                }
                handled = true;
            }
            return IntPtr.Zero;
        }
    }
}
