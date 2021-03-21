using Carnassial.Control;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Carnassial
{
    public static class Constant
    {
        // cases in CarnassialWindow.Window_KeyDown must be kept in sync with the number of analysis slots
        public const int AnalysisSlots = 9;

        public const string ApplicationName = "Carnassial";
        public const string Debug = "DEBUG";
        public const int DefaultControlGridWidth = 300; // keep in sync with DefaultControlGridWidth in CarnassialWindowStyle.xaml
        public const int DefaultPrefetchStride = 1;
        public const int LargeNumberOfFilesToDelete = 100;
        public const string MainWindowBaseTitle = "Carnassial: Simplifying Remote Camera Data";
        public const int MaximumUndoableCommands = 100;
        public const int NumberOfMostRecentDatabasesToTrack = 9;
        public const double PageUpDownNavigationFraction = 0.1;

        public static readonly TimeSpan CheckForUpdateInterval = TimeSpan.FromDays(1.25);
        public static readonly CultureInfo InvariantCulture = CultureInfo.InvariantCulture;
        public static readonly Version Windows8MinimumVersion = new(6, 2, 0, 0);

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
            public const string User32 = "user32.dll";
        }

        public static class ComGuid
        {
            public const string IFileOperation = "947aab5f-0a5c-4c13-b4d6-4bf7836fc9f8";
            public const string IFileOperationProgressSink = "04b0f1a7-9490-44bc-96e1-4296a31252e2";
            public const string IShellItem = "43826d1e-e718-42ee-bc55-a1e261c37bfe";

            public static readonly Guid IFileOperationClsid = new("3ad05575-8857-4850-9277-11b85bdb8e09");
        }

        public static class Control
        {
            // hotkey characters in use by the top level of Carnassial's main menu or the three data entry buttons and therefore unavailable as control 
            // shortcuts in either upper or lower case
            public const string ReservedHotKeys = "FfEeOoVvSsHhPp123456789";
            public const char WellKnownValuesDelimiter = '|';

            public static readonly ReadOnlyCollection<string> StandardControls = new List<string>()
            {
                Constant.FileColumn.Classification,
                Constant.FileColumn.DateTime,
                Constant.FileColumn.DeleteFlag,
                Constant.FileColumn.File,
                Constant.FileColumn.RelativePath,
                Constant.FileColumn.UtcOffset
            }.AsReadOnly();
        }

        public static class ControlColumn
        {
            // columns unique to the controls table
            public const string AnalysisLabel = "AnalysisLabel";
            public const string ControlOrder = "ControlOrder";
            public const string Copyable = "Copyable";
            public const string DataLabel = "DataLabel";
            public const string DefaultValue = "DefaultValue";
            public const string IndexInFileTable = "IndexInFileTable";
            public const string Label = "Label";
            public const string MaxWidth = "MaxWidth";
            public const string SpreadsheetOrder = "SpreadsheetOrder";
            public const string Tooltip = "Tooltip";
            public const string Type = "Type";
            public const string Visible = "Visible";
            public const string WellKnownValues = "WellKnownValues";

            [Obsolete("Legacy value for backwards compatibility with Carnassial 2.2.0.2 and earlier.")]
            public const string List = "List";
            [Obsolete("Legacy value for backwards compatibility with Carnassial 2.2.0.2 and earlier.")]
            public const string Width = "Width";

            public static readonly ReadOnlyCollection<string> Columns = new List<string>()
            {
                ControlColumn.AnalysisLabel,
                ControlColumn.ControlOrder,
                ControlColumn.Copyable,
                ControlColumn.DataLabel,
                ControlColumn.DefaultValue,
                ControlColumn.IndexInFileTable,
                ControlColumn.Label,
                ControlColumn.MaxWidth,
                ControlColumn.SpreadsheetOrder,
                ControlColumn.Tooltip,
                ControlColumn.Type,
                ControlColumn.Visible,
                ControlColumn.WellKnownValues
            }.AsReadOnly();
        }

        public static class ControlDefault
        {
            // general defaults
            public const int MaxWidth = 500;
            public const string Value = "";

            // user defined controls
            public const string CounterTooltip = "Click the counter button, then click on the image to count the entity. Or just type in a count";
            public const string CounterValue = "0";
            public const string FixedChoiceTooltip = "Choose an item from the menu";

            public const string FlagTooltip = "Toggle between true and false";
            public const string FlagValue = "0";
            public const string FlagWellKnownValues = "0|1";

            public const byte[] MarkerPositions = null;
            public const string NoteTooltip = "Write a textual note";

            // standard controls
            // Classification list contains Ok for backwards compatibility. See remarks for FileSelection.Ok and special case in 
            // SearchTerm..ctor(ControlRow).
            public const string ClassificationTooltip = "Color image, greyscale image, dark if mostly black image, video, corrupt if it can't be read, no longer available if the file is missing.";
            public const string ClassificationWellKnownValues = "Color|Ok|Corrupt|Dark|Greyscale|NoLongerAvailable|Video";

            public const string DateTimeTooltip = "Date and time taken";

            public const string FileTooltip = "The file name";
            public const string RelativePathTooltip = "Path from the folder containing the template and image data files to the file";

            public const string DeleteFlagLabel = "Delete?";    // a flag data type for marking deletion
            public const string DeleteFlagTooltip = "Mark a file as one to be deleted. You can then confirm deletion through the Edit Menu";

            public const string UtcOffsetTooltip = "Universal Time offset of the time zone for date and time taken";

            // UmAlQuraCalendar does not support dates before 1900-04-30T00:00:00 so default to a later date
            // (see also Annus mirabilis)
            public static readonly DateTimeOffset DateTimeValue = new(1905, 6, 30, 12, 0, 0, 0, TimeSpan.Zero);
        }

        public static class Database
        {
            public const string BackupFileNameSuffix = "-backup";
            public const int BackupRetryIntervalInMilliseconds = 100;

            // default values
            public const long DefaultFileID = 1;
            public const string ImageSetDefaultLog = "Add text here";
            public const long InvalidID = -1;
            public const int InvalidRow = -1;

            // see performance remarks in FileDatabase.AddFiles()
            public const int NominalRowsPerTransactionFill = 2500;
            public const int RowsPerTransaction = 5000;

            public static readonly TimeSpan BackupInterval = TimeSpan.FromMinutes(5.0);
        }

        public static class DatabaseColumn
        {
            public const string ID = "Id";
        }

        public static class DatabaseTable
        {
            public const string Controls = "Controls"; // table containing controls
            [Obsolete("Legacy value for backwards compatibility with Carnassial 2.2.0.2 and earlier.")]
            public const string FileData = "FileData";
            public const string Files = "Files";       // table containing image and video data
            public const string ImageSet = "ImageSet"; // table containing information common to the entire image set
        }

        public static class DialogControlName
        {
            public const string AboutEmailLink = "EmailLink";
            public const string AboutIssuesLink = "IssuesLink";
            public const string InstructionsTutorialLink = "TutorialLink";
        }

        public static class Excel
        {
            public const int AutoFilterDropdownWidth = 15;
            public const int CalibriCharacterWidth11Point = 7;
            public const string Extension = ".xlsx";
            public const char False = '0';
            public const string FileDataWorksheetName = "file data";
            public const string MarkerCoordinateFormat = "0.000000";
            public const char MarkerCoordinateSeparator = ',';
            public const char MarkerPositionSeparator = '|';
            public const double MaximumColumnWidth = 40.0;
            public const double MinimumColumnWidth = 5.0;
            public const int MaximumRowsInWorksheet = 1048576;
            public const int MaximumRowsToIncludeInAutoFit = 250;
            public const char True = '1';
        }

        public static class Exif
        {
            public const int MaxMetadataExtractorIssue35Offset = 12;
            public const string JpegCompression = "JPEG";
        }

        public static class File
        {
            public const string AviFileExtension = ".avi";
            public const string CsvFileExtension = ".csv";
            public const string CurrentDirectory = ".";
            public const string DefaultFileDatabaseFileName = "CarnassialData.ddb";
            public const string DefaultTemplateDatabaseFileName = "CarnassialTemplate.tdb";
            public const string ExcelFileExtension = ".xlsx";
            public const string FileDatabaseFileExtension = ".ddb";
            public const string JpgFileExtension = ".jpg";
            public const string Mp4FileExtension = ".mp4";
            public const string NoFileLoaded = "<no file loaded>";
            public const string ParentDirectory = "..";
            public const int RowsBetweenStatusReportChecks = 500;
            public const string TemplateFileExtension = ".tdb";
        }

        public static class FileColumn
        {
            public const string Classification = "Classification";
            public const string DateTime = "DateTime";
            public const string File = "File";
            [Obsolete("Legacy value for backwards compatibility with Carnassial 2.2.0.2 and earlier.")]
            public const string ImageQuality = "ImageQuality";
            public const string DeleteFlag = "DeleteFlag";
            public const string RelativePath = "RelativePath";
            public const string UtcOffset = "UtcOffset";

            public const string MarkerPositionSuffix = "Markers";
        }

        public static class Gestures
        {
            public const long MaximumMouseHWheelIncrement = 16000;
            public const long MouseHWheelStep = 3 * 120;
        }

        public static class GitHub
        {
            public static readonly Uri ApiBaseAddress = new("https://api.github.com/repos/");
            public static readonly Uri BaseAddress = new("https://github.com/");
        }

        public static class ImageDisplay
        {
            public const double ImageZoomMaximum = 10.0;      // user configurable maximum amount of zoom in a display image
            public const double ImageZoomMaximumRangeMaximum = 50.0; // the highest zoom a user can configure for a display image
            public const double ImageZoomMaximumRangeMinimum = 2.0;
            public const double ImageZoomMinimum = 1.0;       // minimum amount of zoom

            public const double MagnifyingGlassDefaultFieldOfView = 300.0;
            public const int MagnifyingGlassDiameter = 275;
            public const double MagnifyingGlassFieldOfViewIncrement = 1.2;
            public const double MagnifyingGlassMaximumFieldOfView = 350.0;
            public const double MagnifyingGlassMinimumFieldOfView = 20.0;

            public const int MarkerDiameter = 10;
            public const int MarkerGlowDiameterIncrease = 14;
            public const int MarkerStrokeThickness = 2;
            public const double MarkerGlowOpacity = 0.35;
            public const int MarkerGlowStrokeThickness = 7;

            public static readonly SolidColorBrush MarkerFillBrush = new(Color.FromArgb(2, 0, 0, 0));
        }

        public static class Images
        {
            // default threshold below which the mean luminosity of pixels in an image is considerd to be dark rather than greyscale
            public const double DarkLuminosityThresholdDefault = 0.0;
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

            public static readonly FileDisplayMessage FileCorruptMessage;
            public static readonly FileDisplayMessage FileNoLongerAvailableMessage;
            public static readonly FileDisplayMessage NoSelectableFileMessage;

            public static readonly Lazy<BitmapImage> StatusError = Images.LoadBitmap("StatusCriticalError_64x.png");
            public static readonly Lazy<BitmapImage> StatusHelp = Images.LoadBitmap("StatusHelp_64x.png");
            public static readonly Lazy<BitmapImage> StatusInformation = Images.LoadBitmap("StatusInformation_64x.png");
            public static readonly Lazy<BitmapImage> StatusWarning = Images.LoadBitmap("StatusWarning_64x.png");

            static Images()
            {
                Images.FileCorruptMessage = new FileDisplayMessage("Image or video not available.",
                    ":-(", 72,
                    "The file appears corrupted and cannot be displayed.");
                Images.FileNoLongerAvailableMessage = new FileDisplayMessage("Image or video not available.",
                    ":-(", 72,
                    "The file cannot be found.  Was it moved or deleted?");
                Images.NoSelectableFileMessage = new FileDisplayMessage("No file is available to display.",
                    "This occurs when\n• no image set is open\n• the image set has no files in it, or\n• no files are selected.", 12,
                    "To resolve this\n• open an image set if one isn't open (File menu)\n• add files if the image set is empty (File menu), or\n• change the selection (Select menu)");
            }

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
                    BitmapImage image = new();
                    image.BeginInit();
                    image.UriSource = new Uri("pack://application:,,/Resources/" + fileName);
                    image.EndInit();
                    image.Freeze();
                    return image;
                });
            }
        }

        public static class ImageSetColumn
        {
            public const string FileSelection = "FileSelection";
            public const string InitialFolderName = "InitialFolderName";
            public const string Log = "Log";
            public const string MostRecentFileID = "MostRecentFileID";
            public const string Options = "Options";
            public const string TimeZone = "TimeZone";

            public static readonly ReadOnlyCollection<string> Columns = new List<string>()
            {
                ImageSetColumn.FileSelection,
                ImageSetColumn.InitialFolderName,
                ImageSetColumn.Log,
                ImageSetColumn.MostRecentFileID,
                ImageSetColumn.Options,
                ImageSetColumn.TimeZone
            }.AsReadOnly();
        }

        public static class Manufacturer
        {
            public const string Bushnell = "Bushnell";
            public const int BushnellInfoBarHeight = 100;
            public const string Reconyx = "Reconyx";
            public const int ReconyxInfoBarHeight = 32;
        }

        public static class OpenXml
        {
            public const string Namespace = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

            public static class Attribute
            {
                public const string ActiveCell = "activeCell";
                public const string ActivePane = "activePane";
                public const string CellReference = "r";
                public const string CellStyle = "s";
                public const string CellType = "t";
                public const string CountAttribute = "count";
                public const string CustomWidth = "customWidth";
                public const string DefaultRowHeight = "defaultRowHeight";
                public const string Maximum = "max";
                public const string Minimum = "min";
                public const string Pane = "pane";
                public const string Reference = "ref";
                public const string Sqref = "sqref";
                public const string State = "state";
                public const string TopLeftCell = "topLeftCell";
                public const string UniqueCountAttribute = "uniqueCount";
                public const string Width = "width";
                public const string WorkbookViewId = "workbookViewId";
                public const string YSplit = "ySplit";
            }

            public static class CellType
            {
                public const string Boolean = "b";
                public const string InlineString = "inlineStr";
                public const string SharedString = "s";
            }

            public static class Element
            {
                public const string AutoFilter = "autoFilter";
                public const string Cell = "c";
                public const string CellValue = "v";
                public const string Column = "col";
                public const string Columns = "cols";
                public const string Dimension = "dimension";
                public const string Pane = "pane";
                public const string Row = "row";
                public const string Selection = "selection";
                public const string SharedString = "si";
                public const string SharedStringText = "t";
                public const string SheetData = "sheetData";
                public const string SheetFormatProperties = "sheetFormatPr";
                public const string SheetView = "sheetView";
                public const string SheetViews = "sheetViews";
                public const string Worksheet = "worksheet";
            }
        }

        public static class Platform
        {
            public const string Windows = "windows";
        }

        public static class Registry
        {
            public static class CarnassialKey
            {
                public const string AudioFeedback = "AudioFeedback";
                public const string CarnassialWindowPosition = "CarnassialWindowPosition";
                public const string ControlGridWidth = "ControlGridWidth";

                // most recently used operator for custom selections
                public const string CustomSelectionTermCombiningOperator = "CustomSelectionTermCombiningOperator";

                public const string DarkLuminosityThreshold = "DarkLuminosityThreshold";
                public const string DesiredImageRendersPerSecond = "DesiredImageRendersPerSecond";
                public const string ImageClassificationChangeSlowdown = "ImageClassificationChangeSlowdown";
                public const string MostRecentCheckForUpdates = "MostRecentCheckForUpdates";

                // key containing the list of most recently image sets opened by Carnassial
                public const string MostRecentlyUsedImageSets = "MostRecentlyUsedImageSets";

                public const string OrderFilesByDateTime = "OrderFilesByDateTime";
                public const string SkipFileClassification = "SkipFileClassification";

                // dialog opt outs
                public const string SuppressFileCountOnImportDialog = "SuppressFileCountOnImportDialog";
                public const string SuppressImportPrompt = "SuppressImportPrompt";

                public const string VideoSlowdown = "VideoSlowdown";
            }

            public const string RootKey = @"Software\Cascades Carnivore Project\Carnassial\2.0";
        }

        public static class Release
        {
            public static readonly Version V2_2_0_3 = new(2, 2, 0, 3);
        }

        public static class ResourceKey
        {
            public const string AboutTermsOfUse = "About.TermsOfUse";
            public const string AnalysisAssignToolTip = "AnalysisButtons.MenuAssignToSlotToolTip";
            public const string AnalysisHeader = "AnalysisButtons.MenuHeader";
            public const string AnalysisPasteToolTip = "AnalysisButtons.MenuPasteFromSlotToolTip";
            public const string ApplicationWindowException = "ApplicationWindow.Exception";
            public const string CarnassialWindowClockDriftFailed = "CarnassialWindow.ClockDrift.Failed";
            public const string CarnassialWindowCopyFile = "CarnassialWindow.CopyFile";
            public const string CarnassialWindowCopyFileFailed = "CarnassialWindow.CopyFile.Failed";
            public const string CarnassialWindowDatabaseLoadFailed = "CarnassialWindow.DatabaseLoad.Failed";
            public const string CarnassialWindowDaylightSavingsFailed = "CarnassialWindow.DaylightSavings.Failed";
            public const string CarnassialWindowFileMoveIncomplete = "CarnassialWindow.FileMove.Incomplete";
            public const string CarnassialWindowExportSpreadsheet = "CarnassialWindow.ExportSpreadsheet";
            public const string CarnassialWindowExportSpreadsheetFailed = "CarnassialWindow.ExportSpreadsheet.Failed";
            public const string CarnassialWindowImageMetadataFailed = "CarnassialWindow.ImageMetadata.Failed";
            public const string CarnassialWindowImport = "CarnassialWindow.Import";
            public const string CarnassialWindowImportFailed = "CarnassialWindow.Import.Failed";
            public const string CarnassialWindowImportIncomplete = "CarnassialWindow.Import.Incomplete";
            public const string CarnassialWindowNoAmbiguousDates = "CarnassialWindow.NoAmbiguousDates";
            public const string CarnassialWindowNoDeletableFiles = "CarnassialWindow.NoDeletableFiles";
            public const string CarnassialWindowNoMetadataAvailable = "CarnassialWindow.NoMetadataAvailable";
            public const string CarnassialWindowSelectFolder = "CarnassialWindow.SelectFolder";
            public const string CarnassialWindowStatusCombinedDifference = "CarnassialWindow.Status.CombinedDifference";
            public const string CarnassialWindowStatusCombinedDifferenceCurrentNotLoadable = "CarnassialWindow.Status.CombinedDifference.CurrentNotLoadable";
            public const string CarnassialWindowStatusCombinedDifferenceNextNotAvailable = "CarnassialWindow.Status.CombinedDifference.NextNotAvailable";
            public const string CarnassialWindowStatusCombinedDifferenceNotCalculable = "CarnassialWindow.Status.CombinedDifference.NotCalculable";
            public const string CarnassialWindowStatusCombinedDifferencePreviousNotAvailable = "CarnassialWindow.Status.CombinedDifference.PreviousNotAvailable";
            public const string CarnassialWindowStatusCopyFileCompleted = "CarnassialWindow.Status.CopyFile.Completed";
            public const string CarnassialWindowStatusCopyFileFailed = "CarnassialWindow.Status.CopyFile.Failed";
            public const string CarnassialWindowStatusImageSetEmpty = "CarnassialWindow.Status.ImageSet.Empty";
            public const string CarnassialWindowStatusImageSetNone = "CarnassialWindow.Status.ImageSet.None";
            public const string CarnassialWindowStatusImageSetOpening = "CarnassialWindow.Status.ImageSet.Opening";
            public const string CarnassialWindowStatusImageSetOpened = "CarnassialWindow.Status.ImageSet.Opened";
            public const string CarnassialWindowStatusImageSetLoadingFolders = "CarnassialWindow.Status.ImageSet.LoadingFolders";
            public const string CarnassialWindowStatusImageSetLoadingFoldersComplete = "CarnassialWindow.Status.ImageSet.LoadingFolders.Complete";
            public const string CarnassialWindowStatusImageSetLoadingFoldersWithClassification = "CarnassialWindow.Status.ImageSet.LoadingFolders.WithClassification";
            public const string CarnassialWindowStatusMoveFilesComplete = "CarnassialWindow.Status.MoveFiles.Complete";
            public const string CarnassialWindowStatusPreviousNextDifference = "CarnassialWindow.Status.PreviousNextDifference";
            public const string CarnassialWindowStatusPreviousNextDifferenceCurrentNotLoadable = "CarnassialWindow.Status.PreviousNextDifference.CurrentNotLoadable";
            public const string CarnassialWindowStatusPreviousNextDifferenceOtherNotLoadable = "CarnassialWindow.Status.PreviousNextDifference.OtherNotLoadable";
            public const string CarnassialWindowStatusPreviousNextDifferenceOtherNotCompatible = "CarnassialWindow.Status.PreviousNextDifference.OtherNotCompatible";
            public const string CarnassialWindowStatusRedo = "CarnassialWindow.Status.Redo";
            public const string CarnassialWindowStatusSelectionReverted = "CarnassialWindow.Status.Selection.Reverted";
            public const string CarnassialWindowStatusSpreadsheetExport = "CarnassialWindow.Status.Spreadsheet.Export";
            public const string CarnassialWindowStatusSpreadsheetExportCompleted = "CarnassialWindow.Status.Spreadsheet.Export.Completed";
            public const string CarnassialWindowStatusSpreadsheetExportExcelLimitExceeded = "CarnassialWindow.Status.Spreadsheet.Export.ExcelLimitExceeded";
            public const string CarnassialWindowStatusImport = "CarnassialWindow.Status.Import";
            public const string CarnassialWindowStatusSpreadsheetImportCompleted = "CarnassialWindow.Status.Import.Completed";
            public const string CarnassialWindowStatusSpreadsheetImportFailed = "CarnassialWindow.Status.Import.Failed";
            public const string CarnassialWindowStatusUndo = "CarnassialWindow.Status.Undo";
            public const string CarnassialWindowTemplateLoadFailed = "CarnassialWindow.TemplateLoad.Failed";
            public const string ControlRowSynchronizeDataLabelMismatch = "ControlRow.Synchronize.DataLabelMismatch";
            public const string CsvFileFilter = "Csv.FileFilter";
            public const string DataEntryHandlerConfirmCopyAll = "DataEntryHandler.ConfirmCopyAll";
            public const string DataEntryHandlerConfirmCopyForward = "DataEntryHandler.ConfirmCopyForward";
            public const string DataEntryHandlerConfirmPropagateToHere = "DataEntryHandler.ConfirmPropagateToHere";
            public const string DataEntryHandlerNothingToCopyForward = "DataEntryHandler.NothingToCopyForward";
            public const string DataEntryHandlerNothingToPropagate = "DataEntryHandler.NothingToPropagate";
            public const string DateTimeOffsetPickerCoerceMaximum = "DateTimeOffsetPicker.CoerceMaximum";
            public const string DateTimeOffsetPickerCoerceMinimum = "DateTimeOffsetPicker.CoerceMinimum";
            public const string DateTimeRereadResultDifferentDate = "DateTimeRereadResult.DifferentDate";
            public const string DateTimeRereadResultDifferentTime = "DateTimeRereadResult.DifferentTime";
            public const string DateTimeRereadResultDifferentUtcOffset = "DateTimeRereadResult.DifferentUtcOffset";
            public const string DateTimeRereadResultFileDateTime = "DateTimeRereadResult.FileDateTime";
            public const string DateTimeRereadResultMetadataDateTime = "DateTimeRereadResult.MetadataDateTime";
            public const string DateTimeRereadResultNoChange = "DateTimeRereadResult.NoChange";
            public const string DateTimeRereadResultPreviousMetadataDateTime = "DateTimeRereadResult.PreviousMetadataDateTime";
            public const string DateTimeRereadResultSameDate = "DateTimeRereadResult.SameDate";
            public const string DateTimeRereadResultSameTime = "DateTimeRereadResult.SameTime";
            public const string DateTimeRereadResultSameUtcOffset = "DateTimeRereadResult.SameUtcOffset";
            public const string DeleteFilesMessageCurrentFileAndData = "DeleteFiles.Message.CurrentFileAndData";
            public const string DeleteFilesMessageCurrentFileOnly = "DeleteFiles.Message.CurrentFileOnly";
            public const string DeleteFilesMessageFilesAndData = "DeleteFiles.Message.FilesAndData";
            public const string DeleteFilesMessageFilesOnly = "DeleteFiles.Message.FilesOnly";
            public const string DialogCancel = "Dialog.Cancel";
            public const string DialogNo = "Dialog.No";
            public const string DialogOK = "Dialog.OK";
            public const string DialogYes = "Dialog.Yes";
            public const string ExcelFileFilter = "Excel.FileFilter";
            public const string FileCountsByClassificationMessageHint = "FileCountsByClassification.Message.Hint";
            public const string FileDatabaseImportColumnLayoutMismatch = "FileDatabase.Import.ColumnLayoutMismatch";
            public const string FileDatabaseImportOtherColumnNotInThis = "FileDatabase.Import.OtherColumnNotInThis";
            public const string FileDatabaseImportThisColumnNotInOther = "FileDatabase.Import.ThisColumnNotInOther";
            public const string FileDatabaseEndBeforeStart = "FileDatabase.EndBeforeStart";
            public const string FileDatabaseSelectionNotAll = "FileDatabase.SelectionNotAll";
            public const string FileFindReplaceNoControlsVisible = "FileFindReplace.NoControlsVisible";
            public const string FileIOComputeTransactionManagerCantRerun = "FileIOComputeTransactionManager.CantRerun";
            public const string FileIOComputeTransactionManagerNullTask = "FileIOComputeTransactionManager.NullTask";
            public const string FindReplaceNoMatchingFile = "FindReplace.NoMatchingFile";
            public const string FindReplaceOneMatchingFile = "FindReplace.OneMatchingFile";
            public const string FileTableNoColumns = "FileTable.NoColumns";
            public const string GithubReleaseClientGetNewVersion = "GithubReleaseClient.GetNewVersion";
            public const string GithubReleaseClientNoUpdates = "GithubReleaseClient.NoUpdates";
            public const string GoToFileNumberRange = "GoToFile.FileNumberRange";
            public const string HyperthreadSiblingAffinityTaskID = "HyperthreadSiblingAffinity.TaskID";
            public const string ImageRowIDImmutable = "ImageRow.IDImmutable";
            public const string ImageRowImportInvalidChoice = "ImageRow.Import.InvalidChoice";
            public const string ImageRowImportInvalidClassification = "ImageRow.Import.InvalidClassification";
            public const string ImageRowImportInvalidCount = "ImageRow.Import.InvalidCount";
            public const string ImageRowImportInvalidDateTime = "ImageRow.Import.InvalidDateTime";
            public const string ImageRowImportInvalidDeleteFlag = "ImageRow.Import.InvalidDeleteFlag";
            public const string ImageRowImportInvalidFlag = "ImageRow.Import.InvalidFlag";
            public const string ImageRowImportInvalidMarkerPosition = "ImageRow.Import.InvalidMarkerPosition";
            public const string ImageRowImportInvalidUtcOffset = "ImageRow.Import.InvalidUtcOffset";
            public const string ImageRowSetDateTimeThroughDateTimeOffset = "ImageRow.SetDateTimeThroughDateTimeOffset";
            public const string ImageRowSetFileNameThroughFileName = "ImageRowSet.SetFileNameThroughFileName";
            public const string ImageRowSetUtcOffsetThroughDateTimeOffset = "ImageRow.SetUtcOffsetThroughDateTimeOffset";
            public const string JpegImageMetadataRequired = "JpegImage.MetadataRequired";
            public const string KeyboardControl = "Keyboard.Control";
            public const string MarkersForCounterFloatsUnpaired = "MarkersForCounter.FloatsUnpaired";
            public const string NativeMethodsPathNullOrEmpty = "NativeMethods.PathNullOrEmpty";
            public const string NativeMethodsPathPrefixRequired = "NativeMethods.PathPrefixRequired";
            public const string OpenXmlStylesheetInsufficientFonts = "OpenXmlStylesheet.InsufficientFonts";
            public const string ReclassifyFilesUnloadable = "ReclassifyFiles.Unloadable";
            public const string ReclassifyFilesVideo = "ReclassifyFiles.Video";
            public const string SearchTermDateTimeNotUtc = "SearchTerm.DateTimeNotUtc";
            public const string SearchTermInvalidBoolean = "SearchTerm.InvalidBoolean";
            public const string SearchTermListCellMargin = "SearchTermList.CellMargin";
            public const string SharedStringIndexDuplicateOrMissing = "SharedStringIndex.DuplicateOrMissing";
            public const string SharedStringIndexNoSharedString = "SharedStringIndex.NoSharedString";
            public const string SpreadsheetImportColumnNotInImageSet = "Spreadsheet.Import.ColumnNotInImageSet";
            public const string SpreadsheetImportColumnNotInSpreadsheet = "Spreadsheet.Import.ColumnNotInSpreadsheet";
            public const string SpreadsheetImportRequiredColumnNotInSpreadsheet = "Spreadsheet.Import.RequiredColumnNotInSpreadsheet";
            public const string SpreadsheetReaderWriterFileSelectionNotAll = "SpreadsheetReaderWriter.FileSelectionNotAll";
            public const string SpreadsheetReaderWriterNoCellValue = "SpreadsheetReaderWriter.NoCellValue";
            public const string SpreadsheetReaderWriterNoDimensionReference = "SpreadsheetReaderWriter.NoDimensionReference";
            public const string SpreadsheetReaderWriterNoSharedString = "SpreadsheetReaderWriter.NoSharedString";
            public const string SpreadsheetReaderWriterNoStringTableCount = "SpreadsheetReaderWriter.NoStringTableCount";
            public const string SpreadsheetReaderWriterNoWorksheetDimension = "SpreadsheetReaderWriter.NoWorksheetDimension";
            public const string SqliteDatabaseBuildInvalid = "SqliteDatabase.BuildInvalid";
            public const string SqliteDatabaseRevisionInvalid = "SqliteDatabase.RevsionInvalid";
            public const string SqliteDatabaseMinorVersionInvalid = "SqliteDatabase.MajorVersionInvalid";
            public const string SqliteDatabaseMajorVersionInvalid = "SqliteDatabase.MinorVersionInvalid";
            public const string TimeSpanPickerCoerceMaximum = "TimeSpanPicker.CoerceMaximum";
            public const string TimeSpanPickerCoerceMinimum = "TimeSpanPicker.CoerceMinimum";
            public const string UnbufferedSequentialReaderBytesRequired = "UnbufferedSequentialReader.BytesRequired";
            public const string UnbufferedSequentialReaderEndOfFile = "UnbufferedSequentialReader.EndOfFile";
            public const string UnbufferedSequentialReaderExtendBuffer = "UnbufferedSequentialReader.ExtendBuffer";
            public const string UnbufferedSequentialReaderLittleEndian = "UnbufferedSequentialReader.LittleEndian";
        }

        public static class SearchTermOperator
        {
            public const string Equal = "\u003D";
            public const string Glob = "GLOB";
            public const string GreaterThan = "\u003E";
            public const string GreaterThanOrEqual = "\u2265";
            public const string LessThan = "\u003C";
            public const string LessThanOrEqual = "\u2264";
            public const string NotEqual = "\u2260";
        }

        public static class Sql
        {
            public const string CreationStringPrimaryKey = "INTEGER PRIMARY KEY AUTOINCREMENT";
            public const string FalseString = "0";
            public const string MainDatabase = "main";
            public const string TrueString = "1";
            public const string Where = " WHERE ";
        }

        public static class SQLiteAffinity
        {
            public const string Blob = "BLOB";
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
            public const double ImageClassificationSlowdownDefault = 2.4;
            public const double ImageClassificationSlowdownMaximum = 5.0;
            public const double ImageClassificationSlowdownMinimum = 0.0;
            public const int MaximumBlackFrameAttempts = 5;
            public const int MaximumRenderAttempts = 10;
            public const int SleepForImageRenderInterval = 100;
            public const double VideoSlowdownDefault = 5.0;
            public const double VideoSlowdownMaximum = 10.0;
            public const double VideoSlowdownMinimum = 0.0;

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
            public const double MaximumUtcOffsetInHours = 14.0;
            public const double MinimumUtcOffsetInHours = -12.0;
            public const int MonthsInYear = 12;
            public const string TimeSpanDisplayFormat = @"hh\:mm\:ss";
            public const string UtcOffsetDisplayFormat = @"h\:mm";
            public const int UtcOffsetGranularityInMinutes = 15;
            public const string VideoPositionFormat = @"mm\:ss";

            public static readonly TimeSpan DateTimeDatabaseResolution = TimeSpan.FromMilliseconds(1.0);
            public static readonly ReadOnlyCollection<char> DateTimeFieldCharacters = new List<char>() { 'd', 'f', 'h', 'H', 'K', 'm', 'M', 's', 't', 'y' }.AsReadOnly();
            public static readonly ReadOnlyCollection<char> TimeSpanFieldCharacters = new List<char>() { 'd', 'f', 'F', 'h', 's', 'm' }.AsReadOnly();

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

        public static class UserInterface
        {
            public const string ApplicationBackgroundBrush = "ApplicationBackgroundBrush";
            public const double FindOperatorWidth = 60.0;
            public const int FindTextBoxHeight = 22;
            public const int FindValueWidth = 200;
            public const string NoFindValue = "(none)";

            public static readonly Thickness FindCellMargin = new(5, 2, 5, 2);
            public static readonly ReadOnlyCollection<string> Localizations = new List<string>() { "ar", "es", "hi", "ms", "ru", "zh-Hans" }.AsReadOnly();
        }

        public static class Win32Messages
        {
            public const int WM_MOUSEHWHEEL = 0x20e;
        }
    }
}
