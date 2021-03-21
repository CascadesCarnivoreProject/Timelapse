using Carnassial.Data;
using Carnassial.Data.Spreadsheet;
using Carnassial.Database;
using Carnassial.Dialog;
using Carnassial.Images;
using Carnassial.Util;
using MetadataExtractor.Formats.Exif;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using MetadataTag = MetadataExtractor.Tag;

namespace Carnassial.UnitTests
{
    [TestClass]
    public class DatabaseTests : CarnassialTest
    {
        [ClassCleanup]
        public static void ClassCleanup()
        {
            CarnassialTest.TryRevertToDefaultCultures();
        }

        [ClassInitialize]
        public static void ClassInitialize(TestContext testContext)
        {
            CarnassialTest.TryChangeToTestCulture();
        }

        [TestMethod]
        public void CreateReuseDefaultFileDatabase()
        {
            this.CreateReuseFileDatabase(TestConstant.File.DefaultTemplateDatabaseFileName, TestConstant.File.DefaultFileDatabaseFileName, (FileDatabase fileDatabase) =>
            {
                return this.PopulateDefaultDatabase(fileDatabase);
            });
        }

        private void CreateReuseFileDatabase(string templateDatabaseBaseFileName, string fileDatabaseBaseFileName, Func<FileDatabase, List<FileExpectations>> addFiles)
        {
            // create database for test
            using FileDatabase fileDatabase = this.CreateFileDatabase(templateDatabaseBaseFileName, fileDatabaseBaseFileName);
            List<FileExpectations> fileExpectations = addFiles(fileDatabase);

            // sanity coverage of image data table methods
            int deletedFiles = fileDatabase.GetFileCount(FileSelection.MarkedForDeletion);
            Assert.IsTrue(deletedFiles == 0);

            Assert.IsTrue(fileDatabase.GetFileCount(FileSelection.All) == fileExpectations.Count);
            Assert.IsTrue(fileDatabase.GetFileCount(FileSelection.MarkedForDeletion) == 0);
            Dictionary<FileClassification, int> fileCounts = fileDatabase.GetFileCountsByClassification();
            Assert.IsTrue(fileCounts.Count == 6);
            int expectedColorImageCount = fileExpectations.Count(expectation => expectation.Classification == FileClassification.Color);
            int expectedGreyscaleImageCount = fileExpectations.Count(expectation => expectation.Classification == FileClassification.Greyscale);
            Assert.IsTrue(fileCounts[FileClassification.Color] == expectedColorImageCount);
            Assert.IsTrue(fileCounts[FileClassification.Corrupt] == 0);
            Assert.IsTrue(fileCounts[FileClassification.Dark] == 0);
            Assert.IsTrue(fileCounts[FileClassification.Greyscale] == expectedGreyscaleImageCount);
            Assert.IsTrue(fileCounts[FileClassification.NoLongerAvailable] == 0);
            Assert.IsTrue(fileCounts[FileClassification.Video] == 0);

            FileTable filesToDelete = fileDatabase.GetFilesMarkedForDeletion();
            Assert.IsTrue(filesToDelete.RowCount == 0);

            // check images after initial add and again after reopen and application of selection
            // checks are not performed after last selection in list is applied
            List<ControlRow> counterControls = fileDatabase.Controls.Where(control => control.ControlType == ControlType.Counter).ToList();
            Assert.IsTrue(counterControls.Count == 4);
            string currentDirectoryName = Path.GetFileName(fileDatabase.FolderPath);
            fileDatabase.SelectFiles(FileSelection.All);
            TimeZoneInfo imageSetTimeZone = fileDatabase.ImageSet.GetTimeZoneInfo();
            foreach (FileSelection nextSelection in new List<FileSelection>() { FileSelection.All, FileSelection.All })
            {
                Assert.IsTrue(fileDatabase.CurrentlySelectedFileCount == fileExpectations.Count);
                fileDatabase.SelectFiles(FileSelection.All);
                Assert.IsTrue(fileDatabase.Files.RowCount == fileExpectations.Count);
                int displayableFileIndex = fileDatabase.GetCurrentOrNextDisplayableFile(fileDatabase.GetFileOrNextFileIndex(Constant.Database.DefaultFileID));
                Assert.IsTrue(displayableFileIndex == 0);

                for (int fileIndex = 0; fileIndex < fileExpectations.Count; ++fileIndex)
                {
                    // verify file
                    ImageRow file = fileDatabase.Files[fileIndex];
                    FileExpectations fileExpectation = fileExpectations[fileIndex];
                    fileExpectation.Verify(file, imageSetTimeZone);

                    // verify markers associated with file
                    foreach (ControlRow counter in counterControls)
                    {
                        MarkersForCounter markerForCounter = file.GetMarkersForCounter(counter.DataLabel);
                        Assert.IsTrue(String.Equals(markerForCounter.DataLabel, counter.DataLabel, StringComparison.Ordinal));
                        Assert.IsTrue(markerForCounter.Count == (int)file[counter.DataLabel]);
                        Assert.IsTrue(markerForCounter.Markers.Count >= 0);
                        Assert.IsTrue(markerForCounter.Markers.Count <= (int)file[counter.DataLabel]);
                    }

                    // retrieval by specific method
                    Assert.IsTrue(fileDatabase.IsFileDisplayable(fileIndex));
                    Assert.IsTrue(fileDatabase.IsFileRowInRange(fileIndex));

                    // retrieval by table
                    fileExpectation.Verify(fileDatabase.Files[fileIndex], imageSetTimeZone);
                }

                // reopen database for test and refresh images so next iteration of the loop checks state after reload
                Assert.IsTrue(FileDatabase.TryCreateOrOpen(fileDatabase.FilePath, fileDatabase, false, LogicalOperator.And, out FileDatabase fileDatabaseReopened));
                using (fileDatabaseReopened)
                {
                    fileDatabaseReopened.SelectFiles(nextSelection);
                    Assert.IsTrue(fileDatabaseReopened.Files.RowCount > 0);
                }
            }

            foreach (ControlRow control in fileDatabase.Controls)
            {
                List<string> distinctValues = fileDatabase.GetDistinctValuesInFileDataColumn(control.DataLabel);
                var expectedValues = control.DataLabel switch
                {
                    Constant.FileColumn.DateTime or 
                    Constant.FileColumn.File or 
                    Constant.DatabaseColumn.ID => fileExpectations.Count,
                    Constant.FileColumn.RelativePath => fileExpectations.Select(expectation => expectation.RelativePath).Distinct().Count(),
                    Constant.FileColumn.UtcOffset => fileExpectations.Select(expectation => expectation.DateTime.Offset).Distinct().Count(),
                    Constant.FileColumn.DeleteFlag or 
                    TestConstant.DefaultDatabaseColumn.Choice0 or 
                    TestConstant.DefaultDatabaseColumn.Choice3 or 
                    TestConstant.DefaultDatabaseColumn.ChoiceNotVisible or 
                    TestConstant.DefaultDatabaseColumn.ChoiceWithCustomDataLabel or 
                    TestConstant.DefaultDatabaseColumn.Counter3 or
                    TestConstant.DefaultDatabaseColumn.CounterNotVisible or
                    TestConstant.DefaultDatabaseColumn.CounterWithCustomDataLabel or
                    TestConstant.DefaultDatabaseColumn.Flag0 or
                    TestConstant.DefaultDatabaseColumn.Flag3 or
                    TestConstant.DefaultDatabaseColumn.FlagWithCustomDataLabel or
                    TestConstant.DefaultDatabaseColumn.NoteNotVisible => 1,
                    TestConstant.DefaultDatabaseColumn.FlagNotVisible or
                    TestConstant.DefaultDatabaseColumn.NoteWithCustomDataLabel or
                    Constant.FileColumn.Classification => 2,
                    TestConstant.DefaultDatabaseColumn.Counter0 or
                    TestConstant.DefaultDatabaseColumn.Note0 or
                    TestConstant.DefaultDatabaseColumn.Note3 => 3,
                    _ => throw new NotSupportedException(String.Format(CultureInfo.CurrentCulture, "Unhandled data label '{0}'.", control.DataLabel)),
                };
                Assert.IsTrue(distinctValues != null && distinctValues.Count == expectedValues);
                Assert.IsTrue(distinctValues.Count == distinctValues.Distinct().Count());
            }

            // sanity coverage of image set table methods
            string originalTimeZoneID = fileDatabase.ImageSet.TimeZone;

            this.VerifyDefaultImageSet(fileDatabase);
            fileDatabase.ImageSet.FileSelection = FileSelection.Custom;
            fileDatabase.ImageSet.MostRecentFileID = -1;
            fileDatabase.AppendToImageSetLog(new StringBuilder("Test log entry."));
            fileDatabase.ImageSet.Options = fileDatabase.ImageSet.Options.SetFlag(ImageSetOptions.Magnifier, true);
            fileDatabase.ImageSet.TimeZone = "Test Time Zone";
            fileDatabase.TrySyncImageSetToDatabase();
            Assert.IsTrue(fileDatabase.ImageSet.ID == 1);
            Assert.IsTrue(fileDatabase.ImageSet.FileSelection == FileSelection.Custom);
            Assert.IsTrue(fileDatabase.ImageSet.MostRecentFileID == -1);
            Assert.IsTrue(fileDatabase.ImageSet.Log == Constant.Database.ImageSetDefaultLog + "Test log entry.");
            Assert.IsTrue(fileDatabase.ImageSet.Options.HasFlag(ImageSetOptions.Magnifier));
            Assert.IsTrue(fileDatabase.ImageSet.TimeZone == "Test Time Zone");

            fileDatabase.ImageSet.TimeZone = originalTimeZoneID;

            // time and date manipulation
            fileDatabase.SelectFiles(FileSelection.All);
            Assert.IsTrue(fileDatabase.Files.RowCount == fileExpectations.Count);
            List<DateTimeOffset> fileTimesBeforeAdjustment = fileDatabase.GetFileTimes().ToList();
            TimeSpan adjustment = new(0, 1, 2, 3, 0);
            fileDatabase.AdjustFileTimes(adjustment);
            foreach (FileExpectations fileExpectation in fileExpectations)
            {
                fileExpectation.DateTime += adjustment;
            }
            this.VerifyFiles(fileDatabase, fileExpectations, imageSetTimeZone);
            fileDatabase.SelectFiles(FileSelection.All);
            this.VerifyFiles(fileDatabase, fileExpectations, imageSetTimeZone);

            fileTimesBeforeAdjustment = fileDatabase.GetFileTimes().ToList();
            adjustment = new TimeSpan(-1, -2, -3, -4, 0);
            int startRow = 1;
            int endRow = fileDatabase.CurrentlySelectedFileCount - 1;
            fileDatabase.AdjustFileTimes(adjustment, startRow, endRow);
            this.VerifyFileTimeAdjustment(fileTimesBeforeAdjustment, fileDatabase.GetFileTimes().ToList(), startRow, endRow, adjustment);
            fileDatabase.SelectFiles(FileSelection.All);
            this.VerifyFileTimeAdjustment(fileTimesBeforeAdjustment, fileDatabase.GetFileTimes().ToList(), startRow, endRow, adjustment);

            fileDatabase.ExchangeDayAndMonthInFileDates(0, fileDatabase.Files.RowCount - 1);

            // custom selection coverage
            // search terms should be created for all visible and copyable controls except Folder, but DateTime gets two
            int expectedSearchTerms = fileDatabase.Controls.Count(control => control.Copyable && control.Visible);
            Assert.IsTrue(expectedSearchTerms == fileDatabase.CustomSelection.SearchTerms.Count);
            Assert.IsTrue(fileDatabase.CustomSelection.CreateSelect().Where.Count == 0);
            Assert.IsTrue(fileDatabase.GetFileCount(FileSelection.Custom) == -1);
            foreach (SearchTerm searchTerm in fileDatabase.CustomSelection.SearchTerms)
            {
                Assert.IsTrue(String.IsNullOrWhiteSpace(searchTerm.ToString()) == false);
            }

            ControlRow dateTimeControl = fileDatabase.Controls[Constant.FileColumn.DateTime];
            SearchTerm dateTime = dateTimeControl.CreateSearchTerm();
            dateTime.UseForSearching = true;
            dateTime.DatabaseValue = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            fileDatabase.CustomSelection.SearchTerms.Add(dateTime);
            Assert.IsTrue(fileDatabase.CustomSelection.CreateSelect().Where.Count == 1);
            fileDatabase.SelectFiles(FileSelection.Custom);
            Assert.IsTrue(fileDatabase.Files.RowCount == fileExpectations.Count);

            dateTime.Operator = Constant.SearchTermOperator.Equal;
            Assert.IsTrue(fileDatabase.CustomSelection.CreateSelect().Where.Count == 1);
            fileDatabase.SelectFiles(FileSelection.Custom);
            Assert.IsTrue(fileDatabase.Files.RowCount == 0);

            dateTime.UseForSearching = false;
            fileDatabase.CustomSelection.TermCombiningOperator = LogicalOperator.And;

            ControlRow fileControl = fileDatabase.Controls[Constant.FileColumn.File];
            SearchTerm fileName = fileControl.CreateSearchTerm();
            fileName.UseForSearching = true;
            fileName.Operator = Constant.SearchTermOperator.Glob;
            fileName.DatabaseValue = "*" + Constant.File.JpgFileExtension.ToUpperInvariant();
            fileDatabase.CustomSelection.SearchTerms.Add(fileName);
            Assert.IsTrue(fileDatabase.CustomSelection.CreateSelect().Where.Count == 1);
            fileDatabase.SelectFiles(FileSelection.Custom);
            Assert.IsTrue(fileDatabase.Files.RowCount == fileExpectations.Count);

            ControlRow classificationControl = fileDatabase.Controls[Constant.FileColumn.Classification];
            SearchTerm fileClassification = classificationControl.CreateSearchTerm();
            fileClassification.UseForSearching = true;
            fileClassification.Operator = Constant.SearchTermOperator.Equal;
            fileClassification.DatabaseValue = FileClassification.Color;
            fileDatabase.CustomSelection.SearchTerms.Add(fileClassification);
            Assert.IsTrue(fileDatabase.CustomSelection.CreateSelect().Where.Count == 2);
            fileDatabase.SelectFiles(FileSelection.Custom);
            Assert.IsTrue(fileDatabase.Files.RowCount == expectedColorImageCount);

            fileClassification.DatabaseValue = FileClassification.Greyscale;
            Assert.IsTrue(fileDatabase.CustomSelection.CreateSelect().Where.Count == 2);
            fileDatabase.SelectFiles(FileSelection.Custom);
            Assert.IsTrue(fileDatabase.Files.RowCount == expectedGreyscaleImageCount);

            fileClassification.DatabaseValue = FileClassification.Color;
            ControlRow relativePathControl = fileDatabase.Controls[Constant.FileColumn.RelativePath];
            SearchTerm relativePath = relativePathControl.CreateSearchTerm();
            relativePath.UseForSearching = true;
            relativePath.Operator = Constant.SearchTermOperator.Equal;
            relativePath.DatabaseValue = fileExpectations[0].RelativePath;
            fileDatabase.CustomSelection.SearchTerms.Add(relativePath);
            Assert.IsTrue(fileDatabase.CustomSelection.CreateSelect().Where.Count == 3);
            fileDatabase.SelectFiles(FileSelection.Custom);
            Assert.IsTrue(fileDatabase.Files.RowCount == 1);

            ControlRow deleteControl = fileDatabase.Controls[Constant.FileColumn.DeleteFlag];
            SearchTerm markedForDeletion = deleteControl.CreateSearchTerm();
            markedForDeletion.UseForSearching = true;
            markedForDeletion.Operator = Constant.SearchTermOperator.Equal;
            markedForDeletion.DatabaseValue = false;
            fileDatabase.CustomSelection.SearchTerms.Add(markedForDeletion);
            Assert.IsTrue(fileDatabase.CustomSelection.CreateSelect().Where.Count == 4);
            fileDatabase.SelectFiles(FileSelection.Custom);
            Assert.IsTrue(fileDatabase.Files.RowCount == 1);

            fileClassification.DatabaseValue = FileClassification.Dark;
            Assert.IsTrue(fileDatabase.CustomSelection.CreateSelect().Where.Count == 4);
            Assert.IsTrue(fileDatabase.GetFileCount(FileSelection.All) == fileExpectations.Count);
            Assert.IsTrue(fileDatabase.GetFileCount(FileSelection.Color) == expectedColorImageCount);
            Assert.IsTrue(fileDatabase.GetFileCount(FileSelection.Corrupt) == 0);
            Assert.IsTrue(fileDatabase.GetFileCount(FileSelection.Custom) == 0);
            Assert.IsTrue(fileDatabase.GetFileCount(FileSelection.Dark) == 0);
            Assert.IsTrue(fileDatabase.GetFileCount(FileSelection.Greyscale) == expectedGreyscaleImageCount);
            Assert.IsTrue(fileDatabase.GetFileCount(FileSelection.MarkedForDeletion) == 0);
            Assert.IsTrue(fileDatabase.GetFileCount(FileSelection.NoLongerAvailable) == 0);
            Assert.IsTrue(fileDatabase.GetFileCount(FileSelection.Video) == 0);

            // reread
            fileDatabase.SelectFiles(FileSelection.All);

            // get file for marker testing
            int martenImageID = 1;
            FileExpectations martenExpectations = fileExpectations.Single(expectation => expectation.ID == martenImageID);
            ImageRow martenImage = fileDatabase.Files.Single(file => file.ID == martenImageID);
            martenExpectations.Verify(martenImage, imageSetTimeZone);

            // add markers
            for (int counterIndex = 0; counterIndex < counterControls.Count; ++counterIndex)
            {
                ControlRow counter = counterControls[counterIndex];
                MarkersForCounter markersForCounter = martenImage.GetMarkersForCounter(counter.DataLabel);

                List<Point> expectedPositions = new();
                for (int markerIndex = 0; markerIndex < counterIndex; ++markerIndex)
                {
                    int initialCounterCount = markersForCounter.Count;
                    int initialMarkers = markersForCounter.Markers.Count;

                    Point markerPosition = new((0.1 * counterIndex) + (0.1 * markerIndex), (0.05 * counterIndex) + (0.1 * markerIndex));
                    markersForCounter.AddMarker(new Marker(markersForCounter.DataLabel, markerPosition));

                    Assert.IsTrue(markersForCounter.Count == initialCounterCount + 1);
                    Assert.IsTrue(markersForCounter.Markers.Count == initialMarkers + 1);
                }

                martenExpectations.UserControlsByDataLabel[counter.DataLabel] = markersForCounter.Count;
                string dataLabelForMarkerPositions = FileTable.GetMarkerPositionColumnName(counter.DataLabel);
                martenExpectations.UserControlsByDataLabel[dataLabelForMarkerPositions] = markersForCounter.MarkerPositionsToFloatArray();
            }
            martenExpectations.Verify(martenImage, imageSetTimeZone);
            Assert.IsTrue(fileDatabase.TrySyncFileToDatabase(martenImage));
            martenExpectations.Verify(martenImage, imageSetTimeZone);

            // roundtrip
            fileDatabase.SelectFiles(FileSelection.All);
            martenImage = fileDatabase.Files.Single(file => file.ID == martenImageID);
            martenExpectations.Verify(martenImage, imageSetTimeZone);

            // remove last marker
            for (int counterIndex = 0; counterIndex < counterControls.Count; ++counterIndex)
            {
                MarkersForCounter markersForCounter = martenImage.GetMarkersForCounter(counterControls[counterIndex].DataLabel);
                if (markersForCounter.Markers.Count > 0)
                {
                    int initialCounterCount = markersForCounter.Count;
                    int initialMarkers = markersForCounter.Markers.Count;

                    markersForCounter.RemoveMarker(markersForCounter.Markers[^1]);

                    Assert.IsTrue(markersForCounter.Count == initialCounterCount - 1);
                    Assert.IsTrue(markersForCounter.Markers.Count == initialMarkers - 1);
                    martenExpectations.UserControlsByDataLabel[markersForCounter.DataLabel] = markersForCounter.Count;
                    string dataLabelForMarkerPositions = FileTable.GetMarkerPositionColumnName(markersForCounter.DataLabel);
                    martenExpectations.UserControlsByDataLabel[dataLabelForMarkerPositions] = markersForCounter.MarkerPositionsToFloatArray();
                }
            }
            martenExpectations.Verify(martenImage, imageSetTimeZone);
            Assert.IsTrue(fileDatabase.TrySyncFileToDatabase(martenImage));
            martenExpectations.Verify(martenImage, imageSetTimeZone);

            // roundtrip
            fileDatabase.SelectFiles(FileSelection.All);
            martenImage = fileDatabase.Files.Single(file => file.ID == martenImageID);
            martenExpectations.Verify(martenImage, imageSetTimeZone);
        }

        [TestMethod]
        public void CreateUpdateReuseTemplateDatabase()
        {
            string templateDatabaseBaseFileName = TestConstant.File.DefaultNewTemplateDatabaseFileName;
            using TemplateDatabase templateDatabase = this.CreateTemplateDatabase(templateDatabaseBaseFileName);

            // populate template database
            this.VerifyTemplateDatabase(templateDatabase, templateDatabaseBaseFileName);
            int numberOfStandardControls = Constant.Control.StandardControls.Count;
            Assert.IsTrue(templateDatabase.Controls.RowCount == numberOfStandardControls);
            this.VerifyControls(templateDatabase);

            ControlRow newControl = templateDatabase.AppendUserDefinedControl(ControlType.Counter);
            Assert.IsTrue(templateDatabase.Controls.RowCount == numberOfStandardControls + 1);
            this.VerifyControl(newControl);

            newControl = templateDatabase.AppendUserDefinedControl(ControlType.FixedChoice);
            Assert.IsTrue(templateDatabase.Controls.RowCount == numberOfStandardControls + 2);
            this.VerifyControl(newControl);

            newControl = templateDatabase.AppendUserDefinedControl(ControlType.Flag);
            Assert.IsTrue(templateDatabase.Controls.RowCount == numberOfStandardControls + 3);
            this.VerifyControl(newControl);

            newControl = templateDatabase.AppendUserDefinedControl(ControlType.Note);
            Assert.IsTrue(templateDatabase.Controls.RowCount == numberOfStandardControls + 4);
            this.VerifyControl(newControl);
            this.VerifyTemplateDatabase(templateDatabase, templateDatabaseBaseFileName);

            templateDatabase.RemoveUserDefinedControl(templateDatabase.Controls[numberOfStandardControls + 2]);
            Assert.IsTrue(templateDatabase.Controls.RowCount == numberOfStandardControls + 3);
            templateDatabase.RemoveUserDefinedControl(templateDatabase.Controls[numberOfStandardControls + 2]);
            Assert.IsTrue(templateDatabase.Controls.RowCount == numberOfStandardControls + 2);
            templateDatabase.RemoveUserDefinedControl(templateDatabase.Controls[numberOfStandardControls + 0]);
            Assert.IsTrue(templateDatabase.Controls.RowCount == numberOfStandardControls + 1);
            templateDatabase.RemoveUserDefinedControl(templateDatabase.Controls[numberOfStandardControls + 0]);
            Assert.IsTrue(templateDatabase.Controls.RowCount == numberOfStandardControls);
            this.VerifyTemplateDatabase(templateDatabase, templateDatabaseBaseFileName);

            int iterations = 10;
            for (int iteration = 0; iteration < iterations; ++iteration)
            {
                ControlRow noteControl = templateDatabase.AppendUserDefinedControl(ControlType.Note);
                this.VerifyControl(noteControl);
                ControlRow flagControl = templateDatabase.AppendUserDefinedControl(ControlType.Flag);
                this.VerifyControl(flagControl);
                ControlRow choiceControl = templateDatabase.AppendUserDefinedControl(ControlType.FixedChoice);
                choiceControl.WellKnownValues = "DefaultChoice|OtherChoice";
                templateDatabase.TrySyncControlToDatabase(choiceControl);
                this.VerifyControl(choiceControl);
                ControlRow counterControl = templateDatabase.AppendUserDefinedControl(ControlType.Counter);
                this.VerifyControl(counterControl);
            }

            // modify control and spreadsheet orders
            // control order ends up reverse order from ID, spreadsheet order is alphabetical
            Dictionary<string, int> newControlOrderByDataLabel = new(StringComparer.Ordinal);
            int controlOrder = templateDatabase.Controls.RowCount;
            for (int row = 0; row < templateDatabase.Controls.RowCount; --controlOrder, ++row)
            {
                string dataLabel = templateDatabase.Controls[row].DataLabel;
                newControlOrderByDataLabel.Add(dataLabel, controlOrder);
            }
            templateDatabase.UpdateDisplayOrder(Constant.ControlColumn.ControlOrder, newControlOrderByDataLabel);

            List<string> alphabeticalDataLabels = newControlOrderByDataLabel.Keys.ToList();
            alphabeticalDataLabels.Sort();
            Dictionary<string, int> newSpreadsheetOrderByDataLabel = new(StringComparer.Ordinal);
            int spreadsheetOrder = 0;
            foreach (string dataLabel in alphabeticalDataLabels)
            {
                newSpreadsheetOrderByDataLabel.Add(dataLabel, ++spreadsheetOrder);
            }
            templateDatabase.UpdateDisplayOrder(Constant.ControlColumn.SpreadsheetOrder, newSpreadsheetOrderByDataLabel);
            this.VerifyTemplateDatabase(templateDatabase, templateDatabaseBaseFileName);

            // remove some controls and verify the control and spreadsheet orders are properly updated
            templateDatabase.RemoveUserDefinedControl(templateDatabase.Controls[numberOfStandardControls + 22]);
            templateDatabase.RemoveUserDefinedControl(templateDatabase.Controls[numberOfStandardControls + 16]);
            this.VerifyTemplateDatabase(templateDatabase, templateDatabaseBaseFileName);

            // create a database to capture the current template into its template table
            using FileDatabase fileDatabase = this.CreateFileDatabase(templateDatabase, TestConstant.File.DefaultNewFileDatabaseFileName);
            // modify UX properties of some controls by data row manipulation
            int copyableIndex = numberOfStandardControls + (3 * iterations) - 1;
            ControlRow copyableControl = templateDatabase.Controls[copyableIndex];
            bool modifiedCopyable = !copyableControl.Copyable;
            copyableControl.Copyable = modifiedCopyable;
            templateDatabase.TrySyncControlToDatabase(copyableControl);
            Assert.IsTrue(templateDatabase.Controls[copyableIndex].Copyable == modifiedCopyable);

            int defaultValueIndex = numberOfStandardControls + (3 * iterations) - 4;
            ControlRow defaultValueControl = templateDatabase.Controls[defaultValueIndex];
            Assert.IsTrue(defaultValueControl.ControlType == ControlType.Flag); // check
            int modifiedDefaultValue = 1;
            defaultValueControl.DefaultValue = modifiedDefaultValue.ToString(Constant.InvariantCulture);
            templateDatabase.TrySyncControlToDatabase(defaultValueControl);
            Assert.IsTrue(String.Equals(templateDatabase.Controls[defaultValueIndex].DefaultValue, modifiedDefaultValue.ToString(Constant.InvariantCulture), StringComparison.Ordinal));

            int labelIndex = numberOfStandardControls + (3 * iterations) - 3;
            ControlRow labelControl = templateDatabase.Controls[labelIndex];
            string modifiedLabel = "Label modification roundtrip.";
            labelControl.Label = modifiedLabel;
            templateDatabase.TrySyncControlToDatabase(labelControl);
            Assert.IsTrue(templateDatabase.Controls[labelIndex].Label == modifiedLabel);

            int listIndex = numberOfStandardControls + (3 * iterations) - 2;
            ControlRow listControl = templateDatabase.Controls[listIndex];
            string modifiedList = listControl.WellKnownValues + "|NewChoice0|NewChoice1";
            listControl.WellKnownValues = modifiedList;
            templateDatabase.TrySyncControlToDatabase(listControl);
            Assert.IsTrue(templateDatabase.Controls[listIndex].WellKnownValues == modifiedList);

            int tooltipIndex = numberOfStandardControls + (3 * iterations) - 3;
            ControlRow tooltipControl = templateDatabase.Controls[tooltipIndex];
            string modifiedTooltip = "Tooltip modification roundtrip.";
            tooltipControl.Tooltip = modifiedTooltip;
            templateDatabase.TrySyncControlToDatabase(tooltipControl);
            Assert.IsTrue(templateDatabase.Controls[tooltipIndex].Tooltip == modifiedTooltip);

            int widthIndex = numberOfStandardControls + (3 * iterations) - 2;
            ControlRow widthControl = templateDatabase.Controls[widthIndex];
            int modifiedWidth = 1000;
            widthControl.MaxWidth = modifiedWidth;
            templateDatabase.TrySyncControlToDatabase(widthControl);
            Assert.IsTrue(templateDatabase.Controls[widthIndex].MaxWidth == modifiedWidth);

            int visibleIndex = numberOfStandardControls + (3 * iterations) - 3;
            ControlRow visibleControl = templateDatabase.Controls[visibleIndex];
            bool modifiedVisible = !visibleControl.Visible;
            visibleControl.Visible = modifiedVisible;
            templateDatabase.TrySyncControlToDatabase(visibleControl);
            Assert.IsTrue(templateDatabase.Controls[visibleIndex].Visible == modifiedVisible);

            newControl = templateDatabase.AppendUserDefinedControl(ControlType.Note);
            newControl.AnalysisLabel = true;
            templateDatabase.TrySyncControlToDatabase(newControl);

            // reopen the template database and check again
            string templateDatabaseFilePath = templateDatabase.FilePath;
            Assert.IsTrue(TemplateDatabase.TryCreateOrOpen(templateDatabaseFilePath, out TemplateDatabase templateDatabaseReopened));

            using (templateDatabaseReopened)
            {
                this.VerifyTemplateDatabase(templateDatabaseReopened, templateDatabaseFilePath);
                int expectedControlCount = numberOfStandardControls + (4 * iterations) - 1;
                Assert.IsTrue(templateDatabaseReopened.Controls.RowCount == expectedControlCount);
                this.VerifyControls(templateDatabaseReopened);
                Assert.IsTrue(templateDatabaseReopened.Controls[copyableIndex].Copyable == modifiedCopyable);
                Assert.IsTrue(String.Equals(templateDatabaseReopened.Controls[defaultValueIndex].DefaultValue, modifiedDefaultValue.ToString(Constant.InvariantCulture), StringComparison.Ordinal));
                Assert.IsTrue(templateDatabaseReopened.Controls[labelIndex].Label == modifiedLabel);
                Assert.IsTrue(templateDatabaseReopened.Controls[listIndex].WellKnownValues == modifiedList);
                Assert.IsTrue(templateDatabaseReopened.Controls[tooltipIndex].Tooltip == modifiedTooltip);
                Assert.IsTrue(templateDatabaseReopened.Controls[visibleIndex].Visible == modifiedVisible);
                Assert.IsTrue(templateDatabaseReopened.Controls[widthIndex].MaxWidth == modifiedWidth);

                // reopen the file database to synchronize its template table with the modified table in the current template
                Assert.IsTrue(FileDatabase.TryCreateOrOpen(fileDatabase.FilePath, templateDatabaseReopened, false, LogicalOperator.And, out FileDatabase fileDatabaseReopened));
                using (fileDatabaseReopened)
                {
                    Assert.IsTrue(fileDatabaseReopened.ControlSynchronizationIssues.Count == 0);
                    this.VerifyTemplateDatabase(fileDatabaseReopened, fileDatabaseReopened.FilePath);
                    Assert.IsTrue(fileDatabaseReopened.Controls.RowCount == expectedControlCount);
                    this.VerifyControls(fileDatabaseReopened);
                    Assert.IsTrue(fileDatabaseReopened.Controls[copyableIndex].Copyable == modifiedCopyable);
                    Assert.IsTrue(String.Equals(fileDatabaseReopened.Controls[defaultValueIndex].DefaultValue, modifiedDefaultValue.ToString(Constant.InvariantCulture), StringComparison.Ordinal));
                    Assert.IsTrue(fileDatabaseReopened.Controls[labelIndex].Label == modifiedLabel);
                    Assert.IsTrue(fileDatabaseReopened.Controls[listIndex].WellKnownValues == modifiedList);
                    Assert.IsTrue(fileDatabaseReopened.Controls[tooltipIndex].Tooltip == modifiedTooltip);
                    Assert.IsTrue(fileDatabaseReopened.Controls[visibleIndex].Visible == modifiedVisible);
                    Assert.IsTrue(fileDatabaseReopened.Controls[widthIndex].MaxWidth == modifiedWidth);

                    ControlRow newControlInFileDatabase = fileDatabaseReopened.Controls.Single(control => String.Equals(control.DataLabel, newControl.DataLabel, StringComparison.Ordinal));
                    Assert.IsTrue(fileDatabaseReopened.Files.UserColumnsByName.Count == expectedControlCount - numberOfStandardControls + fileDatabaseReopened.Controls.Count(control => control.ControlType == ControlType.Counter)); // counters have two columns each
                }
            }
        }

        [TestMethod]
        public void DateTimeHandling()
        {
            // DateTimeExtensions
            DateTime utcNow = DateTime.UtcNow;
            DateTime utcNowUnspecified = utcNow.AsUnspecifed();
            Assert.IsTrue(utcNow.Date == utcNowUnspecified.Date);
            Assert.IsTrue((utcNow.Hour == utcNowUnspecified.Hour) &&
                          (utcNow.Minute == utcNowUnspecified.Minute) &&
                          (utcNow.Second == utcNowUnspecified.Second) &&
                          (utcNow.Millisecond == utcNowUnspecified.Millisecond));
            Assert.IsTrue(utcNowUnspecified.Kind == DateTimeKind.Unspecified);

            // DateTimeOffsetExtensions
            DateTime utcNowWithoutMicroseconds = new(utcNow.Year, utcNow.Month, utcNow.Day, utcNow.Hour, utcNow.Minute, utcNow.Second, utcNow.Millisecond, DateTimeKind.Utc);
            DateTimeOffset utcNowOffset = new(utcNowWithoutMicroseconds);
            TimeZoneInfo minUtcOffsetTimeZone = TimeZoneInfo.FindSystemTimeZoneById(TestConstant.TimeZone.Dateline);
            TimeZoneInfo maxUtcOffsetTimeZone = TimeZoneInfo.FindSystemTimeZoneById(TestConstant.TimeZone.LineIslands);
            DateTimeOffset minUtcOffset = utcNowOffset.SetOffset(minUtcOffsetTimeZone.BaseUtcOffset);
            DateTimeOffset localUtcOffset = utcNowOffset.SetOffset(TimeZoneInfo.Local.BaseUtcOffset);
            DateTimeOffset utcOffsetRoundtrip = utcNowOffset.SetOffset(TimeZoneInfo.Utc.BaseUtcOffset);
            DateTimeOffset maxUtcOffset = utcNowOffset.SetOffset(maxUtcOffsetTimeZone.BaseUtcOffset);

            Assert.IsTrue((minUtcOffset.Date == utcNowOffset.Date) &&
                          (minUtcOffset.TimeOfDay == utcNowOffset.TimeOfDay) &&
                          (minUtcOffset.Offset == minUtcOffsetTimeZone.BaseUtcOffset));
            Assert.IsTrue(minUtcOffset.DateTime == utcNowUnspecified);
            Assert.IsTrue((localUtcOffset.Date == utcNowOffset.Date) &&
                          (localUtcOffset.TimeOfDay == utcNowOffset.TimeOfDay) &&
                          (localUtcOffset.Offset == TimeZoneInfo.Local.BaseUtcOffset));
            Assert.IsTrue(localUtcOffset.DateTime == utcNowUnspecified);
            Assert.IsTrue((utcOffsetRoundtrip.Date == utcNowOffset.Date) &&
                          (utcOffsetRoundtrip.TimeOfDay == utcNowOffset.TimeOfDay) &&
                          (utcOffsetRoundtrip.Offset == TimeZoneInfo.Utc.BaseUtcOffset));
            Assert.IsTrue(utcOffsetRoundtrip.DateTime == utcNowUnspecified);
            Assert.IsTrue(utcOffsetRoundtrip.UtcDateTime == utcNowOffset.UtcDateTime);
            Assert.IsTrue(utcOffsetRoundtrip.UtcDateTime == utcNowUnspecified);
            Assert.IsTrue((maxUtcOffset.Date == utcNowOffset.Date) &&
                          (maxUtcOffset.TimeOfDay == utcNowOffset.TimeOfDay) &&
                          (maxUtcOffset.Offset == maxUtcOffsetTimeZone.BaseUtcOffset));
            Assert.IsTrue(maxUtcOffset.DateTime == utcNowUnspecified);

            // DateTimeHandler
            this.DateTimeHandling(new DateTimeOffset(utcNowUnspecified, minUtcOffsetTimeZone.GetUtcOffset(utcNowUnspecified)));

            DateTime now = DateTime.Now;
            DateTime nowWithoutMicroseconds = new(now.Year, now.Month, now.Day, now.Hour, now.Minute, now.Second, now.Millisecond, DateTimeKind.Local);
            this.DateTimeHandling(new DateTimeOffset(nowWithoutMicroseconds));
            DateTime nowUnspecified = now.AsUnspecifed();
            this.DateTimeHandling(new DateTimeOffset(nowUnspecified, TimeZoneInfo.Local.GetUtcOffset(nowUnspecified)));

            this.DateTimeHandling(new DateTimeOffset(utcNowWithoutMicroseconds));

            this.DateTimeHandling(new DateTimeOffset(utcNowUnspecified, maxUtcOffsetTimeZone.GetUtcOffset(utcNowUnspecified)));

            DateTime nowWithoutMilliseconds = new(now.Year, now.Month, now.Day, now.Hour, now.Minute, now.Second);
            foreach (string format in new List<string>() { "yyyy:MM:dd HH:mm:ss", "ddd MMM dd HH:mm:ss K yyyy" })
            {
                string metadataDateAsString = now.ToString(format, Constant.InvariantCulture);
                Assert.IsTrue(DateTimeHandler.TryParseMetadataDateTaken(metadataDateAsString, TimeZoneInfo.Local, out DateTimeOffset metadataDateParsed));
                Assert.IsTrue((metadataDateParsed.Date == nowWithoutMilliseconds.Date) &&
                              (metadataDateParsed.TimeOfDay == nowWithoutMilliseconds.TimeOfDay) &&
                              (metadataDateParsed.Offset == TimeZoneInfo.Local.GetUtcOffset(nowWithoutMilliseconds)));
            }

            DateTimeOffset swappable = new(new DateTime(now.Year, 1, 12, now.Hour, now.Minute, now.Second, now.Millisecond), TimeZoneInfo.Local.BaseUtcOffset);
            Assert.IsTrue(DateTimeHandler.TrySwapDayMonth(swappable, out DateTimeOffset _));
            DateTimeOffset notSwappable = new(new DateTime(now.Year, 1, 13, now.Hour, now.Minute, now.Second, now.Millisecond), TimeZoneInfo.Local.BaseUtcOffset);
            Assert.IsFalse(DateTimeHandler.TrySwapDayMonth(notSwappable, out DateTimeOffset _));

            string timeSpanDisplayStringLessThanOneDay = DateTimeHandler.ToDisplayTimeSpanString(new TimeSpan(-1, -45, -15));
            string timeSpanDisplayStringOneDay = DateTimeHandler.ToDisplayTimeSpanString(new TimeSpan(1, 13, 00, 18));
            string timeSpanDisplayStringMoreThanOneDay = DateTimeHandler.ToDisplayTimeSpanString(new TimeSpan(-2, -22, -22, -22, -222));
            Assert.IsTrue(timeSpanDisplayStringLessThanOneDay == "-01:45:15");
            Assert.IsTrue(timeSpanDisplayStringOneDay == "1 day 13:00:18");
            Assert.IsTrue(timeSpanDisplayStringMoreThanOneDay == "-2 days -22:22:22");
        }

        private void DateTimeHandling(DateTimeOffset dateTimeOffset)
        {
            // database format roundtrips
            string dateTimeAsDatabaseString = DateTimeHandler.ToDatabaseDateTimeString(dateTimeOffset);
            DateTime dateTimeParse = DateTimeHandler.ParseDatabaseDateTime(dateTimeAsDatabaseString);
            Assert.IsTrue(DateTimeHandler.TryParseDatabaseDateTime(dateTimeAsDatabaseString, out DateTime dateTimeTryParse));

            Assert.IsTrue(dateTimeParse == dateTimeOffset.UtcDateTime);
            Assert.IsTrue(dateTimeTryParse == dateTimeOffset.UtcDateTime);

            string utcOffsetAsDatabaseString = DateTimeHandler.ToDatabaseUtcOffsetString(dateTimeOffset.Offset);
            Assert.IsTrue(DateTimeHandler.TryParseDatabaseUtcOffset(utcOffsetAsDatabaseString, out TimeSpan utcOffsetTryParse));
            Assert.IsTrue(utcOffsetTryParse == dateTimeOffset.Offset);

            // display format roundtrips
            string dateTimeAsDisplayString = DateTimeHandler.ToDisplayDateTimeString(dateTimeOffset);
            dateTimeParse = DateTimeHandler.ParseDisplayDateTimeString(dateTimeAsDisplayString);

            DateTimeOffset dateTimeOffsetWithoutMilliseconds = new(new DateTime(dateTimeOffset.Year, dateTimeOffset.Month, dateTimeOffset.Day, dateTimeOffset.Hour, dateTimeOffset.Minute, dateTimeOffset.Second), dateTimeOffset.Offset);
            Assert.IsTrue(dateTimeParse == dateTimeOffsetWithoutMilliseconds.DateTime);

            // display only formats
            string dateTimeOffsetAsDisplayString = DateTimeHandler.ToDisplayDateTimeUtcOffsetString(dateTimeOffset);
            string utcOffsetAsDisplayString = DateTimeHandler.ToDisplayUtcOffsetString(dateTimeOffset.Offset);

            Assert.IsTrue(dateTimeOffsetAsDisplayString.Length > Constant.Time.DateTimeDisplayFormat.Length);
            Assert.IsTrue(utcOffsetAsDisplayString.Length >= Constant.Time.UtcOffsetDisplayFormat.Length - 1);
        }

        [TestMethod]
        public void EnumHandling()
        {
            ImageSetOptions options = ImageSetOptions.None;

            options = options.SetFlag(ImageSetOptions.None, false);
            Assert.IsTrue(options == ImageSetOptions.None);
            options = options.SetFlag(ImageSetOptions.None, true);
            Assert.IsTrue(options == ImageSetOptions.None);
            options = options.SetFlag(ImageSetOptions.None, false);
            Assert.IsTrue(options == ImageSetOptions.None);

            options = options.SetFlag(ImageSetOptions.Magnifier, false);
            Assert.IsTrue(options == ImageSetOptions.None);
            options = options.SetFlag(ImageSetOptions.Magnifier, true);
            Assert.IsTrue(options == ImageSetOptions.Magnifier);
            options = options.SetFlag(ImageSetOptions.Magnifier, false);
            Assert.IsTrue(options == ImageSetOptions.None);
        }

        [TestMethod]
        public void FileDatabaseNegative()
        {
            using TemplateDatabase templateDatabase = this.CloneTemplateDatabase(TestConstant.File.DefaultTemplateDatabaseFileName);
            using FileDatabase fileDatabase = this.CreateFileDatabase(templateDatabase, TestConstant.File.DefaultFileDatabaseFileName);
            this.PopulateDefaultDatabase(fileDatabase);

            // FileDatabase methods
            int firstDisplayableFile = fileDatabase.GetCurrentOrNextDisplayableFile(fileDatabase.CurrentlySelectedFileCount);
            Assert.IsTrue(firstDisplayableFile == fileDatabase.CurrentlySelectedFileCount - 1);

            int closestDisplayableFile = fileDatabase.GetFileOrNextFileIndex(Int64.MinValue);
            Assert.IsTrue(closestDisplayableFile == 0);
            closestDisplayableFile = fileDatabase.GetFileOrNextFileIndex(Int64.MaxValue);
            Assert.IsTrue(closestDisplayableFile == fileDatabase.CurrentlySelectedFileCount - 1);

            Assert.IsFalse(fileDatabase.IsFileDisplayable(-1));
            Assert.IsFalse(fileDatabase.IsFileDisplayable(fileDatabase.CurrentlySelectedFileCount));

            Assert.IsFalse(fileDatabase.IsFileRowInRange(-1));
            Assert.IsFalse(fileDatabase.IsFileRowInRange(fileDatabase.CurrentlySelectedFileCount));

            ImageRow file = fileDatabase.Files[0];
            FileInfo fileInfo = file.GetFileInfo(fileDatabase.FolderPath);

            // template table synchronization
            // Remove choices in use and change a note to a choice to produce two synchronization issues.  Unused choices
            // are also removed to verify their removal doesn't raise a synchronization issue.
            ControlRow choiceControl = templateDatabase.Controls[TestConstant.DefaultDatabaseColumn.Choice0];
            choiceControl.WellKnownValues = "Choice0|Choice1|Choice2|Choice3|Choice4|Choice5|Choice6|Choice7";
            templateDatabase.TrySyncControlToDatabase(choiceControl);
            ControlRow noteControl = templateDatabase.Controls[TestConstant.DefaultDatabaseColumn.Note0];
            noteControl.ControlType = ControlType.FixedChoice;
            templateDatabase.TrySyncControlToDatabase(noteControl);

            Assert.IsFalse(FileDatabase.TryCreateOrOpen(fileDatabase.FileName, templateDatabase, false, LogicalOperator.And, out FileDatabase fileDatabaseReopened));
            using (fileDatabaseReopened)
            {
                Assert.IsTrue(fileDatabaseReopened.ControlSynchronizationIssues.Count == 2);
                Assert.IsTrue(fileDatabaseReopened.ControlSynchronizationIssues[0].IndexOf(TestConstant.DefaultDatabaseColumn.Choice0, StringComparison.Ordinal) != -1);
                Assert.IsTrue(fileDatabaseReopened.ControlSynchronizationIssues[1].IndexOf(TestConstant.DefaultDatabaseColumn.Note0, StringComparison.Ordinal) != -1);
            }
        }

        [TestMethod]
        public async Task HybridVideoFileIOTransactionsAsync()
        {
            // add files to database
            using FileDatabase fileDatabase = this.CreateFileDatabase(TestConstant.File.DefaultTemplateDatabaseFileName, TestConstant.File.DefaultNewFileDatabaseFileName);
            CarnassialState state = new()
            {
                SkipFileClassification = false
            };
            TimeSpan desiredStatusUpdateInterval = state.Throttles.GetDesiredProgressUpdateInterval();
            string folderToLoad = Path.Combine(this.WorkingDirectory, TestConstant.File.HybridVideoDirectoryName);
            FileInfo[] imagesAndVideos = new DirectoryInfo(folderToLoad).GetFiles();

            Stopwatch stopwatch = new();
            stopwatch.Start();
            using (AddFilesIOComputeTransactionManager folderLoad = new(this.UpdateFolderLoadProgress, desiredStatusUpdateInterval))
            {
                folderLoad.FolderPaths.Add(folderToLoad);
                folderLoad.FindFilesToLoad(fileDatabase.FolderPath);
                Assert.IsTrue(folderLoad.FilesToLoad == imagesAndVideos.Length);

                int filesLoaded = await folderLoad.AddFilesAsync(fileDatabase, state, Constant.Images.MinimumRenderWidth).ConfigureAwait(false);
                Assert.IsTrue(filesLoaded == imagesAndVideos.Length);
                Assert.IsTrue(fileDatabase.Files.RowCount == imagesAndVideos.Length);
                fileDatabase.SelectFiles(FileSelection.All);
                Assert.IsTrue(fileDatabase.Files.RowCount == imagesAndVideos.Length);
                this.TestContext.WriteLine("AddFilesAsync({0}): {1:0.000}s", filesLoaded, stopwatch.Elapsed.TotalSeconds);

                Assert.IsTrue(folderLoad.IODuration >= TimeSpan.Zero);
                Assert.IsTrue(folderLoad.ComputeDuration >= folderLoad.IODuration);
                Assert.IsTrue(folderLoad.DatabaseDuration >= folderLoad.ComputeDuration);
            }
            stopwatch.Stop();

            // components for rereading metadata
            List<DateTimeOffset> fileDateTimes = new(imagesAndVideos.Length);
            Dictionary<string, Dictionary<string, ImageRow>> filesByRelativePathAndName = fileDatabase.Files.GetFilesByRelativePathAndName();
            TimeZoneInfo imageSetTimeZone = fileDatabase.ImageSet.GetTimeZoneInfo();
            ImageRow previousFile = null;
            for (int fileIndex = 0; fileIndex < imagesAndVideos.Length; ++fileIndex)
            {
                FileInfo fileInfo = imagesAndVideos[fileIndex];
                ImageRow file = filesByRelativePathAndName[TestConstant.File.HybridVideoDirectoryName][fileInfo.Name];
                Assert.IsTrue(String.Equals(file.FileName, fileInfo.Name, StringComparison.OrdinalIgnoreCase));
                DateTimeOffset dateTimeOffsetAsAdded = file.DateTimeOffset;
                bool expectedIsVideo = !fileInfo.Name.EndsWith(Constant.File.JpgFileExtension, StringComparison.OrdinalIgnoreCase);
                Assert.IsTrue(file.IsVideo == expectedIsVideo);
                FileClassification expectedClassification = expectedIsVideo ? FileClassification.Video : FileClassification.Color;
                Assert.IsTrue(file.Classification == expectedClassification);

                if (expectedIsVideo == false)
                {
                    using CachedImage image = await file.TryLoadImageAsync(fileDatabase.FolderPath, Constant.Images.MinimumRenderWidth).ConfigureAwait(true);
                    stopwatch.Restart();
                    double luminosity = image.Image.GetLuminosityAndColoration(0, out double coloration);
                    file.Classification = new ImageProperties(luminosity, coloration).EvaluateNewClassification(Constant.Images.DarkLuminosityThresholdDefault);
                    stopwatch.Stop();
                    this.TestContext.WriteLine("Classify({0}, {1:0.00}MP): {2}ms", file.FileName, 1E-6 * image.Image.TotalPixels, stopwatch.ElapsedMilliseconds);
                    Assert.IsTrue(file.Classification == FileClassification.Color);
                }
                else
                {
                    Assert.IsTrue(file.Classification == FileClassification.Video);
                }

                // for images, verify the date can be found in metadata
                // for videos, verify the date is found in the previous image's metadata or not found if there's no previous image
                FileLoad firstLoad = new(file);
                FileLoad secondLoad = new((string)null);
                bool isSecondFile = false;
                if (file.IsVideo && (previousFile != null) && (previousFile.IsVideo == false))
                {
                    secondLoad = firstLoad;
                    firstLoad = new FileLoad(previousFile);
                    isSecondFile = true;
                }
                MetadataReadResults metadataReadResult;
                using (FileLoadAtom loadAtom = new(file.RelativePath, firstLoad, secondLoad, 0))
                {
                    loadAtom.CreateJpegs(fileDatabase.FolderPath);
                    loadAtom.ReadDateTimeOffsets(fileDatabase.FolderPath, imageSetTimeZone);
                    metadataReadResult = isSecondFile ? loadAtom.Second.MetadataReadResult : loadAtom.First.MetadataReadResult;
                }

                Assert.IsFalse(metadataReadResult.HasFlag(MetadataReadResults.Failed));
                Assert.IsTrue(this.DateTimesWithinOneMillisecond(file.DateTimeOffset, dateTimeOffsetAsAdded));
                if (String.Equals(file.FileName, "06260001.AVI", StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(Path.GetFileNameWithoutExtension(file.FileName), "06260048", StringComparison.OrdinalIgnoreCase))
                {
                    // 06260001.AVI, 06260048.AVI, and 06260048.mp4 lack an associated .jpg to read metadata from
                    Assert.IsTrue(metadataReadResult == MetadataReadResults.None);
                    Assert.IsTrue(file.DateTimeOffset.Date >= TestConstant.FileExpectation.HybridVideoFileDate);
                    Assert.IsTrue(file.DateTimeOffset.Date < DateTime.Now);
                }
                else if (String.Equals(Path.GetExtension(file.FileName), Constant.File.JpgFileExtension, StringComparison.OrdinalIgnoreCase))
                {
                    Assert.IsTrue(file.DateTimeOffset.Date == TestConstant.FileExpectation.HybridVideoFileDate);
                    Assert.IsTrue(metadataReadResult == MetadataReadResults.DateTime);
                }
                else
                {
                    Assert.IsTrue(file.DateTimeOffset.Date == TestConstant.FileExpectation.HybridVideoFileDate);
                    Assert.IsTrue(metadataReadResult == MetadataReadResults.DateTimeInferredFromPrevious);
                }

                fileDateTimes.Add(dateTimeOffsetAsAdded);
                previousFile = file;
            }

            // verify add of files already added doesn't duplicate them
            using (AddFilesIOComputeTransactionManager folderLoad = new(this.UpdateFolderLoadProgress, desiredStatusUpdateInterval))
            {
                folderLoad.FolderPaths.Add(folderToLoad);
                folderLoad.FindFilesToLoad(fileDatabase.FolderPath);
                int filesLoaded = await folderLoad.AddFilesAsync(fileDatabase, state, Constant.Images.MinimumRenderWidth).ConfigureAwait(false);
                Assert.IsTrue(filesLoaded == 0);
            }
            fileDatabase.SelectFiles(FileSelection.All);
            Assert.IsTrue(fileDatabase.Files.RowCount == imagesAndVideos.Length);

            // reread datetimes
            ObservableArray<DateTimeRereadResult> dateTimeResults = new(imagesAndVideos.Length, DateTimeRereadResult.Default);
            using (DateTimeRereadIOComputeTransactionManager rereadDateTimes = new(this.UpdateDateTimeRereadProgress, dateTimeResults, desiredStatusUpdateInterval))
            {
                await rereadDateTimes.RereadDateTimesAsync(fileDatabase).ConfigureAwait(false);
                Assert.IsTrue(rereadDateTimes.FilesCompleted == imagesAndVideos.Length);
                foreach (DateTimeRereadResult feedbackRow in dateTimeResults)
                {
                    Assert.IsNotNull(feedbackRow);
                    Assert.IsFalse(String.IsNullOrWhiteSpace(feedbackRow.FileName));
                    Assert.IsFalse(String.IsNullOrWhiteSpace(feedbackRow.Message));
                }
                Assert.IsTrue(rereadDateTimes.IODuration >= TimeSpan.Zero);
                Assert.IsTrue(rereadDateTimes.ComputeDuration >= rereadDateTimes.IODuration);
                Assert.IsTrue(rereadDateTimes.DatabaseDuration >= rereadDateTimes.ComputeDuration);
            }
            for (int fileIndex = 0; fileIndex < imagesAndVideos.Length; ++fileIndex)
            {
                DateTimeOffset dateTimeOffsetAsAdded = fileDateTimes[fileIndex];
                ImageRow file = fileDatabase.Files[fileIndex];
                Assert.IsTrue(this.DateTimesWithinOneMillisecond(file.DateTimeOffset, dateTimeOffsetAsAdded));
            }

            // reclassify
            using (ReclassifyIOComputeTransaction reclassify = new(this.UpdateReclassifyProgress, desiredStatusUpdateInterval))
            {
                await reclassify.ReclassifyFilesAsync(fileDatabase, state.DarkLuminosityThreshold, Constant.Images.MinimumRenderWidth).ConfigureAwait(false);
            }
            fileDatabase.SelectFiles(FileSelection.All);
            filesByRelativePathAndName = fileDatabase.Files.GetFilesByRelativePathAndName();
            for (int fileIndex = 0; fileIndex < imagesAndVideos.Length; ++fileIndex)
            {
                FileInfo fileInfo = imagesAndVideos[fileIndex];
                ImageRow file = filesByRelativePathAndName[TestConstant.File.HybridVideoDirectoryName][fileInfo.Name];
                if (file.IsVideo)
                {
                    Assert.IsTrue(file.Classification == FileClassification.Video);
                }
                else
                {
                    Assert.IsTrue(file.Classification == FileClassification.Color);
                }
            }

            // read metadata
            MetadataTag note0Tag;
            MetadataTag note3Tag;
            using (JpegImage jpeg = new(imagesAndVideos.First(file => JpegImage.IsJpeg(file.FullName)).FullName))
            {
                Assert.IsTrue(jpeg.TryGetMetadata());
                ExifSubIfdDirectory subIfd = jpeg.Metadata.OfType<ExifSubIfdDirectory>().Single();
                note0Tag = subIfd.Tags.Single(tag => tag.Type == ExifSubIfdDirectory.TagExifImageHeight);
                note3Tag = subIfd.Tags.Single(tag => tag.Type == ExifSubIfdDirectory.TagExifImageWidth);
            }
            ObservableArray<MetadataFieldResult> metadataResults = new(imagesAndVideos.Length, MetadataFieldResult.Default);
            using (MetadataIOComputeTransactionManager readMetadata = new(this.UpdateMetadataProgress, metadataResults, desiredStatusUpdateInterval))
            {
                await readMetadata.ReadFieldAsync(fileDatabase, TestConstant.DefaultDatabaseColumn.Note0, note0Tag, false).ConfigureAwait(false);
            }
            fileDatabase.SelectFiles(FileSelection.All);
            foreach (MetadataFieldResult result in metadataResults)
            {
                Assert.IsFalse(Object.ReferenceEquals(result, MetadataFieldResult.Default));
                Assert.IsFalse(String.IsNullOrWhiteSpace(result.FileName));
                Assert.IsFalse(String.IsNullOrWhiteSpace(result.Message));
            }

            metadataResults = new ObservableArray<MetadataFieldResult>(imagesAndVideos.Length, MetadataFieldResult.Default);
            using (MetadataIOComputeTransactionManager readMetadata = new(this.UpdateMetadataProgress, metadataResults, desiredStatusUpdateInterval))
            {
                await readMetadata.ReadFieldAsync(fileDatabase, TestConstant.DefaultDatabaseColumn.Note3, note3Tag, true).ConfigureAwait(false);
            }
            fileDatabase.SelectFiles(FileSelection.All);
            foreach (MetadataFieldResult result in metadataResults)
            {
                Assert.IsFalse(Object.ReferenceEquals(result, MetadataFieldResult.Default));
                Assert.IsFalse(String.IsNullOrWhiteSpace(result.FileName));
                Assert.IsFalse(String.IsNullOrWhiteSpace(result.Message));
            }
        }

        [TestMethod]
        public void ImportData()
        {
            using FileDatabase fileDatabase = this.CreateFileDatabase(TestConstant.File.DefaultTemplateDatabaseFileName, TestConstant.File.DefaultNewFileDatabaseFileName);
            List<FileExpectations> fileExpectations = this.PopulateDefaultDatabase(fileDatabase, true);

            // importing self should be a no op
            DataImportProgress importStatus = new((DataImportProgress status) => { }, Constant.ThrottleValues.DesiredIntervalBetweenStatusUpdates);
            FileImportResult result = fileDatabase.TryImportData(fileDatabase.FilePath, importStatus);
            Assert.IsTrue(result.Errors.Count == 0);
            Assert.IsTrue(result.FilesAdded == 0);
            Assert.IsTrue(result.FilesChanged == 0);
            Assert.IsTrue(result.FilesProcessed == fileExpectations.Count);
            Assert.IsTrue(result.FilesUpdated == 0);
            TimeZoneInfo imageSetTimeZone = fileDatabase.ImageSet.GetTimeZoneInfo();

            fileDatabase.SelectFiles(FileSelection.All);
            this.VerifyFiles(fileDatabase, fileExpectations, imageSetTimeZone);

            List<FileExpectations> fileExpectationsWithSubfolder;
            string subfolderDatabasePath;
            using (FileDatabase fileDatabaseWithSubfolderImages = this.CreateFileDatabase(TestConstant.File.DefaultTemplateDatabaseFileName, "DefaultUnitTestWithSubfulder.ddb"))
            {
                subfolderDatabasePath = fileDatabaseWithSubfolderImages.FilePath;
                fileExpectationsWithSubfolder = this.PopulateDefaultDatabase(fileDatabaseWithSubfolderImages);

                // add files from subfolder
                result = fileDatabase.TryImportData(subfolderDatabasePath, importStatus);
                Assert.IsTrue(result.Errors.Count == 0);
                Assert.IsTrue(result.FilesAdded == fileExpectationsWithSubfolder.Count - fileExpectations.Count);
                Assert.IsTrue(result.FilesChanged == result.FilesAdded);
                Assert.IsTrue(result.FilesProcessed == fileExpectationsWithSubfolder.Count);
                Assert.IsTrue(result.FilesUpdated == 0);

                fileDatabase.SelectFiles(FileSelection.All);
                this.VerifyFiles(fileDatabase, fileExpectationsWithSubfolder, imageSetTimeZone);

                // modify files in subfolder database
                FileExpectations martenPairExpectation = fileExpectationsWithSubfolder[fileExpectations.Count];
                martenPairExpectation.Classification = FileClassification.Greyscale;
                martenPairExpectation.DateTime += TimeSpan.FromHours(1);
                martenPairExpectation.DeleteFlag = true;
                martenPairExpectation.UserControlsByDataLabel[TestConstant.DefaultDatabaseColumn.Choice0] = "choice c";
                martenPairExpectation.UserControlsByDataLabel[TestConstant.DefaultDatabaseColumn.Counter0] = 2;
                martenPairExpectation.UserControlsByDataLabel[TestConstant.DefaultDatabaseColumn.Flag3] = true;
                martenPairExpectation.UserControlsByDataLabel[TestConstant.DefaultDatabaseColumn.Note3] = "American marten pair";

                ImageRow martenPair = fileDatabaseWithSubfolderImages.Files[fileExpectations.Count];
                martenPair.Classification = FileClassification.Greyscale;
                martenPair.DateTimeOffset += TimeSpan.FromHours(1);
                martenPair.DeleteFlag = true;
                martenPair[TestConstant.DefaultDatabaseColumn.Choice0] = "choice c";
                martenPair[TestConstant.DefaultDatabaseColumn.Counter0] = 2;
                martenPair[TestConstant.DefaultDatabaseColumn.Flag3] = true;
                martenPair[TestConstant.DefaultDatabaseColumn.Note3] = "American marten pair";
                Assert.IsTrue(fileDatabaseWithSubfolderImages.TrySyncFileToDatabase(martenPair));
                Assert.IsTrue(martenPair.HasChanges == false);

                FileExpectations coyoteExpectation = fileExpectationsWithSubfolder[fileExpectations.Count + 1];
                coyoteExpectation.UserControlsByDataLabel[TestConstant.DefaultDatabaseColumn.Note3] = "coyote modified";

                ImageRow coyote = fileDatabaseWithSubfolderImages.Files[fileExpectations.Count + 1];
                coyote[TestConstant.DefaultDatabaseColumn.Note3] = "coyote modified";
                Assert.IsTrue(fileDatabaseWithSubfolderImages.TrySyncFileToDatabase(coyote));
                Assert.IsTrue(coyote.HasChanges == false);
            }

            // re-import subfolder database to update with modifications
            result = fileDatabase.TryImportData(subfolderDatabasePath, importStatus);
            Assert.IsTrue(result.Errors.Count == 0);
            Assert.IsTrue(result.FilesAdded == 0);
            Assert.IsTrue(result.FilesChanged == 2);
            Assert.IsTrue(result.FilesProcessed == fileExpectationsWithSubfolder.Count);
            Assert.IsTrue(result.FilesUpdated == result.FilesChanged);

            fileDatabase.SelectFiles(FileSelection.All);
            this.VerifyFiles(fileDatabase, fileExpectationsWithSubfolder, imageSetTimeZone);
        }

        [TestMethod]
        public void MoveFile()
        {
            // remove any existing files from previous test executions
            string sourceFilePath = Path.Combine(Environment.CurrentDirectory, "DatabaseTests.MoveFile.jpg");
            if (File.Exists(sourceFilePath))
            {
                File.Delete(sourceFilePath);
            }
            string subfolderPath = Path.Combine(Environment.CurrentDirectory, "DatabaseTests.MoveFileFolder");
            if (Directory.Exists(subfolderPath) == false)
            {
                Directory.CreateDirectory(subfolderPath);
            }
            string destinationFilePath = Path.Combine(subfolderPath, Path.GetFileName(sourceFilePath));
            if (File.Exists(destinationFilePath))
            {
                File.Delete(destinationFilePath);
            }

            FileExpectations fileExpectation = new(TestConstant.FileExpectation.DaylightBobcat)
            {
                ID = Constant.Database.InvalidID,
                SkipUserControlVerification = true
            };

            // create ImageRow object for file
            File.Copy(fileExpectation.FileName, sourceFilePath);
            FileInfo fileInfo = new(sourceFilePath);

            using FileDatabase fileDatabase = this.CreateFileDatabase(TestConstant.File.DefaultTemplateDatabaseFileName, TestConstant.File.DefaultNewFileDatabaseFileName);
            ImageRow file = fileDatabase.Files.CreateAndAppendFile(fileInfo.Name, String.Empty);
            TimeZoneInfo imageSetTimeZone = fileDatabase.ImageSet.GetTimeZoneInfo();
            using (FileLoadAtom loadAtom = new(file.RelativePath, new FileLoad(file), new FileLoad((string)null), 0))
            {
                loadAtom.CreateJpegs(fileDatabase.FolderPath);
                loadAtom.ReadDateTimeOffsets(fileDatabase.FolderPath, imageSetTimeZone);
            }

            fileExpectation.FileName = Path.GetFileName(sourceFilePath);
            fileExpectation.Verify(file, imageSetTimeZone);

            // move file
            Assert.IsTrue(file.TryMoveFileToFolder(fileDatabase.FolderPath, subfolderPath));
            fileExpectation.RelativePath = Path.GetFileName(subfolderPath);
            fileExpectation.Verify(file, imageSetTimeZone);

            // move file back
            Assert.IsTrue(file.TryMoveFileToFolder(fileDatabase.FolderPath, fileDatabase.FolderPath));
            fileExpectation.RelativePath = null;
            fileExpectation.Verify(file, imageSetTimeZone);
        }

        [TestMethod]
        public void RoundtripSpreadsheets()
        {
            foreach (string spreadsheetFileExtension in new List<string>() { Constant.File.CsvFileExtension, Constant.File.ExcelFileExtension })
            {
                bool xlsx = spreadsheetFileExtension == Constant.File.ExcelFileExtension;

                // create database, push test images into the database, and load the image data table
                using (FileDatabase fileDatabase = this.CreateFileDatabase(TestConstant.File.DefaultTemplateDatabaseFileName, TestConstant.File.DefaultNewFileDatabaseFileName))
                {
                    List<FileExpectations> fileExpectations = this.PopulateDefaultDatabase(fileDatabase);

                    // roundtrip data
                    SpreadsheetReaderWriter readerWriter = new((SpreadsheetReadWriteStatus status) => { }, Constant.ThrottleValues.DesiredIntervalBetweenStatusUpdates);
                    string initialFilePath = this.GetUniqueFilePathForTest(Path.GetFileNameWithoutExtension(Constant.File.DefaultFileDatabaseFileName) + spreadsheetFileExtension);
                    if (xlsx)
                    {
                        readerWriter.ExportFileDataToXlsx(fileDatabase, initialFilePath);
                    }
                    else
                    {
                        readerWriter.ExportFileDataToCsv(fileDatabase, initialFilePath);
                    }

                    FileImportResult importResult = readerWriter.TryImportData(initialFilePath, fileDatabase);
                    Assert.IsTrue(importResult.Errors.Count == 0);

                    // verify file table content hasn't changed other than negligible truncation error in marker positions
                    TimeZoneInfo imageSetTimeZone = fileDatabase.ImageSet.GetTimeZoneInfo();
                    for (int fileIndex = 0; fileIndex < fileExpectations.Count; ++fileIndex)
                    {
                        ImageRow file = fileDatabase.Files[fileIndex];
                        FileExpectations fileExpectation = fileExpectations[fileIndex];
                        fileExpectation.SkipMarkerByteVerification = true;
                        fileExpectation.Verify(file, imageSetTimeZone);
                    }

                    // verify consistency of .csv export
                    string roundtripFilePath = Path.Combine(Path.GetDirectoryName(initialFilePath), Path.GetFileNameWithoutExtension(initialFilePath) + ".Roundtrip" + spreadsheetFileExtension);
                    if (xlsx)
                    {
                        readerWriter.ExportFileDataToXlsx(fileDatabase, roundtripFilePath);
                    }
                    else
                    {
                        readerWriter.ExportFileDataToCsv(fileDatabase, roundtripFilePath);
                    }

                    if (xlsx == false)
                    {
                        // check .csv content is identical
                        // For .xlsx this isn't meaningful as file internals can change.
                        string initialFileContent = File.ReadAllText(initialFilePath);
                        string roundtripFileContent = File.ReadAllText(roundtripFilePath);
                        Assert.IsTrue(initialFileContent == roundtripFileContent, "Initial and roundtrip {0} files don't match.", spreadsheetFileExtension);
                    }

                    // merge and refresh in memory table
                    int filesBeforeMerge = fileDatabase.CurrentlySelectedFileCount;
                    string mergeFilePath = Path.Combine(Path.GetDirectoryName(initialFilePath), Path.GetFileNameWithoutExtension(initialFilePath) + ".FilesToMerge" + spreadsheetFileExtension);
                    importResult = readerWriter.TryImportData(mergeFilePath, fileDatabase);
                    Assert.IsTrue(importResult.Errors.Count == 0);

                    fileDatabase.SelectFiles(FileSelection.All);
                    Assert.IsTrue(fileDatabase.CurrentlySelectedFileCount - filesBeforeMerge == 2);

                    // verify merge didn't affect existing file table content other than negligible truncation error in marker positions
                    for (int fileIndex = 0; fileIndex < fileExpectations.Count; ++fileIndex)
                    {
                        ImageRow file = fileDatabase.Files[fileIndex];
                        FileExpectations fileExpectation = fileExpectations[fileIndex];
                        fileExpectation.SkipMarkerByteVerification = true;
                        fileExpectation.Verify(file, imageSetTimeZone);
                    }
                }

                // force SQLite to release its handle on the database file
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }

        [TestMethod]
        public void TimeZones()
        {
            // no change - daylight savings -> UTC
            // change from Pacific time to mountain standard time as it has the same UTC offset as Pacific daylight savings time
            this.TimeZones(TestConstant.TimeZone.Pacific, TestConstant.TimeZone.Arizona);

            // daylight savings -> UTC
            this.TimeZones(TestConstant.TimeZone.Pacific, "UTC-08");
            this.TimeZones(TestConstant.TimeZone.Pacific, "UTC-09");

            // daylight savings -> daylight savings - hour earlier
            this.TimeZones(TestConstant.TimeZone.Pacific, TestConstant.TimeZone.Mountain);
            // daylight savings -> daylight savings - hour later
            this.TimeZones(TestConstant.TimeZone.Pacific, TestConstant.TimeZone.Alaska);

            // UTC -> UTC - hour earlier
            this.TimeZones(TestConstant.TimeZone.CapeVerde, TestConstant.TimeZone.Utc);
            // UTC -> UTC - hour later
            this.TimeZones(TestConstant.TimeZone.Utc, TestConstant.TimeZone.WestCentralAfrica);

            // UTC -> daylight savings
            this.TimeZones(TestConstant.TimeZone.Utc, TestConstant.TimeZone.Gmt);
        }

        private void TimeZones(string initialTimeZoneID, string secondTimeZoneID)
        {
            // create file database and populate images in initial time zone
            using (FileDatabase fileDatabase = this.CreateFileDatabase(TestConstant.File.DefaultTemplateDatabaseFileName, Constant.File.DefaultFileDatabaseFileName))
            {
                Assert.IsTrue(fileDatabase.ImageSet.TimeZone == TimeZoneInfo.Local.Id);
                // TimeZoneInfo doesn't implement operator == so Equals() must be called
                Assert.IsTrue(TimeZoneInfo.Local.Equals(fileDatabase.ImageSet.GetTimeZoneInfo()));

                fileDatabase.ImageSet.TimeZone = initialTimeZoneID;
                fileDatabase.TrySyncImageSetToDatabase();

                TimeZoneInfo initialImageSetTimeZone = TimeZoneInfo.FindSystemTimeZoneById(initialTimeZoneID);
                Assert.IsTrue(fileDatabase.ImageSet.TimeZone == initialTimeZoneID);
                Assert.IsTrue(initialImageSetTimeZone.Equals(fileDatabase.ImageSet.GetTimeZoneInfo()));

                List<FileExpectations> fileExpectations = this.PopulateDefaultDatabase(fileDatabase, true);

                // change to second time zone
                fileDatabase.ImageSet.TimeZone = secondTimeZoneID;
                fileDatabase.TrySyncImageSetToDatabase();

                TimeZoneInfo secondImageSetTimeZone = TimeZoneInfo.FindSystemTimeZoneById(secondTimeZoneID);
                Assert.IsTrue(fileDatabase.ImageSet.TimeZone == secondTimeZoneID);
                Assert.IsTrue(secondImageSetTimeZone.Equals(fileDatabase.ImageSet.GetTimeZoneInfo()));

                // verify date times of existing images haven't changed
                int initialFileCount = fileDatabase.Files.RowCount;
                this.VerifyFiles(fileDatabase, fileExpectations, initialImageSetTimeZone, initialFileCount, secondImageSetTimeZone);

                // add more images
                ImageRow martenPairImage = this.CreateFile(fileDatabase, secondImageSetTimeZone, TestConstant.FileExpectation.DaylightMartenPair, out MetadataReadResults metadataReadResult);
                Assert.IsTrue(metadataReadResult == MetadataReadResults.DateTime);
                ImageRow coyoteImage = this.CreateFile(fileDatabase, secondImageSetTimeZone, TestConstant.FileExpectation.DaylightCoyote, out metadataReadResult);
                Assert.IsTrue(metadataReadResult == MetadataReadResults.DateTime);

                using (AddFilesTransactionSequence addFiles = fileDatabase.CreateAddFilesTransaction())
                {
                    addFiles.AddToSequence(new List<FileLoad>() { new FileLoad(martenPairImage), new FileLoad(coyoteImage) }, 0, 2);
                    addFiles.Commit();
                }
                fileDatabase.SelectFiles(FileSelection.All);

                // generate expectations for new images
                FileExpectations martenPairExpectation = new(TestConstant.FileExpectation.DaylightMartenPair)
                {
                    ID = fileExpectations.Count + 1,
                    SkipUserControlVerification = true
                };
                fileExpectations.Add(martenPairExpectation);

                FileExpectations daylightCoyoteExpectation = new(TestConstant.FileExpectation.DaylightCoyote)
                {
                    ID = fileExpectations.Count + 1,
                    SkipUserControlVerification = true
                };
                fileExpectations.Add(daylightCoyoteExpectation);

                // verify new images pick up the current timezone
                this.VerifyFiles(fileDatabase, fileExpectations, initialImageSetTimeZone, initialFileCount, secondImageSetTimeZone);
            }

            // force SQLite to release its handle on the database file
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        private bool DateTimesWithinOneMillisecond(DateTimeOffset actual, DateTimeOffset expected)
        {
            TimeSpan difference = actual - expected;
            if (difference < TimeSpan.Zero)
            {
                difference = difference.Negate();
            }
            return difference <= TimeSpan.FromMilliseconds(1.0);
        }

        private void UpdateDateTimeRereadProgress(ObservableStatus<DateTimeRereadResult> status)
        {
            // for now, do nothing
        }

        private void UpdateFolderLoadProgress(FileLoadStatus status)
        {
            // for now, do nothing
        }

        private void UpdateMetadataProgress(ObservableStatus<MetadataFieldResult> status)
        {
            // for now, do nothing
        }

        private void UpdateReclassifyProgress(ReclassifyStatus status)
        {
            // for now, do nothing
        }

        private void VerifyControl(ControlRow control)
        {
            Assert.IsTrue(control.ControlOrder > 0);
            Assert.IsTrue((control.Copyable == true) || (control.Copyable == false));
            Assert.IsFalse(String.IsNullOrWhiteSpace(control.DataLabel));
            // nothing to sanity check for control.DefaultValue
            Assert.IsTrue(control.ID >= 0);
            Assert.IsFalse(String.IsNullOrWhiteSpace(control.Label));
            // nothing to sanity check for control.List
            Assert.IsTrue(control.SpreadsheetOrder > 0);
            Assert.IsTrue(control.MaxWidth > 0);
            Assert.IsFalse(String.IsNullOrWhiteSpace(control.Tooltip));
            Assert.IsTrue(Enum.IsDefined(typeof(ControlType), control.ControlType));
            Assert.IsTrue((control.Visible == true) || (control.Visible == false));
        }

        private void VerifyControls(TemplateDatabase database)
        {
            for (int row = 0; row < database.Controls.RowCount; ++row)
            {
                // sanity check control
                ControlRow control = database.Controls[row];
                this.VerifyControl(control);

                // verify controls are sorted in control order and that control order is ones based
                Assert.IsTrue(control.ControlOrder == row + 1);
            }
        }

        private void VerifyFiles(FileDatabase fileDatabase, List<FileExpectations> fileExpectations, TimeZoneInfo imageSetTimeZone)
        {
            for (int file = 0; file < fileExpectations.Count; ++file)
            {
                FileExpectations fileExpectation = fileExpectations[file];
                fileExpectation.Verify(fileDatabase.Files[file], imageSetTimeZone);
            }
        }

        private void VerifyFiles(FileDatabase fileDatabase, List<FileExpectations> fileExpectations, TimeZoneInfo initialImageSetTimeZone, int initialImageCount, TimeZoneInfo secondImageSetTimeZone)
        {
            for (int file = 0; file < fileExpectations.Count; ++file)
            {
                TimeZoneInfo expectedTimeZone = file >= initialImageCount ? secondImageSetTimeZone : initialImageSetTimeZone;
                FileExpectations fileExpectation = fileExpectations[file];
                fileExpectation.Verify(fileDatabase.Files[file], expectedTimeZone);
            }
        }

        private void VerifyFileTimeAdjustment(List<DateTimeOffset> fileTimesBeforeAdjustment, List<DateTimeOffset> fileTimesAfterAdjustment, int startRow, int endRow, TimeSpan expectedAdjustment)
        {
            for (int row = 0; row < startRow; ++row)
            {
                TimeSpan actualAdjustment = fileTimesAfterAdjustment[row] - fileTimesBeforeAdjustment[row];
                Assert.IsTrue(actualAdjustment == TimeSpan.Zero, "Expected file time not to change but it shifted by {0}.", actualAdjustment);
            }
            for (int row = startRow; row <= endRow; ++row)
            {
                TimeSpan actualAdjustment = fileTimesAfterAdjustment[row] - fileTimesBeforeAdjustment[row];
                Assert.IsTrue(actualAdjustment == expectedAdjustment, "Expected file time to change by {0} but it shifted by {1}.", expectedAdjustment, actualAdjustment);
            }
            for (int row = endRow + 1; row < fileTimesBeforeAdjustment.Count; ++row)
            {
                TimeSpan actualAdjustment = fileTimesAfterAdjustment[row] - fileTimesBeforeAdjustment[row];
                Assert.IsTrue(actualAdjustment == TimeSpan.Zero, "Expected file time not to change but it shifted by {0}.", actualAdjustment);
            }
        }

        private void VerifyTemplateDatabase(TemplateDatabase templateDatabase, string templateDatabaseBaseFileName)
        {
            // sanity checks
            Assert.IsNotNull(templateDatabase);
            Assert.IsNotNull(templateDatabase.FilePath);
            Assert.IsNotNull(templateDatabase.Controls);

            // FilePath checks
            string templateDatabaseFileName = Path.GetFileName(templateDatabase.FilePath);
            Assert.IsTrue(templateDatabaseFileName.StartsWith(Path.GetFileNameWithoutExtension(templateDatabaseBaseFileName), StringComparison.Ordinal));
            Assert.IsTrue(templateDatabaseFileName.EndsWith(Path.GetExtension(templateDatabaseBaseFileName), StringComparison.Ordinal));
            Assert.IsTrue(File.Exists(templateDatabase.FilePath));

            // TemplateTable checks
            List<long> ids = new();
            List<long> controlOrders = new();
            List<long> spreadsheetOrders = new();
            foreach (ControlRow control in templateDatabase.Controls)
            {
                ids.Add(control.ID);
                controlOrders.Add(control.ControlOrder);
                spreadsheetOrders.Add(control.SpreadsheetOrder);
            }
            List<long> uniqueIDs = ids.Distinct().ToList();
            Assert.IsTrue(ids.Count == uniqueIDs.Count, "Expected {0} unique control IDs but found {1}.  {2} id(s) are duplicated.", ids.Count, uniqueIDs.Count, ids.Count - uniqueIDs.Count);
            List<long> uniqueControlOrders = controlOrders.Distinct().ToList();
            Assert.IsTrue(controlOrders.Count == uniqueControlOrders.Count, "Expected {0} unique control orders but found {1}.  {2} order(s) are duplicated.", controlOrders.Count, uniqueControlOrders.Count, controlOrders.Count - uniqueControlOrders.Count);
            List<long> uniqueSpreadsheetOrders = spreadsheetOrders.Distinct().ToList();
            Assert.IsTrue(spreadsheetOrders.Count == uniqueSpreadsheetOrders.Count, "Expected {0} unique spreadsheet orders but found {1}.  {2} order(s) are duplicated.", spreadsheetOrders.Count, uniqueSpreadsheetOrders.Count, spreadsheetOrders.Count - uniqueSpreadsheetOrders.Count);
        }

        private void VerifyDefaultImageSet(FileDatabase fileDatabase)
        {
            // InitialFolderName - current directory case occurs when the test creates a new file database, UnitTests case when the default .ddb is cloned
            Assert.IsTrue(fileDatabase.ImageSet.FileSelection == FileSelection.All);
            Assert.IsTrue((fileDatabase.ImageSet.InitialFolderName == Path.GetFileName(Environment.CurrentDirectory)) ||
                          (fileDatabase.ImageSet.InitialFolderName == "UnitTests"));
            Assert.IsTrue(fileDatabase.ImageSet.MostRecentFileID == Constant.Database.DefaultFileID);
            Assert.IsTrue(fileDatabase.ImageSet.Log == Constant.Database.ImageSetDefaultLog);
            Assert.IsFalse(fileDatabase.ImageSet.Options.HasFlag(ImageSetOptions.Magnifier));
            Assert.IsTrue(fileDatabase.ImageSet.TimeZone == TimeZoneInfo.Local.Id);
        }
    }
}
