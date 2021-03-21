using Carnassial.Data;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Carnassial.UnitTests
{
    internal static class TestConstant
    {
        public const string CarnassialAutomationID = "Carnassial";
        public const string CarnassialTemplateEditorAutomationID = "CarnassialTemplateEditor";
        public const double LuminosityAndColorationTolerance = 0.00000001;
        public const string DateTimeWithOffsetFormat = "yyyy-MM-ddTHH:mm:ss.fffK";
        public const byte[] DefaultMarkerPositions = null;
        public const string EditorTemplateDatabaseFieldName = "templateDatabase";
        public const string FileCountsAutomationID = "FileCountsByClassification";
        public const string InitializeDataGridMethodName = "InitializeDataGrid";
        public const string MessageBoxAutomationID = "CarnassialMessageBox";
        public const string OkButtonAutomationID = "OkButton";
        public const string TemplatePaneAutomationID = "TemplatePane";

        public static readonly TimeSpan UIElementSearchTimeout = TimeSpan.FromSeconds(15.0);

        public static readonly ReadOnlyCollection<string> DefaultFileColumns = new List<string>()
        {
            Constant.DatabaseColumn.ID,
            Constant.FileColumn.File,
            Constant.FileColumn.RelativePath,
            Constant.FileColumn.DateTime,
            Constant.FileColumn.UtcOffset,
            Constant.FileColumn.Classification,
            Constant.FileColumn.DeleteFlag,
            TestConstant.DefaultDatabaseColumn.Counter0,
            TestConstant.DefaultDatabaseColumn.Counter0Markers,
            TestConstant.DefaultDatabaseColumn.Choice0,
            TestConstant.DefaultDatabaseColumn.Note0,
            TestConstant.DefaultDatabaseColumn.Flag0,
            TestConstant.DefaultDatabaseColumn.CounterWithCustomDataLabel,
            TestConstant.DefaultDatabaseColumn.CounterWithCustomDataLabelMarkers,
            TestConstant.DefaultDatabaseColumn.ChoiceWithCustomDataLabel,
            TestConstant.DefaultDatabaseColumn.NoteWithCustomDataLabel,
            TestConstant.DefaultDatabaseColumn.FlagWithCustomDataLabel,
            TestConstant.DefaultDatabaseColumn.CounterNotVisible,
            TestConstant.DefaultDatabaseColumn.CounterNotVisibleMarkers,
            TestConstant.DefaultDatabaseColumn.ChoiceNotVisible,
            TestConstant.DefaultDatabaseColumn.NoteNotVisible,
            TestConstant.DefaultDatabaseColumn.FlagNotVisible,
            TestConstant.DefaultDatabaseColumn.Counter3,
            TestConstant.DefaultDatabaseColumn.Counter3Markers,
            TestConstant.DefaultDatabaseColumn.Choice3,
            TestConstant.DefaultDatabaseColumn.Note3,
            TestConstant.DefaultDatabaseColumn.Flag3
        }.AsReadOnly();

        public static class CarnivoreDatabaseColumn
        {
            public const string Pelage = "Pelage";
        }

        public static class DefaultDatabaseColumn
        {
            public const string Counter0 = "Counter0";
            public const string Counter0Markers = "Counter0Markers";
            public const string Choice0 = "Choice0";
            public const string Note0 = "Note0";
            public const string Flag0 = "Flag0";
            public const string CounterWithCustomDataLabel = "कस्टम डेटा लेबल के साथ काउंटर"; // Hindi
            public const string CounterWithCustomDataLabelMarkers = "कस्टम डेटा लेबल के साथ काउंटरMarkers";
            public const string ChoiceWithCustomDataLabel = "Bandera con\"etiqueta\"de\"datos personalizada"; // Spanish
            public const string NoteWithCustomDataLabel = "注意使用自定义数据标签"; // Chinese (simplified)
            public const string FlagWithCustomDataLabel = "カスタムデータラベルのフラグ"; // Japanese
            public const string CounterNotVisible = "CounterNotVisible";
            public const string CounterNotVisibleMarkers = "CounterNotVisibleMarkers";
            public const string ChoiceNotVisible = "ChoiceNotVisible";
            public const string NoteNotVisible = "NoteNotVisible";
            public const string FlagNotVisible = "FlagNotVisible";
            public const string Counter3 = "Counter3";
            public const string Counter3Markers = "Counter3Markers";
            public const string Choice3 = "Choice3";
            public const string Note3 = "Note3";
            public const string Flag3 = "Flag3";
        }

        public static class Exif
        {
            public const string DateTimeFormat = "yyyy:MM:dd HH:mm:ss";
        }

        public static class File
        {
            public const string CarnivoreDirectoryName = "CarnivoreTestImages";
            public const string HybridVideoDirectoryName = "HybridVideo";

            // file databases for backwards compatibility testing
            // Version is the Carnassial version used for creation.
            public const string DefaultFileDatabaseFileName = "DefaultData 2.2.0.3.ddb";

            // template databases for backwards compatibility testing
            // Version is the editor version used for creation.
            public const string DefaultTemplateDatabaseFileName = "DefaultTemplate 2.2.0.3.tdb";

            // databases generated dynamically by tests
            // See also use of Constants.File.Default*DatabaseFileName.
            public const string DefaultNewFileDatabaseFileName = "DefaultUnitTest.ddb";
            public const string DefaultNewTemplateDatabaseFileName = "DefaultUnitTest.tdb";
        }

        public static class FileExpectation
        {
            public const string DaylightBobcatFileName = "BushnellTrophyHD-119677C-20160805-926.JPG";

            public static readonly FileExpectations CorruptFieldScan;
            public static readonly FileExpectations DaylightBobcat;
            public static readonly FileExpectations DaylightCoyote;
            public static readonly FileExpectations DaylightMartenPair;
            public static readonly FileExpectations InfraredMarten;

            public static readonly DateTime HybridVideoFileDate = new(2016, 06, 26);

            static FileExpectation()
            {
                TimeZoneInfo pacificTime = TimeZoneInfo.FindSystemTimeZoneById(TestConstant.TimeZone.Pacific);

                FileExpectation.CorruptFieldScan = new FileExpectations(pacificTime)
                {
                    FileName = "BushnellTrophyHD-119677C-20170403-179.JPG",
                    Coloration = 0.5,
                    Classification = FileClassification.Corrupt
                };

                FileExpectation.DaylightBobcat = new FileExpectations(pacificTime)
                {
                    Luminosity = 0.22963220152779396,
                    DateTime = FileExpectations.ParseDateTimeOffsetString("2015-08-05T08:06:23.000-07:00"),
                    FileName = FileExpectation.DaylightBobcatFileName,
                    Coloration = 0.072226041956181958,
                    Classification = FileClassification.Color
                };

                FileExpectation.DaylightCoyote = new FileExpectations(pacificTime)
                {
                    Coloration = 0.10284907771379409,
                    DateTime = FileExpectations.ParseDateTimeOffsetString("2016-04-21T06:31:13.000-07:00"),
                    FileName = "BushnellTrophyHDAggressor-119777C-20160421-112.JPG",
                    Luminosity = 0.2765857055787681,
                    Classification = FileClassification.Color,
                    RelativePath = TestConstant.File.CarnivoreDirectoryName
                };

                FileExpectation.DaylightMartenPair = new FileExpectations(pacificTime)
                {
                    Coloration = 0.11669914145874821,
                    DateTime = FileExpectations.ParseDateTimeOffsetString("2015-01-28T11:17:34.000-08:00"),
                    FileName = "Reconyx-HC500-20150128-201.JPG",
                    Luminosity = 0.20386141575831992,
                    Classification = FileClassification.Color,
                    RelativePath = TestConstant.File.CarnivoreDirectoryName
                };

                FileExpectation.InfraredMarten = new FileExpectations(pacificTime)
                {
                    Coloration = 0.002260494349364121,
                    DateTime = FileExpectations.ParseDateTimeOffsetString("2016-02-24T04:59:46.000-08:00"),
                    FileName = "BushnellTrophyHD-119677C-20160224-056.JPG",
                    Luminosity = 0.3138861038100762,
                    Classification = FileClassification.Greyscale
                };
            }
        }

        // approximate coverage of most widely spoken languages worldwide
        //   1-6 as commonly estimated, from most to least spoken: Chinese, English, Hindi, Spanish, Arabic, Malay
        //   7: estimates vary among Portuguese, Russian, and Bengali; Russian is chosen here for Cyrillic coverage
        //   next most used: Japanese, German, Punjabi, Telgu, Javanese, Tamil, Urdu
        public static class Globalization
        {
            public const string AlternateUITestCultureName = "ja";

            public static readonly string[] DefaultUITestCultureNames = new string[] { "zh-Hans", "en", "hi", "es", "ar", "ms", "ru" };
        }

        public static class TimeZone
        {
            public const string Alaska = "Alaskan Standard Time"; // UTC-9
            public const string Arizona = "US Mountain Standard Time"; // UTC-7
            public const string CapeVerde = "Cape Verde Standard Time"; // UTC-1
            public const string Dateline = "Dateline Standard Time"; // UTC-12
            public const string Gmt = "GMT Standard Time"; // UTC+0
            public const string LineIslands = "Line Islands Standard Time"; // UTC+14
            public const string Mountain = "Mountain Standard Time"; // UTC-7
            public const string Pacific = "Pacific Standard Time"; // UTC-8
            public const string Utc = "UTC";
            public const string WestCentralAfrica = "W. Central Africa Standard Time"; // UTC+1
        }
    }
}
