﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Media.Imaging;

namespace Carnassial
{
    public static class Constant
    {
        // cases in CarnassialWindow.Window_PreviewKeyDown must be kept in sync with the number of analysis slots
        public const int AnalysisSlots = 9;

        public const string ApplicationName = "Carnassial";
        public const string Debug = "DEBUG";
        public const int LargeNumberOfFilesToDelete = 100;
        public const string MainWindowBaseTitle = "Carnassial: Simplifying Remote Camera Data";
        public const int MaximumUndoableCommands = 100;
        public const int NumberOfMostRecentDatabasesToTrack = 9;
        public const double PageUpDownNavigationFraction = 0.1;

        public static readonly TimeSpan CheckForUpdateInterval = TimeSpan.FromDays(1.25);
        public static readonly Version Windows8MinimumVersion = new Version(6, 2, 0, 0);

        public static class ApplicationSettings
        {
            public const string DevTeamEmail = "devTeamEmail";
            public const string GithubOrganizationAndRepo = "githubOrganizationAndRepo";
        }

        public static class Assembly
        {
            public const string Kernel32 = "kernel32.dll";
            public const string Shell32 = "shell32.dll";
            public const string Shlwapi = "shlwapi.dll";
        }

        public static class ComGuid
        {
            public const string IFileOperation = "947aab5f-0a5c-4c13-b4d6-4bf7836fc9f8";
            public const string IFileOperationProgressSink = "04b0f1a7-9490-44bc-96e1-4296a31252e2";
            public const string IShellItem = "43826d1e-e718-42ee-bc55-a1e261c37bfe";

            public static readonly Guid IFileOperationClsid = new Guid("3ad05575-8857-4850-9277-11b85bdb8e09");
        }

        public static class Control
        {
            // columns unique to the template table
            public const string ControlOrder = "ControlOrder";
            public const string Copyable = "Copyable";     // whether the content of this item should be copied from previous values
            public const string DataLabel = "DataLabel";   // if not empty, its used instead of the label as the header for the column when writing the spreadsheet
            public const string DefaultValue = "DefaultValue"; // a default value for that code
            public const string Label = "Label";           // a label used to describe that code
            public const string List = "List";             // a fixed list of choices
            public const string SpreadsheetOrder = "SpreadsheetOrder";
            public const string Tooltip = "Tooltip";       // the tooltip text that describes the code
            public const string Type = "Type";             // the data type
            public const string Visible = "Visible";       // whether an item should be visible (used by standard items)
            public const string Width = "Width";           // the width of the textbox

            // hotkey characters in use by the top level of Carnassial's main menu or the three data entry buttons and therefore unavailable as control 
            // shortcuts in either upper or lower case
            public const string ReservedHotKeys = "FfEeOoVvSsHhPp12";

            public static readonly ReadOnlyCollection<string> StandardControls = new List<string>()
            {
                Constant.DatabaseColumn.DateTime,
                Constant.DatabaseColumn.DeleteFlag,
                Constant.DatabaseColumn.File,
                Constant.DatabaseColumn.ImageQuality,
                Constant.DatabaseColumn.RelativePath,
                Constant.DatabaseColumn.UtcOffset
            }.AsReadOnly();
        }

        // see also ControlLabelStyle and ControlContentStyle
        public static class ControlStyle
        {
            public const string ContainerStyle = "ContainerStyle";
        }

        public static class ControlDefault
        {
            // general defaults
            public const int MaxWidth = 500;
            public const string Value = "";

            // user defined controls
            public const string CounterTooltip = "Click the counter button, then click on the image to count the entity. Or just type in a count";
            public const string CounterValue = "0";              // Default for: counters
            public const string FixedChoiceTooltip = "Choose an item from the menu";

            public const string FlagTooltip = "Toggle between true and false";
            public const string FlagValue = "False"; // can't use Boolean.FalseString as it's not const
            public const string NoteTooltip = "Write a textual note";

            // standard controls
            public const string DateTimeTooltip = "Date and time taken";

            public const string FileTooltip = "The file name";
            public const string RelativePathTooltip = "Path from the folder containing the template and image data files to the file";

            public const string ImageQualityTooltip = "System-determined image quality: Ok, dark if mostly black, corrupted if it can not be read, missing if the image/video file is missing";

            public const string DeleteFlagLabel = "Delete?";    // a flag data type for marking deletion
            public const string DeleteFlagTooltip = "Mark a file as one to be deleted. You can then confirm deletion through the Edit Menu";

            public const string UtcOffsetTooltip = "Universal Time offset of the time zone for date and time taken";

            public static readonly DateTimeOffset DateTimeValue = new DateTimeOffset(1900, 1, 1, 12, 0, 0, 0, TimeSpan.Zero);
        }

        public static class Database
        {
            // default values
            public const long DefaultFileID = 1;
            public const string ImageSetDefaultLog = "Add text here";
            public const long InvalidID = -1;
            public const int InvalidRow = -1;

            // see performance remarks in FileDatabase.AddFiles()
            public const int NominalRowsPerTransactionFill = 2500;
            public const int RowsPerTransaction = 5000;

            // special characters
            // separator used to separate marker points in the database i.e. "2.3,5.6 | 7.1, 3.3"
            public const char BarDelimiter = '|';
        }

        public static class DatabaseColumn
        {
            public const string ID = "Id";

            // columns in FileData
            public const string DateTime = "DateTime";
            public const string File = "File";
            public const string ImageQuality = "ImageQuality";
            public const string DeleteFlag = "DeleteFlag";
            public const string RelativePath = "RelativePath";
            public const string UtcOffset = "UtcOffset";

            // columns in ImageSet
            public const string FileSelection = "FileSelection";
            public const string InitialFolderName = "InitialFolderName";
            public const string Log = "Log";
            public const string MostRecentFileID = "MostRecentFileID";
            public const string Options = "Options";
            public const string TimeZone = "TimeZone";
        }

        public static class DatabaseTable
        {
            public const string Controls = "Controls"; // table containing controls
            public const string FileData = "FileData";     // table containing image and video data
            public const string ImageSet = "ImageSet"; // table containing information common to the entire image set
        }

        public static class Excel
        {
            public const string Extension = ".xlsx";
            public const string FileDataWorksheetName = "file data";
            public const double MinimumColumnWidth = 5.0;
            public const double MaximumColumnWidth = 40.0;
        }

        public static class Exif
        {
            public const int MaxMetadataExtractorIssue35Offset = 12;
            public const string JpegCompression = "JPEG";
        }

        public static class File
        {
            public const string AviFileExtension = ".avi";
            public const string BackupFileSuffixFormat = "yyyy-MM-ddTHH-mm-ss.fffK";
            public const string BackupFileSuffixPattern = ".????-??-??T??-??-??.??????_??";
            public const string BackupFolder = "Backups"; // Sub-folder that will contain database and csv file backups  
            public const int NumberOfBackupFilesToKeep = 9; // Maximum number of backup files to keep
            public const string CsvFileExtension = ".csv";
            public const string DefaultFileDatabaseFileName = "CarnassialData.ddb";
            public const string DefaultTemplateDatabaseFileName = "CarnassialTemplate.tdb";
            public const string ExcelFileExtension = ".xlsx";
            public const string FileDatabaseFileExtension = ".ddb";
            public const string JpgFileExtension = ".jpg";
            public const string Mp4FileExtension = ".mp4";
            public const string TemplateFileExtension = ".tdb";

            public static readonly TimeSpan BackupInterval = TimeSpan.FromMinutes(10);
        }

        public static class Gestures
        {
            public const long MaximumMouseHWheelIncrement = 16000;
            public const long MouseHWheelStep = 3 * 120;
        }

        public static class GitHub
        {
            public static readonly Uri ApiBaseAddress = new Uri("https://api.github.com/repos/");
            public static readonly Uri BaseAddress = new Uri("https://github.com/");
        }

        // shorthands for FileSelection.<value>.ToString()
        public static class ImageQuality
        {
            public const string Dark = "Dark";
            public const string Ok = "Ok";

            public const string ListOfValues = "Ok|Dark|Corrupt|NoLongerAvailable";
        }

        public static class Images
        {
            // default threshold where the ratio of pixels below a given darkness in an image is used to determine whether the image is classified as 'dark'
            public const double DarkLuminosityThresholdDefault = 0.1;
            // difference threshold for masking differences between images, per RGB component per pixel
            public const byte DifferenceThresholdDefault = 20;
            public const byte DifferenceThresholdMax = 255;
            public const byte DifferenceThresholdMin = 0;

            public const double GreyscaleColorationThreshold = 0.005;
            public const int ImageCacheSize = 9;
            public const int JpegInitialBufferSize = 2 * 4096;
            public const int MinimumRenderWidth = 800;
            public const int NoThumbnailClassificationRequestedWidthInPixels = 200;
            public const int SmallestValidJpegSizeInBytes = 107; // with creative encoding; single pixel jpegs are usually somewhat larger
            public const int ThumbnailFallbackWidthInPixels = 200;

            public static readonly TimeSpan DefaultHybridVideoLag = TimeSpan.FromSeconds(2.0);
            public static readonly TimeSpan MagnifierRotationTime = TimeSpan.FromMilliseconds(450);

            public static readonly Lazy<BitmapImage> Copy = Images.LoadBitmap("Menu/Copy_16x.png");
            public static readonly Lazy<BitmapImage> Paste = Images.LoadBitmap("Menu/Paste_16x.png");

            public static readonly Lazy<BitmapImage> CorruptFile = Images.LoadBitmap("CorruptFile_480x.png");
            public static readonly Lazy<BitmapImage> FileNoLongerAvailable = Images.LoadBitmap("FileNoLongerAvailable_480x.png");
            public static readonly Lazy<BitmapImage> NoSelectableFile = Images.LoadBitmap("NoSelectableFile_480x.png");

            public static readonly Lazy<BitmapImage> StatusError = Images.LoadBitmap("StatusCriticalError_64x.png");
            public static readonly Lazy<BitmapImage> StatusHelp = Images.LoadBitmap("StatusHelp_64x.png");
            public static readonly Lazy<BitmapImage> StatusInformation = Images.LoadBitmap("StatusInformation_64x.png");
            public static readonly Lazy<BitmapImage> StatusWarning = Images.LoadBitmap("StatusWarning_64x.png");

            private static Lazy<BitmapImage> LoadBitmap(string fileName)
            {
                return new Lazy<BitmapImage>(() =>
                {
                    // if the requested image is available as an application resource, prefer that
                    if (Application.Current != null && Application.Current.Resources.Contains(fileName))
                    {
                        return (BitmapImage)Application.Current.Resources[fileName];
                    }

                    // if it's not (editor, unit tests, resource not listed in App.xaml) fall back to loading from the resources assembly
                    BitmapImage image = new BitmapImage();
                    image.BeginInit();
                    image.UriSource = new Uri("pack://application:,,/Resources/" + fileName);
                    image.EndInit();
                    image.Freeze();
                    return image;
                });
            }
        }

        public static class MarkableCanvas
        {
            public const double ImageZoomMaximum = 10.0;      // user configurable maximum amount of zoom in a display image
            public const double ImageZoomMaximumRangeMaximum = 50.0; // the highest zoom a user can configure for a display image
            public const double ImageZoomMaximumRangeMinimum = 2.0;
            public const double ImageZoomMinimum = 1.0;       // minimum amount of zoom

            public const double MagnifyingGlassDefaultFieldOfView = 150.0;
            public const double MagnifyingGlassFieldOfViewIncrement = 1.2;
            public const double MagnifyingGlassMaximumFieldOfView = 300.0;
            public const double MagnifyingGlassMinimumFieldOfView = 15.0;

            public const int MagnifyingGlassDiameter = 250;
            public const int MagnifyingGlassHandleStart = 200;
            public const int MagnifyingGlassHandleEnd = 250;

            public const int MarkerDiameter = 10;
            public const int MarkerGlowDiameterIncrease = 14;
            public const int MarkerStrokeThickness = 2;
            public const double MarkerGlowOpacity = 0.35;
            public const int MarkerGlowStrokeThickness = 7;
        }

        public static class Manufacturer
        {
            public const string Bushnell = "Bushnell";
            public const int BushnellInfoBarHeight = 100;
            public const string Reconyx = "Reconyx";
            public const int ReconyxInfoBarHeight = 32;
        }

        public static class Registry
        {
            public static class CarnassialKey
            {
                public const string AudioFeedback = "AudioFeedback";
                public const string CarnassialWindowPosition = "CarnassialWindowPosition";

                // most recently used operator for custom selections
                public const string CustomSelectionTermCombiningOperator = "CustomSelectionTermCombiningOperator";
                public const string DarkLuminosityThreshold = "DarkLuminosityThreshold";
                // the value for rendering
                public const string DesiredImageRendersPerSecond = "DesiredImageRendersPerSecond";
                public const string MostRecentCheckForUpdates = "MostRecentCheckForUpdates";
                // key containing the list of most recently image sets opened by Carnassial
                public const string MostRecentlyUsedImageSets = "MostRecentlyUsedImageSets";

                public const string OrderFilesByDateTime = "OrderFilesByDateTime";
                public const string SkipDarkImagesCheck = "SkipDarkImagesCheck";

                // dialog opt outs
                public const string SuppressAmbiguousDatesDialog = "SuppressAmbiguousDatesDialog";
                public const string SuppressFileCountOnImportDialog = "SuppressFileCountOnImportDialog";
                public const string SuppressSpreadsheetImportPrompt = "SuppressSpreadsheetImportPrompt";
            }

            public const string RootKey = @"Software\Cascades Carnivore Project\Carnassial\2.0";
        }

        public static class SearchTermOperator
        {
            public const string Equal = "\u003D";
            public const string Glob = " GLOB ";
            public const string GreaterThan = "\u003E";
            public const string GreaterThanOrEqual = "\u2265";
            public const string LessThan = "\u003C";
            public const string LessThanOrEqual = "\u2264";
            public const string NotEqual = "\u2260";
        }

        public static class Sql
        {
            public const string CreationStringPrimaryKey = "INTEGER PRIMARY KEY AUTOINCREMENT";
            public const string Where = " WHERE ";
        }

        public static class SqlColumnType
        {
            public const string DateTime = "DATETIME";
            public const string Integer = "INTEGER";
            public const string Real = "REAL";
            public const string Text = "TEXT";
        }

        public static class SqlOperator
        {
            public const string Equal = "=";
            public const string Glob = "GLOB";
            public const string GreaterThan = ">";
            public const string GreaterThanOrEqual = ">=";
            public const string LessThan = "<";
            public const string LessThanOrEqual = "<=";
            public const string NotEqual = "<>";
        }

        public static class ThrottleValues
        {
            public const double DesiredMaximumImageRendersPerSecondLowerBound = 1.0;
            public const double DesiredMaximumImageRendersPerSecondDefault = 5.0;
            public const double DesiredMaximumImageRendersPerSecondUpperBound = 12.0;
            public const int MaximumBlackFrameAttempts = 5;
            public const int MaximumRenderAttempts = 10;
            public const int SleepForImageRenderInterval = 100;

            public static readonly TimeSpan DesiredIntervalBetweenImageUpdates = TimeSpan.FromSeconds(5.0);
            public static readonly TimeSpan DesiredIntervalBetweenStatusUpdates = TimeSpan.FromMilliseconds(500);
            public static readonly TimeSpan PollIntervalForVideoLoad = TimeSpan.FromMilliseconds(1.0);
            public static readonly TimeSpan RenderingBackoffTime = TimeSpan.FromMilliseconds(25.0);
        }

        public static class Time
        {
            public const string DateFormat = "dd-MMM-yyyy";
            public const string DateTimeDatabaseFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
            public const string DateTimeDisplayFormat = "dd-MMM-yyyy HH:mm:ss";
            public const string DateTimeOffsetFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
            public const string DateTimeOffsetDisplayFormat = "dd-MMM-yyyy HH:mm:ss K";
            public const char DateTimeOffsetPart = 'K';
            public const int MonthsInYear = 12;
            public const string TimeFormat = "HH:mm:ss";
            public const string TimeSpanDisplayFormat = @"hh\:mm\:ss";
            public const string UtcOffsetDatabaseFormat = "0.00";
            public const string UtcOffsetDisplayFormat = @"hh\:mm";
            public const string VideoPositionFormat = @"mm\:ss";

            public static readonly TimeSpan DateTimeDatabaseResolution = TimeSpan.FromMilliseconds(1.0);
            public static readonly ReadOnlyCollection<char> DateTimeFieldCharacters = new List<char>() { 'd', 'f', 'h', 'H', 'K', 'm', 'M', 's', 't', 'y' }.AsReadOnly();
            public static readonly TimeSpan MaximumUtcOffset = TimeSpan.FromHours(14.0);
            public static readonly TimeSpan MinimumUtcOffset = TimeSpan.FromHours(-12.0);
            public static readonly ReadOnlyCollection<char> TimeSpanFieldCharacters = new List<char>() { 'd', 'f', 'F', 'h', 's', 'm' }.AsReadOnly();
            public static readonly TimeSpan UtcOffsetGranularity = TimeSpan.FromTicks(9000000000); // 15 minutes

            public static readonly string[] DateTimeMetadataFormats =
            {
                // known formats supported by Metadata Extractor
                "yyyy:MM:dd HH:mm:ss.fff",
                "yyyy:MM:dd HH:mm:ss",
                "yyyy:MM:dd HH:mm",
                "yyyy-MM-dd HH:mm:ss",
                "yyyy-MM-dd HH:mm",
                "yyyy.MM.dd HH:mm:ss",
                "yyyy.MM.dd HH:mm",
                "yyyy-MM-ddTHH:mm:ss.fff",
                "yyyy-MM-ddTHH:mm:ss.ff",
                "yyyy-MM-ddTHH:mm:ss.f",
                "yyyy-MM-ddTHH:mm:ss",
                "yyyy-MM-ddTHH:mm.fff",
                "yyyy-MM-ddTHH:mm.ff",
                "yyyy-MM-ddTHH:mm.f",
                "yyyy-MM-ddTHH:mm",
                "yyyy:MM:dd",
                "yyyy-MM-dd",
                "yyyy-MM",
                "yyyy",

                // File.File Modified Date
                "ddd MMM dd HH:mm:ss K yyyy"
            };
        }

        public static class UndoRedo
        {
            public const string CustomSelection = "CustomSelection";
            public const string FileIndex = "FileIndex";
            public const string FileOrdering = "FileOrdering";
        }

        [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1310:FieldNamesMustNotContainUnderscore", Justification = "Win32 naming.")]
        public static class Win32Messages
        {
            public const int WM_MOUSEHWHEEL = 0x20e;
        }
    }
}
