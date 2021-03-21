using Carnassial.Control;
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

        public event PropertyChangedEventHandler? PropertyChanged;

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

        public object? this[string propertyName]
        {
            get
            {
                switch (propertyName)
                {
                    case Constant.FileColumn.DateTime:
                        throw new NotSupportedException(App.FindResource<string>(Constant.ResourceKey.ImageRowSetDateTimeThroughDateTimeOffset));
                    case nameof(this.DateTimeOffset):
                        return this.DateTimeOffset;
                    case Constant.FileColumn.DeleteFlag:
                        return this.DeleteFlag;
                    case Constant.FileColumn.File:
                        throw new NotSupportedException(App.FindResource<string>(Constant.ResourceKey.ImageRowSetFileNameThroughFileName));
                    case nameof(ImageRow.FileName):
                        return this.FileName;
                    case Constant.DatabaseColumn.ID:
                        return this.ID;
                    case Constant.FileColumn.Classification:
                        return this.Classification;
                    case Constant.FileColumn.RelativePath:
                        return this.RelativePath;
                    case Constant.FileColumn.UtcOffset:
                        throw new NotSupportedException(App.FindResource<string>(Constant.ResourceKey.ImageRowSetUtcOffsetThroughDateTimeOffset));
                    default:
                        FileTableColumn userColumn = this.table.UserColumnsByName[propertyName];
                        return userColumn.DataType switch
                        {
                            SqlDataType.Boolean => this.UserFlags[userColumn.DataIndex],
                            SqlDataType.Blob => this.UserMarkerPositions[userColumn.DataIndex],
                            SqlDataType.Integer => this.UserCounters[userColumn.DataIndex],
                            SqlDataType.String => this.UserNotesAndChoices[userColumn.DataIndex],
                            _ => throw new NotSupportedException(String.Format(CultureInfo.CurrentCulture, "Unhandled column data type {0}.", userColumn.DataIndex)),
                        };
                }
            }
            set
            {
                switch (propertyName)
                {
                    // standard controls
                    // Property change notification is sent from the properties called.
                    case Constant.FileColumn.DateTime:
                        throw new NotSupportedException(App.FindResource<string>(Constant.ResourceKey.ImageRowSetDateTimeThroughDateTimeOffset));
                    case nameof(this.DateTimeOffset):
                        this.DateTimeOffset = (DateTimeOffset)(value ?? throw new ArgumentNullException(nameof(value), "Datetime offset cannot be null."));
                        break;
                    case Constant.FileColumn.DeleteFlag:
                        this.DeleteFlag = (bool)(value ?? throw new ArgumentNullException(nameof(value), "Delete flag cannot be null."));
                        break;
                    case Constant.FileColumn.File:
                        throw new NotSupportedException(App.FindResource<string>(Constant.ResourceKey.ImageRowSetFileNameThroughFileName));
                    case nameof(this.FileName):
                        this.FileName = (string)(value ?? throw new ArgumentNullException(nameof(value), "File name cannot be null."));
                        break;
                    case Constant.DatabaseColumn.ID:
                        throw new NotSupportedException(App.FindResource<string>(Constant.ResourceKey.ImageRowIDImmutable));
                    case Constant.FileColumn.Classification:
                        this.Classification = (FileClassification)(value ?? throw new ArgumentNullException(nameof(value), "File classification cannot be null"));
                        break;
                    case Constant.FileColumn.RelativePath:
                        this.RelativePath = (string)(value ?? throw new ArgumentNullException(nameof(value), "Felative path cannot be null.")); 
                        break;
                    case Constant.FileColumn.UtcOffset:
                        throw new NotSupportedException(App.FindResource<string>(Constant.ResourceKey.ImageRowSetUtcOffsetThroughDateTimeOffset));
                    // user defined controls
                    default:
                        FileTableColumn userColumn = this.table.UserColumnsByName[propertyName];
                        bool valueDifferent = false;
                        switch (userColumn.DataType)
                        {
                            case SqlDataType.Boolean:
                                bool valueAsBool = (bool)(value ?? throw new ArgumentNullException(nameof(value), String.Format("Boolean value for '{0}' cannot be null.", propertyName)));
                                valueDifferent = this.UserFlags[userColumn.DataIndex] != valueAsBool;
                                this.UserFlags[userColumn.DataIndex] = valueAsBool;
                                break;
                            case SqlDataType.Blob:
                                byte[] valueAsByteArray = (byte[])(value ?? throw new ArgumentNullException(nameof(value), String.Format("Blob value for '{0}' cannot be null.", propertyName))); ;
                                valueDifferent = ImageRow.ByteArraysEqual(this.UserMarkerPositions[userColumn.DataIndex], valueAsByteArray) == false;
                                this.UserMarkerPositions[userColumn.DataIndex] = valueAsByteArray;
                                break;
                            case SqlDataType.Integer:
                                int valueAsInt = (int)(value ?? throw new ArgumentNullException(nameof(value), String.Format("Integer value for '{0}' cannot be null.", propertyName)));
                                valueDifferent = this.UserCounters[userColumn.DataIndex] != valueAsInt;
                                this.UserCounters[userColumn.DataIndex] = valueAsInt;
                                break;
                            case SqlDataType.String:
                                string valueAsString = (string)(value ?? throw new ArgumentNullException(nameof(value), String.Format("String value for '{0}' cannot be null.", propertyName)));
                                valueDifferent = String.Equals(this.UserNotesAndChoices[userColumn.DataIndex], valueAsString, StringComparison.Ordinal) == false;
                                this.UserNotesAndChoices[userColumn.DataIndex] = valueAsString;
                                break;
                            default:
                                throw new NotSupportedException(String.Format(CultureInfo.CurrentCulture, "Unhandled column data type {0}.", userColumn.DataType));
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

        private static bool ByteArraysEqual(byte[] currentValue, byte[] newValue)
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

        public object? GetDatabaseValue(string dataLabel)
        {
            switch (dataLabel)
            {
                case Constant.FileColumn.DateTime:
                    return this.UtcDateTime;
                case nameof(this.DateTimeOffset):
                    throw new NotSupportedException(String.Format(CultureInfo.CurrentCulture, "Database values for {0} and {1} must be accessed through {2}.", nameof(this.UtcDateTime), nameof(this.UtcOffset), nameof(this.DateTimeOffset)));
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
                    return userColumn.DataType switch
                    {
                        SqlDataType.Boolean => this.UserFlags[userColumn.DataIndex],
                        SqlDataType.Blob => this.UserMarkerPositions[userColumn.DataIndex],
                        SqlDataType.Integer => this.UserCounters[userColumn.DataIndex],
                        SqlDataType.String => this.UserNotesAndChoices[userColumn.DataIndex],
                        _ => throw new NotSupportedException(String.Format(CultureInfo.CurrentCulture, "Unhandled column data type {0}.", userColumn.DataIndex)),
                    };
            }
        }

        public static string GetDataBindingPath(ControlRow control)
        {
            string dataLabel = control.DataLabel;
            Debug.Assert(dataLabel != Constant.FileColumn.UtcOffset, String.Format(CultureInfo.InvariantCulture, "Display and editing of UTC offset should be integrated into {0}.", nameof(DataEntryDateTimeOffset)));

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

        public static Dictionary<string, object?> GetDefaultValues(FileDatabase fileDatabase)
        {
            Dictionary<string, object?> defaultValues = new(StringComparer.Ordinal);
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
                            throw new ArgumentOutOfRangeException(control.DataLabel, String.Format(CultureInfo.CurrentCulture, "'{0}' is not a valid classification.", control.DefaultValue));
                        }
                        break;
                    // not currently supported
                    // case SqlDataType.DateTime:
                    //     dateTime = DateTimeHandler.ParseDatabaseDateTime(control.DefaultValue);
                    //     continue;
                    case SqlDataType.Integer:
                        if (String.Equals(control.DataLabel, Constant.FileColumn.Classification, StringComparison.Ordinal))
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
                        throw new NotSupportedException(String.Format(CultureInfo.CurrentCulture, "Unhandled SQL data type {0} for column '{1}'.", dataType, control.DataLabel));
                }

                defaultValues.Add(control.DataLabel, defaultValue);
                if (control.ControlType == ControlType.Counter)
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

        public string? GetDisplayString(DataEntryControl control)
        {
            switch (control.PropertyName)
            {
                case Constant.FileColumn.DateTime:
                    throw new NotSupportedException(String.Format(CultureInfo.CurrentCulture, "Control has unexpected property name {0}.", Constant.FileColumn.DateTime));
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
                    return userColumn.DataType switch
                    {
                        SqlDataType.Boolean => this.UserFlags[userColumn.DataIndex] ? Boolean.TrueString : Boolean.FalseString,
                        SqlDataType.Integer => this.UserCounters[userColumn.DataIndex].ToString(Constant.InvariantCulture),
                        SqlDataType.String => this.UserNotesAndChoices[userColumn.DataIndex],
                        _ => throw new NotSupportedException(String.Format(CultureInfo.CurrentCulture, "Unhandled column data type {0}.", userColumn.DataType))
                    };
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

            MarkersForCounter markersForCounter = new(counterDataLabel, this.UserCounters[counterColumn.DataIndex]);
            markersForCounter.MarkerPositionsFromFloatArray(this.UserMarkerPositions[markerColumn.DataIndex]);
            markersForCounter.PropertyChanged += this.MarkersForCounter_PropertyChanged;
            return markersForCounter;
        }

        public static string GetPropertyName(string dataLabel)
        {
            Debug.Assert(dataLabel != Constant.FileColumn.UtcOffset, String.Format(CultureInfo.InvariantCulture, "UTC offset should be accessed by {0}.", nameof(ImageRow.DateTimeOffset)));

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

        public string? GetSpreadsheetString(string dataLabel)
        {
            switch (dataLabel)
            {
                case Constant.FileColumn.DateTime:
                    return DateTimeHandler.ToDatabaseDateTimeString(this.UtcDateTime);
                case Constant.FileColumn.DeleteFlag:
                    return this.DeleteFlag ? Constant.Sql.TrueString : Constant.Sql.FalseString;
                case nameof(this.DateTimeOffset):
                    throw new NotSupportedException(String.Format(CultureInfo.CurrentCulture, "Unexpected data label {0}.", nameof(this.DateTimeOffset)));
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
                            throw new NotSupportedException(String.Format(CultureInfo.CurrentCulture, "Unhandled column data type {0}.", userColumn.DataType));
                    }
            }
        }

        public Dictionary<string, object?> GetValues()
        {
            // UtcDateTime and UtcOffset aren't included as they're portions of DateTimeOffset
            Dictionary<string, object?> valuesByDataLabel = new(StringComparer.Ordinal)
            {
                { nameof(this.DateTimeOffset), this.DateTimeOffset },
                { Constant.FileColumn.DeleteFlag, this.DeleteFlag },
                { nameof(this.FileName), this.FileName },
                { Constant.DatabaseColumn.ID, this.ID },
                { Constant.FileColumn.Classification, this.Classification },
                { Constant.FileColumn.RelativePath, this.RelativePath }
            };
            foreach (KeyValuePair<string, FileTableColumn> columnAndName in this.table.UserColumnsByName)
            {
                FileTableColumn userColumn = columnAndName.Value;
                object value = userColumn.DataType switch
                {
                    SqlDataType.Boolean => this.UserFlags[userColumn.DataIndex],
                    SqlDataType.Blob => this.UserMarkerPositions[userColumn.DataIndex],
                    SqlDataType.Integer => this.UserCounters[userColumn.DataIndex],
                    SqlDataType.String => this.UserNotesAndChoices[userColumn.DataIndex],
                    _ => throw new NotSupportedException(String.Format(CultureInfo.CurrentCulture, "Unhandled column data type {0}.", userColumn.DataIndex)),
                };
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
            if (fileNameWithoutExtension.EndsWith("0001", StringComparison.Ordinal))
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

        private void MarkersForCounter_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender == null)
            {
                throw new ArgumentNullException(nameof(sender), "Expected source of change notification to be specified.");
            }

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

        public void SetValues(ImageRow other, FileTableColumnMap fileTableMap, FileImportResult result)
        {
            Debug.Assert(String.Equals(this.FileName, other.FileName, StringComparison.OrdinalIgnoreCase), "Unexpected file name mismatch.");

            // sync standard controls
            this.DateTimeOffset = other.DateTimeOffset;
            this.Classification = other.Classification;
            this.DeleteFlag = other.DeleteFlag;

            // sync user controls
            // As with the standard controls, all values are either value types or immutable reference types, so only shallow 
            // copies are required.
            // counters
            for (int dataIndex = 0; dataIndex < this.UserCounters.Length; ++dataIndex)
            {
                if (this.UserCounters[dataIndex] != other.UserCounters[dataIndex])
                {
                    this.UserCounters[dataIndex] = other.UserCounters[dataIndex];
                    this.HasChanges |= true;
                    this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(fileTableMap.UserCounters[dataIndex].Control.DataLabel));
                }
                if (ImageRow.ByteArraysEqual(this.UserMarkerPositions[dataIndex], other.UserMarkerPositions[dataIndex]) == false)
                {
                    this.UserMarkerPositions[dataIndex] = other.UserMarkerPositions[dataIndex];
                    this.HasChanges |= true;
                    this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(fileTableMap.UserCounters[dataIndex].Control.DataLabel));
                }
            }

            // flags
            for (int dataIndex = 0; dataIndex < this.UserFlags.Length; ++dataIndex)
            {
                if (this.UserFlags[dataIndex] != other.UserFlags[dataIndex])
                {
                    this.UserFlags[dataIndex] = other.UserFlags[dataIndex];
                    this.HasChanges |= true;
                    this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(fileTableMap.UserFlags[dataIndex].Control.DataLabel));
                }
            }

            // notes and choices
            for (int dataIndex = 0; dataIndex < this.UserNotesAndChoices.Length; ++dataIndex)
            {
                string value = other.UserNotesAndChoices[dataIndex];
                if (String.Equals(this.UserNotesAndChoices[dataIndex], value, StringComparison.Ordinal) == false)
                {
                    List<string> choiceValues = fileTableMap.UserNoteAndChoiceValues[dataIndex];
                    if ((choiceValues == null) || choiceValues.Contains(value, StringComparer.Ordinal))
                    {
                        this.UserNotesAndChoices[dataIndex] = other.UserNotesAndChoices[dataIndex];
                        this.HasChanges |= true;
                        this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(fileTableMap.UserNotesAndChoices[dataIndex].Control.DataLabel));
                    }
                    else
                    {
                        result.Errors.Add(App.FormatResource(Constant.ResourceKey.ImageRowImportInvalidChoice, value, fileTableMap.UserNotesAndChoices[dataIndex].Control.DataLabel));
                    }
                }
            }
        }

        public void SetValuesFromSpreadsheet(List<string> row, FileTableSpreadsheetMap spreadsheetMap, FileImportResult result)
        {
            // obtain file's date time and UTC offset
            if (DateTimeHandler.TryParseDatabaseDateTime(row[spreadsheetMap.DateTimeSpreadsheetIndex], out DateTime dateTime))
            {
                if (DateTimeHandler.TryParseDatabaseUtcOffset(row[spreadsheetMap.UtcOffsetSpreadsheetIndex], out TimeSpan utcOffset))
                {
                    this.DateTimeOffset = DateTimeHandler.FromDatabaseDateTimeOffset(dateTime, utcOffset);
                }
                else
                {
                    result.Errors.Add(App.FormatResource(Constant.ResourceKey.ImageRowImportInvalidUtcOffset, row[spreadsheetMap.UtcOffsetSpreadsheetIndex]));
                }
            }
            else
            {
                result.Errors.Add(App.FormatResource(Constant.ResourceKey.ImageRowImportInvalidDateTime, row[spreadsheetMap.DateTimeSpreadsheetIndex]));
            }

            // remaining standard controls
            if (spreadsheetMap.ClassificationSpreadsheetIndex != -1)
            {
                if (ImageRow.TryParseFileClassification(row[spreadsheetMap.ClassificationSpreadsheetIndex], out FileClassification classification))
                {
                    this.Classification = classification;
                }
                else
                {
                    result.Errors.Add(App.FormatResource(Constant.ResourceKey.ImageRowImportInvalidClassification, row[spreadsheetMap.ClassificationSpreadsheetIndex]));
                }
            }

            if (spreadsheetMap.DeleteFlagSpreadsheetIndex != -1)
            {
                // Excel uses "0" and "1", as does Carnassial .csv export.  "False" and "True" are also allowed, case insensitive.
                string flagAsString = row[spreadsheetMap.DeleteFlagSpreadsheetIndex];
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
                        result.Errors.Add(App.FormatResource(Constant.ResourceKey.ImageRowImportInvalidDeleteFlag, flagAsString));
                    }
                }
                else if (Boolean.TryParse(flagAsString, out bool flag))
                {
                    this.DeleteFlag = flag;
                }
                else
                {
                    result.Errors.Add(App.FormatResource(Constant.ResourceKey.ImageRowImportInvalidDeleteFlag, flagAsString));
                }
            }

            // get values of user defined columns
            // counters
            for (int dataIndex = 0; dataIndex < spreadsheetMap.UserCounterSpreadsheetIndices.Count; ++dataIndex)
            {
                int counterSpreadsheetIndex = spreadsheetMap.UserCounterSpreadsheetIndices[dataIndex];
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
                    result.Errors.Add(App.FormatResource(Constant.ResourceKey.ImageRowImportInvalidCount, countAsString, spreadsheetMap.UserCounters[dataIndex].Control.DataLabel));
                }

                int markerSpreadsheetIndex = spreadsheetMap.UserMarkerSpreadsheetIndices[dataIndex];
                string positionsAsString = row[markerSpreadsheetIndex];
                if (MarkersForCounter.TryParseExcelStringToPackedFloats(positionsAsString, out byte[] packedFloats))
                {
                    if (ImageRow.ByteArraysEqual(this.UserMarkerPositions[dataIndex], packedFloats) == false)
                    {
                        this.HasChanges |= true;
                        this.UserMarkerPositions[dataIndex] = packedFloats;
                        this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(spreadsheetMap.UserCounters[dataIndex].Control.DataLabel));
                    }
                }
                else
                {
                    result.Errors.Add(App.FormatResource(Constant.ResourceKey.ImageRowImportInvalidMarkerPosition, positionsAsString, spreadsheetMap.UserCounters[dataIndex].Control.DataLabel));
                }
            }

            // flags
            for (int dataIndex = 0; dataIndex < spreadsheetMap.UserFlagSpreadsheetIndices.Count; ++dataIndex)
            {
                int spreadsheetIndexIndex = spreadsheetMap.UserFlagSpreadsheetIndices[dataIndex];
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
                        result.Errors.Add(App.FormatResource(Constant.ResourceKey.ImageRowImportInvalidFlag, flagAsString, spreadsheetMap.UserFlags[dataIndex].Control.DataLabel));
                        continue;
                    }
                }
                else if (Boolean.TryParse(flagAsString, out flag))
                {
                    // nothing further to do
                }
                else
                {
                    result.Errors.Add(App.FormatResource(Constant.ResourceKey.ImageRowImportInvalidFlag, flagAsString, spreadsheetMap.UserFlags[dataIndex].Control.DataLabel));
                    continue;
                }

                if (this.UserFlags[dataIndex] != flag)
                {
                    this.HasChanges |= true;
                    this.UserFlags[dataIndex] = flag;
                    this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(spreadsheetMap.UserFlags[dataIndex].Control.DataLabel));
                }
            }

            // notes and choices
            for (int dataIndex = 0; dataIndex < spreadsheetMap.UserNoteAndChoiceSpreadsheetIndices.Count; ++dataIndex)
            {
                int spreadsheetIndex = spreadsheetMap.UserNoteAndChoiceSpreadsheetIndices[dataIndex];

                string value = row[spreadsheetIndex];
                if (String.Equals(this.UserNotesAndChoices[dataIndex], value, StringComparison.Ordinal) == false)
                {
                    List<string> choiceValues = spreadsheetMap.UserNoteAndChoiceValues[dataIndex];
                    if ((choiceValues == null) || choiceValues.Contains(value, StringComparer.Ordinal))
                    {
                        this.HasChanges |= true;
                        this.UserNotesAndChoices[dataIndex] = value;
                        this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(spreadsheetMap.UserNotesAndChoices[dataIndex].Control.DataLabel));
                    }
                    else
                    {
                        result.Errors.Add(App.FormatResource(Constant.ResourceKey.ImageRowImportInvalidChoice, value, spreadsheetMap.UserNotesAndChoices[dataIndex].Control.DataLabel));
                    }
                }
            }
        }

        public static string ToString(FileClassification classification)
        {
            return classification switch
            {
                FileClassification.Color => "Color",
                FileClassification.Corrupt => "Corrupt",
                FileClassification.Dark => "Dark",
                FileClassification.Greyscale => "Greyscale",
                FileClassification.NoLongerAvailable => "NoLongerAvailable",
                FileClassification.Video => "Video",
                _ => throw new NotSupportedException(String.Format(CultureInfo.CurrentCulture, "Unhandled classification {0}.", classification)),
            };
        }

        public async Task<CachedImage> TryLoadImageAsync(string imageSetFolderPath)
        {
            return await this.TryLoadImageAsync(imageSetFolderPath, null).ConfigureAwait(true);
        }

        public async virtual Task<CachedImage> TryLoadImageAsync(string imageSetFolderPath, int? expectedDisplayWidth)
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

            using FileStream stream = new(jpeg.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
            if (stream.Length < Constant.Images.SmallestValidJpegSizeInBytes)
            {
                return new CachedImage()
                {
                    ImageNotDecodable = true
                };
            }

            byte[] buffer = new byte[stream.Length];
            await stream.ReadAsync(buffer.AsMemory(0, buffer.Length)).ConfigureAwait(true);
            MemoryImage image = new(buffer, expectedDisplayWidth);
            // stopwatch.Stop();
            // Trace.WriteLine(stopwatch.Elapsed.ToString("s\\.fffffff"));
            return new CachedImage(image);
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

            string? relativePath = NativeMethods.GetRelativePathFromDirectoryToDirectory(imageSetFolderPath, destinationFolderPath);
            if (String.IsNullOrEmpty(relativePath))
            {
                relativePath = ".";
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
                classification = default;
                return false;
            }

            return true;
        }

        public MetadataReadResults TryReadDateTimeFromMetadata(IReadOnlyList<MetadataDirectory> metadataDirectories, TimeZoneInfo imageSetTimeZone)
        {
            ExifSubIfdDirectory? exifSubIfd = metadataDirectories.OfType<ExifSubIfdDirectory>().FirstOrDefault();
            if (exifSubIfd == null)
            {
                // no EXIF information and no other plausible source of metadata
                return MetadataReadResults.None;
            }

            object? dateTimeOriginalAsObject = exifSubIfd.GetObject(ExifSubIfdDirectory.TagDateTimeOriginal);
            if (dateTimeOriginalAsObject == null)
            {
                // camera doesn't provide an EXIF standard DateTimeOriginal
                // check for a Reconyx makernote
                ReconyxHyperFireMakernoteDirectory? hyperfireMakernote = metadataDirectories.OfType<ReconyxHyperFireMakernoteDirectory>().FirstOrDefault();
                if (hyperfireMakernote != null)
                {
                    dateTimeOriginalAsObject = hyperfireMakernote.GetObject(ReconyxHyperFireMakernoteDirectory.TagDateTimeOriginal);
                }
                else
                {
                    ReconyxUltraFireMakernoteDirectory? ultrafireMakernote = metadataDirectories.OfType<ReconyxUltraFireMakernoteDirectory>().FirstOrDefault();
                    if (ultrafireMakernote != null)
                    {
                        dateTimeOriginalAsObject = ultrafireMakernote.GetObject(ReconyxUltraFireMakernoteDirectory.TagDateTimeOriginal);
                    }
                }
            }

            // work around https://github.com/drewnoakes/metadata-extractor-dotnet/issues/138
            if (dateTimeOriginalAsObject == null)
            {
                return MetadataReadResults.None;
            }
            else if (dateTimeOriginalAsObject is DateTime dateTimeOriginalAsDateTime)
            {
                this.DateTimeOffset = DateTimeHandler.CreateDateTimeOffset(dateTimeOriginalAsDateTime, imageSetTimeZone);
                return MetadataReadResults.DateTime;
            }
            else if (dateTimeOriginalAsObject is StringValue dateTimeOriginalAsStringValue)
            {
                if (DateTimeHandler.TryParseMetadataDateTaken(dateTimeOriginalAsStringValue.ToString(), imageSetTimeZone, out DateTimeOffset dateTimeOriginal))
                {
                    this.DateTimeOffset = dateTimeOriginal;
                    return MetadataReadResults.DateTime;
                }
                else
                {
                    return MetadataReadResults.None;
                }
            }
            else
            {
                throw new NotSupportedException("Unhandled DateTimeOriginal type " + dateTimeOriginalAsObject.GetType() + ".");
            }
        }
    }
}
