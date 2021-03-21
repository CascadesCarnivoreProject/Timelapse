using Carnassial.Command;
using Carnassial.Control;
using Carnassial.Data;
using Carnassial.Dialog;
using Carnassial.Editor;
using Carnassial.Editor.Dialog;
using Carnassial.Images;
using Carnassial.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;
using System.Windows.Threading;
using MessageBox = Carnassial.Dialog.MessageBox;

namespace Carnassial.UnitTests
{
    [TestClass]
    public class UserInterfaceTests : CarnassialTest
    {
        public UserInterfaceTests()
        {
            this.EnsureTestClassSubdirectory();
        }

        [ClassCleanup]
        public static void ClassCleanup()
        {
            CarnassialTest.TryRevertToDefaultCultures();
        }
        
        [ClassInitialize]
        public static void ClassInitialize(TestContext testContext)
        {
            CarnassialTest.TryChangeToTestCulture();
        }

        [TestMethod]
        public void Carnassial()
        {
            // open, do nothing, close
            using (CarnassialWindow carnassial = new())
            {
                carnassial.Show();
                this.WaitForRenderingComplete();
                carnassial.Close();
                this.WaitForRenderingComplete();
            }

            // create template database and remove any file database from previous test executions
            string templateDatabaseFilePath;
            using (TemplateDatabase templateDatabase = this.CloneTemplateDatabase(TestConstant.File.DefaultTemplateDatabaseFileName))
            {
                templateDatabaseFilePath = templateDatabase.FilePath;
            }

            string fileDatabaseFilePath = Path.Combine(Path.GetDirectoryName(templateDatabaseFilePath), Path.GetFileNameWithoutExtension(templateDatabaseFilePath) + Constant.File.FileDatabaseFileExtension);
            if (File.Exists(fileDatabaseFilePath))
            {
                File.Delete(fileDatabaseFilePath);
            }

            // open, load database by scanning folder, move through images, close
            // The threading model for this is somewhat involved.  The test thread is the UI thread and therefore must drive the dispatcher.  This means the test
            // thread locks into UI message pumping when modal dialog is displayed, such as when loading files from a directory pops the file count summary upon 
            // completion.  The test must therefore spin up a separate thread to close the dialogs and allow the main test thread to return from the dispatcher 
            // and resume test execution.  If something jams up on the dialog handler thread Visual Studio may still consider the test running when also attached 
            // as a debugger even if the test thread has completed.
            // See remarks in CreateReuseControlsAndPropagate() regarding lock.
            lock (CarnassialTest.App)
            {
                using (CarnassialWindow carnassial = new())
                {
                    // show main window
                    carnassial.Show();
                    this.WaitForRenderingComplete();

                    // start thread for handling file dialogs
                    using CancellationTokenSource cancellationTokenSource = new();
                    Task fileCountsDismissal = Task.Run(() =>
                    {
                        AutomationElement carnassialAutomation = AutomationElement.RootElement.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, TestConstant.CarnassialAutomationID));
                        if (this.TryFindDialogOkButton(carnassialAutomation, TestConstant.FileCountsAutomationID, cancellationTokenSource.Token, out InvokePattern fileCountsOkButton))
                        {
                            fileCountsOkButton.Invoke();
                        }
                    }, cancellationTokenSource.Token);

                    // import files from directory
                    Task<bool> loadFolder = null;
                    Dispatcher.CurrentDispatcher.Invoke(() =>
                    {
                        loadFolder = carnassial.TryOpenTemplateAndFileDatabaseAsync(templateDatabaseFilePath);
                    });
                    this.WaitForFolderLoadComplete(loadFolder);

                    // verify import succeeded
                    Assert.IsTrue(carnassial.DataHandler.FileDatabase.FilePath == fileDatabaseFilePath);
                    Assert.IsTrue(carnassial.DataHandler.FileDatabase.CurrentlySelectedFileCount == 2);
                    Assert.IsNotNull(carnassial.DataHandler.ImageCache.Current);

                    // verify forward and backward moves of the displayed file
                    // The template is set for defaulting to file ID 1.  With two files in the image set ID 1 can be either the first or last image in the set depending
                    // on whether files are sorted by date.  Set the direction to move so that both move invocations change the displayed file.
                    bool moveDirection = carnassial.DataHandler.ImageCache.CurrentRow == 0;
                    DispatcherOperation<Task> initialMove = carnassial.Dispatcher.InvokeAsync(async () =>
                    {
                        await carnassial.ShowFileWithoutSliderCallbackAsync(moveDirection, ModifierKeys.None).ConfigureAwait(true);
                        await carnassial.ShowFileWithoutSliderCallbackAsync(moveDirection, ModifierKeys.None).ConfigureAwait(true);
                    });
                    this.WaitForRenderingComplete();

                    moveDirection = !moveDirection;
                    DispatcherOperation<Task> returnMove = carnassial.Dispatcher.InvokeAsync(async () =>
                    {
                        await carnassial.ShowFileWithoutSliderCallbackAsync(moveDirection, ModifierKeys.None).ConfigureAwait(true);
                        await carnassial.ShowFileWithoutSliderCallbackAsync(moveDirection, ModifierKeys.None).ConfigureAwait(true);
                    });
                    this.WaitForRenderingComplete();

                    // verify undo/redo of navigation
                    Assert.IsFalse(carnassial.MenuEditRedo.IsEnabled);
                    Assert.IsTrue(carnassial.MenuEditUndo.IsEnabled);
                    carnassial.Dispatcher.Invoke(() => { carnassial.MenuEditUndo_Click(null, null); });
                    this.WaitForRenderingComplete();
                    Assert.IsTrue(carnassial.DataHandler.ImageCache.CurrentRow == (moveDirection ? 0 : 1));

                    Assert.IsTrue(carnassial.MenuEditRedo.IsEnabled);
                    Assert.IsTrue(carnassial.MenuEditUndo.IsEnabled);
                    carnassial.Dispatcher.Invoke(() => { carnassial.MenuEditRedo_Click(null, null); });
                    this.WaitForRenderingComplete();
                    Assert.IsTrue(carnassial.DataHandler.ImageCache.CurrentRow == (moveDirection ? 1 : 0));

                    // file ordering
                    bool originalOrderFilesByDateTime = carnassial.State.OrderFilesByDateTime;
                    FileOrdering orderingCommand = new(carnassial.DataHandler.ImageCache);
                    Assert.IsTrue(orderingCommand.CanExecute(carnassial));
                    Assert.IsFalse(orderingCommand.CanUndo(carnassial));
                    Assert.IsTrue(orderingCommand.IsAsync);
                    Assert.IsFalse(orderingCommand.IsExecuted);
                    DispatcherOperation<Task> changeOrdering = carnassial.Dispatcher.InvokeAsync(async () =>
                    {
                        await orderingCommand.ExecuteAsync(carnassial).ConfigureAwait(true);
                    });
                    this.WaitForRenderingComplete();
                    Assert.IsTrue(carnassial.DataHandler.FileDatabase.OrderFilesByDateTime != originalOrderFilesByDateTime);
                    Assert.IsTrue(carnassial.State.OrderFilesByDateTime != originalOrderFilesByDateTime);
                    Assert.IsFalse(orderingCommand.CanExecute(carnassial));
                    Assert.IsTrue(orderingCommand.CanUndo(carnassial));

                    DispatcherOperation<Task> undoOrdering = carnassial.Dispatcher.InvokeAsync(async () =>
                    {
                        await orderingCommand.UndoAsync(carnassial).ConfigureAwait(true);
                    });
                    this.WaitForRenderingComplete();
                    Assert.IsTrue(carnassial.DataHandler.FileDatabase.OrderFilesByDateTime == originalOrderFilesByDateTime);
                    Assert.IsTrue(carnassial.State.OrderFilesByDateTime == originalOrderFilesByDateTime);
                    Assert.IsTrue(orderingCommand.CanExecute(carnassial));
                    Assert.IsFalse(orderingCommand.CanUndo(carnassial));

                    // sanity check file selection
                    DispatcherOperation<Task> selectAll = carnassial.Dispatcher.InvokeAsync(async () =>
                    {
                        await carnassial.SelectFilesAndShowFileAsync(FileSelection.All).ConfigureAwait(true);
                    });
                    this.WaitForRenderingComplete();
                    Assert.IsTrue(carnassial.DataHandler.FileDatabase.ImageSet.FileSelection == FileSelection.All);
                    Assert.IsFalse(carnassial.MenuEditRedo.IsEnabled);
                    Assert.IsTrue(carnassial.MenuEditUndo.IsEnabled);

                    carnassial.Dispatcher.Invoke(() => { carnassial.MenuEditUndo_Click(null, null); });
                    this.WaitForRenderingComplete();
                    Assert.IsTrue(carnassial.DataHandler.FileDatabase.ImageSet.FileSelection == FileSelection.All);

                    carnassial.Dispatcher.Invoke(() => { carnassial.MenuEditRedo_Click(null, null); });
                    this.WaitForRenderingComplete();
                    Assert.IsTrue(carnassial.DataHandler.FileDatabase.ImageSet.FileSelection == FileSelection.All);

                    // file edit commands on notes
                    Assert.IsTrue(carnassial.State.CurrentFileSnapshot.Count == TestConstant.DefaultFileColumns.Count - 1);
                    string originalNote0Value = (string)carnassial.DataHandler.ImageCache.Current[TestConstant.DefaultDatabaseColumn.Note0];
                    string newNote0Value = "note 0 new value";
                    string originalNote3Value = (string)carnassial.DataHandler.ImageCache.Current[TestConstant.DefaultDatabaseColumn.Note3];
                    string newNote3Value = "note 3 new value";
                    Dictionary<string, object> multipleChanges = carnassial.DataHandler.ImageCache.Current.GetValues();
                    multipleChanges[TestConstant.DefaultDatabaseColumn.Note0] = newNote0Value;
                    multipleChanges[TestConstant.DefaultDatabaseColumn.Note3] = newNote3Value;
                    Assert.IsTrue(multipleChanges.Count == TestConstant.DefaultFileColumns.Count - 1);
                    FileMultipleFieldChange multipleEdit = new(carnassial.DataHandler.ImageCache, multipleChanges);
                    Assert.IsTrue(multipleEdit.CanExecute(carnassial));
                    Assert.IsFalse(multipleEdit.CanUndo(carnassial));
                    Assert.IsTrue(multipleEdit.Changes == 2);
                    Assert.IsFalse(multipleEdit.IsAsync);
                    Assert.IsFalse(multipleEdit.IsExecuted);
                    Assert.IsTrue((string)carnassial.DataHandler.ImageCache.Current[TestConstant.DefaultDatabaseColumn.Note0] == originalNote0Value);
                    Assert.IsTrue((string)carnassial.DataHandler.ImageCache.Current[TestConstant.DefaultDatabaseColumn.Note3] == originalNote3Value);
                    Assert.IsTrue(carnassial.State.CurrentFileSnapshot.Count == TestConstant.DefaultFileColumns.Count - 1);
                    Assert.IsTrue((string)carnassial.State.CurrentFileSnapshot[TestConstant.DefaultDatabaseColumn.Note0] == originalNote0Value);
                    Assert.IsTrue((string)carnassial.State.CurrentFileSnapshot[TestConstant.DefaultDatabaseColumn.Note3] == originalNote3Value);

                    multipleEdit.Execute(carnassial);
                    Assert.IsFalse(multipleEdit.CanExecute(carnassial));
                    Assert.IsTrue(multipleEdit.CanUndo(carnassial));
                    Assert.IsTrue(multipleEdit.Changes == 2);
                    Assert.IsTrue(multipleEdit.IsExecuted);
                    Assert.IsTrue((string)carnassial.DataHandler.ImageCache.Current[TestConstant.DefaultDatabaseColumn.Note0] == newNote0Value);
                    Assert.IsTrue((string)carnassial.DataHandler.ImageCache.Current[TestConstant.DefaultDatabaseColumn.Note3] == newNote3Value);
                    Assert.IsTrue(carnassial.State.CurrentFileSnapshot.Count == TestConstant.DefaultFileColumns.Count - 1);
                    Assert.IsTrue((string)carnassial.State.CurrentFileSnapshot[TestConstant.DefaultDatabaseColumn.Note0] == newNote0Value);
                    Assert.IsTrue((string)carnassial.State.CurrentFileSnapshot[TestConstant.DefaultDatabaseColumn.Note3] == newNote3Value);
                    this.WaitForRenderingComplete();

                    multipleEdit.Undo(carnassial);
                    Assert.IsTrue(multipleEdit.CanExecute(carnassial));
                    Assert.IsFalse(multipleEdit.CanUndo(carnassial));
                    Assert.IsTrue(multipleEdit.Changes == 2);
                    Assert.IsFalse(multipleEdit.IsExecuted);
                    Assert.IsTrue((string)carnassial.DataHandler.ImageCache.Current[TestConstant.DefaultDatabaseColumn.Note0] == originalNote0Value);
                    Assert.IsTrue((string)carnassial.DataHandler.ImageCache.Current[TestConstant.DefaultDatabaseColumn.Note3] == originalNote3Value);
                    Assert.IsTrue((string)carnassial.State.CurrentFileSnapshot[TestConstant.DefaultDatabaseColumn.Note0] == originalNote0Value);
                    Assert.IsTrue((string)carnassial.State.CurrentFileSnapshot[TestConstant.DefaultDatabaseColumn.Note3] == originalNote3Value);
                    this.WaitForRenderingComplete();

                    DispatcherOperation<Task> showFile = carnassial.Dispatcher.InvokeAsync(async () =>
                    {
                            // move to the other file so change no longer matches current file and can't be exected or undone
                            int otherFileIndex = carnassial.DataHandler.ImageCache.CurrentRow == 0 ? 1 : 0;
                        await carnassial.ShowFileAsync(otherFileIndex, true).ConfigureAwait(true);
                    });
                    this.WaitForRenderingComplete();
                    Assert.IsFalse(multipleEdit.CanExecute(carnassial));
                    Assert.IsFalse(multipleEdit.CanUndo(carnassial));

                    string previousNoteValue = (string)carnassial.DataHandler.ImageCache.Current[TestConstant.DefaultDatabaseColumn.NoteWithCustomDataLabel];
                    string newNoteValue = "note single change value";
                    Dictionary<string, object> singleChangeAsMultipleChanges = carnassial.DataHandler.ImageCache.Current.GetValues();
                    singleChangeAsMultipleChanges[TestConstant.DefaultDatabaseColumn.NoteWithCustomDataLabel] = newNoteValue;
                    FileMultipleFieldChange singleEditAsMultiple = new(carnassial.DataHandler.ImageCache, singleChangeAsMultipleChanges);
                    Assert.IsTrue(singleEditAsMultiple.Changes == 1);
                    FileSingleFieldChange singleEdit = singleEditAsMultiple.AsSingleChange();
                    Assert.IsTrue(singleEdit.CanExecute(carnassial));
                    Assert.IsFalse(singleEdit.CanUndo(carnassial));
                    Assert.IsTrue(String.Equals(singleEdit.DataLabel, TestConstant.DefaultDatabaseColumn.NoteWithCustomDataLabel, StringComparison.Ordinal));
                    Assert.IsFalse(singleEdit.IsAsync);
                    Assert.IsFalse(singleEdit.IsExecuted);
                    Assert.IsTrue((string)singleEdit.NewValue == newNoteValue);
                    Assert.IsTrue((string)singleEdit.PreviousValue == previousNoteValue);
                    Assert.IsTrue(singleEdit.PropertyName == TestConstant.DefaultDatabaseColumn.NoteWithCustomDataLabel);
                    this.WaitForRenderingComplete();

                    singleEdit.Execute(carnassial);
                    Assert.IsFalse(singleEdit.CanExecute(carnassial));
                    Assert.IsTrue(singleEdit.CanUndo(carnassial));
                    Assert.IsTrue(String.Equals(singleEdit.DataLabel, TestConstant.DefaultDatabaseColumn.NoteWithCustomDataLabel, StringComparison.Ordinal));
                    Assert.IsFalse(singleEdit.IsAsync);
                    Assert.IsTrue(singleEdit.IsExecuted);
                    Assert.IsTrue((string)singleEdit.NewValue == newNoteValue);
                    Assert.IsTrue((string)singleEdit.PreviousValue == previousNoteValue);
                    Assert.IsTrue(singleEdit.PropertyName == TestConstant.DefaultDatabaseColumn.NoteWithCustomDataLabel);
                    Assert.IsTrue((string)carnassial.DataHandler.ImageCache.Current[TestConstant.DefaultDatabaseColumn.NoteWithCustomDataLabel] == newNoteValue);
                    Assert.IsTrue((string)carnassial.State.CurrentFileSnapshot[TestConstant.DefaultDatabaseColumn.NoteWithCustomDataLabel] == newNoteValue);
                    this.WaitForRenderingComplete();

                    singleEdit.Undo(carnassial);
                    Assert.IsTrue(singleEdit.CanExecute(carnassial));
                    Assert.IsFalse(singleEdit.CanUndo(carnassial));
                    Assert.IsTrue(String.Equals(singleEdit.DataLabel, TestConstant.DefaultDatabaseColumn.NoteWithCustomDataLabel, StringComparison.Ordinal));
                    Assert.IsFalse(singleEdit.IsAsync);
                    Assert.IsFalse(singleEdit.IsExecuted);
                    Assert.IsTrue((string)singleEdit.NewValue == newNoteValue);
                    Assert.IsTrue((string)singleEdit.PreviousValue == previousNoteValue);
                    Assert.IsTrue(singleEdit.PropertyName == TestConstant.DefaultDatabaseColumn.NoteWithCustomDataLabel);
                    Assert.IsTrue((string)carnassial.DataHandler.ImageCache.Current[TestConstant.DefaultDatabaseColumn.NoteWithCustomDataLabel] == previousNoteValue);
                    Assert.IsTrue((string)carnassial.State.CurrentFileSnapshot[TestConstant.DefaultDatabaseColumn.NoteWithCustomDataLabel] == previousNoteValue);
                    this.WaitForRenderingComplete();

                    // choice change
                    string previousChoiceValue = (string)carnassial.DataHandler.ImageCache.Current[TestConstant.DefaultDatabaseColumn.NoteWithCustomDataLabel];
                    string newChoiceValue = "choice changed value";
                    FileSingleFieldChange choiceEdit = new(carnassial.DataHandler.ImageCache.Current.ID, TestConstant.DefaultDatabaseColumn.Choice0, TestConstant.DefaultDatabaseColumn.Choice0, previousChoiceValue, newChoiceValue, false);
                    choiceEdit.Execute(carnassial);
                    Assert.IsTrue((string)carnassial.DataHandler.ImageCache.Current[TestConstant.DefaultDatabaseColumn.Choice0] == newChoiceValue);
                    choiceEdit.Undo(carnassial);
                    Assert.IsTrue((string)carnassial.DataHandler.ImageCache.Current[TestConstant.DefaultDatabaseColumn.Choice0] == previousChoiceValue);

                    // flag change
                    bool previousFlagValue = (bool)carnassial.DataHandler.ImageCache.Current[TestConstant.DefaultDatabaseColumn.Flag0];
                    bool newFlagValue = !previousFlagValue;
                    FileSingleFieldChange flagEdit = new(carnassial.DataHandler.ImageCache.Current.ID, TestConstant.DefaultDatabaseColumn.Flag0, TestConstant.DefaultDatabaseColumn.Flag0, previousFlagValue, newFlagValue, false);
                    flagEdit.Execute(carnassial);
                    Assert.IsTrue((bool)carnassial.DataHandler.ImageCache.Current[TestConstant.DefaultDatabaseColumn.Flag0] == newFlagValue);
                    flagEdit.Undo(carnassial);
                    Assert.IsTrue((bool)carnassial.DataHandler.ImageCache.Current[TestConstant.DefaultDatabaseColumn.Flag0] == previousFlagValue);

                    // marker insertion and removal
                    MarkersForCounter previousMarkers = carnassial.DataHandler.ImageCache.Current.GetMarkersForCounter(TestConstant.DefaultDatabaseColumn.Counter0);
                    Marker newMarker = new(TestConstant.DefaultDatabaseColumn.Counter0, default) { LabelShownPreviously = false, ShowLabel = true };
                    MarkerCreatedOrDeletedEventArgs markerEvent = new(newMarker, true);
                    FileMarkerChange counterEdit = new(carnassial.DataHandler.ImageCache.Current.ID, markerEvent);
                    counterEdit.Execute(carnassial);
                    MarkersForCounter newMarkers = carnassial.DataHandler.ImageCache.Current.GetMarkersForCounter(TestConstant.DefaultDatabaseColumn.Counter0);
                    Assert.IsTrue(newMarkers.Count == previousMarkers.Count + 1);
                    counterEdit.Undo(carnassial);
                    MarkersForCounter currentMarkers = carnassial.DataHandler.ImageCache.Current.GetMarkersForCounter(TestConstant.DefaultDatabaseColumn.Counter0);
                    Assert.IsTrue(currentMarkers.Count == previousMarkers.Count);

                    Assert.IsTrue(carnassial.DataHandler.ImageCache.Current.HasChanges);
                    Assert.IsTrue(carnassial.DataHandler.TrySyncCurrentFileToDatabase());
                    Assert.IsFalse(carnassial.DataHandler.ImageCache.Current.HasChanges);

                    // custom selection edit
                    Data.CustomSelection currentSelection = carnassial.DataHandler.FileDatabase.CustomSelection;
                    Data.CustomSelection undoSelection = new(currentSelection);
                    CustomSelectionChange customSelectionEdit = new(undoSelection, currentSelection);
                    Assert.IsFalse(customSelectionEdit.CanExecute(carnassial));
                    Assert.IsTrue(customSelectionEdit.CanUndo(carnassial));
                    Assert.IsFalse(customSelectionEdit.IsAsync);
                    Assert.IsTrue(customSelectionEdit.IsExecuted);

                    Assert.IsFalse(customSelectionEdit.HasChanges());
                    undoSelection.SearchTerms[0].UseForSearching = !undoSelection.SearchTerms[0].UseForSearching;
                    Assert.IsTrue(customSelectionEdit.HasChanges());

                    customSelectionEdit.Undo(carnassial);
                    Assert.IsTrue(Object.ReferenceEquals(undoSelection, carnassial.DataHandler.FileDatabase.CustomSelection));
                    Assert.IsTrue(customSelectionEdit.CanExecute(carnassial));
                    Assert.IsFalse(customSelectionEdit.CanUndo(carnassial));
                    Assert.IsFalse(customSelectionEdit.IsExecuted);

                    customSelectionEdit.Execute(carnassial);
                    // CustomSelectionChange..ctor() clones the current selection, so the clone should be in place after execution
                    Assert.IsFalse(Object.ReferenceEquals(currentSelection, carnassial.DataHandler.FileDatabase.CustomSelection));
                    Assert.IsFalse(Object.ReferenceEquals(undoSelection, carnassial.DataHandler.FileDatabase.CustomSelection));
                    Assert.IsFalse(customSelectionEdit.CanExecute(carnassial));
                    Assert.IsTrue(customSelectionEdit.CanUndo(carnassial));
                    Assert.IsTrue(customSelectionEdit.IsExecuted);

                    // verify application exit
                    Task<bool> backupTask = carnassial.DataHandler.FileDatabase.TryBackupAsync();
                    carnassial.Close();
                    Assert.IsTrue(backupTask.IsCompleted);
                    Assert.IsTrue(backupTask.Result);

                    if (cancellationTokenSource.Token.CanBeCanceled)
                    {
                        cancellationTokenSource.Cancel();
                    }
                }

                // open, load existing database, pop dialogs, backup database, close
                using (CarnassialWindow carnassial = new())
                {
                    carnassial.Show();
                    this.WaitForRenderingComplete();

                    Task<bool> loadFolder = null;
                    Dispatcher.CurrentDispatcher.Invoke(() =>
                    {
                        loadFolder = carnassial.TryOpenTemplateAndFileDatabaseAsync(templateDatabaseFilePath);
                    });
                    this.WaitForFolderLoadComplete(loadFolder);

                    Assert.IsTrue(carnassial.DataHandler.FileDatabase.CurrentlySelectedFileCount > 0);
                    Assert.IsNotNull(carnassial.DataHandler.ImageCache.Current);

                    this.ShowDialog(new About(carnassial));
                    this.ShowDialog(new AdvancedCarnassialOptions(carnassial.State, carnassial.FileDisplay, carnassial));
                    this.ShowDialog(new ChooseFileDatabase(new List<string>() { TestConstant.File.DefaultNewFileDatabaseFileName }, TestConstant.File.DefaultTemplateDatabaseFileName, carnassial));

                    this.ShowDialog(new Dialog.CustomSelection(carnassial.DataHandler.FileDatabase, carnassial));

                    this.ShowDialog(new DateCorrectAmbiguous(carnassial.DataHandler.FileDatabase, carnassial));
                    this.ShowDialog(new DateDaylightSavingsTimeCorrection(carnassial.DataHandler.FileDatabase, carnassial.DataHandler.ImageCache, carnassial));

                    DateTimeFixedCorrection clockSetCorrection = new(carnassial.DataHandler.FileDatabase, carnassial.DataHandler.ImageCache, carnassial);
                    this.ShowDialog(clockSetCorrection);

                    DateTimeLinearCorrection clockDriftCorrection = new(carnassial.DataHandler.FileDatabase, carnassial);
                    Assert.IsTrue(clockDriftCorrection.Abort == (carnassial.DataHandler.ImageCache.Current == null));
                    this.ShowDialog(clockDriftCorrection);

                    this.ShowDialog(new DateTimeRereadFromFiles(carnassial.DataHandler.FileDatabase, carnassial.State.Throttles.GetDesiredProgressUpdateInterval(), carnassial));
                    this.ShowDialog(new DateTimeSetTimeZone(carnassial.DataHandler.FileDatabase, carnassial.DataHandler.ImageCache, carnassial));
                    this.ShowDialog(new FileCountsByClassification(carnassial.DataHandler.FileDatabase.GetFileCountsByClassification(), carnassial));
                    this.ShowDialog(new EditLog(carnassial.DataHandler.FileDatabase.ImageSet.Log, carnassial));
                    this.ShowDialog(new FindReplace(carnassial));
                    this.ShowDialog(new GoToFile(carnassial.DataHandler.ImageCache.CurrentRow, carnassial.DataHandler.FileDatabase.Files.RowCount, carnassial));

                    this.ShowDialog(MessageBox.FromResource(Constant.ResourceKey.ApplicationWindowException, carnassial, CarnassialConfigurationSettings.GetIssuesBrowserAddress(), CarnassialConfigurationSettings.GetDevTeamEmailLink().ToEmailAddress(), typeof(CarnassialWindow).Assembly.GetName(), Environment.OSVersion, RuntimeInformation.FrameworkDescription, "database path", null));
                    this.ShowDialog(MessageBox.FromResource(Constant.ResourceKey.CarnassialWindowClockDriftFailed, carnassial));
                    this.ShowDialog(MessageBox.FromResource(Constant.ResourceKey.CarnassialWindowCopyFileFailed, carnassial, "file path"));
                    this.ShowDialog(MessageBox.FromResource(Constant.ResourceKey.CarnassialWindowDatabaseLoadFailed, carnassial, "file path"));
                    this.ShowDialog(MessageBox.FromResource(Constant.ResourceKey.CarnassialWindowDaylightSavingsFailed, carnassial));
                    this.ShowDialog(MessageBox.FromResource(Constant.ResourceKey.CarnassialWindowExportSpreadsheetFailed, carnassial, "file path", "Exception.Type", "exception message"));
                    this.ShowDialog(MessageBox.FromResource(Constant.ResourceKey.CarnassialWindowFileMoveIncomplete, carnassial, 0, 1, 1));
                    this.ShowDialog(MessageBox.FromResource(Constant.ResourceKey.CarnassialWindowImport, carnassial, Constant.Time.DateTimeDatabaseFormat, DateTimeHandler.ToDatabaseUtcOffsetString(TimeSpan.FromHours(Constant.Time.MinimumUtcOffsetInHours)), DateTimeHandler.ToDatabaseUtcOffsetString(TimeSpan.FromHours(Constant.Time.MinimumUtcOffsetInHours)), Constant.Excel.FileDataWorksheetName, Constant.File.FileDatabaseFileExtension));
                    this.ShowDialog(MessageBox.FromResource(Constant.ResourceKey.CarnassialWindowImportFailed, carnassial, "file path", "Exception.Type", "exception message"));
                    this.ShowDialog(MessageBox.FromResource(Constant.ResourceKey.CarnassialWindowImportIncomplete, carnassial, "file path"));
                    this.ShowDialog(MessageBox.FromResource(Constant.ResourceKey.CarnassialWindowNoAmbiguousDates, carnassial));
                    this.ShowDialog(MessageBox.FromResource(Constant.ResourceKey.CarnassialWindowNoDeletableFiles, carnassial));
                    this.ShowDialog(MessageBox.FromResource(Constant.ResourceKey.CarnassialWindowNoMetadataAvailable, carnassial));
                    this.ShowDialog(MessageBox.FromResource(Constant.ResourceKey.CarnassialWindowSelectFolder, carnassial, carnassial.DataHandler.FileDatabase.FolderPath));
                    this.ShowDialog(MessageBox.FromResource(Constant.ResourceKey.CarnassialWindowTemplateLoadFailed, carnassial, "file path"));
                    this.ShowDialog(MessageBox.FromResource(Constant.ResourceKey.GithubReleaseClientGetNewVersion, App.Current.MainWindow, new Version(), Constant.ApplicationName, new Version(), CarnassialConfigurationSettings.GetReleasesBrowserAddress()));
                    this.ShowDialog(MessageBox.FromResource(Constant.ResourceKey.GithubReleaseClientNoUpdates, App.Current.MainWindow, Constant.ApplicationName, new Version()));

                    this.ShowDialog(new PopulateFieldWithMetadata(carnassial.DataHandler.FileDatabase, carnassial.DataHandler.ImageCache.Current.GetFilePath(carnassial.DataHandler.FileDatabase.FolderPath), carnassial.State.Throttles.GetDesiredProgressUpdateInterval(), carnassial));
                    using (ReclassifyFiles reclassify = new(carnassial.DataHandler.FileDatabase, carnassial.DataHandler.ImageCache, new CarnassialState(), carnassial))
                    {
                        this.ShowDialog(reclassify);
                    }
                    this.ShowDialog(new RenameFileDatabaseFile(carnassial.DataHandler.FileDatabase.FileName, carnassial));
                    this.ShowDialog(new TemplateSynchronization(carnassial.DataHandler.FileDatabase.ControlSynchronizationIssues, carnassial));

                    Task<bool> backupTask = carnassial.DataHandler.FileDatabase.TryBackupAsync();
                    carnassial.Close();
                    Assert.IsTrue(backupTask.IsCompleted);
                    Assert.IsFalse(backupTask.Result);
                }
            }
        }

        [TestMethod]
        public void DataEntryHandler()
        {
            List<DatabaseExpectations> databaseExpectations = new()
            {
                new DatabaseExpectations()
                {
                    FileName = Constant.File.DefaultFileDatabaseFileName,
                    TemplateDatabaseFileName = TestConstant.File.DefaultTemplateDatabaseFileName,
                    ExpectedColumns = TestConstant.DefaultFileColumns
                }
            };

            // CreateReuseControlsAndPropagate() needs an app instance in order for DataEntryControls controls to load resources
            // but Carnassial() requires only one .ddb be present in the UI tests subdirectory; lock to exclude concurrent execution
            // of the relative parts of these two tests
            lock (CarnassialTest.App)
            {
                foreach (DatabaseExpectations databaseExpectation in databaseExpectations)
                {
                    using FileDatabase fileDatabase = this.CreateFileDatabase(databaseExpectation.TemplateDatabaseFileName, databaseExpectation.FileName);
                    using (DataEntryHandler dataHandler = new(fileDatabase))
                    {
                        DataEntryControls controls = new();
                        controls.CreateControls(fileDatabase, dataHandler, (string dataLabel) => { return fileDatabase.GetDistinctValuesInFileDataColumn(dataLabel); });

                        // 4 controls not visible in default database + 4 marker position columns + date time and utc offset as single control + no control for id column
                        int expectedDataEntryControls = databaseExpectation.ExpectedColumns.Count - 4 - 4 - 1 - 1;
                        Assert.IsTrue(controls.ControlsByDataLabel.Count == expectedDataEntryControls, "Expected {0} data entry controls to be generated but {1} were.", expectedDataEntryControls, controls.ControlsByDataLabel.Count);

                        // check copies aren't possible when the image enumerator's not pointing to an image
                        List<DataEntryControl> copyableControls = controls.Controls.Where(control => control.Copyable).ToList();
                        foreach (DataEntryControl control in copyableControls)
                        {
                            Assert.IsFalse(dataHandler.IsCopyForwardPossible(control));
                            Assert.IsFalse(dataHandler.IsCopyFromLastNonEmptyValuePossible(control));
                        }

                        // check only copy forward is possible when enumerator's on first image
                        List<FileExpectations> fileExpectations = this.PopulateDefaultDatabase(fileDatabase);
                        foreach (FileExpectations fileExpectation in fileExpectations)
                        {
                            fileExpectation.RelativePath = Constant.File.ParentDirectory;
                        }
                        Assert.IsTrue(dataHandler.ImageCache.MoveNext());

                        foreach (DataEntryControl control in copyableControls)
                        {
                            Assert.IsTrue(dataHandler.IsCopyForwardPossible(control));
                            Assert.IsFalse(dataHandler.IsCopyFromLastNonEmptyValuePossible(control));
                        }

                        // check only copy last is possible when enumerator's on last image
                        // check also copy last is not possible if no previous instance of the field has been filled out
                        while (dataHandler.ImageCache.CurrentRow < fileExpectations.Count - 1)
                        {
                            Assert.IsTrue(dataHandler.ImageCache.MoveNext());
                        }

                        foreach (DataEntryControl control in copyableControls)
                        {
                            Assert.IsFalse(dataHandler.IsCopyForwardPossible(control));
                            if (String.Equals(control.DataLabel, TestConstant.CarnivoreDatabaseColumn.Pelage, StringComparison.Ordinal) ||
                                String.Equals(control.DataLabel, TestConstant.DefaultDatabaseColumn.ChoiceNotVisible, StringComparison.Ordinal) ||
                                String.Equals(control.DataLabel, TestConstant.DefaultDatabaseColumn.Choice3, StringComparison.Ordinal) ||
                                String.Equals(control.DataLabel, TestConstant.DefaultDatabaseColumn.Counter3, StringComparison.Ordinal) ||
                                String.Equals(control.DataLabel, TestConstant.DefaultDatabaseColumn.Flag0, StringComparison.Ordinal))
                            {
                                Assert.IsFalse(dataHandler.IsCopyFromLastNonEmptyValuePossible(control), control.DataLabel);
                            }
                            else
                            {
                                Assert.IsTrue(dataHandler.IsCopyFromLastNonEmptyValuePossible(control), control.DataLabel);
                            }
                        }

                        // propagation methods
                        // Currently no coverage due to need for UX automation beyond ShowDialog() to confirm changes.
                        // DataEntryControl noteControl = controls.ControlsByDataLabel[TestConstant.DefaultDatabaseColumn.NoteWithCustomDataLabel];
                        // Assert.IsTrue(dataHandler.ImageCache.TryMoveToFile(fileDatabase.Files.RowCount - 1));
                        // Assert.IsTrue(dataHandler.TryCopyForward(noteControl) == false);
                        // Assert.IsTrue(dataHandler.TryCopyFromLastNonEmptyValue(control, out object value) == false);
                        // Assert.IsTrue(dataHandler.TryCopyToAll(control) == false);
                        Window owner = new();
                        owner.Show();
                        this.ShowDialog(MessageBox.FromResource(Constant.ResourceKey.DataEntryHandlerConfirmCopyAll, owner, 0, null));
                        this.ShowDialog(MessageBox.FromResource(Constant.ResourceKey.DataEntryHandlerConfirmCopyForward, owner, null, 0));
                        this.ShowDialog(MessageBox.FromResource(Constant.ResourceKey.DataEntryHandlerConfirmPropagateToHere, owner, null, 0));
                        this.ShowDialog(MessageBox.FromResource(Constant.ResourceKey.DataEntryHandlerNothingToCopyForward, owner));
                        this.ShowDialog(MessageBox.FromResource(Constant.ResourceKey.DataEntryHandlerNothingToPropagate, owner));
                        owner.Close();

                        // verify roundtrip of fields subject to copy/paste and analysis assignment
                        // GetValues() returns a dictionary with one fewer values than there are columns as the DateTime and UtcOffset
                        // columns are merged to DateTimeOffset.
                        Assert.IsTrue(dataHandler.ImageCache.TryMoveToFile(0));
                        ImageRow firstFile = fileDatabase.Files[0];
                        FileExpectations firstFileExpectations = fileExpectations[0];

                        Dictionary<string, object> firstFileValuesByPropertyName = firstFile.GetValues();
                        Assert.IsTrue(firstFileValuesByPropertyName.Count == databaseExpectation.ExpectedColumns.Count - 1);
                        foreach (KeyValuePair<string, object> singlePropertyEdit in firstFileValuesByPropertyName)
                        {
                            if (singlePropertyEdit.Key == Constant.DatabaseColumn.ID)
                            {
                                continue;
                            }
                            firstFile[singlePropertyEdit.Key] = singlePropertyEdit.Value;
                        }

                        TimeZoneInfo imageSetTimeZone = fileDatabase.ImageSet.GetTimeZoneInfo();
                        firstFileExpectations.Verify(firstFile, imageSetTimeZone);

                        // verify availability of database strings
                        foreach (string dataLabel in databaseExpectation.ExpectedColumns)
                        {
                            string databaseString = firstFile.GetSpreadsheetString(dataLabel);
                        }

                        // verify availability of default values (used for resetting file values)
                        Dictionary<string, object> defaultValuesByPropertyName = ImageRow.GetDefaultValues(fileDatabase);
                        Assert.IsTrue(firstFileValuesByPropertyName.Count - 5 == defaultValuesByPropertyName.Count);
                        foreach (string property in firstFileValuesByPropertyName.Keys)
                        {
                            if (String.Equals(property, nameof(ImageRow.DateTimeOffset), StringComparison.Ordinal) ||
                                String.Equals(property, nameof(ImageRow.FileName), StringComparison.Ordinal) ||
                                String.Equals(property, Constant.DatabaseColumn.ID, StringComparison.Ordinal) ||
                                String.Equals(property, Constant.FileColumn.RelativePath, StringComparison.Ordinal) ||
                                String.Equals(property, Constant.FileColumn.Classification, StringComparison.Ordinal))
                            {
                                continue;
                            }
                            Assert.IsTrue(defaultValuesByPropertyName.ContainsKey(property));
                        }
                        FileMultipleFieldChange resetChange = new(dataHandler.ImageCache, defaultValuesByPropertyName);
                        Assert.IsTrue(resetChange.Changes == 5);

                        // find and replace
                        // no op/default state case
                        Assert.IsTrue(dataHandler.TryFindNext(out int fileIndex) == false);
                        Assert.IsTrue(dataHandler.TryFindPrevious(out fileIndex) == false);
                        Assert.IsTrue(dataHandler.FindReplace.TryReplace(fileDatabase.Files[0]) == false);
                        Assert.IsTrue(dataHandler.ReplaceAll() == 0);
                        Assert.IsTrue(dataHandler.FindReplace != null);
                        Assert.IsTrue(String.Equals(dataHandler.FindReplace.FindTerm1.DataLabel, Constant.FileColumn.File, StringComparison.Ordinal));
                        Assert.IsTrue(dataHandler.FindReplace.FindTerm2 == null);
                        Assert.IsTrue(dataHandler.FindReplace.ReplaceTerm == null);

                        // single matching file
                        ControlRow note = fileDatabase.Controls[TestConstant.DefaultDatabaseColumn.Note3];
                        SearchTerm bobcatName = note.CreateSearchTerm();
                        bobcatName.DatabaseValue = "bobcat";
                        bobcatName.UseForSearching = true;
                        dataHandler.FindReplace.FindTerm1 = bobcatName;

                        int bobcatFileIndex = (int)(fileExpectations.Single(expectation => String.Equals(expectation.FileName, TestConstant.FileExpectation.DaylightBobcatFileName, StringComparison.Ordinal)).ID - 1);
                        ImageRow bobcatFile = fileDatabase.Files[bobcatFileIndex];
                        Assert.IsTrue(dataHandler.TryFindNext(out fileIndex));
                        Assert.IsTrue(bobcatFileIndex == fileIndex);
                        Assert.IsTrue(dataHandler.TryFindPrevious(out fileIndex));
                        Assert.IsTrue(bobcatFileIndex == fileIndex);
                        Assert.IsTrue(dataHandler.FindReplace.TryReplace(bobcatFile) == false);
                        Assert.IsTrue(dataHandler.ReplaceAll() == 0);

                        // no matching files
                        SearchTerm ocelot = note.CreateSearchTerm();
                        ocelot.DatabaseValue = "ocelot";
                        ocelot.UseForSearching = true;
                        dataHandler.FindReplace.FindTerm2 = ocelot;
                        Assert.IsTrue(dataHandler.TryFindNext(out fileIndex) == false);
                        Assert.IsTrue(dataHandler.TryFindPrevious(out fileIndex) == false);
                        Assert.IsTrue(dataHandler.FindReplace.TryReplace(bobcatFile) == false);
                        Assert.IsTrue(dataHandler.ReplaceAll() == 0);

                        // multiple matching files
                        ControlRow classification = fileDatabase.Controls[Constant.FileColumn.Classification];
                        SearchTerm color = classification.CreateSearchTerm();
                        color.DatabaseValue = FileClassification.Color;
                        color.UseForSearching = true;
                        dataHandler.FindReplace.FindTerm1 = color;
                        dataHandler.FindReplace.FindTerm2 = null;

                        Assert.IsTrue(dataHandler.TryFindNext(out fileIndex));
                        Assert.IsTrue(fileIndex == bobcatFileIndex);
                        Assert.IsTrue(dataHandler.ImageCache.TryMoveToFile(fileIndex));
                        Assert.IsTrue(dataHandler.TryFindNext(out fileIndex));
                        Assert.IsTrue(fileIndex == bobcatFileIndex + 1);
                        Assert.IsTrue(dataHandler.ImageCache.TryMoveToFile(fileIndex));
                        Assert.IsTrue(dataHandler.TryFindNext(out fileIndex));
                        Assert.IsTrue(fileIndex == bobcatFileIndex + 2);
                        Assert.IsTrue(dataHandler.ImageCache.TryMoveToFile(fileIndex));
                        Assert.IsTrue(dataHandler.TryFindNext(out fileIndex));
                        Assert.IsTrue(fileIndex == bobcatFileIndex);
                        Assert.IsTrue(dataHandler.ImageCache.TryMoveToFile(fileIndex));

                        Assert.IsTrue(dataHandler.TryFindPrevious(out fileIndex));
                        Assert.IsTrue(fileIndex == bobcatFileIndex + 2);
                        Assert.IsTrue(dataHandler.ImageCache.TryMoveToFile(fileIndex));
                        Assert.IsTrue(dataHandler.TryFindPrevious(out fileIndex));
                        Assert.IsTrue(fileIndex == bobcatFileIndex + 1);
                        Assert.IsTrue(dataHandler.ImageCache.TryMoveToFile(fileIndex));
                        Assert.IsTrue(dataHandler.TryFindPrevious(out fileIndex));
                        Assert.IsTrue(fileIndex == bobcatFileIndex);
                        Assert.IsTrue(dataHandler.ImageCache.TryMoveToFile(fileIndex));
                        Assert.IsTrue(dataHandler.TryFindPrevious(out fileIndex));
                        Assert.IsTrue(fileIndex == bobcatFileIndex + 2);
                        Assert.IsTrue(dataHandler.ImageCache.TryMoveToFile(fileIndex));

                        // multiple terms, one matching file
                        dataHandler.FindReplace.FindTerm2 = bobcatName;
                        Assert.IsTrue(dataHandler.TryFindPrevious(out fileIndex));
                        Assert.IsTrue(fileIndex == bobcatFileIndex);
                        Assert.IsTrue(dataHandler.TryFindNext(out fileIndex));
                        Assert.IsTrue(fileIndex == bobcatFileIndex);

                        // replace
                        Assert.IsTrue(dataHandler.FindReplace.TryReplace(bobcatFile) == false);

                        SearchTerm dark = classification.CreateSearchTerm();
                        dark.DatabaseValue = FileClassification.Dark;
                        dark.UseForSearching = true;
                        dataHandler.FindReplace.ReplaceTerm = dark;
                        Assert.IsTrue(dataHandler.FindReplace.TryReplace(bobcatFile));
                        Assert.IsTrue(bobcatFile.Classification == FileClassification.Dark);

                        dataHandler.FindReplace.FindTerm1 = dark;
                        dataHandler.FindReplace.ReplaceTerm = color;
                        Assert.IsTrue(dataHandler.FindReplace.TryReplace(bobcatFile));
                        Assert.IsTrue(bobcatFile.Classification == FileClassification.Color);

                        // replace all
                        dataHandler.FindReplace.ReplaceTerm = null;
                        Assert.IsTrue(dataHandler.ReplaceAll() == 0);

                        // reclassify color files as dark
                        dataHandler.FindReplace.FindTerm1 = color;
                        dataHandler.FindReplace.FindTerm2 = null;
                        dataHandler.FindReplace.ReplaceTerm = dark;
                        int colorFiles = fileDatabase.Files.Count(file => file.Classification == FileClassification.Color);
                        Assert.IsTrue(dataHandler.ReplaceAll() == colorFiles);
                        int darkFiles = fileDatabase.Files.Count(file => file.Classification == FileClassification.Dark);
                        Assert.IsTrue(darkFiles == colorFiles);

                        // change dark files back to color
                        dataHandler.FindReplace.FindTerm1 = dark;
                        dataHandler.FindReplace.ReplaceTerm = color;
                        Assert.IsTrue(dataHandler.ReplaceAll() == darkFiles);
                        colorFiles = fileDatabase.Files.Count(file => file.Classification == FileClassification.Color);
                        Assert.IsTrue(darkFiles == colorFiles);
                    }

                    // force SQLite to release its handle on the database file so that it can be deleted
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    File.Delete(fileDatabase.FilePath);
                }
            }
        }

        [TestMethod]
        public void Editor()
        {
            // open, do nothing, close
            using (EditorWindow editor = new())
            {
                editor.Show();
                this.WaitForRenderingComplete();
                editor.Close();
            }

            // open, create template database, close
            string templateDatabaseFilePath = this.GetUniqueFilePathForTest(TestConstant.File.DefaultNewTemplateDatabaseFileName);
            if (File.Exists(templateDatabaseFilePath))
            {
                File.Delete(templateDatabaseFilePath);
            }
            using (EditorWindow editor = new())
            {
                editor.Show();
                this.WaitForRenderingComplete();
                //TODO: PrivateObject
                //PrivateObject editorAccessor = new PrivateObject(editor);
                //editorAccessor.Invoke(TestConstant.InitializeDataGridMethodName, templateDatabaseFilePath);
                this.WaitForRenderingComplete();
                editor.Close();
            }

            // open, load existing database, pop dialogs, close
            using (EditorWindow editor = new())
            {
                editor.Show();
                this.WaitForRenderingComplete();
                //TODO: PrivateObject
                //PrivateObject editorAccessor = new PrivateObject(editor);
                //editorAccessor.Invoke(TestConstant.InitializeDataGridMethodName, templateDatabaseFilePath);
                //this.WaitForRenderingComplete();

                //editor.Tabs.SelectedIndex = 1;
                //this.WaitForRenderingComplete();

                //this.ShowDialog(new AboutEditor(editor));
                //TemplateDatabase templateDatabase = (TemplateDatabase)editorAccessor.GetField(TestConstant.EditorTemplateDatabaseFieldName);
                //this.ShowDialog(new AdvancedImageSetOptions(templateDatabase, editor));
                //this.ShowDialog(new EditWellKnownValues(editor.ControlDataGrid, new List<string>() { "Choice0", "Choice1", "Choice2", "Choice3" }, editor));

                this.ShowDialog(MessageBox.FromResource(EditorConstant.ResourceKey.EditorWindowDataLabelEmpty, editor));
                this.ShowDialog(MessageBox.FromResource(EditorConstant.ResourceKey.EditorWindowDataLabelNotUnique, editor, "currentDataLabel"));
                this.ShowDialog(MessageBox.FromResource(EditorConstant.ResourceKey.EditorWindowTemplateLoadFailed, editor, "file path"));

                editor.Close();
            }
        }

        private void ShowDialog(Window dialog)
        {
            dialog.Loaded += (object sender, RoutedEventArgs eventArgs) => { dialog.Close(); };
            Dispatcher.CurrentDispatcher.InvokeAsync(() =>
            {
                dialog.ShowDialog();
            });
            this.WaitForRenderingComplete();
        }

        private bool TryFindDialogOkButton(AutomationElement parent, string automationID, CancellationToken cancellationToken, out InvokePattern okButtonInvoke)
        {
            okButtonInvoke = null;
            if (cancellationToken.IsCancellationRequested)
            {
                return false;
            }

            AutomationElement dialog = null;
            DateTime startTime = DateTime.UtcNow;
            while ((dialog == null) && (DateTime.UtcNow - startTime < TestConstant.UIElementSearchTimeout))
            {
                Thread.Sleep(TimeSpan.FromMilliseconds(100));
                if (cancellationToken.IsCancellationRequested)
                {
                    return false;
                }
                dialog = parent.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, automationID));
            }
            if (cancellationToken.IsCancellationRequested)
            {
                return false;
            }
            Assert.IsNotNull(dialog);

            AutomationElement okButton = dialog.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, TestConstant.OkButtonAutomationID));
            Assert.IsNotNull(okButton);
            okButtonInvoke = (InvokePattern)okButton.GetCurrentPattern(InvokePattern.Pattern);
            return true;
        }

        private void WaitForFolderLoadComplete(Task<bool> loadFolder)
        {
            while (loadFolder.IsCompleted == false)
            {
                this.WaitForRenderingComplete();
            }
            Assert.IsFalse(loadFolder.IsCanceled);
            if (loadFolder.IsFaulted)
            {
                throw loadFolder.Exception;
            }
            Assert.IsTrue(loadFolder.Result);
            this.WaitForRenderingComplete();
        }

        private void WaitForRenderingComplete()
        {
            // make a best effort at letting WPF's render thread(s) catch up
            // from https://msdn.microsoft.com/en-us/library/system.windows.threading.dispatcher.pushframe.aspx
            DispatcherFrame frame = new();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new DispatcherOperationCallback((object arg) =>
            {
                ((DispatcherFrame)arg).Continue = false;
                return null;
            }), frame);
            Dispatcher.PushFrame(frame);
        }
    }
}
