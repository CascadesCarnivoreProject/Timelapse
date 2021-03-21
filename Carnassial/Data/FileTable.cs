using Carnassial.Database;
using Carnassial.Images;
using Carnassial.Util;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Carnassial.Data
{
    public class FileTable : SQLiteTable<ImageRow>
    {
        public Dictionary<string, SqlDataType> StandardColumnDataTypesByName { get; private set; }
        public Dictionary<string, FileTableColumn> UserColumnsByName { get; private set; }
        public int UserCounters { get; private set; }
        public int UserFlags { get; private set; }
        public int UserNotesAndChoices { get; private set; }

        public FileTable()
        {
            this.StandardColumnDataTypesByName = new Dictionary<string, SqlDataType>(StringComparer.Ordinal)
            {
                { Constant.FileColumn.Classification, SqlDataType.Integer },
                { Constant.FileColumn.DateTime, SqlDataType.DateTime },
                { Constant.FileColumn.DeleteFlag, SqlDataType.Boolean },
                { Constant.FileColumn.File, SqlDataType.String },
                { Constant.FileColumn.RelativePath, SqlDataType.String },
                { Constant.FileColumn.UtcOffset, SqlDataType.Real }
            };
            this.UserColumnsByName = new Dictionary<string, FileTableColumn>(StringComparer.Ordinal);
        }

        public static SQLiteTableSchema CreateSchema(ControlTable controls)
        {
            SQLiteTableSchema schema = new(Constant.DatabaseTable.Files);
            schema.ColumnDefinitions.Add(ColumnDefinition.CreatePrimaryKey());

            // derive schema from the controls defined
            foreach (ControlRow control in controls)
            {
                schema.ColumnDefinitions.AddRange(FileTable.CreateFileTableColumnDefinitions(control));

                if (control.IndexInFileTable)
                {
                    schema.Indices.Add(SecondaryIndex.CreateFileTableIndex(control));
                }
            }

            return schema;
        }

        public ImageRow CreateAndAppendFile(string fileName, string relativePath)
        {
            ImageRow file;
            if (FileTable.IsVideo(fileName))
            {
                file = new VideoRow(fileName, relativePath, this);
            }
            else if (JpegImage.IsJpeg(fileName))
            {
                file = new ImageRow(fileName, relativePath, this);
            }
            else
            {
                throw new NotSupportedException(String.Format(CultureInfo.CurrentCulture, "Unhandled extension for file '{0}'.", fileName));
            }

            this.Rows.Add(file);
            return file;
        }

        public static IEnumerable<ColumnDefinition> CreateFileTableColumnDefinitions(ControlRow control)
        {
            if (String.Equals(control.DataLabel, Constant.FileColumn.Classification, StringComparison.Ordinal))
            {
                yield return new ColumnDefinition(control.DataLabel, Constant.SQLiteAffinity.Integer)
                {
                    DefaultValue = ((int)default(FileClassification)).ToString(CultureInfo.InvariantCulture),
                    NotNull = true
                };
                yield break;
            }
            if (String.Equals(control.DataLabel, Constant.FileColumn.DateTime, StringComparison.Ordinal))
            {
                yield return new ColumnDefinition(control.DataLabel, Constant.ControlDefault.DateTimeValue.DateTime);
                yield break;
            }
            if (String.Equals(control.DataLabel, Constant.FileColumn.UtcOffset, StringComparison.Ordinal))
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
                yield return new ColumnDefinition(control.DataLabel, Constant.ControlDefault.DateTimeValue.Offset);
                yield break;
            }

            ColumnDefinition column = new(control.DataLabel, Constant.SQLiteAffinity.Text);
            switch (control.ControlType)
            {
                case ControlType.Counter:
                case ControlType.Flag:
                    column.NotNull = true;
                    column.Type = Constant.SQLiteAffinity.Integer;
                    break;
                case ControlType.Note:
                    if (String.Equals(control.DataLabel, Constant.FileColumn.File, StringComparison.Ordinal))
                    {
                        column.NotNull = true;
                    }
                    break;
                default:
                    // nothing to do
                    break;
            }
            if (String.IsNullOrWhiteSpace(control.DefaultValue) == false)
            {
                column.DefaultValue = control.DefaultValue;
            }
            yield return column;

            if (control.ControlType == ControlType.Counter)
            {
                yield return new ColumnDefinition(FileTable.GetMarkerPositionColumnName(control.DataLabel), Constant.SQLiteAffinity.Blob);
            }
        }

        public Dictionary<string, Dictionary<string, ImageRow>> GetFilesByRelativePathAndName()
        {
            Dictionary<string, Dictionary<string, ImageRow>> filesByRelativePathAndName = new(StringComparer.OrdinalIgnoreCase);
            foreach (ImageRow file in this.Rows)
            {
                if (filesByRelativePathAndName.TryGetValue(file.RelativePath, out Dictionary<string, ImageRow>? filesInFolderByName) == false)
                {
                    filesInFolderByName = new Dictionary<string, ImageRow>(StringComparer.OrdinalIgnoreCase);
                    filesByRelativePathAndName.Add(file.RelativePath, filesInFolderByName);
                }
                filesInFolderByName.Add(file.FileName, file);
            }
            return filesByRelativePathAndName;
        }

        public SortedDictionary<string, List<string>> GetFileNamesByRelativePath()
        {
            SortedDictionary<string, List<string>> filesByRelativePath = new(StringComparer.OrdinalIgnoreCase);
            foreach (ImageRow file in this.Rows)
            {
                if (filesByRelativePath.TryGetValue(file.RelativePath, out List<string>? filesInFolder) == false)
                {
                    filesInFolder = new List<string>();
                    filesByRelativePath.Add(file.RelativePath, filesInFolder);
                }
                filesInFolder.Add(file.FileName);
            }
            return filesByRelativePath;
        }

        public static string GetMarkerPositionColumnName(string counterDataLabel)
        {
            return counterDataLabel + Constant.FileColumn.MarkerPositionSuffix;
        }

        public Dictionary<string, HashSet<string>> HashFileNamesByRelativePath()
        {
            // for now, the primary purpose of this function is allow the caller to quickly check if a file is in the database
            // Therefore, assemble a Dictionary<, HashSet<>> as both these collection types have O(1) Contains().
            Dictionary<string, HashSet<string>> filesByRelativePath = new(StringComparer.OrdinalIgnoreCase);
            foreach (ImageRow file in this.Rows)
            {
                if (filesByRelativePath.TryGetValue(file.RelativePath, out HashSet<string>? filesInFolder) == false)
                {
                    filesInFolder = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    filesByRelativePath.Add(file.RelativePath, filesInFolder);
                }
                filesInFolder.Add(file.FileName);
            }
            return filesByRelativePath;
        }

        public static bool IsVideo(string fileName)
        {
            return fileName.EndsWith(Constant.File.AviFileExtension, StringComparison.OrdinalIgnoreCase) ||
                   fileName.EndsWith(Constant.File.Mp4FileExtension, StringComparison.OrdinalIgnoreCase);
        }

        public override void Load(SQLiteDataReader reader)
        {
            if (reader.FieldCount < 1)
            {
                throw new SQLiteException(SQLiteErrorCode.Schema, App.FindResource<string>(Constant.ResourceKey.FileTableNoColumns));
            }

            // locate columns in schema
            // Standard columns usually appear first but, depending on the table's evolution, this may not always be the case.
            int dateTimeIndex = -1;
            int deleteFlagIndex = -1;
            int fileNameIndex = -1;
            int idIndex = -1;
            int classificationIndex = -1;
            int relativePathIndex = -1;
            int utcOffsetIndex = -1;

            int userCounter = -1;
            int[] userCounterSqlIndices = new int[this.UserCounters];
            int userFlag = -1;
            int[] userFlagSqlIndices = new int[this.UserFlags];
            int userMarkerPosition = -1;
            int[] userMarkerPositionSqlIndices = new int[this.UserCounters];
            int userNoteOrChoice = -1;
            int[] userNoteAndChoiceSqlIndices = new int[this.UserNotesAndChoices];

            for (int columnIndex = 0; columnIndex < reader.FieldCount; ++columnIndex)
            {
                string column = reader.GetName(columnIndex);
                switch (column)
                {
                    case Constant.FileColumn.DateTime:
                        dateTimeIndex = columnIndex;
                        break;
                    case Constant.FileColumn.DeleteFlag:
                        deleteFlagIndex = columnIndex;
                        break;
                    case Constant.FileColumn.File:
                        fileNameIndex = columnIndex;
                        break;
                    case Constant.DatabaseColumn.ID:
                        idIndex = columnIndex;
                        break;
                    case Constant.FileColumn.Classification:
                        classificationIndex = columnIndex;
                        break;
                    case Constant.FileColumn.RelativePath:
                        relativePathIndex = columnIndex;
                        break;
                    case Constant.FileColumn.UtcOffset:
                        utcOffsetIndex = columnIndex;
                        break;
                    default:
                        FileTableColumn userColumn = this.UserColumnsByName[column];
                        int dataIndex;
                        switch (userColumn.DataType)
                        {
                            case SqlDataType.Boolean:
                                dataIndex = ++userFlag;
                                userFlagSqlIndices[dataIndex] = columnIndex;
                                break;
                            case SqlDataType.Blob:
                                dataIndex = ++userMarkerPosition;
                                userMarkerPositionSqlIndices[dataIndex] = columnIndex;
                                break;
                            case SqlDataType.Integer:
                                dataIndex = ++userCounter;
                                userCounterSqlIndices[dataIndex] = columnIndex;
                                break;
                            case SqlDataType.String:
                                dataIndex = ++userNoteOrChoice;
                                userNoteAndChoiceSqlIndices[dataIndex] = columnIndex;
                                break;
                            default:
                                throw new NotSupportedException(String.Format(CultureInfo.CurrentCulture, "Unhandled column data type {0}.", userColumn.DataIndex));
                        }
                        userColumn.DataIndex = dataIndex;
                        break;
                }
            }

            bool allStandardColumnsPresent = (dateTimeIndex != -1) &&
                                             (deleteFlagIndex != -1) &&
                                             (fileNameIndex != -1) &&
                                             (idIndex != -1) &&
                                             (classificationIndex != -1) &&
                                             (relativePathIndex != -1) &&
                                             (utcOffsetIndex != -1);
            if (allStandardColumnsPresent == false)
            {
                throw new SQLiteException(SQLiteErrorCode.Schema, "At least one standard column is missing from table " + reader.GetTableName(0));
            }

            this.Rows.Clear();

            while (reader.Read())
            {
                string fileName = reader.GetString(fileNameIndex);
                string relativePath = reader.GetString(relativePathIndex);
                ImageRow file = this.CreateAndAppendFile(fileName, relativePath);

                // read file values
                // Carnassial versions prior to 2.2.0.3 had a bug where UTC offsets where written as TimeSpans rather than doubles
                // which, combined with column type real, produces an odd situation where IDataRecord.GetValue() returns a correct
                // double but GetDouble() throws on System.Data.SQLite 1.0.107.  As a workaround for backwards compatibility, 
                // GetValue() is called and cast to double.
                double utcOffset = reader.GetDouble(utcOffsetIndex);
                file.Classification = (FileClassification)reader.GetInt32(classificationIndex);
                file.DateTimeOffset = DateTimeHandler.FromDatabaseDateTimeOffset(reader.GetDateTime(dateTimeIndex), DateTimeHandler.FromDatabaseUtcOffset(utcOffset));
                file.DeleteFlag = reader.GetBoolean(deleteFlagIndex);
                file.ID = reader.GetInt64(idIndex);
                foreach (FileTableColumn userColumn in this.UserColumnsByName.Values)
                {
                    switch (userColumn.DataType)
                    {
                        case SqlDataType.Boolean:
                            int sqlIndex = userFlagSqlIndices[userColumn.DataIndex];
                            file.UserFlags[userColumn.DataIndex] = reader.GetBoolean(sqlIndex);
                            break;
                        case SqlDataType.Blob:
                            sqlIndex = userMarkerPositionSqlIndices[userColumn.DataIndex];
                            byte[] value;
                            if (reader.IsDBNull(sqlIndex))
                            {
                                value = Array.Empty<byte>();
                            }
                            else
                            {
                                byte[] packedFloats = (byte[])reader.GetValue(sqlIndex);
                                if (packedFloats.Length > 0)
                                {
                                    value = packedFloats;
                                }
                                else
                                {
                                    value = Array.Empty<byte>();
                                }
                            }
                            file.UserMarkerPositions[userColumn.DataIndex] = value;
                            break;
                        case SqlDataType.Integer:
                            sqlIndex = userCounterSqlIndices[userColumn.DataIndex];
                            file.UserCounters[userColumn.DataIndex] = reader.GetInt32(sqlIndex);
                            break;
                        case SqlDataType.String:
                            sqlIndex = userNoteAndChoiceSqlIndices[userColumn.DataIndex];
                            file.UserNotesAndChoices[userColumn.DataIndex] = reader.GetString(sqlIndex);
                            break;
                        default:
                            throw new NotSupportedException(String.Format(CultureInfo.CurrentCulture, "Unhandled column data type {0}.", userColumn.DataIndex));
                    }
                }
                file.AcceptChanges();
            }
        }

        public void SetUserControls(ControlTable controls)
        {
            this.UserColumnsByName.Clear();
            this.UserCounters = 0;
            this.UserFlags = 0;
            this.UserNotesAndChoices = 0;

            foreach (ControlRow control in controls)
            {
                if (control.IsUserControl())
                {
                    this.UserColumnsByName.Add(control.DataLabel, new FileTableColumn(control));
                    switch (control.ControlType)
                    {
                        case ControlType.Counter:
                            string markerColumnName = FileTable.GetMarkerPositionColumnName(control.DataLabel);
                            FileTableColumn markerColumn = new(markerColumnName, control);
                            this.UserColumnsByName.Add(markerColumnName, markerColumn);
                            ++this.UserCounters;
                            break;
                        case ControlType.FixedChoice:
                        case ControlType.Note:
                            ++this.UserNotesAndChoices;
                            break;
                        case ControlType.Flag:
                            ++this.UserFlags;
                            break;
                        case ControlType.DateTime:
                        case ControlType.UtcOffset:
                        default:
                            throw new NotSupportedException(String.Format(CultureInfo.CurrentCulture, "Unhandled control type {0}.", control.ControlType));
                    }
                }
            }
        }

        public bool TryGetPreviousFile(int fileIndex, [NotNullWhen(true)] out ImageRow? previousFile)
        {
            if (fileIndex > 1)
            {
                previousFile = this[fileIndex - 1];
                return true;
            }

            previousFile = null;
            return false;
        }

        public bool TryFind(long id, [NotNullWhen(true)] out ImageRow? file, out int fileIndex)
        {
            for (fileIndex = 0; fileIndex < this.Rows.Count; ++fileIndex)
            {
                file = this.Rows[fileIndex];
                if (file.ID == id)
                {
                    return true;
                }
            }
            file = null;
            return false;
        }

        private class UserControlIndices
        {
            public int SqlColumnIndex { get; private set; }
            public int UserControlArrayIndex { get; private set; }

            public UserControlIndices(int sqlColumnIndex, int userControlArrayIndex)
            {
                this.SqlColumnIndex = sqlColumnIndex;
                this.UserControlArrayIndex = userControlArrayIndex;
            }
        }
    }
}
