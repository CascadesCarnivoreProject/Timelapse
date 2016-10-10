﻿using Carnassial.Images;
using Carnassial.Util;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Controls;

namespace Carnassial.Database
{
    public class FileDatabase : TemplateDatabase
    {
        private DataGrid boundDataGrid;
        private bool disposed;
        private DataRowChangeEventHandler onFileDataTableRowChanged;

        public CustomSelection CustomSelection { get; private set; }

        /// <summary>Gets the file name of the database on disk.</summary>
        public string FileName { get; private set; }

        /// <summary>Gets the complete path to the folder containing the database.</summary>
        public string FolderPath { get; private set; }

        public Dictionary<string, string> DataLabelFromStandardControlType { get; private set; }

        public Dictionary<string, FileTableColumn> FileTableColumnsByDataLabel { get; private set; }

        // contains the results of the data query
        public FileTable Files { get; private set; }

        public ImageSetRow ImageSet { get; private set; }

        // contains the markers
        public DataTableBackedList<MarkerRow> Markers { get; private set; }

        public bool OrderFilesByDateTime { get; set; }

        public List<string> TemplateSynchronizationIssues { get; private set; }

        private FileDatabase(string filePath)
            : base(filePath)
        {
            this.DataLabelFromStandardControlType = new Dictionary<string, string>();
            this.disposed = false;
            this.FolderPath = Path.GetDirectoryName(filePath);
            this.FileName = Path.GetFileName(filePath);
            this.FileTableColumnsByDataLabel = new Dictionary<string, FileTableColumn>();
            this.OrderFilesByDateTime = false;
            this.TemplateSynchronizationIssues = new List<string>();
        }

        public static FileDatabase CreateOrOpen(string filePath, TemplateDatabase templateDatabase, bool orderFilesByDate, CustomSelectionOperator customSelectionTermCombiningOperator)
        {
            // check for an existing database before instantiating the databse as SQL wrapper instantiation creates the database file
            bool populateDatabase = !File.Exists(filePath);

            FileDatabase fileDatabase = new FileDatabase(filePath);
            if (populateDatabase)
            {
                // initialize the database if it's newly created
                fileDatabase.OnDatabaseCreated(templateDatabase);
            }
            else
            {
                // if it's an existing database check if it needs updating to current structure and load data tables
                fileDatabase.OnExistingDatabaseOpened(templateDatabase);
            }

            // ensure all tables have been loaded from the database
            if (fileDatabase.ImageSet == null)
            {
                fileDatabase.GetImageSet();
            }
            fileDatabase.GetMarkers();

            fileDatabase.CustomSelection = new CustomSelection(fileDatabase.Controls, customSelectionTermCombiningOperator);
            fileDatabase.OrderFilesByDateTime = orderFilesByDate;
            fileDatabase.PopulateDataLabelMaps();
            return fileDatabase;
        }

        /// <summary>Gets the number of files currently in the files table.</summary>
        public int CurrentlySelectedFileCount
        {
            get { return this.Files.RowCount; }
        }

        [SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1126:PrefixCallsCorrectly", Justification = "StyleCop bug.")]
        public void AddFiles(List<ImageRow> files, Action<ImageRow, int> onFileAdded)
        {
            // setup
            string lastUserDefinedCounterDataLabel = null;
            Dictionary<string, string> defaultValuesByDataLabel = new Dictionary<string, string>();
            foreach (KeyValuePair<string, FileTableColumn> column in this.FileTableColumnsByDataLabel)
            {
                // skip standard controls
                string dataLabel = column.Key;
                if ((dataLabel == Constant.DatabaseColumn.ID) ||
                    Constant.Control.StandardTypes.Contains(dataLabel))
                {
                    // don't specify ID in the insert statement as it's an autoincrement primary key
                    // don't generate tuples for standard controls as the call to GetColumnTuples() above does that
                    continue;
                }

                defaultValuesByDataLabel.Add(dataLabel, this.FindControl(dataLabel).DefaultValue);
                if (column.Value.ControlType == Constant.Control.Counter)
                {
                    lastUserDefinedCounterDataLabel = dataLabel;
                }
            }

            // add files to database in chunks
            for (int fileIndex = 0; fileIndex < files.Count; fileIndex += Constant.Database.RowsPerInsert)
            {
                List<List<ColumnTuple>> filesToInsert = new List<List<ColumnTuple>>();
                List<List<ColumnTuple>> markersToInsert = new List<List<ColumnTuple>>();
                for (int insertIndex = fileIndex; (insertIndex < (fileIndex + Constant.Database.RowsPerInsert)) && (insertIndex < files.Count); ++insertIndex)
                {
                    // get tuples for standard controls
                    ImageRow file = files[insertIndex];
                    ColumnTuplesWithWhere fileTuples = file.GetColumnTuples();

                    // get tuples for user defined controls and markers
                    foreach (KeyValuePair<string, FileTableColumn> column in this.FileTableColumnsByDataLabel)
                    {
                        // skip standard controls
                        string dataLabel = column.Key;
                        if ((dataLabel == Constant.DatabaseColumn.ID) ||
                            Constant.Control.StandardTypes.Contains(dataLabel))
                        {
                            // don't specify ID in the insert statement as it's an autoincrement primary key
                            // don't generate tuples for standard controls as the call to GetColumnTuples() above does that
                            continue;
                        }

                        // set file's value for this column to the default
                        fileTuples.Columns.Add(new ColumnTuple(dataLabel, defaultValuesByDataLabel[dataLabel]));
                    }

                    filesToInsert.Add(fileTuples.Columns);
                    if (lastUserDefinedCounterDataLabel != null)
                    {
                        // default marker column associated with counter to empty
                        // This is redundant with the default value on the column but, within Carnassial's current SQL infrastructure, at least
                        // one tuple is needed to trigger creation of the row.
                        markersToInsert.Add(new List<ColumnTuple>() { new ColumnTuple(lastUserDefinedCounterDataLabel, String.Empty) });
                    }
                }

                this.CreateBackupIfNeeded();
                this.Database.Insert(Constant.DatabaseTable.FileData, filesToInsert);
                if (markersToInsert.Count > 0)
                {
                    this.Database.Insert(Constant.DatabaseTable.Markers, markersToInsert);
                }

                if (onFileAdded != null)
                {
                    int lastImageInserted = Math.Min(files.Count - 1, fileIndex + Constant.Database.RowsPerInsert);
                    onFileAdded.Invoke(files[lastImageInserted], lastImageInserted);
                }
            }

            // Load the marker table from the database - Doing so here will make sure that there is one row for each image.
            this.GetMarkers();
        }

        public void AppendToImageSetLog(StringBuilder logEntry)
        {
            this.ImageSet.Log += logEntry;
            this.SyncImageSetToDatabase();
        }

        public void BindToDataGrid(DataGrid dataGrid, DataRowChangeEventHandler onRowChanged)
        {
            this.boundDataGrid = dataGrid;
            this.onFileDataTableRowChanged = onRowChanged;
            this.Files.BindDataGrid(dataGrid, onRowChanged);
        }

        /// <summary>
        /// Make an empty Data Table based on the information in the Template Table.
        /// Assumes that the database has already been opened and that the Template Table is loaded, where the DataLabel always has a valid value.
        /// Then create both the ImageSet table and the Markers table
        /// </summary>
        protected override void OnDatabaseCreated(TemplateDatabase templateDatabase)
        {
            // copy the template's controls table
            base.OnDatabaseCreated(templateDatabase);

            // create FileData from the controls
            List<ColumnDefinition> columnDefinitions = new List<ColumnDefinition>();
            columnDefinitions.Add(new ColumnDefinition(Constant.DatabaseColumn.ID, Constant.Sql.CreationStringPrimaryKey));  // It begins with the ID integer primary key
            foreach (ControlRow control in this.Controls)
            {
                columnDefinitions.Add(this.CreateFileDataColumnDefinition(control));
            }
            this.Database.CreateTable(Constant.DatabaseTable.FileData, columnDefinitions);

            // index the DateTime column
            this.Database.ExecuteNonQuery("CREATE INDEX 'FileDateTimeIndex' ON 'FileData' ('DateTime')");

            // initialize Files
            // this is necessary as images can't be added unless Files.Columns is available
            // can't use TryGetImagesAll() here as that function's contract is not to update ImageDataTable if the select against the underlying database table 
            // finds no rows, which is the case for a database being created
            this.SelectFiles(FileSelection.All);

            // Create ImageSet table with a singleton row
            columnDefinitions.Clear();
            columnDefinitions.Add(new ColumnDefinition(Constant.DatabaseColumn.ID, Constant.Sql.CreationStringPrimaryKey));  // It begins with the ID integer primary key
            columnDefinitions.Add(new ColumnDefinition(Constant.DatabaseColumn.InitialFolderName, Constant.Sql.Text));
            columnDefinitions.Add(new ColumnDefinition(Constant.DatabaseColumn.Log, Constant.Sql.Text));
            columnDefinitions.Add(new ColumnDefinition(Constant.DatabaseColumn.Magnifier, Constant.Sql.Text));
            columnDefinitions.Add(new ColumnDefinition(Constant.DatabaseColumn.MostRecentFileID, Constant.Sql.Integer));
            columnDefinitions.Add(new ColumnDefinition(Constant.DatabaseColumn.FileSelection, Constant.Sql.Text));
            columnDefinitions.Add(new ColumnDefinition(Constant.DatabaseColumn.TimeZone, Constant.Sql.Text));
            this.Database.CreateTable(Constant.DatabaseTable.ImageSet, columnDefinitions);

            List<ColumnTuple> columnsToUpdate = new List<ColumnTuple>();
            columnsToUpdate.Add(new ColumnTuple(Constant.DatabaseColumn.InitialFolderName, Path.GetFileName(this.FolderPath)));
            columnsToUpdate.Add(new ColumnTuple(Constant.DatabaseColumn.Log, Constant.Database.ImageSetDefaultLog));
            columnsToUpdate.Add(new ColumnTuple(Constant.DatabaseColumn.Magnifier, Boolean.FalseString));
            columnsToUpdate.Add(new ColumnTuple(Constant.DatabaseColumn.MostRecentFileID, Constant.Database.DefaultFileID));
            columnsToUpdate.Add(new ColumnTuple(Constant.DatabaseColumn.FileSelection, FileSelection.All.ToString()));
            columnsToUpdate.Add(new ColumnTuple(Constant.DatabaseColumn.TimeZone, TimeZoneInfo.Local.Id));
            List<List<ColumnTuple>> insertionStatements = new List<List<ColumnTuple>>();
            insertionStatements.Add(columnsToUpdate);
            this.Database.Insert(Constant.DatabaseTable.ImageSet, insertionStatements);

            // Create the Markers table and initialize it from the controls
            columnDefinitions.Clear();
            columnDefinitions.Add(new ColumnDefinition(Constant.DatabaseColumn.ID, Constant.Sql.CreationStringPrimaryKey));
            string type = String.Empty;
            foreach (ControlRow control in this.Controls)
            {
                if (control.Type.Equals(Constant.Control.Counter))
                {
                    columnDefinitions.Add(new ColumnDefinition(control.DataLabel, Constant.Sql.Text, String.Empty));
                }
            }
            this.Database.CreateTable(Constant.DatabaseTable.Markers, columnDefinitions);
        }

        protected override void OnExistingDatabaseOpened(TemplateDatabase templateDatabase)
        {
            // perform TemplateTable initializations and migrations, then check for synchronization issues
            base.OnExistingDatabaseOpened(templateDatabase);

            List<string> templateDataLabels = templateDatabase.GetDataLabelsExceptIDInSpreadsheetOrder();
            List<string> dataLabels = this.GetDataLabelsExceptIDInSpreadsheetOrder();
            List<string> dataLabelsInTemplateButNotFileDatabase = templateDataLabels.Except(dataLabels).ToList();
            foreach (string dataLabel in dataLabelsInTemplateButNotFileDatabase)
            {
                this.TemplateSynchronizationIssues.Add("- A field with the DataLabel '" + dataLabel + "' was found in the template, but nothing matches that in the file database." + Environment.NewLine);
            }
            List<string> dataLabelsInIFileButNotTemplateDatabase = dataLabels.Except(templateDataLabels).ToList();
            foreach (string dataLabel in dataLabelsInIFileButNotTemplateDatabase)
            {
                this.TemplateSynchronizationIssues.Add("- A field with the DataLabel '" + dataLabel + "' was found in the file database, but nothing matches that in the template." + Environment.NewLine);
            }

            if (this.TemplateSynchronizationIssues.Count == 0)
            {
                foreach (string dataLabel in dataLabels)
                {
                    ControlRow fileDatabaseControl = this.FindControl(dataLabel);
                    ControlRow templateControl = templateDatabase.FindControl(dataLabel);

                    if (fileDatabaseControl.Type != templateControl.Type)
                    {
                        this.TemplateSynchronizationIssues.Add(String.Format("- The field with DataLabel '{0}' is of type '{1}' in the image data file but of type '{2}' in the template.{3}", dataLabel, fileDatabaseControl.Type, templateControl.Type, Environment.NewLine));
                    }

                    List<string> fileDatabaseChoices = fileDatabaseControl.GetChoices();
                    List<string> templateChoices = templateControl.GetChoices();
                    List<string> choiceValuesRemovedInTemplate = fileDatabaseChoices.Except(templateChoices).ToList();
                    foreach (string removedValue in choiceValuesRemovedInTemplate)
                    {
                        this.TemplateSynchronizationIssues.Add(String.Format("- The choice with DataLabel '{0}' allows the value of '{1}' in the image data file but not in the template.{2}", dataLabel, removedValue, Environment.NewLine));
                    }
                }
            }

            // if there are no synchronization difficulties synchronize the image database's TemplateTable with the template's TemplateTable          
            if (this.TemplateSynchronizationIssues.Count == 0)
            {
                foreach (string dataLabel in dataLabels)
                {
                    ControlRow fileDatabaseControl = this.FindControl(dataLabel);
                    ControlRow templateControl = templateDatabase.FindControl(dataLabel);
                    if (fileDatabaseControl.Synchronize(templateControl))
                    {
                        this.SyncControlToDatabase(fileDatabaseControl);
                    }
                }
            }
        }

        /// <summary>
        /// Create lookup tables that allow us to retrieve a key from a type and vice versa
        /// </summary>
        private void PopulateDataLabelMaps()
        {
            foreach (ControlRow control in this.Controls)
            {
                FileTableColumn column = FileTableColumn.Create(control);
                this.FileTableColumnsByDataLabel.Add(column.DataLabel, column);

                // don't type map user defined controls as if there are multiple ones the key would not be unique
                if (Constant.Control.StandardTypes.Contains(column.ControlType))
                {
                    this.DataLabelFromStandardControlType.Add(column.ControlType, column.DataLabel);
                }
            }
        }

        public void RenameFile(string newFileName)
        {
            if (File.Exists(Path.Combine(this.FolderPath, this.FileName)))
            {
                File.Move(Path.Combine(this.FolderPath, this.FileName),
                          Path.Combine(this.FolderPath, newFileName));  // Change the file name to the new file name
                this.FileName = newFileName; // Store the file name
                this.Database = new SQLiteWrapper(Path.Combine(this.FolderPath, newFileName));          // Recreate the database connecction
            }
        }

        private ImageRow GetFile(string where)
        {
            if (String.IsNullOrWhiteSpace(where))
            {
                throw new ArgumentOutOfRangeException("where");
            }

            string query = "Select * FROM " + Constant.DatabaseTable.FileData + " WHERE " + where;
            DataTable images = this.Database.GetDataTableFromSelect(query);
            FileTable temporaryTable = new FileTable(images);
            if (temporaryTable.RowCount != 1)
            {
                return null;
            }
            return temporaryTable[0];
        }

        /// <summary> 
        /// Populate the image table so that it matches all the entries in its associated database table.
        /// Then set the currentID and currentRow to the the first record in the returned set
        /// </summary>
        public void SelectFiles(FileSelection selection)
        {
            string query = "SELECT * FROM " + Constant.DatabaseTable.FileData;
            string where = this.GetFilesWhere(selection);
            if (String.IsNullOrEmpty(where) == false)
            {
                query += Constant.Sql.Where + where;
            }
            if (this.OrderFilesByDateTime)
            {
                query += " ORDER BY " + Constant.DatabaseColumn.DateTime;
            }

            DataTable images = this.Database.GetDataTableFromSelect(query);
            this.Files = new FileTable(images);
            this.Files.BindDataGrid(this.boundDataGrid, this.onFileDataTableRowChanged);
        }

        public FileTable GetFilesMarkedForDeletion()
        {
            string where = this.DataLabelFromStandardControlType[Constant.DatabaseColumn.DeleteFlag] + "=\"true\""; // = value
            string query = "Select * FROM " + Constant.DatabaseTable.FileData + " WHERE " + where;
            DataTable images = this.Database.GetDataTableFromSelect(query);
            return new FileTable(images);
        }

        /// <summary>
        /// Get the row matching the specified image or create a new image.  The caller is responsible to add newly created images the database and data table.
        /// </summary>
        /// <returns>true if the image is already in the database</returns>
        public bool GetOrCreateFile(FileInfo fileInfo, TimeZoneInfo imageSetTimeZone, out ImageRow file)
        {
            // GetRelativePath() includes the image's file name; remove that from the relative path as it's stored separately
            // GetDirectoryName() returns String.Empty if there's no relative path; the SQL layer treats this inconsistently, resulting in 
            // DataRows returning with RelativePath = String.Empty even if null is passed despite setting String.Empty as a column default
            // resulting in RelativePath = null.  As a result, String.IsNullOrEmpty() is the appropriate test for lack of a RelativePath.
            string relativePath = NativeMethods.GetRelativePath(this.FolderPath, fileInfo.FullName);
            relativePath = Path.GetDirectoryName(relativePath);

            ColumnTuplesWithWhere imageQuery = new ColumnTuplesWithWhere();
            imageQuery.SetWhere(relativePath, fileInfo.Name);
            file = this.GetFile(imageQuery.Where);

            if (file != null)
            {
                return true;
            }
            else
            {
                file = this.Files.NewRow(fileInfo);
                file.RelativePath = relativePath;
                file.SetDateTimeOffsetFromFileInfo(this.FolderPath, imageSetTimeZone);
                return false;
            }
        }

        public Dictionary<FileSelection, int> GetFileCountsBySelection()
        {
            Dictionary<FileSelection, int> counts = new Dictionary<FileSelection, int>(4);
            counts[FileSelection.Dark] = this.GetFileCount(FileSelection.Dark);
            counts[FileSelection.Corrupt] = this.GetFileCount(FileSelection.Corrupt);
            counts[FileSelection.NoLongerAvailable] = this.GetFileCount(FileSelection.NoLongerAvailable);
            counts[FileSelection.Ok] = this.GetFileCount(FileSelection.Ok);
            return counts;
        }

        public int GetFileCount(FileSelection fileSelection)
        {
            string query = "Select Count(*) FROM " + Constant.DatabaseTable.FileData;
            string where = this.GetFilesWhere(fileSelection);
            if (String.IsNullOrEmpty(where))
            {
                if (fileSelection == FileSelection.Custom)
                {
                    // if no search terms are active the image count is undefined as no filtering is in operation
                    return -1;
                }
                // otherwise, the query is for all images as no where clause is present
            }
            else
            {
                query += Constant.Sql.Where + where;
            }

            return this.Database.GetCountFromSelect(query);
        }

        private string GetFilesWhere(FileSelection selection)
        {
            switch (selection)
            {
                case FileSelection.All:
                    return String.Empty;
                case FileSelection.Corrupt:
                case FileSelection.Dark:
                case FileSelection.NoLongerAvailable:
                case FileSelection.Ok:
                    return this.DataLabelFromStandardControlType[Constant.DatabaseColumn.ImageQuality] + "=\"" + selection + "\"";
                case FileSelection.MarkedForDeletion:
                    return this.DataLabelFromStandardControlType[Constant.DatabaseColumn.DeleteFlag] + "=\"true\"";
                case FileSelection.Custom:
                    return this.CustomSelection.GetFilesWhere();
                default:
                    throw new NotSupportedException(String.Format("Unhandled selection {0}.", selection));
            }
        }

        /// <summary>
        /// Update a column value (identified by its key) in an existing row (identified by its ID) 
        /// By default, if the table parameter is not included, we use the TABLEDATA table
        /// </summary>
        public void UpdateFile(long fileID, string dataLabel, string value)
        {
            // update the data table
            ImageRow file = this.Files.Find(fileID);
            file.SetValueFromDatabaseString(dataLabel, value);

            // update the row in the database
            this.CreateBackupIfNeeded();

            ColumnTuplesWithWhere columnToUpdate = new ColumnTuplesWithWhere();
            columnToUpdate.Columns.Add(new ColumnTuple(dataLabel, value)); // Populate the data 
            columnToUpdate.SetWhere(fileID);
            this.Database.Update(Constant.DatabaseTable.FileData, columnToUpdate);
        }

        // Set one property on all rows in the selection to a given value
        public void UpdateFiles(ImageRow valueSource, string dataLabel)
        {
            this.UpdateFiles(valueSource, dataLabel, 0, this.CurrentlySelectedFileCount - 1);
        }

        public void UpdateFiles(List<ColumnTuplesWithWhere> imagesToUpdate)
        {
            this.CreateBackupIfNeeded();
            this.Database.Update(Constant.DatabaseTable.FileData, imagesToUpdate);
        }

        public void UpdateFiles(ImageRow valueSource, string dataLabel, int fromIndex, int toIndex)
        {
            if (fromIndex < 0)
            {
                throw new ArgumentOutOfRangeException("fromIndex");
            }
            if (toIndex < fromIndex || toIndex > this.CurrentlySelectedFileCount - 1)
            {
                throw new ArgumentOutOfRangeException("toIndex");
            }

            string value = valueSource.GetValueDatabaseString(dataLabel);
            List<ColumnTuplesWithWhere> imagesToUpdate = new List<ColumnTuplesWithWhere>();
            for (int index = fromIndex; index <= toIndex; index++)
            {
                // update data table
                ImageRow image = this.Files[index];
                image.SetValueFromDatabaseString(dataLabel, value);

                // update database
                List<ColumnTuple> columnToUpdate = new List<ColumnTuple>() { new ColumnTuple(dataLabel, value) };
                ColumnTuplesWithWhere imageUpdate = new ColumnTuplesWithWhere(columnToUpdate, image.ID);
                imagesToUpdate.Add(imageUpdate);
            }

            this.CreateBackupIfNeeded();
            this.Database.Update(Constant.DatabaseTable.FileData, imagesToUpdate);
        }

        public void AdjustFileTimes(TimeSpan adjustment)
        {
            this.AdjustFileDateTimes(adjustment, 0, this.CurrentlySelectedFileCount - 1);
        }

        public void AdjustFileDateTimes(TimeSpan adjustment, int startRow, int endRow)
        {
            if (adjustment.Milliseconds != 0)
            {
                throw new ArgumentOutOfRangeException("adjustment", "The current format of the time column does not support milliseconds.");
            }
            this.AdjustFileTimes((DateTimeOffset imageTime) => { return imageTime + adjustment; }, startRow, endRow);
        }

        // Given a time difference in ticks, update all the date/time field in the database
        // Note that it does NOT update the dataTable - this has to be done outside of this routine by regenerating the datatables with whatever selection is being used.
        public void AdjustFileTimes(Func<DateTimeOffset, DateTimeOffset> adjustment, int startRow, int endRow)
        {
            if (this.IsFileRowInRange(startRow) == false)
            {
                throw new ArgumentOutOfRangeException("startRow");
            }
            if (this.IsFileRowInRange(endRow) == false)
            {
                throw new ArgumentOutOfRangeException("endRow");
            }
            if (endRow < startRow)
            {
                throw new ArgumentOutOfRangeException("endRow", "endRow must be greater than or equal to startRow.");
            }
            if (this.CurrentlySelectedFileCount == 0)
            {
                return;
            }

            // We now have an unfiltered temporary data table
            // Get the original value of each, and update each date by the corrected amount if possible
            List<ImageRow> imagesToAdjust = new List<ImageRow>();
            TimeSpan mostRecentAdjustment = TimeSpan.Zero;
            for (int row = startRow; row <= endRow; ++row)
            { 
                ImageRow image = this.Files[row];
                DateTimeOffset currentImageDateTime = image.GetDateTime();

                // adjust the date/time
                DateTimeOffset newImageDateTime = adjustment.Invoke(currentImageDateTime);
                if (newImageDateTime == currentImageDateTime)
                {
                    continue;
                }
                mostRecentAdjustment = newImageDateTime - currentImageDateTime;
                image.SetDateTimeOffset(newImageDateTime);
                imagesToAdjust.Add(image);
            }

            // update the database with the new date/time values
            List<ColumnTuplesWithWhere> imagesToUpdate = new List<ColumnTuplesWithWhere>();
            foreach (ImageRow image in imagesToAdjust)
            {
                imagesToUpdate.Add(image.GetDateTimeColumnTuples());
            }

            if (imagesToUpdate.Count > 0)
            {
                this.CreateBackupIfNeeded();
                this.Database.Update(Constant.DatabaseTable.FileData, imagesToUpdate);

                // Add an entry into the log detailing what we just did
                StringBuilder log = new StringBuilder(Environment.NewLine);
                log.AppendFormat("System entry: Adjusted dates and times of {0} selected files.{1}", imagesToAdjust.Count, Environment.NewLine);
                log.AppendFormat("The first file adjusted was '{0}', the last '{1}', and the last file was adjusted by {2}.{3}", imagesToAdjust[0].FileName, imagesToAdjust[imagesToAdjust.Count - 1].FileName, mostRecentAdjustment, Environment.NewLine);
                this.AppendToImageSetLog(log);
            }
        }

        // Update all the date fields by swapping the days and months.
        // This should ONLY be called if such swapping across all dates (excepting corrupt ones) is possible
        // as otherwise it will only swap those dates it can
        // It also assumes that the data table is showing All images
        public void ExchangeDayAndMonthInFileDates()
        {
            this.ExchangeDayAndMonthInFileDates(0, this.CurrentlySelectedFileCount - 1);
        }

        // Update all the date fields between the start and end index by swapping the days and months.
        public void ExchangeDayAndMonthInFileDates(int startRow, int endRow)
        {
            if (this.IsFileRowInRange(startRow) == false)
            {
                throw new ArgumentOutOfRangeException("startRow");
            }
            if (this.IsFileRowInRange(endRow) == false)
            {
                throw new ArgumentOutOfRangeException("endRow");
            }
            if (endRow < startRow)
            {
                throw new ArgumentOutOfRangeException("endRow", "endRow must be greater than or equal to startRow.");
            }
            if (this.CurrentlySelectedFileCount == 0)
            {
                return;
            }

            // Get the original date value of each. If we can swap the date order, do so. 
            List<ColumnTuplesWithWhere> imagesToUpdate = new List<ColumnTuplesWithWhere>();
            ImageRow firstImage = this.Files[startRow];
            ImageRow lastImage = null;
            DateTimeOffset mostRecentOriginalDateTime = DateTime.MinValue;
            DateTimeOffset mostRecentReversedDateTime = DateTime.MinValue;
            for (int row = startRow; row <= endRow; row++)
            {
                ImageRow image = this.Files[row];
                DateTimeOffset originalDateTime = image.GetDateTime();
                DateTimeOffset reversedDateTime;
                if (DateTimeHandler.TrySwapDayMonth(originalDateTime, out reversedDateTime) == false)
                {
                    continue;
                }

                // Now update the actual database with the new date/time values stored in the temporary table
                image.SetDateTimeOffset(reversedDateTime);
                imagesToUpdate.Add(image.GetDateTimeColumnTuples());
                lastImage = image;
                mostRecentOriginalDateTime = originalDateTime;
                mostRecentReversedDateTime = reversedDateTime;
            }

            if (imagesToUpdate.Count > 0)
            {
                this.CreateBackupIfNeeded();
                this.Database.Update(Constant.DatabaseTable.FileData, imagesToUpdate);

                StringBuilder log = new StringBuilder(Environment.NewLine);
                log.AppendFormat("System entry: Swapped days and months for {0} files.{1}", imagesToUpdate.Count, Environment.NewLine);
                log.AppendFormat("The first file adjusted was '{0}' and the last '{1}'.{2}", firstImage.FileName, lastImage.FileName, Environment.NewLine);
                log.AppendFormat("The last file's date was changed from '{0}' to '{1}'.{2}", DateTimeHandler.ToDisplayDateString(mostRecentOriginalDateTime), DateTimeHandler.ToDisplayDateString(mostRecentReversedDateTime), Environment.NewLine);
                this.AppendToImageSetLog(log);
            }
        }

        // Delete the data (including markers) associated with the files identified by the list of IDs.
        public void DeleteFilesAndMarkers(List<long> fileIDs)
        {
            if (fileIDs.Count < 1)
            {
                // nothing to do
                return;
            }

            List<string> idClauses = new List<string>();
            foreach (long fileID in fileIDs)
            {
                idClauses.Add(Constant.DatabaseColumn.ID + " = " + fileID.ToString());
            }

            // Delete the data and markers associated with that image
            this.CreateBackupIfNeeded();
            this.Database.Delete(Constant.DatabaseTable.FileData, idClauses);
            this.Database.Delete(Constant.DatabaseTable.Markers, idClauses);
        }

        /// <summary>A convenience routine for checking to see if the image in the given row is displayable (i.e., not corrupted or missing)</summary>
        public bool IsFileDisplayable(int rowIndex)
        {
            if (this.IsFileRowInRange(rowIndex) == false)
            {
                return false;
            }

            return this.Files[rowIndex].IsDisplayable();
        }

        public bool IsFileRowInRange(int imageRowIndex)
        {
            return (imageRowIndex >= 0) && (imageRowIndex < this.CurrentlySelectedFileCount) ? true : false;
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

        // Find the file whose ID is closest to the provided ID in the current image set
        // If the ID does not exist, then return the file whose ID is just greater than the provided one. 
        // However, if there is no greater ID (i.e., we are at the end) return the last row. 
        public int GetFileOrNextFileIndex(long fileID)
        {
            // try primary key lookup first as typically the requested ID will be present in the data table
            // (ideally the caller could use the ImageRow found directly, but this doesn't compose with index based navigation)
            ImageRow file = this.Files.Find(fileID);
            if (file != null)
            {
                return this.Files.IndexOf(file);
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

        public List<string> GetDistinctValuesInFileDataColumn(string dataLabel)
        {
            List<string> distinctValues = new List<string>();
            foreach (object value in this.Database.GetDistinctValuesInColumn(Constant.DatabaseTable.FileData, dataLabel))
            {
                distinctValues.Add(value.ToString());
            }
            return distinctValues;
        }

        private void GetImageSet()
        {
            string imageSetQuery = "Select * From " + Constant.DatabaseTable.ImageSet + " WHERE " + Constant.DatabaseColumn.ID + " = " + Constant.Database.ImageSetRowID.ToString();
            DataTable imageSetTable = this.Database.GetDataTableFromSelect(imageSetQuery);
            this.ImageSet = new ImageSetRow(imageSetTable.Rows[0]);
        }

        /// <summary>
        /// Get all markers for the specified file.
        /// </summary>
        /// <returns>list of counters having an entry for each counter even if there are no markers </returns>
        public List<MarkersForCounter> GetMarkersOnFile(long fileID)
        {
            List<MarkersForCounter> markersForAllCounters = new List<MarkersForCounter>();
            MarkerRow markersForImage = this.Markers.Find(fileID);
            if (markersForImage == null)
            {
                // if no counter controls are defined no rows are added to the marker table
                return markersForAllCounters;
            }

            foreach (string dataLabel in markersForImage.DataLabels)
            {
                // create a marker for each point and add it to the counter's markers
                MarkersForCounter markersForCounter = new MarkersForCounter(dataLabel);
                string pointList;
                try
                {
                    pointList = markersForImage[dataLabel];
                }
                catch (Exception exception)
                {
                    Debug.Fail(String.Format("Read of marker failed for dataLabel '{0}'.", dataLabel), exception.ToString());
                    pointList = String.Empty;
                }

                markersForCounter.Parse(pointList);
                markersForAllCounters.Add(markersForCounter);
            }

            return markersForAllCounters;
        }

        private void GetMarkers()
        {
            string markersQuery = "Select * FROM " + Constant.DatabaseTable.Markers;
            this.Markers = new DataTableBackedList<MarkerRow>(this.Database.GetDataTableFromSelect(markersQuery), (DataRow row) => { return new MarkerRow(row); });
        }

        /// <summary>
        /// Set the list of marker points on the current row in the marker table. 
        /// </summary>
        public void SetMarkerPositions(long imageID, MarkersForCounter markersForCounter)
        {
            // Find the current row number
            MarkerRow marker = this.Markers.Find(imageID);
            if (marker == null)
            {
                Debug.Fail(String.Format("Image ID {0} missing in markers table.", imageID));
                return;
            }

            // Update the database and datatable
            marker[markersForCounter.DataLabel] = markersForCounter.GetPointList();
            this.SyncMarkerToDatabase(marker);
        }

        public void SyncImageSetToDatabase()
        {
            // don't trigger backups on image set updates as none of the properties in the image set table is particularly important
            // For example, this avoids creating a backup when a custom selection is reverted to all when Carnassial exits.
            this.Database.Update(Constant.DatabaseTable.ImageSet, this.ImageSet.GetColumnTuples());
        }

        public void SyncMarkerToDatabase(MarkerRow marker)
        {
            this.CreateBackupIfNeeded();
            this.Database.Update(Constant.DatabaseTable.Markers, marker.GetColumnTuples());
        }

        // The id is the row to update, the datalabels are the labels of each control to updata, 
        // and the markers are the respective point lists for each of those labels
        public void UpdateMarkers(List<ColumnTuplesWithWhere> markersToUpdate)
        {
            // update markers in database
            this.CreateBackupIfNeeded();
            this.Database.Update(Constant.DatabaseTable.Markers, markersToUpdate);

            // update markers in marker data table
            this.GetMarkers();
        }

        protected override void Dispose(bool disposing)
        {
            if (this.disposed)
            {
                return;
            }

            if (disposing)
            {
                if (this.Files != null)
                {
                    this.Files.Dispose();
                }
                if (this.Markers != null)
                {
                    this.Markers.Dispose();
                }
            }

            base.Dispose(disposing);
            this.disposed = true;
        }

        private ColumnDefinition CreateFileDataColumnDefinition(ControlRow control)
        {
            if (control.DataLabel == Constant.DatabaseColumn.DateTime)
            {
                return new ColumnDefinition(control.DataLabel, "DATETIME", DateTimeHandler.ToDatabaseDateTimeString(Constant.ControlDefault.DateTimeValue));
            }
            if (control.DataLabel == Constant.DatabaseColumn.UtcOffset)
            {
                // UTC offsets are typically represented as TimeSpans but the least awkward way to store them in SQLite is as a real column containing the offset in
                // hours.  This is because SQLite
                // - handles TIME columns as DateTime rather than TimeSpan, requiring the associated DataTable column also be of type DateTime
                // - doesn't support negative values in time formats, requiring offsets for time zones west of Greenwich be represented as positive values
                // - imposes an upper bound of 24 hours on time formats, meaning the 26 hour range of UTC offsets (UTC-12 to UTC+14) cannot be accomodated
                // - lacks support for DateTimeOffset, so whilst offset information can be written to the database it cannot be read from the database as .NET
                //   supports only DateTimes whose offset matches the current system time zone
                // Storing offsets as ticks, milliseconds, seconds, minutes, or days offers equivalent functionality.  Potential for rounding error in roundtrip 
                // calculations on offsets is similar to hours for all formats other than an INTEGER (long) column containing ticks.  Ticks are a common 
                // implementation choice but testing shows no roundoff errors at single tick precision (100 nanoseconds) when using hours.  Even with TimeSpans 
                // near the upper bound of 256M hours, well beyond the plausible range of time zone calculations.  So there does not appear to be any reason to 
                // avoid using hours for readability when working with the database directly.
                return new ColumnDefinition(control.DataLabel, "REAL", DateTimeHandler.ToDatabaseUtcOffsetString(Constant.ControlDefault.DateTimeValue.Offset));
            }
            if (String.IsNullOrWhiteSpace(control.DefaultValue))
            { 
                 return new ColumnDefinition(control.DataLabel, Constant.Sql.Text);
            }
            return new ColumnDefinition(control.DataLabel, Constant.Sql.Text, control.DefaultValue);
        }
    }
}
