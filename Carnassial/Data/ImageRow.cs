﻿using Carnassial.Control;
using Carnassial.Database;
using Carnassial.Images;
using Carnassial.Interop;
using Carnassial.Native;
using Carnassial.Util;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using MetadataExtractor.Formats.Exif.Makernotes;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MetadataDirectory = MetadataExtractor.Directory;

namespace Carnassial.Data
{
    /// <summary>
    /// A row in the file database representing a single image (see also <seealso cref="VideoRow"/>).
    /// </summary>
    public class ImageRow : SQLiteRow, INotifyPropertyChanged
    {
        public class Foo
        {
            private readonly Bar field; // IDE0044: Make field readonly

            public Foo()
            {
                this.field = new Bar();
            }

            public class Bar
            {
                public int Mutable { get; set; }
            }
        }

        private FileClassification classification;
        private DateTimeOffset dateTimeOffset;
        private bool deleteFlag;
        private string fileName;
        private string relativePath;
        private readonly FileTable table;

        public int[] UserCounters { get; private set; }
        public bool[] UserFlags { get; private set; }
        public byte[][] UserMarkerPositions { get; private set; }
        public string[] UserNotesAndChoices { get; private set; }

        public ImageRow(string fileName, string relativePath, FileTable table)
        {
            this.classification = FileClassification.Color;
            this.dateTimeOffset = Constant.ControlDefault.DateTimeValue;
            this.deleteFlag = false;
            this.fileName = fileName;
            this.relativePath = relativePath;
            this.table = table;
            this.UserCounters = new int[table.UserCounters];
            this.UserFlags = new bool[table.UserFlags];
            this.UserMarkerPositions = new byte[table.UserFlags][];
            this.UserNotesAndChoices = new string[table.UserNotesAndChoices];
        }

        public FileClassification Classification
        {
            get
            {
                return this.classification;
            }
            set
            {
                if (this.classification == value)
                {
                    return;
                }
                this.classification = value;
                this.HasChanges |= true;
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.Classification)));
            }
        }

        public DateTimeOffset DateTimeOffset
        {
            get
            {
                return this.dateTimeOffset;
            }
            set
            {
                if (this.dateTimeOffset == value)
                {
                    return;
                }
                this.dateTimeOffset = value;
                this.HasChanges |= true;
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.DateTimeOffset)));
            }
        }

        public bool DeleteFlag
        {
            get
            {
                return this.deleteFlag;
            }
            set
            {
                if (this.deleteFlag == value)
                {
                    return;
                }
                this.deleteFlag = value;
                this.HasChanges |= true;
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.DeleteFlag)));
            }
        }

        public string FileName
        {
            get
            {
                return this.fileName;
            }
            set
            {
                if (String.Equals(this.fileName, value, StringComparison.Ordinal))
                {
                    return;
                }
                this.fileName = value;
                this.HasChanges |= true;
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.FileName)));
            }
        }

        public virtual bool IsVideo
        {
            get
            {
                Debug.Assert(this.classification != FileClassification.Video, "Image unexpectedly classified as video.");
                return false;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public string RelativePath
        {
            get
            {
                return this.relativePath;
            }
            set
            {
                if (String.Equals(this.relativePath, value, StringComparison.Ordinal))
                {
                    return;
                }
                this.HasChanges |= true;
                this.relativePath = value;
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.RelativePath)));
            }
        }

        public object this[string propertyName]
        {
            get
            {
                switch (propertyName)
                {
                    case Constant.FileColumn.DateTime:
                        throw new NotSupportedException("Access DateTime through DateTimeOffset.");
                    case nameof(this.DateTimeOffset):
                        return this.DateTimeOffset;
                    case Constant.FileColumn.DeleteFlag:
                        return this.DeleteFlag;
                    case Constant.FileColumn.File:
                        throw new NotSupportedException("Access FileName through FileName.");
                    case nameof(ImageRow.FileName):
                        return this.FileName;
                    case Constant.DatabaseColumn.ID:
                        return this.ID;
                    case Constant.FileColumn.Classification:
                        return this.Classification;
                    case Constant.FileColumn.RelativePath:
                        return this.RelativePath;
                    case Constant.FileColumn.UtcOffset:
                        throw new NotSupportedException("Access UtcOffset through DateTimeOffset.");
                    default:
                        FileTableColumn userColumn = this.table.UserColumnsByName[propertyName];
                        switch (userColumn.DataType)
                        {
                            case SqlDataType.Boolean:
                                return this.UserFlags[userColumn.DataIndex];
                            case SqlDataType.Blob:
                                return this.UserMarkerPositions[userColumn.DataIndex];
                            case SqlDataType.Integer:
                                return this.UserCounters[userColumn.DataIndex];
                            case SqlDataType.String:
                                return this.UserNotesAndChoices[userColumn.DataIndex];
                            default:
                                throw new NotSupportedException(String.Format("Unhandled column data type {0}.", userColumn.DataIndex));
                        }
                }
            }
            set
            {
                switch (propertyName)
                {
                    // standard controls
                    // Property change notification is sent from the properties called.
                    case Constant.FileColumn.DateTime:
                        throw new NotSupportedException("DateTime must be set through DateTimeOffset.");
                    case nameof(this.DateTimeOffset):
                        this.DateTimeOffset = (DateTimeOffset)value;
                        break;
                    case Constant.FileColumn.DeleteFlag:
                        this.DeleteFlag = (bool)value;
                        break;
                    case Constant.FileColumn.File:
                        throw new NotSupportedException("FileName must be set through FileName.");
                    case nameof(this.FileName):
                        this.FileName = (string)value;
                        break;
                    case Constant.DatabaseColumn.ID:
                        throw new NotSupportedException("ID is immutable.");
                    case Constant.FileColumn.Classification:
                        this.Classification = (FileClassification)value;
                        break;
                    case Constant.FileColumn.RelativePath:
                        this.RelativePath = (string)value;
                        break;
                    case Constant.FileColumn.UtcOffset:
                        throw new NotSupportedException("UtcOffset must be set through DateTimeOffset.");
                    // user defined controls
                    default:
                        FileTableColumn userColumn = this.table.UserColumnsByName[propertyName];
                        bool valueDifferent = false;
                        switch (userColumn.DataType)
                        {
                            case SqlDataType.Boolean:
                                bool valueAsBool = (bool)value;
                                valueDifferent = this.UserFlags[userColumn.DataIndex] != valueAsBool;
                                this.UserFlags[userColumn.DataIndex] = valueAsBool;
                                break;
                            case SqlDataType.Blob:
                                byte[] valueAsByteArray = (byte[])value;
                                valueDifferent = this.ByteArraysEqual(this.UserMarkerPositions[userColumn.DataIndex], valueAsByteArray) == false;
                                this.UserMarkerPositions[userColumn.DataIndex] = valueAsByteArray;
                                break;
                            case SqlDataType.Integer:
                                int valueAsInt = (int)value;
                                valueDifferent = this.UserCounters[userColumn.DataIndex] != valueAsInt;
                                this.UserCounters[userColumn.DataIndex] = valueAsInt;
                                break;
                            case SqlDataType.String:
                                string valueAsString = (string)value;
                                valueDifferent = String.Equals(this.UserNotesAndChoices[userColumn.DataIndex], valueAsString, StringComparison.Ordinal) == false;
                                this.UserNotesAndChoices[userColumn.DataIndex] = valueAsString;
                                break;
                            default:
                                throw new NotSupportedException(String.Format("Unhandled column data type {0}.", userColumn.DataType));
                        }
                        if (valueDifferent)
                        {
                            this.HasChanges |= true;
                            this.PropertyChanged?.Invoke(this, new IndexedPropertyChangedEventArgs<string>(propertyName));
                        }
                        break;
                }
            }
        }

        public DateTime UtcDateTime
        {
            get { return this.dateTimeOffset.UtcDateTime; }
        }

        public TimeSpan UtcOffset
        {
            get { return this.dateTimeOffset.Offset; }
        }

        private bool ByteArraysEqual(byte[] currentValue, byte[] newValue)
        {
            if (currentValue == null)
            {
                return newValue == null;
            }
            if (newValue == null)
            {
                return false;
            }
            return currentValue.SequenceEqual(newValue);
        }

        public object GetDatabaseValue(string dataLabel)
        {
            switch (dataLabel)
            {
                case Constant.FileColumn.DateTime:
                    return this.UtcDateTime;
                case nameof(this.DateTimeOffset):
                    throw new NotSupportedException(String.Format("Database values for {0} and {1} must be accessed through {2}.", nameof(this.UtcDateTime), nameof(this.UtcOffset), nameof(this.DateTimeOffset)));
                case Constant.FileColumn.DeleteFlag:
                    return this.DeleteFlag;
                case Constant.FileColumn.File:
                    return this.FileName;
                case Constant.DatabaseColumn.ID:
                    return this.ID;
                case Constant.FileColumn.Classification:
                    return (int)this.Classification;
                case Constant.FileColumn.RelativePath:
                    return this.RelativePath;
                case Constant.FileColumn.UtcOffset:
                    return DateTimeHandler.ToDatabaseUtcOffset(this.UtcOffset);
                default:
                    FileTableColumn userColumn = this.table.UserColumnsByName[dataLabel];
                    switch (userColumn.DataType)
                    {
                        case SqlDataType.Boolean:
                            return this.UserFlags[userColumn.DataIndex];
                        case SqlDataType.Blob:
                            return this.UserMarkerPositions[userColumn.DataIndex];
                        case SqlDataType.Integer:
                            return this.UserCounters[userColumn.DataIndex];
                        case SqlDataType.String:
                            return this.UserNotesAndChoices[userColumn.DataIndex];
                        default:
                            throw new NotSupportedException(String.Format("Unhandled column data type {0}.", userColumn.DataIndex));
                    }
            }
        }

        [SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1126:PrefixCallsCorrectly", Justification = "StyleCop bug.")]
        public static string GetDataBindingPath(ControlRow control)
        {
            string dataLabel = control.DataLabel;
            Debug.Assert(dataLabel != Constant.FileColumn.UtcOffset, String.Format("Display and editing of UTC offset should be integrated into {0}.", nameof(DataEntryDateTimeOffset)));

            if (String.Equals(dataLabel, Constant.FileColumn.DateTime, StringComparison.Ordinal))
            {
                return nameof(ImageRow.DateTimeOffset);
            }
            if (String.Equals(dataLabel, Constant.FileColumn.File, StringComparison.Ordinal))
            {
                return nameof(ImageRow.FileName);
            }
            if (control.IsUserControl())
            {
                return "[" + dataLabel + "]";
            }
            return dataLabel;
        }

        public static string GetDataLabel(string propertyName)
        {
            if (String.Equals(propertyName, nameof(ImageRow.DateTimeOffset), StringComparison.Ordinal))
            {
                return Constant.FileColumn.DateTime;
            }
            if (String.Equals(propertyName, nameof(ImageRow.FileName), StringComparison.Ordinal))
            {
                return Constant.FileColumn.File;
            }

            return propertyName;
        }

        public static Dictionary<string, object> GetDefaultValues(FileDatabase fileDatabase)
        {
            Dictionary<string, object> defaultValues = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (ControlRow control in fileDatabase.Controls)
            {
                if (String.Equals(control.DataLabel, Constant.DatabaseColumn.ID, StringComparison.Ordinal) ||
                    String.Equals(control.DataLabel, Constant.FileColumn.Classification, StringComparison.Ordinal) ||
                    String.Equals(control.DataLabel, Constant.FileColumn.DateTime, StringComparison.Ordinal) ||
                    String.Equals(control.DataLabel, Constant.FileColumn.File, StringComparison.Ordinal) ||
                    String.Equals(control.DataLabel, Constant.FileColumn.RelativePath, StringComparison.Ordinal) ||
                    String.Equals(control.DataLabel, Constant.FileColumn.UtcOffset, StringComparison.Ordinal))
                {
                    // primary key (ID), file name, relative path, and classification don't have default values
                    // For now, date time and UTC offset are also excluded as they're perhaps more naturally reset by rereading 
                    // file datetimes.
                    continue;
                }

                if (fileDatabase.Files.StandardColumnDataTypesByName.TryGetValue(control.DataLabel, out SqlDataType dataType) == false)
                {
                    dataType = fileDatabase.Files.UserColumnsByName[control.DataLabel].DataType;
                }
                object defaultValue;
                switch (dataType)
                {
                    case SqlDataType.Boolean:
                        if (String.Equals(control.DefaultValue, Constant.Sql.FalseString, StringComparison.Ordinal))
                        {
                            defaultValue = false;
                        }
                        else if (String.Equals(control.DefaultValue, Constant.Sql.TrueString, StringComparison.Ordinal))
                        {
                            defaultValue = true;
                        }
                        else
                        {
                            throw new ArgumentOutOfRangeException(control.DataLabel, String.Format("'{0}' is not a valid classification.", control.DefaultValue));
                        }
                        break;
                    // not currently supported
                    // case SqlDataType.DateTime:
                    //     dateTime = DateTimeHandler.ParseDatabaseDateTime(control.DefaultValue);
                    //     continue;
                    case SqlDataType.Integer:
                        if (String.Equals(control.DataLabel, Constant.FileColumn.Classification))
                        {
                            // classification doesn't have a default value
                            continue;
                        }
                        else
                        {
                            defaultValue = Int32.Parse(control.DefaultValue, NumberStyles.AllowLeadingSign, Constant.InvariantCulture);
                        }
                        break;
                    // not currently supported
                    // case SqlDataType.Real:
                    //     if (DateTimeHandler.TryParseDatabaseUtcOffset(control.DefaultValue, out utcOffset) == false)
                    //     {
                    //         throw new ArgumentOutOfRangeException(control.DataLabel, String.Format("'{0}' is not a valid UTC offset.", control.DefaultValue));
                    //     }
                    //     continue;
                    case SqlDataType.String:
                        defaultValue = control.DefaultValue;
                        break;
                    case SqlDataType.Blob:
                    default:
                        throw new NotSupportedException(String.Format("Unhandled SQL data type {0} for column '{1}'.", dataType, control.DataLabel));
                }

                defaultValues.Add(control.DataLabel, defaultValue);
                if (control.Type == ControlType.Counter)
                {
                    defaultValues.Add(FileTable.GetMarkerPositionColumnName(control.DataLabel), Constant.ControlDefault.MarkerPositions);
                }
            }

            return defaultValues;
        }

        public string GetDisplayDateTime()
        {
            return DateTimeHandler.ToDisplayDateTimeString(this.DateTimeOffset);
        }

        public string GetDisplayString(DataEntryControl control)
        {
            switch (control.PropertyName)
            {
                case Constant.FileColumn.DateTime:
                    throw new NotSupportedException(String.Format("Control has unexpected property name {0}.", Constant.FileColumn.DateTime));
                case nameof(this.DateTimeOffset):
                    return this.GetDisplayDateTime();
                case Constant.FileColumn.DeleteFlag:
                    return this.DeleteFlag ? Boolean.TrueString : Boolean.FalseString;
                case nameof(this.FileName):
                    return this.FileName;
                case Constant.DatabaseColumn.ID:
                    return this.ID.ToString(Constant.InvariantCulture);
                case Constant.FileColumn.Classification:
                    return ImageRow.ToString(this.Classification);
                case Constant.FileColumn.RelativePath:
                    return this.RelativePath;
                case Constant.FileColumn.UtcOffset:
                    return DateTimeHandler.ToDisplayUtcOffsetString(this.UtcOffset);
                default:
                    FileTableColumn userColumn = this.table.UserColumnsByName[control.DataLabel];
                    switch (userColumn.DataType)
                    {
                        case SqlDataType.Boolean:
                            return this.UserFlags[userColumn.DataIndex] ? Boolean.TrueString : Boolean.FalseString;
                        case SqlDataType.Integer:
                            return this.UserCounters[userColumn.DataIndex].ToString(Constant.InvariantCulture);
                        case SqlDataType.String:
                            return this.UserNotesAndChoices[userColumn.DataIndex];
                        case SqlDataType.Blob:
                        default:
                            throw new NotSupportedException(String.Format("Unhandled column data type {0}.", userColumn.DataType));
                    }
            }
        }

        public FileInfo GetFileInfo(string imageSetFolderPath)
        {
            return new FileInfo(this.GetFilePath(imageSetFolderPath));
        }

        public string GetFilePath(string imageSetFolderPath)
        {
            // see RelativePath remarks in constructor
            return Path.Combine(imageSetFolderPath, this.GetRelativePath());
        }

        public MarkersForCounter GetMarkersForCounter(string counterDataLabel)
        {
            FileTableColumn counterColumn = this.table.UserColumnsByName[counterDataLabel];
            FileTableColumn markerColumn = this.table.UserColumnsByName[FileTable.GetMarkerPositionColumnName(counterDataLabel)];

            MarkersForCounter markersForCounter = new MarkersForCounter(counterDataLabel, this.UserCounters[counterColumn.DataIndex]);
            markersForCounter.MarkerPositionsFromFloatArray(this.UserMarkerPositions[markerColumn.DataIndex]);
            markersForCounter.PropertyChanged += this.MarkersForCounter_PropertyChanged;
            return markersForCounter;
        }

        public static string GetPropertyName(string dataLabel)
        {
            Debug.Assert(dataLabel != Constant.FileColumn.UtcOffset, String.Format("UTC offset should be accessed by {0}.", nameof(ImageRow.DateTimeOffset)));

            if (String.Equals(dataLabel, Constant.FileColumn.DateTime, StringComparison.Ordinal))
            {
                return nameof(ImageRow.DateTimeOffset);
            }
            if (String.Equals(dataLabel, Constant.FileColumn.File, StringComparison.Ordinal))
            {
                return nameof(ImageRow.FileName);
            }
            return dataLabel;
        }

        public string GetRelativePath()
        {
            if (String.IsNullOrWhiteSpace(this.RelativePath))
            {
                return this.FileName;
            }
            return Path.Combine(this.RelativePath, this.FileName);
        }

        public string GetSpreadsheetString(string dataLabel)
        {
            switch (dataLabel)
            {
                case Constant.FileColumn.DateTime:
                    return DateTimeHandler.ToDatabaseDateTimeString(this.UtcDateTime);
                case Constant.FileColumn.DeleteFlag:
                    return this.DeleteFlag ? Constant.Sql.TrueString : Constant.Sql.FalseString;
                case nameof(this.DateTimeOffset):
                    throw new NotSupportedException(String.Format("Unexpected data label {0}.", nameof(this.DateTimeOffset)));
                case Constant.FileColumn.File:
                    return this.FileName;
                case Constant.DatabaseColumn.ID:
                    return this.ID.ToString(Constant.InvariantCulture);
                case Constant.FileColumn.Classification:
                    return ImageRow.ToString(this.Classification);
                case Constant.FileColumn.RelativePath:
                    return this.RelativePath;
                case Constant.FileColumn.UtcOffset:
                    return DateTimeHandler.ToDatabaseUtcOffsetString(this.UtcOffset);
                default:
                    FileTableColumn userColumn = this.table.UserColumnsByName[dataLabel];
                    switch (userColumn.DataType)
                    {
                        case SqlDataType.Boolean:
                            return this.UserFlags[userColumn.DataIndex] ? Constant.Sql.TrueString : Constant.Sql.FalseString;
                        case SqlDataType.Blob:
                            byte[] value = this.UserMarkerPositions[userColumn.DataIndex];
                            if (value == null)
                            {
                                return null;
                            }
                            return MarkersForCounter.MarkerPositionsToSpreadsheetString(value);
                        case SqlDataType.Integer:
                            return this.UserCounters[userColumn.DataIndex].ToString(Constant.InvariantCulture);
                        case SqlDataType.String:
                            return this.UserNotesAndChoices[userColumn.DataIndex];
                        default:
                            throw new NotSupportedException(String.Format("Unhandled column data type {0}.", userColumn.DataType));
                    }
            }
        }

        public Dictionary<string, object> GetValues()
        {
            // UtcDateTime and UtcOffset aren't included as they're portions of DateTimeOffset
            Dictionary<string, object> valuesByDataLabel = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { nameof(this.DateTimeOffset), this.DateTimeOffset },
                { Constant.FileColumn.DeleteFlag, this.DeleteFlag },
                { nameof(this.FileName), this.FileName },
                { Constant.DatabaseColumn.ID, this.ID },
                { Constant.FileColumn.Classification, this.Classification },
                { Constant.FileColumn.RelativePath, this.RelativePath },
            };
            foreach (KeyValuePair<string, FileTableColumn> columnAndName in this.table.UserColumnsByName)
            {
                FileTableColumn userColumn = columnAndName.Value;
                object value;
                switch (userColumn.DataType)
                {
                    case SqlDataType.Boolean:
                        value = this.UserFlags[userColumn.DataIndex];
                        break;
                    case SqlDataType.Blob:
                        value = this.UserMarkerPositions[userColumn.DataIndex];
                        break;
                    case SqlDataType.Integer:
                        value = this.UserCounters[userColumn.DataIndex];
                        break;
                    case SqlDataType.String:
                        value = this.UserNotesAndChoices[userColumn.DataIndex];
                        break;
                    default:
                        throw new NotSupportedException(String.Format("Unhandled column data type {0}.", userColumn.DataIndex));
                }
                valuesByDataLabel[columnAndName.Key] = value;
            }
            return valuesByDataLabel;
        }

        public bool IsDisplayable()
        {
            if ((this.Classification == FileClassification.Corrupt) || (this.Classification == FileClassification.NoLongerAvailable))
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// Assuming this file's name in the format MMddnnnn, where nnnn is the image number, make a best effort to find MMddnnnn - 1.jpg
        /// and pull its EXIF date time field if it does.  It's assumed nnnn is ones based, rolls over at 0999, and the previous file
        /// is in the same directory.
        /// </summary>
        /// <remarks>
        /// This algorithm works for file numbering on Bushnell Trophy HD cameras using hybrid video, possibly others.
        /// </remarks>
        public bool IsPreviousJpegName(string fileName)
        {
            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(this.FileName);
            string previousFileNameWithoutExtension;
            if (fileNameWithoutExtension.EndsWith("0001"))
            {
                previousFileNameWithoutExtension = fileNameWithoutExtension.Substring(0, 4) + "0999";
            }
            else
            {
                if (Int32.TryParse(fileNameWithoutExtension, NumberStyles.None, Constant.InvariantCulture, out int fileNumber) == false)
                {
                    return false;
                }
                previousFileNameWithoutExtension = (fileNumber - 1).ToString("00000000", Constant.InvariantCulture);
            }

            string previousJpegName = previousFileNameWithoutExtension + Constant.File.JpgFileExtension;
            return String.Equals(previousJpegName, fileName, StringComparison.OrdinalIgnoreCase);
        }

        private void MarkersForCounter_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            MarkersForCounter markers = (MarkersForCounter)sender;
            this[markers.DataLabel] = markers.Count;
            this[FileTable.GetMarkerPositionColumnName(markers.DataLabel)] = markers.MarkerPositionsToFloatArray();
        }

        public void SetDateTimeOffsetFromFileInfo(FileInfo fileInfo)
        {
            // populate new image's default date and time
            // Typically the creation time is the time a file was created in the local file system and the last write time when it was
            // last modified ever in any file system.  So, for example, copying an image from a camera's SD card to a computer results
            // in the image file on the computer having a write time which is before its creation time.  Check both and take the lesser 
            // of the two to provide a best effort default.  In most cases it's desirable to see if a more accurate time can be obtained
            // from the image's EXIF metadata.
            Debug.Assert(fileInfo.Exists, "Attempt to read DateTimeOffset from nonexistent file.");
            DateTime earliestTimeLocal = fileInfo.CreationTime < fileInfo.LastWriteTime ? fileInfo.CreationTime : fileInfo.LastWriteTime;
            this.DateTimeOffset = new DateTimeOffset(earliestTimeLocal);
        }

        public void SetValuesFromSpreadsheet(FileTableSpreadsheetMap spreadsheetMap, List<string> row, FileImportResult result)
        {
            // obtain file's date time and UTC offset
            if (DateTimeHandler.TryParseDatabaseDateTime(row[spreadsheetMap.DateTimeIndex], out DateTime dateTime))
            {
                if (DateTimeHandler.TryParseDatabaseUtcOffset(row[spreadsheetMap.UtcOffsetIndex], out TimeSpan utcOffset))
                {
                    this.DateTimeOffset = DateTimeHandler.FromDatabaseDateTimeOffset(dateTime, utcOffset);
                }
                else
                {
                    result.Errors.Add(String.Format("Value '{0}' is not a valid UTC offset.  Neither the file's date time nor UTC offset will be updated.", row[spreadsheetMap.UtcOffsetIndex]));
                }
            }
            else
            {
                result.Errors.Add(String.Format("Value '{0}' is not a valid date time.  File's UTC offset will be ignored and neither its date time nor UTC offset will be updated.", row[spreadsheetMap.DateTimeIndex]));
            }

            // remaining standard controls
            if (spreadsheetMap.ClassificationIndex != -1)
            {
                if (ImageRow.TryParseFileClassification(row[spreadsheetMap.ClassificationIndex], out FileClassification classification))
                {
                    this.Classification = classification;
                }
                else
                {
                    result.Errors.Add(String.Format("Value '{0}' is not a valid file classification.", row[spreadsheetMap.ClassificationIndex]));
                }
            }

            if (spreadsheetMap.DeleteFlagIndex != -1)
            {
                // Excel uses "0" and "1", as does Carnassial .csv export.  "False" and "True" are also allowed, case insensitive.
                string flagAsString = row[spreadsheetMap.DeleteFlagIndex];
                if (flagAsString.Length == 1)
                {
                    char flagAsCharacter = flagAsString[0];
                    if (flagAsCharacter == Constant.Excel.False)
                    {
                        this.DeleteFlag = false;
                    }
                    else if (flagAsCharacter == Constant.Excel.True)
                    {
                        this.DeleteFlag = true;
                    }
                    else
                    {
                        result.Errors.Add(String.Format("Value '{0}' is not a valid delete flag.", flagAsString));
                    }
                }
                else if (Boolean.TryParse(flagAsString, out bool flag))
                {
                    this.DeleteFlag = flag;
                }
                else
                {
                    result.Errors.Add(String.Format("Value '{0}' is not a valid delete flag.", flagAsString));
                }
            }

            // get values of user defined columns
            // choices
            for (int indexIndex = 0; indexIndex < spreadsheetMap.UserChoiceIndices.Count; ++indexIndex)
            {
                int spreadsheetIndex = spreadsheetMap.UserChoiceIndices[indexIndex];
                int dataIndex = spreadsheetMap.UserChoiceDataIndices[indexIndex];
                List<string> choiceValues = spreadsheetMap.UserChoiceValues[indexIndex];
                string choice = row[spreadsheetIndex];

                if (choiceValues.Contains(choice, StringComparer.Ordinal))
                {
                    if (String.Equals(this.UserNotesAndChoices[dataIndex], choice, StringComparison.Ordinal) == false)
                    {
                        this.HasChanges |= true;
                        this.UserNotesAndChoices[dataIndex] = choice;
                        this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(spreadsheetMap.UserCounters[dataIndex].Control.DataLabel));
                    }
                }
                else
                {
                    result.Errors.Add(String.Format("Choice '{0}' is not a valid option for the column {1}.", choice, spreadsheetMap.UserChoices[indexIndex].Control.DataLabel));
                }
            }

            // counters
            for (int dataIndex = 0; dataIndex < spreadsheetMap.UserCounterIndices.Count; ++dataIndex)
            {
                int counterSpreadsheetIndex = spreadsheetMap.UserCounterIndices[dataIndex];
                string countAsString = row[counterSpreadsheetIndex];
                if (Int32.TryParse(countAsString, NumberStyles.AllowLeadingSign, Constant.InvariantCulture, out int count))
                {
                    if (this.UserCounters[dataIndex] != count)
                    {
                        this.HasChanges |= true;
                        this.UserCounters[dataIndex] = count;
                        this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(spreadsheetMap.UserCounters[dataIndex].Control.DataLabel));
                    }
                }
                else
                {
                    result.Errors.Add(String.Format("'{0}' is not an integer count for the column {1}.", countAsString, spreadsheetMap.UserCounters[dataIndex].Control.DataLabel));
                }

                int markerSpreadsheetIndex = spreadsheetMap.UserMarkerIndices[dataIndex];
                string positionsAsString = row[markerSpreadsheetIndex];
                if (MarkersForCounter.TryParseExcelStringToPackedFloats(positionsAsString, out byte[] packedFloats))
                {
                    if (this.ByteArraysEqual(this.UserMarkerPositions[dataIndex], packedFloats) == false)
                    {
                        this.HasChanges |= true;
                        this.UserMarkerPositions[dataIndex] = packedFloats;
                        this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(spreadsheetMap.UserCounters[dataIndex].Control.DataLabel));
                    }
                }
                else
                {
                    result.Errors.Add(String.Format("Marker position sequence '{0}' is not valid for the column {1}.", positionsAsString, spreadsheetMap.UserCounters[dataIndex].Control.DataLabel));
                }
            }

            // flags
            for (int dataIndex = 0; dataIndex < spreadsheetMap.UserFlagIndices.Count; ++dataIndex)
            {
                int spreadsheetIndexIndex = spreadsheetMap.UserFlagIndices[dataIndex];
                string flagAsString = row[spreadsheetIndexIndex];

                bool flag;
                if (flagAsString.Length == 1)
                {
                    char flagAsCharacter = flagAsString[0];
                    if (flagAsCharacter == Constant.Excel.False)
                    {
                        flag = false;
                    }
                    else if (flagAsCharacter == Constant.Excel.True)
                    {
                        flag = true;
                    }
                    else
                    {
                        result.Errors.Add(String.Format("'{0}' is not a valid value for the flag column {1}.  Flags must be either true or false, case insensitive.", flagAsString, spreadsheetMap.UserCounters[dataIndex].Control.DataLabel));
                        continue;
                    }
                }
                else if (Boolean.TryParse(flagAsString, out flag))
                {
                    // nothing further to do
                }
                else
                {
                    result.Errors.Add(String.Format("'{0}' is not a valid value for the flag column {1}.  Flags must be either true or false, case insensitive.", flagAsString, spreadsheetMap.UserCounters[dataIndex].Control.DataLabel));
                    continue;
                }

                if (this.UserFlags[dataIndex] != flag)
                {
                    this.HasChanges |= true;
                    this.UserFlags[dataIndex] = flag;
                    this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(spreadsheetMap.UserFlags[dataIndex].Control.DataLabel));
                }
            }

            // notes
            for (int indexIndex = 0; indexIndex < spreadsheetMap.UserNoteIndices.Count; ++indexIndex)
            {
                int dataIndex = spreadsheetMap.UserNoteDataIndices[indexIndex];
                int spreadsheetIndex = spreadsheetMap.UserNoteIndices[indexIndex];

                string note = row[spreadsheetIndex];
                if (String.Equals(this.UserNotesAndChoices[dataIndex], note, StringComparison.Ordinal) == false)
                {
                    this.HasChanges |= true;
                    this.UserNotesAndChoices[dataIndex] = note;
                    this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(spreadsheetMap.UserNotes[indexIndex].Control.DataLabel));
                }
            }
        }

        public static string ToString(FileClassification classification)
        {
            switch (classification)
            {
                case FileClassification.Color:
                    return "Color";
                case FileClassification.Corrupt:
                    return "Corrupt";
                case FileClassification.Dark:
                    return "Dark";
                case FileClassification.Greyscale:
                    return "Greyscale";
                case FileClassification.NoLongerAvailable:
                    return "NoLongerAvailable";
                case FileClassification.Video:
                    return "Video";
                default:
                    throw new NotSupportedException(String.Format("Unhandled classification {0}.", classification));
            }
        }

        public async Task<CachedImage> TryLoadImageAsync(string imageSetFolderPath)
        {
            return await this.TryLoadImageAsync(imageSetFolderPath, null);
        }

        public async virtual Task<CachedImage> TryLoadImageAsync(string imageSetFolderPath, Nullable<int> expectedDisplayWidth)
        {
            // 8MP average performance (n ~= 200), milliseconds
            // scale factor  1.0  1/2   1/4    1/8
            //               110  76.3  55.9   46.1
            // Stopwatch stopwatch = new Stopwatch();
            // stopwatch.Start();
            FileInfo jpeg = this.GetFileInfo(imageSetFolderPath);
            if (jpeg.Exists == false)
            {
                return new CachedImage()
                {
                    FileNoLongerAvailable = true
                };
            }

            using (FileStream stream = new FileStream(jpeg.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                if (stream.Length < Constant.Images.SmallestValidJpegSizeInBytes)
                {
                    return null;
                }

                byte[] buffer = new byte[stream.Length];
                await stream.ReadAsync(buffer, 0, buffer.Length);
                MemoryImage image = new MemoryImage(buffer, expectedDisplayWidth);
                // stopwatch.Stop();
                // Trace.WriteLine(stopwatch.Elapsed.ToString("s\\.fffffff"));
                return new CachedImage(image);
            }
        }

        public bool TryMoveFileToFolder(string imageSetFolderPath, string destinationFolderPath)
        {
            string sourceFilePath = this.GetFilePath(imageSetFolderPath);
            if (!File.Exists(sourceFilePath))
            {
                // nothing to do if the source file doesn't exist
                return false;
            }

            string destinationFilePath = Path.Combine(destinationFolderPath, this.FileName);
            if (String.Equals(sourceFilePath, destinationFilePath, StringComparison.OrdinalIgnoreCase))
            {
                // nothing to do if the source and destination locations are the same
                return true;
            }

            if (File.Exists(destinationFilePath))
            {
                // can't move file since one with the same name already exists at the destination
                return false;
            }

            // the file can't be moved if there's an open FileStream on it, so close any such stream
            try
            {
                File.Move(sourceFilePath, destinationFilePath);
            }
            catch (IOException exception)
            {
                Debug.Fail(exception.ToString());
                return false;
            }

            string relativePath = NativeMethods.GetRelativePathFromDirectoryToDirectory(imageSetFolderPath, destinationFolderPath);
            if (String.Equals(relativePath, String.Empty, StringComparison.Ordinal) || String.Equals(relativePath, ".", StringComparison.Ordinal))
            {
                relativePath = null;
            }
            this.RelativePath = relativePath;
            return true;
        }

        public static bool TryParseFileClassification(string fileClassificationAsString, out FileClassification classification)
        {
            if (String.Equals("Color", fileClassificationAsString, StringComparison.Ordinal) ||
                String.Equals("Ok", fileClassificationAsString, StringComparison.Ordinal))
            {
                classification = FileClassification.Color;
            }
            else if (String.Equals("Greyscale", fileClassificationAsString, StringComparison.Ordinal))
            {
                classification = FileClassification.Greyscale;
            }
            else if (String.Equals("Video", fileClassificationAsString, StringComparison.Ordinal))
            {
                classification = FileClassification.Video;
            }
            else if (String.Equals("Corrupt", fileClassificationAsString, StringComparison.Ordinal))
            {
                classification = FileClassification.Corrupt;
            }
            else if (String.Equals("Dark", fileClassificationAsString, StringComparison.Ordinal))
            {
                classification = FileClassification.Dark;
            }
            else if (String.Equals("NoLongerAvailable", fileClassificationAsString, StringComparison.Ordinal))
            {
                classification = FileClassification.NoLongerAvailable;
            }
            else
            {
                classification = default(FileClassification);
                return false;
            }

            return true;
        }

        public MetadataReadResult TryReadDateTimeFromMetadata(IReadOnlyList<MetadataDirectory> metadataDirectories, TimeZoneInfo imageSetTimeZone)
        {
            ExifSubIfdDirectory exifSubIfd = metadataDirectories.OfType<ExifSubIfdDirectory>().FirstOrDefault();
            if (exifSubIfd == null)
            {
                // no EXIF information and no other plausible source of metadata
                return MetadataReadResult.None;
            }

            object dateTimeOriginalAsObject = exifSubIfd.GetObject(ExifSubIfdDirectory.TagDateTimeOriginal);
            if (dateTimeOriginalAsObject == null)
            {
                // camera doesn't provide an EXIF standard DateTimeOriginal
                // check for a Reconyx makernote
                ReconyxHyperFireMakernoteDirectory hyperfireMakernote = metadataDirectories.OfType<ReconyxHyperFireMakernoteDirectory>().FirstOrDefault();
                if (hyperfireMakernote != null)
                {
                    dateTimeOriginalAsObject = hyperfireMakernote.GetObject(ReconyxHyperFireMakernoteDirectory.TagDateTimeOriginal);
                }
                else
                {
                    ReconyxUltraFireMakernoteDirectory ultrafireMakernote = metadataDirectories.OfType<ReconyxUltraFireMakernoteDirectory>().FirstOrDefault();
                    if (ultrafireMakernote != null)
                    {
                        dateTimeOriginalAsObject = ultrafireMakernote.GetObject(ReconyxUltraFireMakernoteDirectory.TagDateTimeOriginal);
                    }
                }
            }

            // work around https://github.com/drewnoakes/metadata-extractor-dotnet/issues/138
            if (dateTimeOriginalAsObject == null)
            {
                return MetadataReadResult.None;
            }
            else if (dateTimeOriginalAsObject is DateTime dateTimeOriginalAsDateTime)
            {
                this.DateTimeOffset = DateTimeHandler.CreateDateTimeOffset(dateTimeOriginalAsDateTime, imageSetTimeZone);
                return MetadataReadResult.DateTime;
            }
            else if (dateTimeOriginalAsObject is StringValue dateTimeOriginalAsStringValue)
            {
                if (DateTimeHandler.TryParseMetadataDateTaken(dateTimeOriginalAsStringValue.ToString(), imageSetTimeZone, out DateTimeOffset dateTimeOriginal))
                {
                    this.DateTimeOffset = dateTimeOriginal;
                    return MetadataReadResult.DateTime;
                }
                else
                {
                    return MetadataReadResult.None;
                }
            }
            else
            {
                throw new NotSupportedException("Unhandled DateTimeOriginal type " + dateTimeOriginalAsObject.GetType() + ".");
            }
        }
    }
}
