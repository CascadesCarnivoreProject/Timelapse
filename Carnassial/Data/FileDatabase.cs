using Carnassial.Control;
using Carnassial.Database;
using Carnassial.Interop;
using Carnassial.Util;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using ColumnDefinition = Carnassial.Database.ColumnDefinition;

namespace Carnassial.Data
{
    public class FileDatabase : TemplateDatabase
    {
        public AutocompletionCache AutocompletionCache { get; private set; }
        public List<string> ControlSynchronizationIssues { get; private set; }
        public CustomSelection? CustomSelection { get; set; }

        /// <summary>Gets the file name of the database on disk.</summary>
        public string FileName { get; private set; }
        public FileTable Files { get; private set; }
        public bool OrderFilesByDateTime { get; set; }

        private FileDatabase(string filePath)
            : base(filePath)
        {
            this.AutocompletionCache = new AutocompletionCache(this);
            this.ControlSynchronizationIssues = new List<string>();
            this.FileName = Path.GetFileName(filePath);
            this.Files = new FileTable();
            this.OrderFilesByDateTime = false;
        }

        /// <summary>Gets the number of files currently in the files table.</summary>
        public int CurrentlySelectedFileCount
        {
            get { return this.Files.RowCount; }
        }

        public void AdjustFileTimes(TimeSpan adjustment)
        {
            this.AdjustFileTimes(adjustment, 0, this.CurrentlySelectedFileCount - 1);
        }

        public void AdjustFileTimes(TimeSpan adjustment, int startIndex, int endIndex)
        {
            this.AdjustFileTimes((DateTimeOffset fileTime) => { return fileTime + adjustment; }, startIndex, endIndex);
        }

        // invoke the passed function to modify the DateTime field over the specified range of files
        public void AdjustFileTimes(Func<DateTimeOffset, DateTimeOffset> adjustment, int startIndex, int endIndex)
        {
            if (this.IsFileRowInRange(startIndex) == false)
            {
                throw new ArgumentOutOfRangeException(nameof(startIndex));
            }
            if (this.IsFileRowInRange(endIndex) == false)
            {
                throw new ArgumentOutOfRangeException(nameof(endIndex));
            }
            if (endIndex < startIndex)
            {
                throw new ArgumentOutOfRangeException(nameof(endIndex), App.FormatResource(Constant.ResourceKey.FileDatabaseEndBeforeStart, nameof(endIndex), nameof(startIndex)));
            }
            if (this.CurrentlySelectedFileCount == 0)
            {
                return;
            }

            // modify date/times
            List<ImageRow> filesToAdjust = new();
            TimeSpan mostRecentAdjustment = TimeSpan.Zero;
            for (int row = startIndex; row <= endIndex; ++row)
            {
                ImageRow file = this.Files[row];
                Debug.Assert(file.HasChanges == false, "File has unexpected pending changes.");
                DateTimeOffset currentImageDateTime = file.DateTimeOffset;

                // adjust the date/time
                DateTimeOffset newFileDateTime = adjustment.Invoke(currentImageDateTime);
                if (newFileDateTime == currentImageDateTime)
                {
                    continue;
                }

                mostRecentAdjustment = newFileDateTime - currentImageDateTime;
                file.DateTimeOffset = newFileDateTime;
                filesToAdjust.Add(file);
            }

            // update the database with the new date/times
            if (filesToAdjust.Count > 0)
            {
                using (UpdateFileDateTimeOffsetTransactionSequence updateTransaction = this.CreateUpdateFileDateTimeTransaction())
                {
                    updateTransaction.UpdateFiles(filesToAdjust);
                    updateTransaction.Commit();
                }

                // add log entry recording the change
                StringBuilder log = new(Environment.NewLine);
                log.AppendFormat(CultureInfo.CurrentCulture, "System entry: Adjusted dates and times of {0} selected files.{1}", filesToAdjust.Count, Environment.NewLine);
                log.AppendFormat(CultureInfo.CurrentCulture, "The first file adjusted was '{0}', the last '{1}', and the last file was adjusted by {2}.{3}", filesToAdjust[0].FileName, filesToAdjust[^1].FileName, mostRecentAdjustment, Environment.NewLine);
                this.AppendToImageSetLog(log);
            }
        }

        public void AppendToImageSetLog(StringBuilder logEntry)
        {
            Debug.Assert(this.ImageSet != null);
            this.ImageSet.Log += logEntry;
            this.TrySyncImageSetToDatabase();
        }

        public AddFilesTransactionSequence CreateAddFilesTransaction()
        {
            return new AddFilesTransactionSequence(this, this.Controls);
        }

        public FileTransactionSequence CreateInsertFileTransaction()
        {
            return FileTransactionSequence.CreateInsert(this, this.Files);
        }

        public UpdateFileColumnTransactionSequence CreateUpdateFileColumnTransaction(string dataLabel)
        {
            return new UpdateFileColumnTransactionSequence(dataLabel, this);
        }

        public UpdateFileDateTimeOffsetTransactionSequence CreateUpdateFileDateTimeTransaction()
        {
            return new UpdateFileDateTimeOffsetTransactionSequence(this);
        }

        public FileTransactionSequence CreateUpdateFileTransaction()
        {
            return FileTransactionSequence.CreateUpdate(this, this.Files);
        }

        private Select CreateSelect(FileSelection selection)
        {
            Select select = new(Constant.DatabaseTable.Files);
            switch (selection)
            {
                case FileSelection.All:
                    // no where clause needed
                    break;
                case FileSelection.Color:
                    select.Where.Add(new WhereClause(Constant.FileColumn.Classification, Constant.SqlOperator.Equal, (int)FileClassification.Color));
                    break;
                case FileSelection.Corrupt:
                    select.Where.Add(new WhereClause(Constant.FileColumn.Classification, Constant.SqlOperator.Equal, (int)FileClassification.Corrupt));
                    break;
                case FileSelection.Dark:
                    select.Where.Add(new WhereClause(Constant.FileColumn.Classification, Constant.SqlOperator.Equal, (int)FileClassification.Dark));
                    break;
                case FileSelection.Greyscale:
                    select.Where.Add(new WhereClause(Constant.FileColumn.Classification, Constant.SqlOperator.Equal, (int)FileClassification.Greyscale));
                    break;
                case FileSelection.NoLongerAvailable:
                    select.Where.Add(new WhereClause(Constant.FileColumn.Classification, Constant.SqlOperator.Equal, (int)FileClassification.NoLongerAvailable));
                    break;
                case FileSelection.Video:
                    select.Where.Add(new WhereClause(Constant.FileColumn.Classification, Constant.SqlOperator.Equal, (int)FileClassification.Video));
                    break;
                case FileSelection.MarkedForDeletion:
                    select.Where.Add(new WhereClause(Constant.FileColumn.DeleteFlag, Constant.SqlOperator.Equal, true));
                    break;
                case FileSelection.Custom:
                    Debug.Assert(this.CustomSelection != null);
                    return this.CustomSelection.CreateSelect();
                default:
                    throw new NotSupportedException(String.Format(CultureInfo.CurrentCulture, "Unhandled selection {0}.", selection));
            }
            return select;
        }

        // drop the data (including markers) associated with the files identified by the list of IDs.
        public int DeleteFiles(List<long> fileIDs)
        {
            if (fileIDs.Count < 1)
            {
                // nothing to do
                return 0;
            }

            using (SQLiteTransaction transaction = this.Connection.BeginTransaction())
            {
                using (SQLiteCommand deleteFiles = new(String.Format(CultureInfo.InvariantCulture, "DELETE FROM {0} WHERE {1} = @Id", Constant.DatabaseTable.Files, Constant.DatabaseColumn.ID), this.Connection, transaction))
                {
                    SQLiteParameter id = new("@Id");
                    deleteFiles.Parameters.Add(id);

                    foreach (long fileID in fileIDs)
                    {
                        id.Value = fileID;
                        deleteFiles.ExecuteNonQuery();
                    }

                    this.IncrementalVacuum(transaction);
                }

                transaction.Commit();
            }
            this.RowsDroppedSinceLastBackup += fileIDs.Count;
            return fileIDs.Count;
        }

        // swap the days and months of all file dates between the start and end index
        public void ExchangeDayAndMonthInFileDates(int startRow, int endRow)
        {
            if (this.IsFileRowInRange(startRow) == false)
            {
                throw new ArgumentOutOfRangeException(nameof(startRow));
            }
            if (this.IsFileRowInRange(endRow) == false)
            {
                throw new ArgumentOutOfRangeException(nameof(endRow));
            }
            if (endRow < startRow)
            {
                throw new ArgumentOutOfRangeException(nameof(endRow), App.FormatResource(Constant.ResourceKey.FileDatabaseEndBeforeStart, nameof(endRow), nameof(startRow)));
            }
            if (this.CurrentlySelectedFileCount == 0)
            {
                return;
            }

            // swap day and month for each file where it's possible
            List<ImageRow> filesToUpdate = new();
            ImageRow firstFile = this.Files[startRow];
            ImageRow? lastFile = null;
            DateTimeOffset mostRecentOriginalDateTime = DateTime.MinValue;
            DateTimeOffset mostRecentReversedDateTime = DateTime.MinValue;
            for (int row = startRow; row <= endRow; row++)
            {
                ImageRow file = this.Files[row];
                Debug.Assert(file.HasChanges == false, "File has unexpected pending changes.");
                DateTimeOffset originalDateTime = file.DateTimeOffset;
                if (DateTimeHandler.TrySwapDayMonth(originalDateTime, out DateTimeOffset reversedDateTime) == false)
                {
                    continue;
                }

                // update in memory table with the new datetime
                file.DateTimeOffset = reversedDateTime;
                filesToUpdate.Add(file);
                file.AcceptChanges();

                lastFile = file;
                mostRecentOriginalDateTime = originalDateTime;
                mostRecentReversedDateTime = reversedDateTime;
            }

            // update database with new datetimes
            if (filesToUpdate.Count > 0)
            {
                using (UpdateFileDateTimeOffsetTransactionSequence updateTransaction = this.CreateUpdateFileDateTimeTransaction())
                {
                    updateTransaction.UpdateFiles(filesToUpdate);
                    updateTransaction.Commit();
                }

                StringBuilder log = new(Environment.NewLine);
                log.AppendFormat(CultureInfo.CurrentCulture, "System entry: Swapped days and months for {0} files.", filesToUpdate.Count);
                log.AppendLine();
                log.AppendFormat(CultureInfo.CurrentCulture, "The first file adjusted was '{0}' and the last '{1}'.", firstFile.FileName, lastFile?.FileName);
                log.AppendLine();
                log.AppendFormat(CultureInfo.CurrentCulture, "The last file's date was changed from '{0}' to '{1}'.", DateTimeHandler.ToDisplayDateString(mostRecentOriginalDateTime), DateTimeHandler.ToDisplayDateString(mostRecentReversedDateTime));
                log.AppendLine();
                this.AppendToImageSetLog(log);
            }
        }

        // Find the next displayable file at or after the provided row in the current image set.
        // If there is no next displayable file, then find the closest previous file before the provided row that is displayable.
        public int GetCurrentOrNextDisplayableFile(int startIndex)
        {
            for (int index = startIndex; index < this.CurrentlySelectedFileCount; index++)
            {
                if (this.IsFileDisplayable(index))
                {
                    return index;
                }
            }
            for (int index = startIndex - 1; index >= 0; index--)
            {
                if (this.IsFileDisplayable(index))
                {
                    return index;
                }
            }
            return -1;
        }

        /// <summary>
        /// Find the file whose ID is closest to the provided ID in the current image set. 
        /// </summary>
        /// <returns>If the passed ID does not exist, the index of the file with and ID just greater than the provided one.  Or, if no greater ID, the index
        /// of the file with the ID.</returns>
        public int GetFileOrNextFileIndex(long fileID)
        {
            // try primary key lookup first as typically the requested ID will be present in the data table
            // (ideally the caller could use the ImageRow found directly, but this doesn't compose with index based navigation)
            if (this.Files.TryFind(fileID, out ImageRow? file, out int fileIndex))
            {
                return fileIndex;
            }

            // when sorted by ID ascending so an inexact binary search works
            // Sorting by datetime is usually identical to ID sorting in single camera image sets, so ignoring this.OrderFilesByDateTime has no effect in 
            // simple cases.  In complex, multi-camera image sets it will be wrong but typically still tend to select a plausibly reasonable file rather
            // than a ridiculous one.  But no datetime seed is available if direct ID lookup fails.  Thw API can be reworked to provide a datetime hint
            // if this proves too troublesome.
            int firstIndex = 0;
            int lastIndex = this.CurrentlySelectedFileCount - 1;
            while (firstIndex <= lastIndex)
            {
                int midpointIndex = (firstIndex + lastIndex) / 2;
                file = this.Files[midpointIndex];
                long midpointID = file.ID;

                if (fileID > midpointID)
                {
                    // look at higher index partition next
                    firstIndex = midpointIndex + 1;
                }
                else if (fileID < midpointID)
                {
                    // look at lower index partition next
                    lastIndex = midpointIndex - 1;
                }
                else
                {
                    // found the ID closest to fileID
                    return midpointIndex;
                }
            }

            // all IDs in the selection are smaller than fileID
            if (firstIndex >= this.CurrentlySelectedFileCount)
            {
                return this.CurrentlySelectedFileCount - 1;
            }

            // all IDs in the selection are larger than fileID
            return firstIndex;
        }

        public FileTable GetFilesMarkedForDeletion()
        {
            Select select = new(Constant.DatabaseTable.Files, new WhereClause(Constant.FileColumn.DeleteFlag, Constant.SqlOperator.Equal, Constant.Sql.TrueString));
            FileTable filesToDelete = new();
            filesToDelete.SetUserControls(this.Controls);
            this.LoadDataTableFromSelect(filesToDelete, select);
            return filesToDelete;
        }

        public List<string> GetDistinctValuesInFileDataColumn(string dataLabel)
        {
            List<string> distinctValues = new();
            foreach (object value in this.GetDistinctValuesInColumn(Constant.DatabaseTable.Files, dataLabel))
            {
                string? valueAsString = value.ToString();
                if (valueAsString == null)
                {
                    throw new NotSupportedException(String.Format(CultureInfo.CurrentCulture, "Value in column {0} is unexpectedly null.", dataLabel));
                }
                distinctValues.Add(valueAsString);
            }
            return distinctValues;
        }

        public List<string> GetFileColumnNames()
        {
            List<string> columns = new(this.Controls.RowCount);
            foreach (ControlRow control in this.Controls)
            {
                columns.Add(control.DataLabel);

                if (control.ControlType == ControlType.Counter)
                {
                    string markerColumn = FileTable.GetMarkerPositionColumnName(control.DataLabel);
                    columns.Add(markerColumn);
                }
            }

            return columns;
        }

        public int GetFileCount(FileSelection selection)
        {
            Select select = this.CreateSelect(selection);
            if ((select.Where.Count < 1) && (selection == FileSelection.Custom))
            {
                // if no search terms are active the file count is undefined as no filtering is in operation
                return -1;
            }

            return (int)select.Count(this.Connection);
        }

        private int GetFileCount(FileClassification classification)
        {
            Select select = new(Constant.DatabaseTable.Files);
            select.Where.Add(new WhereClause(Constant.FileColumn.Classification, Constant.SqlOperator.Equal, (int)classification));
            return (int)select.Count(this.Connection);
        }

        public Dictionary<FileClassification, int> GetFileCountsByClassification()
        {
            Dictionary<FileClassification, int> counts = new()
            {
                { FileClassification.Color, this.GetFileCount(FileClassification.Color) },
                { FileClassification.Corrupt, this.GetFileCount(FileClassification.Corrupt) },
                { FileClassification.Dark, this.GetFileCount(FileClassification.Dark) },
                { FileClassification.Greyscale, this.GetFileCount(FileClassification.Greyscale) },
                { FileClassification.NoLongerAvailable, this.GetFileCount(FileClassification.NoLongerAvailable) },
                { FileClassification.Video, this.GetFileCount(FileClassification.Video) }
            };
            return counts;
        }

        /// <summary>A convenience routine for checking to see if the file in the given row is displayable (i.e., not corrupted or missing).</summary>
        public bool IsFileDisplayable(int fileIndex)
        {
            if (this.IsFileRowInRange(fileIndex) == false)
            {
                return false;
            }

            return this.Files[fileIndex].IsDisplayable();
        }

        public bool IsFileRowInRange(int fileIndex)
        {
            return (fileIndex >= 0) && (fileIndex < this.CurrentlySelectedFileCount);
        }

        public List<string> MoveSelectedFilesToFolder(string destinationFolderPath)
        {
            Debug.Assert(destinationFolderPath.StartsWith(this.FolderPath, StringComparison.OrdinalIgnoreCase), String.Format(CultureInfo.InvariantCulture, "Destination path '{0}' is not under '{1}'.", destinationFolderPath, this.FolderPath));

            List<ImageRow> filesToUpdate = new();
            List<string> immovableFiles = new();
            foreach (ImageRow file in this.Files)
            {
                Debug.Assert(file.HasChanges == false, "File has unexpected pending changes.");
                if (file.TryMoveFileToFolder(this.FolderPath, destinationFolderPath))
                {
                    filesToUpdate.Add(file);
                }
                else
                {
                    immovableFiles.Add(file.GetRelativePath());
                }
            }

            using (UpdateFileColumnTransactionSequence updateFiles = this.CreateUpdateFileColumnTransaction(Constant.FileColumn.RelativePath))
            {
                updateFiles.UpdateFiles(filesToUpdate);
                updateFiles.Commit();
            }

            return immovableFiles;
        }

        /// <summary>
        /// Make an empty file table based on the information in the controls table.
        /// </summary>
        protected override void OnDatabaseCreated(TemplateDatabase? templateDatabase)
        {
            // copy the template's controls and image set table
            base.OnDatabaseCreated(templateDatabase);

            SQLiteTableSchema fileTableSchema = FileTable.CreateSchema(this.Controls);
            using (SQLiteTransaction transaction = this.Connection.BeginTransaction())
            {
                fileTableSchema.CreateTableAndIndicies(this.Connection, transaction);
                transaction.Commit();
            }

            // load in memory file table
            this.Files.SetUserControls(this.Controls);
            Debug.Assert(this.ImageSet != null);
            this.SelectFiles(this.ImageSet.FileSelection);
        }

        protected override bool OnExistingDatabaseOpened(TemplateDatabase? templateDatabase)
        {
            // perform Controls and ImageSet initializations and migrations, then check for synchronization issues
            if (base.OnExistingDatabaseOpened(templateDatabase) == false)
            {
                return false;
            }

            // check if any controls present in the file database were removed from the template
            List<string> fileDataLabels = this.Controls.Select(control => control.DataLabel).ToList();
            List<string> templateDataLabels = templateDatabase.Controls.Select(control => control.DataLabel).ToList();
            List<string> dataLabelsInFileButNotTemplateDatabase = fileDataLabels.Except(templateDataLabels).ToList();
            foreach (string dataLabel in dataLabelsInFileButNotTemplateDatabase)
            {
                // columns dropped from the template
                // Renames could be detected by checking for additions which match removals but this isn't currently supported.
                this.ControlSynchronizationIssues.Add("The field " + dataLabel + " is present in the file database but has been removed from the template.");
            }

            // check existing controls for compatibility
            foreach (string dataLabel in fileDataLabels)
            {
                ControlRow fileDatabaseControl = this.Controls[dataLabel];
                if (templateDatabase.Controls.TryGet(dataLabel, out ControlRow templateControl) == false)
                {
                    Debug.Assert(dataLabelsInFileButNotTemplateDatabase.Contains(dataLabel), "Controls not present in the template database should be included in the list of such controls.");
                    continue;
                }

                if (fileDatabaseControl.ControlType != templateControl.ControlType)
                {
                    // controls are potentially interchangeable if their values are compatible
                    // For example, any other control can be converted to a note and a note can be changed to a choice if the choices
                    // include all values in use.  For now, however, such conversions aren't supported.
                    this.ControlSynchronizationIssues.Add(String.Format(CultureInfo.CurrentCulture, "The field {0} is of type '{1}' in the file database but of type '{2}' in the template.", dataLabel, ControlTypeConverter.Convert(fileDatabaseControl.ControlType), ControlTypeConverter.Convert(templateControl.ControlType)));
                }

                if (fileDatabaseControl.ControlType == ControlType.FixedChoice)
                {
                    List<string> fileDatabaseChoices = fileDatabaseControl.GetWellKnownValues();
                    List<string> templateChoices = templateControl.GetWellKnownValues();
                    List<string> choicesRemovedFromTemplate = fileDatabaseChoices.Except(templateChoices).ToList();
                    if (choicesRemovedFromTemplate.Count > 0)
                    {
                        List<object> choicesInUse = this.GetDistinctValuesInColumn(Constant.DatabaseTable.Files, fileDatabaseControl.DataLabel);
                        List<string> removedChoicesInUse = new();
                        if (String.Equals(dataLabel, Constant.FileColumn.Classification, StringComparison.Ordinal))
                        {
                            List<FileClassification> classificationsInUse = new(choicesInUse.Select(choice => (FileClassification)choice));
                            foreach (string removedChoice in choicesRemovedFromTemplate)
                            {
                                if (ImageRow.TryParseFileClassification(removedChoice, out FileClassification classification))
                                {
                                    if (classificationsInUse.Contains(classification))
                                    {
                                        removedChoicesInUse.Add(ImageRow.ToString(classification));
                                    }
                                }
                            }
                        }
                        else
                        {
                            List<string> choiceStringsInUse = new(choicesInUse.Select(choice => (string)choice));
                            foreach (string removedChoice in choicesRemovedFromTemplate)
                            {
                                if (choiceStringsInUse.Contains(removedChoice, StringComparer.Ordinal))
                                {
                                    removedChoicesInUse.Add(removedChoice);
                                }
                            }
                        }

                        foreach (string removedChoice in removedChoicesInUse)
                        {
                            this.ControlSynchronizationIssues.Add(String.Format(CultureInfo.CurrentCulture, "Files have {0} set to the choice '{1}' but this value is removed from the template.", dataLabel, removedChoice));
                        }
                    }
                }
            }

            // if there are no synchronization difficulties 
            // - synchronize existing controls in the file database's Control table to those in the template's Control table
            // - add any new columns needed to the file table
            if (this.ControlSynchronizationIssues.Count == 0)
            {
                using (SQLiteTransaction transaction = this.Connection.BeginTransaction())
                {
                    // synchronize any changes in existing controls
                    List<SecondaryIndex> indicesToCreate = new();
                    List<SecondaryIndex> indicesToDrop = new();
                    using (ControlTransactionSequence synchronizeControls = ControlTransactionSequence.CreateUpdate(this, transaction))
                    {
                        foreach (string dataLabel in fileDataLabels)
                        {
                            ControlRow thisControl = this.Controls[dataLabel];
                            bool thisIndex = thisControl.IndexInFileTable;
                            ControlRow otherControl = templateDatabase.Controls[dataLabel];
                            bool otherIndex = otherControl.IndexInFileTable;

                            if (thisControl.Synchronize(otherControl))
                            {
                                synchronizeControls.AddControl(thisControl);

                                if (thisIndex != otherIndex)
                                {
                                    SecondaryIndex index = SecondaryIndex.CreateFileTableIndex(thisControl);
                                    if (otherIndex)
                                    {
                                        indicesToCreate.Add(index);
                                    }
                                    else
                                    {
                                        indicesToDrop.Add(index);
                                    }
                                }
                            }
                        }
                    }

                    // update file table to 2.2.0.3 schema
                    Version databaseVersion = this.GetUserVersion();
                    if (databaseVersion < Constant.Release.V2_2_0_3)
                    {
                        this.UpdateFileTableTo2203Schema(transaction);
                    }

                    // add any new controls to the file database's control and file tables
                    using (ControlTransactionSequence insertControls = ControlTransactionSequence.CreateInsert(this, transaction))
                    {
                        foreach (string dataLabelToAdd in templateDataLabels.Except(fileDataLabels))
                        {
                            ControlRow templateControl = templateDatabase.Controls[dataLabelToAdd];
                            insertControls.AddControl(templateControl);

                            foreach (ColumnDefinition columnToAdd in FileTable.CreateFileTableColumnDefinitions(templateControl))
                            {
                                int columnNumber = templateDataLabels.IndexOf(dataLabelToAdd);
                                if (columnNumber < 0)
                                {
                                    throw new SQLiteException(SQLiteErrorCode.Constraint, String.Format(CultureInfo.CurrentCulture, "Internal consistency failure: could not add file table column for data label '{0}' because it could not be found in the template table's control definitions.", dataLabelToAdd));
                                }
                                this.AddColumnToTable(transaction, Constant.DatabaseTable.Files, columnNumber, columnToAdd);
                            }

                            if (templateControl.IndexInFileTable)
                            {
                                indicesToCreate.Add(SecondaryIndex.CreateFileTableIndex(templateControl));
                            }
                        }
                    }

                    // update indices
                    foreach (SecondaryIndex index in indicesToCreate)
                    {
                        index.Create(this.Connection, transaction);
                    }
                    foreach (SecondaryIndex index in indicesToDrop)
                    {
                        index.Drop(this.Connection, transaction);
                    }

                    transaction.Commit();
                }

                // load the updated controls table
                this.GetControlsSortedByControlOrder();
            }

            // index user controls
            // This is needed in the normal case of no synchronization issues and also when the user chooses to run with the
            // existing controls table to avoid synchronization issues.
            this.Files.SetUserControls(this.Controls);

            // don't read files from the database into in memory file table at this point
            // For large databases this is a long running operation and a better user experience is provided if it is deferred
            // until later in Carnassial's image set opening sequence.

            // return true if there are synchronization issues as the database was still opened successfully
            return true;
        }

        public void RenameDatabaseFile(string newFileName)
        {
            // force SQLite to release its handle on the database file
            this.Connection.Dispose();
            GC.Collect();
            GC.WaitForPendingFinalizers();

            string oldBackupFilePath = this.GetBackupFilePath();

            // rename database file and update associated fields
            string newFilePath = Path.Combine(this.FolderPath, newFileName);
            File.Move(this.FilePath, newFilePath);
            this.FileName = newFileName;
            this.FilePath = newFilePath;

            // rename database backup file if one is present
            if (File.Exists(oldBackupFilePath))
            {
                string newBackupFilePath = this.GetBackupFilePath();
                File.Move(oldBackupFilePath, newBackupFilePath);
            }

            // reconnect to database file
            this.Connection = SQLiteDatabase.OpenConnection(newFilePath);
        }

        public int ReplaceAllInFiles(FileFindReplace findReplace)
        {
            using FileTransactionSequence updateFiles = this.CreateUpdateFileTransaction();
            foreach (ImageRow file in this.Files)
            {
                if (findReplace.Matches(file))
                {
                    if (findReplace.TryReplace(file))
                    {
                        updateFiles.AddFile(file);
                    }
                }
            }
            updateFiles.Commit();
            return updateFiles.RowsCommitted;
        }

        /// <summary> 
        /// Rebuild the in memory file table with all files in the database table which match the specified selection.
        /// </summary>
        // performance of    time to load 10k files
        // DataTable.Load()  326ms
        // List<ImageRow>    200ms
        public void SelectFiles(FileSelection selection)
        {
            // Stopwatch stopwatch = new Stopwatch();
            // stopwatch.Start();
            Select select = this.CreateSelect(selection);
            if (this.OrderFilesByDateTime)
            {
                select.OrderBy = Constant.FileColumn.DateTime;
            }
            this.LoadDataTableFromSelect(this.Files, select);

            // persist the current selection
            this.ImageSet.FileSelection = selection;
            // stopwatch.Stop();
            // Trace.WriteLine(stopwatch.Elapsed.ToString("s\\.fffffff", CultureInfo.CurrentCulture));
        }

        public static bool TryCreateOrOpen(string filePath, TemplateDatabase templateDatabase, bool orderFilesByDate, LogicalOperator customSelectionTermCombiningOperator, out FileDatabase fileDatabase)
        {
            // check for an existing database before instantiating the databse as SQL wrapper instantiation creates the database file
            bool populateDatabase = !File.Exists(filePath);

            fileDatabase = new FileDatabase(filePath);
            if (populateDatabase)
            {
                // initialize the database if it's newly created
                fileDatabase.OnDatabaseCreated(templateDatabase);
            }
            else
            {
                // if it's an existing database check if it needs updating to current structure and load data tables
                if (fileDatabase.OnExistingDatabaseOpened(templateDatabase) == false)
                {
                    return false;
                }
            }

            // check all tables have been loaded from the database
            Debug.Assert(fileDatabase.Controls != null, "Controls wasn't loaded.");
            Debug.Assert(fileDatabase.Files != null, "Files wasn't loaded.");
            Debug.Assert(fileDatabase.ImageSet != null, "ImageSet wasn't loaded.");

            fileDatabase.CustomSelection = new CustomSelection(fileDatabase.Controls, customSelectionTermCombiningOperator);
            fileDatabase.OrderFilesByDateTime = orderFilesByDate;

            // indicate failure if there are synchronization issues as the caller needs to determine if the database can be used anyway
            // This is different semantics from OnExistingDatabaseOpened().
            return fileDatabase.ControlSynchronizationIssues.Count == 0;
        }

        public FileImportResult TryImportData(string otherDatabasePath, DataImportProgress importStatus)
        {
            if (this.ImageSet.FileSelection != FileSelection.All)
            {
                throw new NotSupportedException(App.FindResource<string>(Constant.ResourceKey.FileDatabaseSelectionNotAll));
            }

            string? relativePathFromThisToOther = NativeMethods.GetRelativePathFromDirectoryToDirectory(Path.GetDirectoryName(this.FilePath), Path.GetDirectoryName(otherDatabasePath));
            if (String.Equals(relativePathFromThisToOther, Constant.File.CurrentDirectory, StringComparison.Ordinal))
            {
                relativePathFromThisToOther = null;
            }
            else if (relativePathFromThisToOther.IndexOf(Constant.File.ParentDirectory, StringComparison.Ordinal) != -1)
            {
                throw new NotSupportedException(String.Format(CultureInfo.CurrentCulture, "Canonicalization of relative path from database to spreadsheet '{0}' is not currently supported.", relativePathFromThisToOther));
            }

            FileImportResult result = new();
            try
            {
                using FileDatabase other = new(otherDatabasePath);
                other.GetControlsSortedByControlOrder();

                // validate file header against the database
                List<string> columnsInOther = other.GetFileColumnNames();
                List<string> columnsInThis = this.GetFileColumnNames();

                List<string> columnsInThisButNotOther = columnsInThis.Except(columnsInOther).ToList();
                foreach (string column in columnsInThisButNotOther)
                {
                    result.Errors.Add(App.FormatResource(Constant.ResourceKey.FileDatabaseImportThisColumnNotInOther, column));
                }

                List<string> columnsInOtherButNotThis = columnsInOther.Except(columnsInThis).ToList();
                foreach (string column in columnsInOtherButNotThis)
                {
                    result.Errors.Add(App.FormatResource(Constant.ResourceKey.FileDatabaseImportOtherColumnNotInThis, column));
                }

                // validate user control data indices and types
                // Requiring indices match simplifies the implementation of ImageRow.SetValue(). If needed, a map similar to the
                // spreadsheet case can be used to accommodate changes in column ordering. However, it's expected such mismatches
                // will be rare and, if they do occur, they can be worked around with spreadsheet export and import.
                other.Files.SetUserControls(other.Controls);
                other.LoadImageSet();
                other.SelectFiles(FileSelection.All);
                foreach (KeyValuePair<string, FileTableColumn> thisColumn in this.Files.UserColumnsByName)
                {
                    FileTableColumn otherColumn = other.Files.UserColumnsByName[thisColumn.Key];
                    if ((thisColumn.Value.DataIndex != otherColumn.DataIndex) ||
                        (thisColumn.Value.DataType != otherColumn.DataType))
                    {
                        result.Errors.Add(App.FormatResource(Constant.ResourceKey.FileDatabaseImportColumnLayoutMismatch, thisColumn.Key, thisColumn.Value.DataIndex, thisColumn.Value.DataType, otherColumn.DataIndex, otherColumn.DataType));
                    }
                }

                if (result.Errors.Count > 0)
                {
                    return result;
                }

                // merge files from the other database
                Dictionary<string, Dictionary<string, ImageRow>> filesAlreadyInThisByRelativePath = this.Files.GetFilesByRelativePathAndName();
                importStatus.BeginRead(other.Files.RowCount);

                FileTableColumnMap fileTableMap = new(this.Files);
                List<ImageRow> filesToInsert = new();
                List<ImageRow> filesToUpdate = new();
                int filesUnchanged = 0;
                for (int fileIndex = 0, mostRecentReportCheck = 0; fileIndex < other.Files.RowCount; ++fileIndex)
                {
                    ImageRow otherFile = other.Files[fileIndex];
                    if (String.IsNullOrWhiteSpace(otherFile.FileName))
                    {
                        result.Errors.Add(String.Format(CultureInfo.CurrentCulture, "No file name found in row {0}.  Row skipped, database will not be updated for this file.", fileIndex));
                        continue;
                    }

                    string? relativePath = otherFile.RelativePath;
                    if (relativePathFromThisToOther != null)
                    {
                        relativePath = Path.Combine(relativePathFromThisToOther, relativePath);
                    }

                    bool addFile = false;
                    if ((filesAlreadyInThisByRelativePath.TryGetValue(relativePath, out Dictionary<string, ImageRow>? filesInFolder) == false) ||
                        (filesInFolder.TryGetValue(otherFile.FileName, out ImageRow? thisFile) == false))
                    {
                        addFile = true;
                        thisFile = this.Files.CreateAndAppendFile(otherFile.FileName, relativePath);
                    }
                    Debug.Assert(addFile || (thisFile.HasChanges == false), "Existing file unexpectedly has changes.");

                    // move row data into file
                    thisFile.SetValues(otherFile, fileTableMap, result);

                    if (addFile)
                    {
                        filesToInsert.Add(thisFile);
                    }
                    else if (thisFile.HasChanges)
                    {
                        filesToUpdate.Add(thisFile);
                    }
                    else
                    {
                        ++filesUnchanged;
                    }

                    if (fileIndex - mostRecentReportCheck > Constant.File.RowsBetweenStatusReportChecks)
                    {
                        if (importStatus.ShouldUpdateProgress())
                        {
                            importStatus.QueueProgressUpdate(fileIndex);
                        }
                        mostRecentReportCheck = fileIndex;
                    }
                }

                // perform inserts and updates
                int totalFiles = filesToInsert.Count + filesToUpdate.Count;
                importStatus.BeginTransactionCommit(totalFiles);
                if (filesToInsert.Count > 0)
                {
                    using (FileTransactionSequence insertFiles = this.CreateInsertFileTransaction())
                    {
                        insertFiles.AddFiles(filesToInsert);
                        insertFiles.Commit();
                    }
                    importStatus.QueueProgressUpdate(filesToInsert.Count);
                }
                if (filesToUpdate.Count > 0)
                {
                    using (FileTransactionSequence updateFiles = this.CreateUpdateFileTransaction())
                    {
                        updateFiles.AddFiles(filesToUpdate);
                        updateFiles.Commit();
                    }
                    importStatus.QueueProgressUpdate(totalFiles);
                }

                result.FilesAdded = filesToInsert.Count;
                result.FilesProcessed = filesToInsert.Count + filesToUpdate.Count + filesUnchanged;
                result.FilesUpdated = filesToUpdate.Count;
            }
            catch (IOException ioException)
            {
                result.Exception = ioException;
            }
            finally
            {
                importStatus.End();
            }

            return result;
        }

        public bool TrySyncFileToDatabase(ImageRow file)
        {
            if (file.HasChanges == false)
            {
                return false;
            }

            using (FileTransactionSequence updateFile = this.CreateUpdateFileTransaction())
            {
                updateFile.AddFile(file);
                updateFile.Commit();
            }
            return true;
        }

        // set one property on all rows in the selection to a given value
        public int UpdateFiles(ImageRow valueSource, DataEntryControl control)
        {
            return this.UpdateFiles(valueSource, control, 0, this.CurrentlySelectedFileCount - 1);
        }

        public int UpdateFiles(ImageRow valueSource, DataEntryControl control, int fromIndex, int toIndex)
        {
            if (fromIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(fromIndex));
            }
            if ((toIndex < fromIndex) || (toIndex > this.CurrentlySelectedFileCount - 1))
            {
                throw new ArgumentOutOfRangeException(nameof(toIndex));
            }

            object? value = valueSource.GetDatabaseValue(control.DataLabel);
            List<ImageRow> filesToUpdate = new(toIndex - fromIndex + 1);
            for (int index = fromIndex; index <= toIndex; index++)
            {
                // update data table
                ImageRow file = this.Files[index];
                Debug.Assert(file.HasChanges == false, "File has unexpected pending changes.");
                file[control.PropertyName] = value;
                // capture change for database update
                filesToUpdate.Add(file);
            }

            using UpdateFileColumnTransactionSequence updateFiles = this.CreateUpdateFileColumnTransaction(control.DataLabel);
            updateFiles.UpdateFiles(filesToUpdate);
            updateFiles.Commit();
            return updateFiles.RowsCommitted;
        }

        private void UpdateFileTableTo2203Schema(SQLiteTransaction transaction)
        {
            // rename FileData table to Files
            #pragma warning disable CS0618 // Type or member is obsolete
            this.RenameTable(transaction, Constant.DatabaseTable.FileData, Constant.DatabaseTable.Files);
            #pragma warning restore CS0618 // Type or member is obsolete

            // convert string ImageQuality column (2.2.0.2 schema) to integer Classification column (2.2.0.3 schema)
            SQLiteTableSchema currentFileSchema = this.GetTableSchema(Constant.DatabaseTable.Files);
            #pragma warning disable CS0618 // Type or member is obsolete
            if (currentFileSchema.ColumnDefinitions.SingleOrDefault(column => String.Equals(column.Name, Constant.FileColumn.ImageQuality, StringComparison.Ordinal)) != null)
            {
                this.RenameColumn(transaction, Constant.DatabaseTable.Files, Constant.FileColumn.ImageQuality, Constant.FileColumn.Classification, (ColumnDefinition newColumnDefinition) =>
                {
                    newColumnDefinition.DefaultValue = ((int)default(FileClassification)).ToString(Constant.InvariantCulture);
                    newColumnDefinition.NotNull = true;
                });
                #pragma warning restore CS0618 // Type or member is obsolete
                this.ConvertNonFlagEnumStringColumnToInteger<FileClassification>(transaction, Constant.DatabaseTable.Files, Constant.FileColumn.Classification);
            }

            // convert flag controls from text to integer columns
            foreach (ControlRow control in this.Controls.Where(control => control.ControlType == ControlType.Flag))
            {
                ColumnDefinition currentColumn = currentFileSchema.ColumnDefinitions.Single(column => String.Equals(column.Name, control.DataLabel, StringComparison.Ordinal));
                if (String.Equals(currentColumn.Type, Constant.SQLiteAffinity.Integer, StringComparison.OrdinalIgnoreCase) == false)
                {
                    this.ConvertBooleanStringColumnToInteger(transaction, Constant.DatabaseTable.Files, control.DataLabel);
                }
            }

            this.SetUserVersion(transaction, Constant.Release.V2_2_0_3);
        }
    }
}
