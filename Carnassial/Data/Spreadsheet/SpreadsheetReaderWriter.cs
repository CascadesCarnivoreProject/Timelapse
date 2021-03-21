using Carnassial.Database;
using Carnassial.Interop;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;

namespace Carnassial.Data.Spreadsheet
{
    /// <summary>
    /// Import and export .csv and .xlsx files.
    /// </summary>
    // Approximate performance, Carnassial 2.2.0.3 @ 65k rows, 16 cells/row:
    // import .csv  - 240k rows/second
    //        .xlsx - 33k rows/second
    //        .ddb - 65k rows/second in FileTable.Load()
    // export .csv  - 125k rows/second
    //        .xlsx - 29k rows/second
    public class SpreadsheetReaderWriter
    {
        private readonly List<string> currentRow;
        private readonly SpreadsheetReadWriteStatus status;

        public SpreadsheetReaderWriter(Action<SpreadsheetReadWriteStatus> onProgressUpdate, TimeSpan desiredProgressUpdateInterval)
        {
            // a pre-populated row list avoids calling into GC on each row and calling List<>.Add() for each column
            // Multiple rows can be used for thread safety if multithreaded reads become supported.
            this.currentRow = new List<string>();
            this.status = new SpreadsheetReadWriteStatus(onProgressUpdate, desiredProgressUpdateInterval);
        }

        private unsafe string? EscapeForCsv(string? value)
        {
            if (value == null)
            {
                return null;
            }

            fixed (char* valueCharacters = value)
            {
                bool escape = false;
                bool replaceQuotes = false;
                for (int index = 0; index < value.Length; ++index)
                {
                    // commas, double quotation marks, line feeds (\n), and carriage returns (\r) require leading and ending double quotation marks be added
                    // double quotation marks within the field also have to be escaped as double quotes
                    // Carriage returns and line feeds are rare and often not supported in .csv code.
                    char character = *(valueCharacters + index);
                    escape |= (character == '"') || (character == ',') || (character == '\n') || (character == '\r');
                    replaceQuotes |= character == '"';
                    if (replaceQuotes)
                    {
                        // in most cases escaping is not required and this loop will run to the full length of the string
                        // There's therefore little to negative value in more complicated logic which would allow breaking on non-quote
                        // characters.
                        break;
                    }
                }

                if (replaceQuotes)
                {
                    value = value.Replace("\"", "\"\"");
                }
                if (escape)
                {
                    return "\"" + value + "\"";
                }
                return value;
            }
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
                List<string> columns = new(database.Controls.RowCount);
                foreach (ControlRow control in database.Controls.InSpreadsheetOrder())
                {
                    columns.Add(control.DataLabel);
                    fileWriter.Write(this.EscapeForCsv(control.DataLabel));
                    fileWriter.Write(',');

                    if (control.ControlType == ControlType.Counter)
                    {
                        string markerColumn = FileTable.GetMarkerPositionColumnName(control.DataLabel);
                        columns.Add(markerColumn);
                        fileWriter.Write(this.EscapeForCsv(markerColumn));
                        fileWriter.Write(',');
                    }
                }
                fileWriter.Write(fileWriter.NewLine);

                for (int fileIndex = 0, mostRecentReportCheck = 0; fileIndex < database.Files.RowCount; ++fileIndex)
                {
                    ImageRow file = database.Files[fileIndex];
                    foreach (string dataLabel in columns)
                    {
                        fileWriter.Write(this.EscapeForCsv(file.GetSpreadsheetString(dataLabel)));
                        fileWriter.Write(',');
                    }
                    fileWriter.Write(fileWriter.NewLine);

                    if (fileIndex - mostRecentReportCheck > Constant.File.RowsBetweenStatusReportChecks)
                    {
                        if (this.status.ShouldUpdateProgress())
                        {
                            this.status.QueueProgressUpdate(fileIndex);
                        }
                        mostRecentReportCheck = fileIndex;
                    }
                }
            }

            this.status.QueueProgressUpdate(database.Files.RowCount);
        }

        /// <summary>
        /// Export all data for the selected files to the .xlsx file indicated.
        /// </summary>
        public void ExportFileDataToXlsx(FileDatabase fileDatabase, string xlsxFilePath)
        {
            bool xlsxExists = File.Exists(xlsxFilePath);
            using FileStream xlsxStream = new(xlsxFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
            SpreadsheetDocument xlsx;
            if (xlsxExists)
            {
                xlsx = SpreadsheetDocument.Open(xlsxStream, true);
            }
            else
            {
                xlsx = SpreadsheetDocument.Create(xlsxStream, SpreadsheetDocumentType.Workbook);
            }
            using (xlsx)
            {
                // ensure workbook has needed parts
                WorkbookPart workbook = xlsx.WorkbookPart;
                if (workbook == null)
                {
                    workbook = xlsx.AddWorkbookPart();
                    workbook.Workbook = new Workbook()
                    {
                        Sheets = new Sheets()
                    };
                }

                SharedStringTablePart sharedStringTable = workbook.SharedStringTablePart;
                if (sharedStringTable == null)
                {
                    sharedStringTable = workbook.AddNewPart<SharedStringTablePart>();
                }
                SharedStringIndex sharedStringIndex = new(sharedStringTable, this.status);

                WorkbookStylesPart styles = workbook.WorkbookStylesPart;
                if (styles == null)
                {
                    styles = workbook.AddNewPart<WorkbookStylesPart>();
                }
                OpenXmlStylesheet stylesheet = new(styles);

                // if desired worksheet doesn't exist, add it to the workbook
                // If worksheet exists it doesn't need to be deleted; creating an OpenXmlWriter on it overwrites its contents.
                Sheet? worksheetInfo = workbook.Workbook.Sheets.Elements<Sheet>().FirstOrDefault(sheet => String.Equals(sheet.Name, Constant.Excel.FileDataWorksheetName, StringComparison.Ordinal));
                WorksheetPart worksheet;
                if (worksheetInfo == null)
                {
                    uint lowestUnusedSheetId = 1; // Excel requires repair of /xl/workbook.xml with a sheet ID of 0
                    if (workbook.Workbook.Sheets.Any())
                    {
                        lowestUnusedSheetId = workbook.Workbook.Sheets.Elements<Sheet>().Max(sheet => sheet.SheetId.Value) + 1;
                    }
                    worksheetInfo = new Sheet()
                    {
                        Name = Constant.Excel.FileDataWorksheetName,
                        SheetId = lowestUnusedSheetId
                    };
                    workbook.Workbook.Sheets.Append(worksheetInfo);

                    worksheet = workbook.AddNewPart<WorksheetPart>();
                    worksheetInfo.Id = xlsx.WorkbookPart.GetIdOfPart(worksheet);
                }
                else
                {
                    worksheet = (WorksheetPart)workbook.GetPartById(worksheetInfo.Id);
                }

                // get column headers for worksheet
                List<string> columns = new(fileDatabase.Controls.RowCount);
                List<SqlDataType> columnDataTypes = new(fileDatabase.Controls.RowCount);
                foreach (ControlRow control in fileDatabase.Controls.InSpreadsheetOrder())
                {
                    string dataLabel = control.DataLabel;
                    columns.Add(dataLabel);

                    if (String.Equals(dataLabel, Constant.FileColumn.Classification, StringComparison.OrdinalIgnoreCase))
                    {
                        columnDataTypes.Add(SqlDataType.String);
                    }
                    else if (fileDatabase.Files.StandardColumnDataTypesByName.TryGetValue(dataLabel, out SqlDataType dataType))
                    {
                        columnDataTypes.Add(dataType);
                    }
                    else
                    {
                        FileTableColumn userColumn = fileDatabase.Files.UserColumnsByName[dataLabel];
                        columnDataTypes.Add(userColumn.DataType);
                    }

                    if (control.ControlType == ControlType.Counter)
                    {
                        columns.Add(FileTable.GetMarkerPositionColumnName(control.DataLabel));
                        columnDataTypes.Add(SqlDataType.Blob);
                    }
                }

                // write worksheet
                // As an aside, OpenXML 2.8.1 has a bug where calling WriteStartElement() on a worksheet with populated members
                // writes only those members and subsequent write calls---such as those for SheetData, here---are ignored
                // If cases arise where OpenXmlWriter is required, the workaround is to create and write the elements 
                // individually rather than populating the properties of worksheet.Worksheet.
                using (XmlWriter writer = XmlWriter.Create(worksheet.GetStream(FileMode.Create, FileAccess.Write)))
                {
                    writer.WriteStartDocument();
                    writer.WriteStartElement(Constant.OpenXml.Element.Worksheet, Constant.OpenXml.Namespace);
                    writer.WriteAttributeString("xmlns", "r", null, "http://schemas.openxmlformats.org/officeDocument/2006/relationships");

                    writer.WriteStartElement(Constant.OpenXml.Element.Dimension, Constant.OpenXml.Namespace);
                    writer.WriteAttributeString(Constant.OpenXml.Attribute.Reference, "A1:" + this.GetExcelColumnReference(columns.Count - 1) + (fileDatabase.Files.RowCount + 1).ToString(Constant.InvariantCulture));
                    writer.WriteEndElement(); // dimension

                    writer.WriteStartElement(Constant.OpenXml.Element.SheetViews, Constant.OpenXml.Namespace);
                    writer.WriteStartElement(Constant.OpenXml.Element.SheetView, Constant.OpenXml.Namespace);
                    writer.WriteAttributeString(Constant.OpenXml.Attribute.WorkbookViewId, "0");

                    writer.WriteStartElement(Constant.OpenXml.Element.Pane, Constant.OpenXml.Namespace);
                    writer.WriteAttributeString(Constant.OpenXml.Attribute.ActivePane, "bottomLeft");
                    writer.WriteAttributeString(Constant.OpenXml.Attribute.State, "frozen");
                    writer.WriteAttributeString(Constant.OpenXml.Attribute.TopLeftCell, "A2");
                    writer.WriteAttributeString(Constant.OpenXml.Attribute.YSplit, "1");
                    writer.WriteEndElement(); // pane

                    writer.WriteStartElement(Constant.OpenXml.Element.Selection, Constant.OpenXml.Namespace);
                    writer.WriteAttributeString(Constant.OpenXml.Attribute.Sqref, "A1");
                    writer.WriteAttributeString(Constant.OpenXml.Attribute.ActiveCell, "A1");
                    writer.WriteAttributeString(Constant.OpenXml.Attribute.Pane, "bottomLeft");
                    writer.WriteEndElement(); // selection

                    writer.WriteEndElement(); // sheetView
                    writer.WriteEndElement(); // sheetViews

                    writer.WriteStartElement(Constant.OpenXml.Element.SheetFormatProperties, Constant.OpenXml.Namespace);
                    writer.WriteAttributeString(Constant.OpenXml.Attribute.DefaultRowHeight, "15");
                    writer.WriteEndElement(); // sheetFormatPr

                    writer.WriteStartElement(Constant.OpenXml.Element.Columns, Constant.OpenXml.Namespace);
                    for (int columnIndex = 0; columnIndex < columns.Count; ++columnIndex)
                    {
                        // for now, special case column width estimates to headers and assume 11 point Calibri
                        // See approximate width formula for this case in https://msdn.microsoft.com/en-us/library/documentformat.openxml.spreadsheet.column.aspx.
                        // There appears to be a typo in that the padding allowance should be 2*4 + 1 = 9 pixels rather than 5.
                        string column = columns[columnIndex];
                        string excelColumnIndex = (columnIndex + 1).ToString(Constant.InvariantCulture);
                        double width = (256.0 * (double)(column.Length * Constant.Excel.CalibriCharacterWidth11Point + Constant.Excel.AutoFilterDropdownWidth + 9) / (double)Constant.Excel.CalibriCharacterWidth11Point) / 256.0;
                        writer.WriteStartElement(Constant.OpenXml.Element.Column, Constant.OpenXml.Namespace);
                        writer.WriteAttributeString(Constant.OpenXml.Attribute.CustomWidth, "1");
                        writer.WriteAttributeString(Constant.OpenXml.Attribute.Maximum, excelColumnIndex);
                        writer.WriteAttributeString(Constant.OpenXml.Attribute.Minimum, excelColumnIndex);
                        writer.WriteAttributeString(Constant.OpenXml.Attribute.Width, width.ToString("0.00", Constant.InvariantCulture));
                        writer.WriteEndElement(); // col
                    }
                    writer.WriteEndElement(); // cols

                    this.status.BeginWrite(fileDatabase.Files.RowCount);
                    writer.WriteStartElement(Constant.OpenXml.Element.SheetData, Constant.OpenXml.Namespace);

                    string? boldFontStyleID = null;
                    if (stylesheet.BoldFontStyleID >= 0)
                    {
                        boldFontStyleID = stylesheet.BoldFontStyleID.ToString(Constant.InvariantCulture);
                    }
                    string[] columnReferences = new string[columns.Count];
                    writer.WriteStartElement(Constant.OpenXml.Element.Row, Constant.OpenXml.Namespace);
                    for (int columnIndex = 0; columnIndex < columns.Count; ++columnIndex)
                    {
                        string columnReference = this.GetExcelColumnReference(columnIndex);
                        columnReferences[columnIndex] = columnReference;

                        string stringIndex = sharedStringIndex.GetOrAdd(columns[columnIndex]).ToString(Constant.InvariantCulture);
                        this.WriteCellToXlsx(writer, columnReference + "1", Constant.OpenXml.CellType.SharedString, boldFontStyleID, stringIndex);
                    }
                    writer.WriteEndElement(); // row

                    for (int fileIndex = 0, mostRecentReportCheck = 0; fileIndex < fileDatabase.Files.RowCount; ++fileIndex)
                    {
                        ImageRow file = fileDatabase.Files[fileIndex];
                        string rowReference = (fileIndex + 2).ToString(Constant.InvariantCulture); // ones based indexing plus header row

                        writer.WriteStartElement(Constant.OpenXml.Element.Row, Constant.OpenXml.Namespace);
                        for (int columnIndex = 0; columnIndex < columns.Count; ++columnIndex)
                        {
                            string column = columns[columnIndex];
                            SqlDataType columnType = columnDataTypes[columnIndex];
                            string? cellType;
                            string? value = file.GetSpreadsheetString(column);
                            if ((columnType == SqlDataType.String) || (columnType == SqlDataType.DateTime) || (columnType == SqlDataType.Blob))
                            {
                                if (value == null)
                                {
                                    // cells without values are omitted in OpenXML
                                    continue;
                                }
                                // Excel "repairs" inline strings by dropping them, so place all strings in the shared string
                                // table even if they're unlikely to occur more than once in a file.
                                cellType = Constant.OpenXml.CellType.SharedString;
                                value = sharedStringIndex.GetOrAdd(value).ToString(Constant.InvariantCulture);
                            }
                            else if (columnType == SqlDataType.Boolean)
                            {
                                cellType = Constant.OpenXml.CellType.Boolean;
                            }
                            else if ((columnType == SqlDataType.Real) || (columnType == SqlDataType.Integer))
                            {
                                cellType = null; // type attribute is omitted for cells of type number
                            }
                            else
                            {
                                throw new NotSupportedException(String.Format(CultureInfo.CurrentCulture, "Unhandled column data type {0}.", columnType));
                            }

                            this.WriteCellToXlsx(writer, columnReferences[columnIndex] + rowReference, cellType, null, value);
                        }
                        writer.WriteEndElement(); // row

                        if ((fileIndex - mostRecentReportCheck > Constant.File.RowsBetweenStatusReportChecks) && this.status.ShouldUpdateProgress())
                        {
                            this.status.QueueProgressUpdate(fileIndex);
                        }
                    }

                    writer.WriteEndElement(); // sheetData

                    writer.WriteStartElement(Constant.OpenXml.Element.AutoFilter, Constant.OpenXml.Namespace);
                    writer.WriteAttributeString(Constant.OpenXml.Attribute.Reference, "A1:" + columnReferences[columns.Count - 1] + "1");
                    writer.WriteEndElement(); // autoFilter

                    writer.WriteEndElement(); // worksheet
                }

                this.status.BeginExcelSave();
                if (sharedStringIndex.HasChanges)
                {
                    // For performance, this would ideally be of the form
                    //   sharedStringIndex.Write(workbook.SharedStringTablePart);
                    // but, unlike Workbook, SharedStringTablePart doesn't implement Save().  Since SAX is not supported
                    // here changes must be persisted through a DOM.
                    workbook.SharedStringTablePart.SharedStringTable = sharedStringIndex.ToTable();
                    workbook.SharedStringTablePart.SharedStringTable.Save();
                    sharedStringIndex.AcceptChanges();
                }
                if (stylesheet.HasChanges)
                {
                    // same lack of SAX support as with shared strings
                    workbook.WorkbookStylesPart.Stylesheet = stylesheet.ToStylesheet();
                    workbook.WorkbookStylesPart.Stylesheet.Save();
                    stylesheet.AcceptChanges();
                }
                workbook.Workbook.Save();
                xlsx.Save();
                this.status.EndExcelWorkbookSave();
            }
        }

        private int GetExcelColumnIndex(string cellReference)
        {
            Debug.Assert(cellReference.Length > 1, "Cell references must contain at least two characters.");
            int index = cellReference[0] - 'A';
            if ((cellReference[1] > '9') || (cellReference[1] < '0'))
            {
                Debug.Assert(cellReference.Length > 2, "Cell references beyond column Z must contain at least three characters.");
                index = 26 * index + cellReference[1] - 'A';
                if ((cellReference[2] > '9') && (cellReference[2] < '0'))
                {
                    // as of Excel 2017, the maximum column is XFD
                    // So no need to check more than the first three characters of the cell reference.  Within
                    // Carnassial's scope it's unlikely to exceed column Z.  Column ZZ is column 676 and even less
                    // likely to be exceeded.
                    Debug.Assert(cellReference.Length > 3, "Cell references beyond column ZZ must contain at least three characters.");
                    index = 26 * index + cellReference[2] - 'A';
                }
            }
            return index;
        }

        private string GetExcelColumnReference(int index)
        {
            if (index < 27)
            {
                return new string((char)('A' + index), 1);
            }
            if (index < (26 * 26 + 1))
            {
                return new string(new char[] { (char)('A' + index / 26), (char)('A' + (index % 26)) });
            }
            else
            {
                throw new NotSupportedException(String.Format(CultureInfo.CurrentCulture, "Unable to translate column {0} to an Excel cell reference.", index));
            }
        }

        private bool ReadAndParseCsvLine(StreamReader csvReader)
        {
            string unparsedLine = csvReader.ReadLine();
            if (unparsedLine == null)
            {
                return false;
            }

            this.currentRow.Clear();
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
                        this.currentRow.Add(String.Empty);
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
                        string field = unparsedLine[fieldStart..index];
                        this.currentRow.Add(field);
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
                                string field = unparsedLine[fieldStart..index];
                                field = field.Replace("\"\"", "\"");
                                this.currentRow.Add(field);
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
                string field = unparsedLine[fieldStart..];
                if (isFieldEscaped)
                {
                    field = field.Replace("\"\"", "\"");
                }
                this.currentRow.Add(field);
            }

            return true;
        }

        protected bool ReadXlsxRow(XmlReader worksheetReader, List<string> sharedStrings)
        {
            while (worksheetReader.EOF == false)
            {
                if (worksheetReader.NodeType != XmlNodeType.Element)
                {
                    worksheetReader.Read();
                }
                else if (String.Equals(worksheetReader.LocalName, Constant.OpenXml.Element.Row, StringComparison.Ordinal))
                {
                    // new row encountered; clear any existing data in row
                    for (int index = 0; index < this.currentRow.Count; ++index)
                    {
                        this.currentRow[index] = String.Empty;
                    }

                    using (XmlReader rowReader = worksheetReader.ReadSubtree())
                    {
                        while (rowReader.EOF == false)
                        {
                            if (rowReader.NodeType != XmlNodeType.Element)
                            {
                                rowReader.Read();
                            }
                            else if (String.Equals(rowReader.LocalName, Constant.OpenXml.Element.Cell, StringComparison.Ordinal))
                            {
                                string cellReference = rowReader.GetAttribute(Constant.OpenXml.Attribute.CellReference);
                                Debug.Assert(cellReference.Length > 1, "Cell references must have at least two characters.");

                                // get cell's column
                                // The XML is sparse in the sense empty cells are omitted, so this is required to correctly output
                                // rows.
                                int column = this.GetExcelColumnIndex(cellReference);

                                // get cell's value
                                bool isSharedString = String.Equals(rowReader.GetAttribute(Constant.OpenXml.Attribute.CellType), Constant.OpenXml.CellType.SharedString, StringComparison.Ordinal);
                                if (rowReader.ReadToDescendant(Constant.OpenXml.Element.CellValue, Constant.OpenXml.Namespace) == false)
                                {
                                    throw new XmlException(App.FindResource<string>(Constant.ResourceKey.SpreadsheetReaderWriterNoCellValue));
                                }
                                string value = rowReader.ReadElementContentAsString();

                                if (isSharedString)
                                {
                                    int sharedStringIndex = 0;
                                    for (int index = 0; index < value.Length; ++index)
                                    {
                                        char character = value[index];
                                        if ((character > '9') || (character < '0'))
                                        {
                                            throw new FormatException("Shared string index '" + value + "' is not an integer greater than or equal to zero.");
                                        }
                                        sharedStringIndex = 10 * sharedStringIndex + character - '0';
                                    }
                                    value = sharedStrings[sharedStringIndex];
                                }

                                // add cell's value to row
                                Debug.Assert(value != null, "Value unexpectedly null.");
                                this.currentRow[column] = value;
                                rowReader.ReadEndElement();
                            }
                            else
                            {
                                rowReader.Read();
                            }
                        }
                    }
                    worksheetReader.ReadEndElement();
                    return true;
                }
                else
                {
                    worksheetReader.Read();
                }
            }
            return false;
        }

        private FileImportResult TryImportData(FileDatabase fileDatabase, Func<bool> readLine, Func<long> getPosition, string spreadsheetFilePath)
        {
            if (fileDatabase.ImageSet.FileSelection != FileSelection.All)
            {
                throw new ArgumentOutOfRangeException(nameof(fileDatabase), App.FindResource<string>(Constant.ResourceKey.SpreadsheetReaderWriterFileSelectionNotAll));
            }

            string? relativePathFromDatabaseToSpreadsheet = NativeMethods.GetRelativePathFromDirectoryToDirectory(Path.GetDirectoryName(fileDatabase.FilePath), Path.GetDirectoryName(spreadsheetFilePath));
            if (String.Equals(relativePathFromDatabaseToSpreadsheet, ".", StringComparison.Ordinal))
            {
                relativePathFromDatabaseToSpreadsheet = null;
            }
            else if (relativePathFromDatabaseToSpreadsheet.IndexOf(Constant.File.ParentDirectory, StringComparison.Ordinal) != -1)
            {
                throw new NotSupportedException(String.Format(CultureInfo.CurrentCulture, "Canonicalization of relative path from database to spreadsheet '{0}' is not currently supported.", relativePathFromDatabaseToSpreadsheet));
            }

            // validate file header against the database
            readLine.Invoke();
            List<string> columnsFromFileHeader = new(this.currentRow);
            List<string> columnsInDatabase = new(fileDatabase.Controls.RowCount);
            foreach (ControlRow control in fileDatabase.Controls)
            {
                columnsInDatabase.Add(control.DataLabel);

                if (control.ControlType == ControlType.Counter)
                {
                    string markerColumn = FileTable.GetMarkerPositionColumnName(control.DataLabel);
                    columnsInDatabase.Add(markerColumn);
                }
            }

            FileImportResult result = new();
            List<string> columnsInDatabaseButNotInHeader = columnsInDatabase.Except(columnsFromFileHeader).ToList();
            foreach (string column in columnsInDatabaseButNotInHeader)
            {
                result.Errors.Add(App.FormatResource(Constant.ResourceKey.SpreadsheetImportColumnNotInSpreadsheet, column));
            }

            List<string> columnsInHeaderButNotDatabase = columnsFromFileHeader.Except(columnsInDatabase).ToList();
            foreach (string column in columnsInHeaderButNotDatabase)
            {
                result.Errors.Add(App.FormatResource(Constant.ResourceKey.SpreadsheetImportColumnNotInImageSet, column));
            }

            if (result.Errors.Count > 0)
            {
                return result;
            }

            FileTableSpreadsheetMap spreadsheetMap = new(columnsFromFileHeader, fileDatabase.Files);
            if (spreadsheetMap.FileNameSpreadsheetIndex == -1)
            {
                result.Errors.Add(App.FormatResource(Constant.ResourceKey.SpreadsheetImportRequiredColumnNotInSpreadsheet, Constant.FileColumn.File));
            }
            if (spreadsheetMap.RelativePathSpreadsheetIndex == -1)
            {
                result.Errors.Add(App.FormatResource(Constant.ResourceKey.SpreadsheetImportRequiredColumnNotInSpreadsheet, Constant.FileColumn.RelativePath));
            }
            if (spreadsheetMap.DateTimeSpreadsheetIndex == -1)
            {
                result.Errors.Add(App.FormatResource(Constant.ResourceKey.SpreadsheetImportRequiredColumnNotInSpreadsheet, Constant.FileColumn.DateTime));
            }
            if (spreadsheetMap.UtcOffsetSpreadsheetIndex == -1)
            {
                result.Errors.Add(App.FormatResource(Constant.ResourceKey.SpreadsheetImportRequiredColumnNotInSpreadsheet, Constant.FileColumn.UtcOffset));
            }

            if (result.Errors.Count > 0)
            {
                return result;
            }

            // read data for file from the .csv or .xlsx file
            Dictionary<string, Dictionary<string, ImageRow>> filesAlreadyInDatabaseByRelativePath = fileDatabase.Files.GetFilesByRelativePathAndName();
            List<ImageRow> filesToInsert = new();
            List<ImageRow> filesToUpdate = new();
            int filesUnchanged = 0;
            int mostRecentReportCheck = 0;
            int rowsRead = 0;
            this.status.QueueProgressUpdate(0);
            while (readLine.Invoke())
            {
                if (this.currentRow.Count == columnsInDatabase.Count - 1)
                {
                    // .csv files are ambiguous in the sense a trailing comma may or may not be present at the end of the line
                    // if the final field has a value this case isn't a concern, but if the final field has no value then there's
                    // no way for the parser to know the exact number of fields in the line
                    this.currentRow.Add(String.Empty);
                }
                else if (this.currentRow.Count != columnsInDatabase.Count)
                {
                    result.Errors.Add(String.Format(CultureInfo.CurrentCulture, "Expected {0} fields in row '{1}' but found {2}.  Row skipped, database will not be updated for this file.", columnsInDatabase.Count, String.Join(",", this.currentRow), this.currentRow.Count));
                    continue;
                }

                // determine whether a new file needs to be added or if this row corresponds to a file already in the image set
                // For now, it's assumed all renames or moves are done through Carnassial and hence relative path + file name form 
                // an immutable (and unique) ID.
                string fileName = this.currentRow[spreadsheetMap.FileNameSpreadsheetIndex];
                if (String.IsNullOrWhiteSpace(fileName))
                {
                    result.Errors.Add(String.Format(CultureInfo.CurrentCulture, "No file name found in row {0}.  Row skipped, database will not be updated for this file.", filesToInsert.Count + filesToUpdate.Count + filesUnchanged + 1));
                    continue;
                }

                string relativePath = this.currentRow[spreadsheetMap.RelativePathSpreadsheetIndex];
                if (relativePathFromDatabaseToSpreadsheet != null)
                {
                    relativePath = Path.Combine(relativePathFromDatabaseToSpreadsheet, relativePath);
                }

                bool addFile = false;
                if ((filesAlreadyInDatabaseByRelativePath.TryGetValue(relativePath, out Dictionary<string, ImageRow>? filesInFolder) == false) ||
                    (filesInFolder.TryGetValue(fileName, out ImageRow? file) == false))
                {
                    addFile = true;
                    file = fileDatabase.Files.CreateAndAppendFile(fileName, relativePath);
                }
                Debug.Assert(addFile || (file.HasChanges == false), "Existing file unexpectedly has changes.");

                // move row data into file
                file.SetValuesFromSpreadsheet(this.currentRow, spreadsheetMap, result);

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

                ++rowsRead;
                if (rowsRead - mostRecentReportCheck > Constant.File.RowsBetweenStatusReportChecks)
                {
                    if (this.status.ShouldUpdateProgress())
                    {
                        this.status.QueueProgressUpdate(getPosition.Invoke());
                    }
                    mostRecentReportCheck = rowsRead;
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
                this.status.QueueProgressUpdate(filesToInsert.Count);
            }
            if (filesToUpdate.Count > 0)
            {
                using (FileTransactionSequence updateFiles = fileDatabase.CreateUpdateFileTransaction())
                {
                    updateFiles.AddFiles(filesToUpdate);
                    updateFiles.Commit();
                }
                this.status.QueueProgressUpdate(totalFiles);
            }

            result.FilesAdded = filesToInsert.Count;
            result.FilesProcessed = filesToInsert.Count + filesToUpdate.Count + filesUnchanged;
            result.FilesUpdated = filesToUpdate.Count;

            this.status.End();
            return result;
        }

        public FileImportResult TryImportData(string spreadsheetFilePath, FileDatabase fileDatabase)
        {
            try
            {
                if (spreadsheetFilePath.EndsWith(Constant.File.ExcelFileExtension, StringComparison.OrdinalIgnoreCase))
                {
                    return this.TryImportXlsx(spreadsheetFilePath, fileDatabase);
                }
                else
                {
                    return this.TryImportCsv(spreadsheetFilePath, fileDatabase);
                }
            }
            catch (IOException ioException)
            {
                return new FileImportResult()
                {
                    Exception = ioException
                };
            }
            finally
            {
                this.status.End();
            }
        }

        private FileImportResult TryImportCsv(string csvFilePath, FileDatabase fileDatabase)
        {
            using FileStream stream = new(csvFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using StreamReader csvReader = new(stream);
            this.status.BeginRead(csvReader.BaseStream.Length);
            FileImportResult result = this.TryImportData(fileDatabase,
                () => { return this.ReadAndParseCsvLine(csvReader); },
                () => { return stream.Position; },
                csvFilePath);

            this.status.End();
            return result;
        }

        private FileImportResult TryImportXlsx(string xlsxFilePath, FileDatabase fileDatabase)
        {
            try
            {
                using FileStream xlsxStream = new(xlsxFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                using SpreadsheetDocument xlsx = SpreadsheetDocument.Open(xlsxStream, false);
                // find worksheet
                WorkbookPart workbook = xlsx.WorkbookPart;
                Sheet? worksheetInfo = workbook.Workbook.Sheets.Elements<Sheet>().FirstOrDefault(sheet => String.Equals(sheet.Name, Constant.Excel.FileDataWorksheetName, StringComparison.Ordinal));
                if (worksheetInfo == null)
                {
                    FileImportResult worksheetNotFound = new();
                    worksheetNotFound.Errors.Add(String.Format(CultureInfo.CurrentCulture, "Worksheet {0} not found.", Constant.Excel.FileDataWorksheetName));
                    return worksheetNotFound;
                }
                WorksheetPart worksheet = (WorksheetPart)workbook.GetPartById(worksheetInfo.Id);

                // load shared strings
                List<string> sharedStrings = SharedStringIndex.GetSharedStrings(workbook.SharedStringTablePart, this.status);

                // read data from worksheet
                using Stream worksheetStream = worksheet.GetStream();
                this.status.BeginRead(worksheetStream.Length);
                using XmlReader reader = XmlReader.Create(worksheetStream);
                // match the length of the pre-populated Excel row to the current worksheet
                reader.MoveToContent();
                if (reader.ReadToDescendant(Constant.OpenXml.Element.Dimension, Constant.OpenXml.Namespace) == false)
                {
                    throw new XmlException(App.FindResource<string>(Constant.ResourceKey.SpreadsheetReaderWriterNoWorksheetDimension));
                }
                string? dimension = reader.GetAttribute(Constant.OpenXml.Attribute.Reference);
                if (dimension == null)
                {
                    throw new XmlException(App.FindResource<string>(Constant.ResourceKey.SpreadsheetReaderWriterNoDimensionReference));
                }
                string[] range = dimension.Split(':');
                if ((range == null) || (range.Length != 2))
                {
                    throw new XmlException(String.Format(CultureInfo.CurrentCulture, "Worksheet dimension reference '{0}' is malformed.", dimension));
                }
                int maximumColumnIndex = this.GetExcelColumnIndex(range[1]);

                if (this.currentRow.Count <= maximumColumnIndex)
                {
                    for (int index = this.currentRow.Count; index <= maximumColumnIndex; ++index)
                    {
                        this.currentRow.Add(String.Empty);
                    }
                }
                else if (this.currentRow.Count > maximumColumnIndex + 1)
                {
                    this.currentRow.RemoveRange(maximumColumnIndex + 1, this.currentRow.Count - maximumColumnIndex - 1);
                }

                reader.ReadToNextSibling(Constant.OpenXml.Element.SheetData, Constant.OpenXml.Namespace);
                FileImportResult result = this.TryImportData(fileDatabase,
                    () => { return this.ReadXlsxRow(reader, sharedStrings); },
                    () => { return worksheetStream.Position; },
                    xlsxFilePath);
                return result;
            }
            catch (IOException ioException)
            {
                FileImportResult result = new();
                result.Errors.Add(ioException.ToString());
                return result;
            }
            finally
            {
                this.status.End();
            }
        }

        private void WriteCellToXlsx(XmlWriter writer, string reference, string? type, string? style, string? value)
        {
            writer.WriteStartElement(Constant.OpenXml.Element.Cell, Constant.OpenXml.Namespace);
            writer.WriteAttributeString(Constant.OpenXml.Attribute.CellReference, reference);
            if (type != null)
            {
                writer.WriteAttributeString(Constant.OpenXml.Attribute.CellType, type);
            }
            if (style != null)
            {
                writer.WriteAttributeString(Constant.OpenXml.Attribute.CellStyle, style);
            }
            writer.WriteStartElement(Constant.OpenXml.Element.CellValue, Constant.OpenXml.Namespace);
            writer.WriteString(value);
            writer.WriteEndElement(); // v
            writer.WriteEndElement(); // c
        }
    }
}
