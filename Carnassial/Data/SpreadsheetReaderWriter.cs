﻿using Carnassial.Interop;
using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace Carnassial.Data
{
    /// <summary>
    /// Import and export .csv and .xlsx files.
    /// </summary>
    public class SpreadsheetReaderWriter
    {
        private readonly SpreadsheetReadWriteStatus status;
        private List<string> xlsxRow;

        public SpreadsheetReaderWriter(Action<SpreadsheetReadWriteStatus> onProgressUpdate, TimeSpan desiredProgressUpdateInterval)
        {
            this.status = new SpreadsheetReadWriteStatus(onProgressUpdate, desiredProgressUpdateInterval);
            this.xlsxRow = null;
        }

        private string AddColumnValue(string value)
        {
            if (value == null)
            {
                return ",";
            }
            if (value.IndexOfAny("\",\x0A\x0D".ToCharArray()) > -1)
            {
                // commas, double quotation marks, line feeds (\x0A), and carriage returns (\x0D) require leading and ending double quotation marks be added
                // double quotation marks within the field also have to be escaped as double quotes
                return "\"" + value.Replace("\"", "\"\"") + "\"" + ",";
            }

            return value + ",";
        }

        /// <summary>
        /// Export all data for the selected files to the .csv file indicated.
        /// </summary>
        public void ExportFileDataToCsv(FileDatabase database, string csvFilePath)
        {
            this.status.BeginWrite(database.Files.RowCount);

            using (TextWriter fileWriter = new StreamWriter(csvFilePath, false, Encoding.UTF8))
            {
                // write the header as defined by the data labels in the template file
                // The append sequence results in a trailing comma which is retained when writing the line.
                StringBuilder header = new StringBuilder();
                List<string> columns = new List<string>(database.Controls.RowCount);
                foreach (ControlRow control in database.Controls.InSpreadsheetOrder())
                {
                    columns.Add(control.DataLabel);
                    header.Append(this.AddColumnValue(control.DataLabel));

                    if (control.Type == ControlType.Counter)
                    {
                        string markerColumn = FileTable.GetMarkerPositionColumnName(control.DataLabel);
                        columns.Add(markerColumn);
                        header.Append(this.AddColumnValue(markerColumn));
                    }
                }
                fileWriter.WriteLine(header.ToString());

                for (int fileIndex = 0; fileIndex < database.Files.RowCount; ++fileIndex)
                {
                    ImageRow file = database.Files[fileIndex];
                    StringBuilder csvRow = new StringBuilder();
                    foreach (string dataLabel in columns)
                    {
                        csvRow.Append(this.AddColumnValue(file.GetExcelString(dataLabel)));
                    }
                    fileWriter.WriteLine(csvRow.ToString());

                    if (this.status.ShouldReport())
                    {
                        this.status.Report(fileIndex);
                    }
                }
            }

            this.status.Report(database.Files.RowCount);
        }

        /// <summary>
        /// Export all data for the selected files to the .xlsx file indicated.
        /// </summary>
        public void ExportFileDataToXlsx(FileDatabase database, string xlsxFilePath)
        {
            this.status.BeginExcelWorksheetLoad();
            using (ExcelPackage xlsxFile = new ExcelPackage(new FileInfo(xlsxFilePath)))
            {
                List<string> columns = new List<string>(database.Controls.RowCount);
                foreach (ControlRow control in database.Controls.InSpreadsheetOrder())
                {
                    columns.Add(control.DataLabel);
                    if (control.Type == ControlType.Counter)
                    {
                        columns.Add(FileTable.GetMarkerPositionColumnName(control.DataLabel));
                    }
                }

                ExcelWorksheet worksheet = this.GetOrCreateTrimmedWorksheet(xlsxFile, Constant.Excel.FileDataWorksheetName, database.Files.RowCount, columns);

                this.status.BeginWrite(database.Files.RowCount);
                for (int fileIndex = 0; fileIndex < database.Files.RowCount; ++fileIndex)
                {
                    ImageRow file = database.Files[fileIndex];
                    for (int columnIndex = 0; columnIndex < columns.Count; ++columnIndex)
                    {
                        string column = columns[columnIndex];
                        // Profiling shows comparable speed for ExcelWorksheet.Cells[] and ExcelWorksheet.SetValue() but suggests
                        // .Cells may be somewhat preferable.  Code review of the two paths is ambiguous so use .Cells for now.
                        worksheet.Cells[fileIndex + 2, columnIndex + 1].Value = file.GetExcelString(column);
                        // worksheet.SetValue(fileIndex + 2, columnIndex + 1, file.GetExcelString(column));
                    }

                    if (this.status.ShouldReport())
                    {
                        this.status.Report(fileIndex);
                    }
                }

                // make a reasonable effort at matching column widths to content
                // For performance, the number of rows included in autofitting is restricted.  More intelligent algorithms can be
                // adopted if needed; see https://github.com/JanKallman/EPPlus/issues/191.
                ExcelAddressBase dimension = worksheet.Dimension;
                int rowsToAutoFit = Math.Min(dimension.Rows, Constant.Excel.MaximumRowsToIncludeInAutoFit);
                worksheet.Cells[1, 1, rowsToAutoFit, dimension.Columns].AutoFitColumns(Constant.Excel.MinimumColumnWidth, Constant.Excel.MaximumColumnWidth);

                this.status.BeginExcelWorkbookSave();
                xlsxFile.Save();
                this.status.EndExcelWorkbookSave();
            }
        }

        protected ExcelWorksheet GetOrCreateTrimmedWorksheet(ExcelPackage xlsxFile, string worksheetName, int dataRows, List<string> columnHeaders)
        {
            ExcelWorksheet worksheet = xlsxFile.Workbook.Worksheets[worksheetName];
            if (worksheet == null)
            {
                // create new worksheet
                worksheet = xlsxFile.Workbook.Worksheets.Add(worksheetName);
                worksheet.View.FreezePanes(2, 1);
            }
            else
            {
                // if needed, trim existing worksheet to output size
                // Existing data will be overwritten and therefore existing cells don't need to be cleared.  Avoiding such clearing
                // meaningfully reduces overhead when writing updates to large, existing spreadsheets.
                // - Carnassial 2.2.0.3 @ 65k rows: +45% overall write performance from not clearing
                ExcelAddressBase dimension = worksheet.Dimension;
                if (dimension.Columns > columnHeaders.Count)
                {
                    // remove unneeded columns
                    worksheet.DeleteColumn(columnHeaders.Count + 1, dimension.Columns - columnHeaders.Count);
                }

                int totalRows = dataRows + 1;
                if (dimension.Rows > totalRows)
                {
                    // remove unneeded rows
                    worksheet.DeleteRow(totalRows + 1, dimension.Rows - totalRows);
                }
            }

            // write header
            for (int index = 0; index < columnHeaders.Count; ++index)
            {
                worksheet.Cells[1, index + 1].Value = columnHeaders[index];
            }

            ExcelRange headerCells = worksheet.Cells[1, 1, 1, columnHeaders.Count];
            headerCells.AutoFilter = true;
            headerCells.Style.Font.Bold = true;

            return worksheet;
        }

        private List<string> ReadAndParseCsvLine(StreamReader csvReader)
        {
            string unparsedLine = csvReader.ReadLine();
            if (unparsedLine == null)
            {
                return null;
            }

            List<string> parsedLine = new List<string>();
            bool isFieldEscaped = false;
            int fieldStart = 0;
            bool inField = false;
            for (int index = 0; index < unparsedLine.Length; ++index)
            {
                char currentCharacter = unparsedLine[index];
                if (inField == false)
                {
                    if (currentCharacter == '\"')
                    {
                        // start of escaped field
                        isFieldEscaped = true;
                        fieldStart = index + 1;
                    }
                    else if (currentCharacter == ',')
                    {
                        // empty field
                        // promote null values to empty values to prevent the presence of SQNull objects in data tables
                        // much Carnassial code assumes data table fields can be blindly cast to string and breaks once the data table has been
                        // refreshed after null values are inserted
                        parsedLine.Add(String.Empty);
                        continue;
                    }
                    else
                    {
                        // start of unescaped field
                        fieldStart = index;
                    }

                    inField = true;
                }
                else
                {
                    if (currentCharacter == ',' && isFieldEscaped == false)
                    {
                        // end of unescaped field
                        inField = false;
                        string field = unparsedLine.Substring(fieldStart, index - fieldStart);
                        parsedLine.Add(field);
                    }
                    else if (currentCharacter == '\"' && isFieldEscaped)
                    {
                        // escaped character encountered; check for end of escaped field
                        int nextIndex = index + 1;
                        if (nextIndex < unparsedLine.Length)
                        {
                            if (unparsedLine[nextIndex] == ',')
                            {
                                // end of escaped field
                                // note: Whilst this implementation supports escaping of carriage returns and line feeds on export it does not support them on
                                // import.  This is common in .csv parsers and can be addressed if needed by appending the next line to unparsedLine and 
                                // continuing parsing rather than terminating the field.
                                inField = false;
                                isFieldEscaped = false;
                                string field = unparsedLine.Substring(fieldStart, index - fieldStart);
                                field = field.Replace("\"\"", "\"");
                                parsedLine.Add(field);
                                ++index;
                            }
                            else if (unparsedLine[nextIndex] == '"')
                            {
                                // escaped double quotation mark
                                // just move next to skip over the second quotation mark as replacement back to one quotation mark is done in field extraction
                                ++index;
                            }
                        }
                    }
                }
            }

            // if the last character is a non-comma add the final (non-empty) field
            // final empty fields are ambiguous at this level and therefore handled by the caller
            if (inField)
            {
                string field = unparsedLine.Substring(fieldStart, unparsedLine.Length - fieldStart);
                if (isFieldEscaped)
                {
                    field = field.Replace("\"\"", "\"");
                }
                parsedLine.Add(field);
            }

            if (this.status.ShouldReport())
            {
                this.status.Report(csvReader.BaseStream.Position);
            }

            return parsedLine;
        }

        protected List<string> ReadXlsxRow(ExcelWorksheet worksheet, ExcelAddressBase dimension, int row)
        {
            if (dimension.Rows < row)
            {
                return null;
            }

            int columns = dimension.Columns;
            if (this.xlsxRow == null)
            {
                // if the Excel row list hasn't been initialized, populate it
                // Using a pre-populated row list avoids calling into GC on each row and calling List<>.Add() on each field. 
                // Row storage can be made thread safe if multithreaded Excel reads become supported.
                this.xlsxRow = new List<string>(columns);
                for (int column = 1; column <= columns; ++column)
                {
                    this.xlsxRow.Add(null);
                }
            }

            for (int column = 1; column <= columns; ++column)
            {
                // As of EPPlus 4.5.2.1, profiling ranks these data access approaches from fastest to slowest:
                //   1) ExcelWorksheet.GetValue<string>()
                //   2) ExcelWorksheet.GetValue() followed by minor manipulation in Carnassial
                //   3) ExcelWorksheet.Cells[row, column].Text
                //   4) ExcelWorksheet.Cells[row, column].Value
                // The performance advantage of avoiding .Cells is roughly 25%.  GetValue<string>() is 3-4% faster than GetValue().
                string value = worksheet.GetValue<string>(row, column);
                if (value == null)
                {
                    // null and String.Empty are equivalent but, for now, alias nulls to String.Empty
                    // This provides back compat with Carnassial versions before 2.2.0.3 which called Cells[].Text and therefore
                    // received String.Empty as the cell value from EPPlus rather than null.
                    value = String.Empty;
                }
                this.xlsxRow[column - 1] = value;
            }

            if (this.status.ShouldReport())
            {
                this.status.Report(row);
            }
            return this.xlsxRow;
        }

        private FileImportResult TryImportFileData(FileDatabase fileDatabase, Func<List<string>> readLine, string spreadsheetFilePath)
        {
            if (fileDatabase.ImageSet.FileSelection != FileSelection.All)
            {
                throw new ArgumentOutOfRangeException(nameof(fileDatabase), "Database doesn't have all files selected.  Checking for files already added to the image set would fail.");
            }

            string relativePathFromDatabaseToSpreadsheet = NativeMethods.GetRelativePathFromDirectoryToDirectory(Path.GetDirectoryName(fileDatabase.FilePath), Path.GetDirectoryName(spreadsheetFilePath));
            if (String.Equals(relativePathFromDatabaseToSpreadsheet, ".", StringComparison.Ordinal))
            {
                relativePathFromDatabaseToSpreadsheet = null;
            }
            else if (relativePathFromDatabaseToSpreadsheet.IndexOf("..", StringComparison.Ordinal) != -1)
            {
                throw new NotSupportedException(String.Format("Canonicalization of relative path from database to spreadsheet '{0}' is not currently supported.", relativePathFromDatabaseToSpreadsheet));
            }

            // validate file header against the database
            List<string> columnsFromFileHeader = readLine.Invoke();
            List<string> columnsInDatabase = new List<string>(fileDatabase.Controls.RowCount);
            foreach (ControlRow control in fileDatabase.Controls)
            {
                columnsInDatabase.Add(control.DataLabel);

                if (control.Type == ControlType.Counter)
                {
                    string markerColumn = FileTable.GetMarkerPositionColumnName(control.DataLabel);
                    columnsInDatabase.Add(markerColumn);
                }
            }

            FileImportResult result = new FileImportResult();
            List<string> columnsInDatabaseButNotInHeader = columnsInDatabase.Except(columnsFromFileHeader).ToList();
            foreach (string column in columnsInDatabaseButNotInHeader)
            {
                result.Errors.Add("- The column '" + column + "' is present in the image set but not in the spreadsheet file." + Environment.NewLine);
            }

            List<string> columnsInHeaderButNotDatabase = columnsFromFileHeader.Except(columnsInDatabase).ToList();
            foreach (string column in columnsInHeaderButNotDatabase)
            {
                result.Errors.Add("- The column '" + column + "' is present in the spreadsheet file but not in the image set." + Environment.NewLine);
            }

            FileTableSpreadsheetMap spreadsheetMap = fileDatabase.Files.IndexSpreadsheetColumns(columnsFromFileHeader);
            if (spreadsheetMap.FileNameIndex == -1)
            {
                result.Errors.Add("- The column '" + Constant.FileColumn.File + "' must be present in the spreadsheet file." + Environment.NewLine);
            }
            if (spreadsheetMap.RelativePathIndex == -1)
            {
                result.Errors.Add("- The column '" + Constant.FileColumn.RelativePath + "' must be present in the spreadsheet file." + Environment.NewLine);
            }
            if (spreadsheetMap.DateTimeIndex == -1)
            {
                result.Errors.Add("- The column '" + Constant.FileColumn.DateTime + "' must be present in the spreadsheet file." + Environment.NewLine);
            }
            if (spreadsheetMap.UtcOffsetIndex == -1)
            {
                result.Errors.Add("- The column '" + Constant.FileColumn.UtcOffset + "' must be present in the spreadsheet file." + Environment.NewLine);
            }

            if (result.Errors.Count > 0)
            {
                return result;
            }

            // read data for file from the .csv or .xlsx file
            Dictionary<string, Dictionary<string, ImageRow>> filesAlreadyInDatabaseByRelativePath = fileDatabase.Files.GetFilesByRelativePathAndName();
            List<ImageRow> filesToInsert = new List<ImageRow>();
            List<ImageRow> filesToUpdate = new List<ImageRow>();
            int filesUnchanged = 0;
            this.status.Report(0);
            for (List<string> row = readLine.Invoke(); row != null; row = readLine.Invoke())
            {
                if (row.Count == columnsInDatabase.Count - 1)
                {
                    // .csv files are ambiguous in the sense a trailing comma may or may not be present at the end of the line
                    // if the final field has a value this case isn't a concern, but if the final field has no value then there's
                    // no way for the parser to know the exact number of fields in the line
                    row.Add(String.Empty);
                }
                else if (row.Count != columnsInDatabase.Count)
                {
                    Debug.Fail(String.Format("Expected {0} fields in line {1} but found {2}.", columnsInDatabase.Count, String.Join(",", row), row.Count));
                }

                // determine whether a new file needs to be added or if this row corresponds to a file already in the image set
                // For now, it's assumed all renames or moves are done through Carnassial and hence relative path + file name form 
                // an immutable (and unique) ID.
                string fileName = row[spreadsheetMap.FileNameIndex];
                if (String.IsNullOrWhiteSpace(fileName))
                {
                    result.Errors.Add(String.Format("No file name found in row {0}.  Row skipped, database will not be updated.", row));
                    continue;
                }

                string relativePath = row[spreadsheetMap.RelativePathIndex];
                if (relativePathFromDatabaseToSpreadsheet != null)
                {
                    relativePath = Path.Combine(relativePathFromDatabaseToSpreadsheet, relativePath);
                }

                bool addFile = false;
                if ((filesAlreadyInDatabaseByRelativePath.TryGetValue(relativePath, out Dictionary<string, ImageRow> filesInFolder) == false) ||
                    (filesInFolder.TryGetValue(fileName, out ImageRow file) == false))
                {
                    addFile = true;
                    file = fileDatabase.Files.CreateAndAppendFile(fileName, relativePath);
                }
                Debug.Assert(addFile || (file.HasChanges == false), "Existing file unexpectedly has changes.");

                // move row data into file
                file.SetValuesFromSpreadsheet(spreadsheetMap, row, result);

                if (addFile)
                {
                    filesToInsert.Add(file);
                }
                else if (file.HasChanges)
                {
                    filesToUpdate.Add(file);
                }
                else
                {
                    ++filesUnchanged;
                }
            }

            // perform inserts and updates
            int totalFiles = filesToInsert.Count + filesToUpdate.Count;
            this.status.BeginTransactionCommit(totalFiles);
            if (filesToInsert.Count > 0)
            {
                using (FileTransactionSequence insertFiles = fileDatabase.CreateInsertFileTransaction())
                {
                    insertFiles.AddFiles(filesToInsert);
                    insertFiles.Commit();
                }
                this.status.Report(filesToInsert.Count);
            }
            if (filesToUpdate.Count > 0)
            {
                using (FileTransactionSequence updateFiles = fileDatabase.CreateUpdateFileTransaction())
                {
                    updateFiles.AddFiles(filesToUpdate);
                    updateFiles.Commit();
                }
                this.status.Report(totalFiles);
            }

            result.FilesAdded = filesToInsert.Count;
            result.FilesProcessed = filesToInsert.Count + filesToUpdate.Count + filesUnchanged;
            result.FilesUpdated = filesToUpdate.Count;
            return result;
        }

        public FileImportResult TryImportFileData(string spreadsheetFilePath, FileDatabase fileDatabase)
        {
            try
            {
                if (spreadsheetFilePath.EndsWith(Constant.File.ExcelFileExtension, StringComparison.OrdinalIgnoreCase))
                {
                    return this.TryImportFileDataFromXlsx(spreadsheetFilePath, fileDatabase);
                }
                else
                {
                    return this.TryImportFileDataFromCsv(spreadsheetFilePath, fileDatabase);
                }
            }
            catch (IOException ioException)
            {
                return new FileImportResult()
                {
                    Exception = ioException
                };
            }
        }

        private FileImportResult TryImportFileDataFromCsv(string csvFilePath, FileDatabase fileDatabase)
        {
            using (FileStream stream = new FileStream(csvFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                using (StreamReader csvReader = new StreamReader(stream))
                {
                    this.status.BeginCsvRead(csvReader.BaseStream.Length);
                    FileImportResult result = this.TryImportFileData(fileDatabase, () => { return this.ReadAndParseCsvLine(csvReader); }, csvFilePath);
                    return result;
                }
            }
        }

        private FileImportResult TryImportFileDataFromXlsx(string xlsxFilePath, FileDatabase fileDatabase)
        {
            try
            {
                using (ExcelPackage xlsxFile = new ExcelPackage(new FileInfo(xlsxFilePath)))
                {
                    this.status.BeginExcelWorksheetLoad();
                    ExcelWorksheet worksheet = xlsxFile.Workbook.Worksheets[Constant.Excel.FileDataWorksheetName];
                    if (worksheet == null)
                    {
                        FileImportResult worksheetNotFound = new FileImportResult();
                        worksheetNotFound.Errors.Add(String.Format("Worksheet {0} not found.", Constant.Excel.FileDataWorksheetName));
                        return worksheetNotFound;
                    }

                    // cache worksheet dimensions
                    // Profiling shows EPPlus 4.5.2.1 doesn't cache the worksheet's size, so keep it on the heap for efficiency. 
                    // ExcelWorksheet.Cells.{Columns, Rows} can't be used for this purpose these properties indicate the maximum
                    // worksheet size rather than the range populated with data.
                    ExcelAddressBase dimension = worksheet.Dimension;

                    this.status.BeginExcelWorkbookRead(dimension.Rows);
                    int row = 0;
                    FileImportResult result = this.TryImportFileData(fileDatabase, () => { return this.ReadXlsxRow(worksheet, dimension, ++row); }, xlsxFilePath);
                    return result;
                }
            }
            catch (IOException ioException)
            {
                FileImportResult result = new FileImportResult();
                result.Errors.Add(ioException.ToString());
                return result;
            }
        }
    }
}
