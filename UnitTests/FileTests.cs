﻿using Carnassial.Data;
using Carnassial.Database;
using Carnassial.Images;
using Carnassial.Interop;
using Carnassial.Native;
using MetadataExtractor.Formats.Exif;
using MetadataExtractor.Formats.Exif.Makernotes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MetadataDirectory = MetadataExtractor.Directory;

namespace Carnassial.UnitTests
{
    [TestClass]
    public class FileTests : CarnassialTest
    {
        [TestMethod]
        public void Backup()
        {
            DateTime utcStart = DateTime.UtcNow;
            string fileNameToBackup = TestConstant.File.DefaultFileDatabaseFileName;
            Assert.IsTrue(FileBackup.TryCreateBackup(fileNameToBackup));
            Assert.IsTrue(FileBackup.TryCreateBackup(fileNameToBackup));
            Assert.IsTrue(FileBackup.TryCreateBackup(fileNameToBackup));

            Assert.IsTrue(Directory.Exists(Constant.File.BackupFolder));
            List<string> filesInBackupFolder = Directory.EnumerateFiles(Constant.File.BackupFolder).ToList();
            Assert.IsTrue(filesInBackupFolder.Count >= 3);
            DateTime mostRecentBackupUtc = FileBackup.GetMostRecentBackup(fileNameToBackup);
            Assert.IsTrue(mostRecentBackupUtc > utcStart);
            // nontrivial to check file with most recent time is in the backup folder since the file's timestamps are likely
            // a few milliseconds later than the time used to make the file name unique; this coverage can be added if needed

            // move the three backup files created above to the Recycle Bin, along with any other files accumulated from other
            // tests
            List<FileInfo> filesToAgeOut = new List<FileInfo>(filesInBackupFolder.Select(filePath => new FileInfo(filePath)));
            using (Recycler recycler = new Recycler())
            {
                recycler.MoveToRecycleBin(filesToAgeOut);
            }

            filesInBackupFolder = Directory.EnumerateFiles(Constant.File.BackupFolder).ToList();
            Assert.IsTrue(filesInBackupFolder.Count == 0);
        }

        [TestMethod]
        public async Task Cache()
        {
            FileDatabase fileDatabase = this.CreateFileDatabase(TestConstant.File.DefaultTemplateDatabaseFileName, TestConstant.File.DefaultNewFileDatabaseFileName);
            List<FileExpectations> fileExpectations = this.PopulateDefaultDatabase(fileDatabase);

            using (ImageCache cache = new ImageCache(fileDatabase))
            {
                Assert.IsNull(cache.Current);
                Assert.IsTrue(cache.CurrentDifferenceState == ImageDifference.Unaltered);
                Assert.IsTrue(cache.CurrentRow == -1);

                Assert.IsTrue(cache.MoveNext());
                Assert.IsTrue(cache.MoveNext());
                Assert.IsTrue(cache.MovePrevious());
                Assert.IsTrue(cache.CurrentDifferenceState == ImageDifference.Unaltered);
                Assert.IsTrue(cache.CurrentRow == 0);
                this.VerifyCurrentImage(cache);

                MoveToFileResult moveToFile = await cache.TryMoveToFileAsync(0, 0);
                Assert.IsTrue(moveToFile.Succeeded);
                Assert.IsFalse(moveToFile.NewFileToDisplay);
                moveToFile = await cache.TryMoveToFileAsync(0, 1);
                Assert.IsTrue(moveToFile.Succeeded);
                Assert.IsFalse(moveToFile.NewFileToDisplay);
                moveToFile = await cache.TryMoveToFileAsync(1, 0);
                Assert.IsTrue(moveToFile.Succeeded);
                Assert.IsTrue(moveToFile.NewFileToDisplay);
                moveToFile = await cache.TryMoveToFileAsync(1, -1);
                Assert.IsTrue(moveToFile.Succeeded);
                Assert.IsFalse(moveToFile.NewFileToDisplay);
                this.VerifyCurrentImage(cache);

                Assert.IsTrue(cache.TryInvalidate(1));
                moveToFile = await cache.TryMoveToFileAsync(0, 0);
                Assert.IsTrue(moveToFile.Succeeded);
                Assert.IsTrue(moveToFile.NewFileToDisplay);
                moveToFile = await cache.TryMoveToFileAsync(1, 0);
                Assert.IsTrue(moveToFile.Succeeded);
                Assert.IsTrue(moveToFile.NewFileToDisplay);
                this.VerifyCurrentImage(cache);

                Assert.IsTrue(cache.TryInvalidate(2));
                moveToFile = await cache.TryMoveToFileAsync(1, 1);
                Assert.IsTrue(moveToFile.Succeeded);
                Assert.IsTrue(moveToFile.NewFileToDisplay);
                moveToFile = await cache.TryMoveToFileAsync(1, 0);
                Assert.IsTrue(moveToFile.Succeeded);
                Assert.IsFalse(moveToFile.NewFileToDisplay);
                moveToFile = await cache.TryMoveToFileAsync(0, -1);
                Assert.IsTrue(moveToFile.Succeeded);
                Assert.IsTrue(moveToFile.NewFileToDisplay);
                this.VerifyCurrentImage(cache);

                moveToFile = await cache.TryMoveToFileAsync(fileExpectations.Count, 0);
                Assert.IsFalse(moveToFile.Succeeded);
                moveToFile = await cache.TryMoveToFileAsync(fileExpectations.Count, 0);
                Assert.IsFalse(moveToFile.Succeeded);

                moveToFile = await cache.TryMoveToFileAsync(0, 0);
                Assert.IsTrue(moveToFile.Succeeded);
                moveToFile = await cache.TryMoveToFileAsync(1, 0);
                Assert.IsTrue(moveToFile.Succeeded);
                moveToFile = await cache.TryMoveToFileAsync(fileExpectations.Count, 0);
                Assert.IsFalse(moveToFile.Succeeded);

                for (int step = 0; step < 4; ++step)
                {
                    cache.MoveToNextStateInCombinedDifferenceCycle();
                    Assert.IsTrue((cache.CurrentDifferenceState == ImageDifference.Combined) ||
                                  (cache.CurrentDifferenceState == ImageDifference.Unaltered));

                    ImageDifferenceResult combinedDifferenceResult = await cache.TryCalculateCombinedDifferenceAsync(Constant.Images.DifferenceThresholdDefault - 2);
                    await this.CheckDifferenceResult(combinedDifferenceResult, cache, fileDatabase);

                    MemoryImage differenceImage = cache.GetCurrentImage();
                    if (combinedDifferenceResult == ImageDifferenceResult.Success)
                    {
                        Assert.IsNotNull(differenceImage);
                    }
                }

                Assert.IsTrue(cache.TryMoveToFile(0));
                for (int step = 0; step < 7; ++step)
                {
                    cache.MoveToNextStateInPreviousNextDifferenceCycle();
                    Assert.IsTrue((cache.CurrentDifferenceState == ImageDifference.Next) ||
                                  (cache.CurrentDifferenceState == ImageDifference.Previous) ||
                                  (cache.CurrentDifferenceState == ImageDifference.Unaltered));

                    ImageDifferenceResult differenceResult = await cache.TryCalculateDifferenceAsync(Constant.Images.DifferenceThresholdDefault + 2);
                    await this.CheckDifferenceResult(differenceResult, cache, fileDatabase);

                    MemoryImage differenceImage = cache.GetCurrentImage();
                    if (differenceResult == ImageDifferenceResult.Success)
                    {
                        Assert.IsNotNull(differenceImage);
                    }
                }

                cache.Reset();
                Assert.IsNull(cache.Current);
                Assert.IsTrue(cache.CurrentDifferenceState == ImageDifference.Unaltered);
                Assert.IsTrue(cache.CurrentRow == Constant.Database.InvalidRow);
            }
        }

        [TestMethod]
        public async Task CorruptFileAsync()
        {
            FileDatabase fileDatabase = this.CreateFileDatabase(TestConstant.File.DefaultTemplateDatabaseFileName, TestConstant.File.DefaultFileDatabaseFileName);
            TimeZoneInfo imageSetTimeZone = fileDatabase.ImageSet.GetTimeZoneInfo();
            ImageRow corruptFile = this.CreateFile(fileDatabase, imageSetTimeZone, TestConstant.FileExpectation.CorruptFieldScan, out MetadataReadResult corruptMetadataRead);
            using (MemoryImage corruptImage = await corruptFile.TryLoadAsync(fileDatabase.FolderPath))
            {
                Assert.IsTrue(corruptImage.DecodeError);
            }
        }

        [TestMethod]
        public void ExifBushnell()
        {
            FileDatabase fileDatabase = this.CreateFileDatabase(TestConstant.File.DefaultTemplateDatabaseFileName, Constant.File.DefaultFileDatabaseFileName);
            IReadOnlyCollection<MetadataDirectory> metadata = this.LoadMetadata(fileDatabase, TestConstant.FileExpectation.InfraredMarten);
            ExifIfd0Directory ifd0 = metadata.OfType<ExifIfd0Directory>().Single();
            ExifSubIfdDirectory subIfd = metadata.OfType<ExifSubIfdDirectory>().Single();

            Assert.IsTrue(DateTime.TryParseExact(ifd0.GetDescription(ExifIfd0Directory.TagDateTime), TestConstant.Exif.DateTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dateTime));
            Assert.IsTrue(DateTime.TryParseExact(subIfd.GetDescription(ExifSubIfdDirectory.TagDateTimeDigitized), TestConstant.Exif.DateTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dateTimeDigitized));
            Assert.IsTrue(DateTime.TryParseExact(subIfd.GetDescription(ExifSubIfdDirectory.TagDateTimeOriginal), TestConstant.Exif.DateTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dateTimeOriginal));
            Assert.IsFalse(String.IsNullOrWhiteSpace(ifd0.GetDescription(ExifSubIfdDirectory.TagSoftware)));
        }

        [TestMethod]
        public void ExifReconyxHyperfire()
        {
            FileDatabase fileDatabase = this.CreateFileDatabase(TestConstant.File.DefaultTemplateDatabaseFileName, Constant.File.DefaultFileDatabaseFileName);
            IReadOnlyCollection<MetadataDirectory> metadata = this.LoadMetadata(fileDatabase, TestConstant.FileExpectation.DaylightMartenPair);
            ExifIfd0Directory ifd0 = metadata.OfType<ExifIfd0Directory>().Single();
            ExifSubIfdDirectory subIfd = metadata.OfType<ExifSubIfdDirectory>().Single();
            ReconyxHyperFireMakernoteDirectory hyperfire = metadata.OfType<ReconyxHyperFireMakernoteDirectory>().Single();

            Assert.IsFalse(String.IsNullOrWhiteSpace(subIfd.GetDescription(ExifSubIfdDirectory.TagExposureTime)));

            Assert.IsFalse(String.IsNullOrWhiteSpace(hyperfire.GetDescription(ReconyxHyperFireMakernoteDirectory.TagAmbientTemperature)));
            Assert.IsFalse(String.IsNullOrWhiteSpace(hyperfire.GetDescription(ReconyxHyperFireMakernoteDirectory.TagAmbientTemperatureFahrenheit)));
            Assert.IsFalse(String.IsNullOrWhiteSpace(hyperfire.GetDescription(ReconyxHyperFireMakernoteDirectory.TagBatteryVoltage)));
            Assert.IsFalse(String.IsNullOrWhiteSpace(hyperfire.GetDescription(ReconyxHyperFireMakernoteDirectory.TagBrightness)));
            Assert.IsFalse(String.IsNullOrWhiteSpace(hyperfire.GetDescription(ReconyxHyperFireMakernoteDirectory.TagContrast)));
            Assert.IsTrue(DateTime.TryParseExact(hyperfire.GetDescription(ReconyxHyperFireMakernoteDirectory.TagDateTimeOriginal), TestConstant.Exif.DateTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dateTimeOriginal));
            Assert.IsFalse(String.IsNullOrWhiteSpace(hyperfire.GetDescription(ReconyxHyperFireMakernoteDirectory.TagEventNumber)));
            Assert.IsFalse(String.IsNullOrWhiteSpace(hyperfire.GetDescription(ReconyxHyperFireMakernoteDirectory.TagFirmwareVersion)));
            Assert.IsFalse(String.IsNullOrWhiteSpace(hyperfire.GetDescription(ReconyxHyperFireMakernoteDirectory.TagInfraredIlluminator)));
            Assert.IsFalse(String.IsNullOrWhiteSpace(hyperfire.GetDescription(ReconyxHyperFireMakernoteDirectory.TagMakernoteVersion)));
            Assert.IsFalse(String.IsNullOrWhiteSpace(hyperfire.GetDescription(ReconyxHyperFireMakernoteDirectory.TagMoonPhase)));
            Assert.IsFalse(String.IsNullOrWhiteSpace(hyperfire.GetDescription(ReconyxHyperFireMakernoteDirectory.TagSaturation)));
            Assert.IsFalse(String.IsNullOrWhiteSpace(hyperfire.GetDescription(ReconyxHyperFireMakernoteDirectory.TagSequence)));
            Assert.IsFalse(String.IsNullOrWhiteSpace(hyperfire.GetDescription(ReconyxHyperFireMakernoteDirectory.TagSerialNumber)));
            Assert.IsFalse(String.IsNullOrWhiteSpace(hyperfire.GetDescription(ReconyxHyperFireMakernoteDirectory.TagSharpness)));
            Assert.IsFalse(String.IsNullOrWhiteSpace(hyperfire.GetDescription(ReconyxHyperFireMakernoteDirectory.TagTriggerMode)));
            Assert.IsFalse(String.IsNullOrWhiteSpace(hyperfire.GetDescription(ReconyxHyperFireMakernoteDirectory.TagUserLabel)));
        }

        [TestMethod]
        public async Task ImageQuality()
        {
            List<FileExpectations> fileExpectations = new List<FileExpectations>()
            {
                new FileExpectations(TestConstant.FileExpectation.DaylightBobcat),
                new FileExpectations(TestConstant.FileExpectation.DaylightCoyote),
                new FileExpectations(TestConstant.FileExpectation.DaylightMartenPair),
                new FileExpectations(TestConstant.FileExpectation.InfraredMarten)
            };

            TemplateDatabase templateDatabase = this.CreateTemplateDatabase(TestConstant.File.DefaultNewTemplateDatabaseFileName);
            FileDatabase fileDatabase = this.CreateFileDatabase(templateDatabase, TestConstant.File.DefaultNewFileDatabaseFileName);
            {
                foreach (FileExpectations fileExpectation in fileExpectations)
                {
                    // load the image
                    FileInfo fileInfo = new FileInfo(Path.Combine(fileExpectation.RelativePath, fileExpectation.FileName));
                    ImageRow file = fileDatabase.Files.CreateAndAppendFile(fileInfo.Name, fileExpectation.RelativePath);
                    using (MemoryImage image = await file.TryLoadAsync(this.WorkingDirectory))
                    {
                        double luminosity = image.GetLuminosityAndColoration(0, out double coloration);
                        FileClassification classification = new ImageProperties(luminosity, coloration).EvaluateNewClassification(Constant.Images.DarkLuminosityThresholdDefault);
                        if (Math.Abs(luminosity - fileExpectation.Luminosity) > TestConstant.LuminosityAndColorationTolerance)
                        {
                            Assert.Fail("{0}: Expected luminosity to be {1}, but it was {2}.", fileExpectation.FileName, fileExpectation.Luminosity, luminosity);
                        }
                        if (Math.Abs(luminosity - fileExpectation.Luminosity) > TestConstant.LuminosityAndColorationTolerance)
                        {
                            Assert.Fail("{0}: Expected coloration to be {1}, but it was {2}.", fileExpectation.FileName, fileExpectation.Coloration, coloration);
                        }
                        Assert.IsTrue(classification == fileExpectation.Classification, "{0}: Expected classification {1}, but it was {2}.", fileExpectation.FileName, fileExpectation.Classification, classification);
                    }
                }
            }
        }

        private async Task CheckDifferenceResult(ImageDifferenceResult result, ImageCache cache, FileDatabase fileDatabase)
        {
            MemoryImage currentImage = cache.GetCurrentImage();
            switch (result)
            {
                case ImageDifferenceResult.CurrentImageNotAvailable:
                case ImageDifferenceResult.NextImageNotAvailable:
                case ImageDifferenceResult.PreviousImageNotAvailable:
                    if (cache.CurrentDifferenceState == ImageDifference.Unaltered)
                    {
                        Assert.IsNotNull(currentImage);
                    }
                    else
                    {
                        Assert.IsNull(currentImage);
                    }
                    break;
                case ImageDifferenceResult.NotCalculable:
                    bool expectNullImage = false;
                    int previousNextImageRow = -1;
                    int otherImageRowForCombined = -1;
                    switch (cache.CurrentDifferenceState)
                    {
                        // as a default assume images are matched and expect differences to be calculable if the necessary images are available
                        case ImageDifference.Combined:
                            expectNullImage = (cache.CurrentRow == 0) || (cache.CurrentRow == fileDatabase.CurrentlySelectedFileCount - 1);
                            previousNextImageRow = cache.CurrentRow - 1;
                            otherImageRowForCombined = cache.CurrentRow + 1;
                            break;
                        case ImageDifference.Next:
                            expectNullImage = cache.CurrentRow == fileDatabase.CurrentlySelectedFileCount - 1;
                            previousNextImageRow = cache.CurrentRow + 1;
                            break;
                        case ImageDifference.Previous:
                            expectNullImage = cache.CurrentRow == 0;
                            previousNextImageRow = cache.CurrentRow - 1;
                            break;
                        case ImageDifference.Unaltered:
                            // result should be NotCalculable on Unaltered
                            expectNullImage = true;
                            return;
                    }

                    // check if the image to diff against is matched
                    if (fileDatabase.IsFileRowInRange(previousNextImageRow))
                    {
                        MemoryImage unalteredImage = await cache.Current.TryLoadAsync(fileDatabase.FolderPath);
                        ImageRow previousNextFile = fileDatabase.Files[previousNextImageRow];
                        MemoryImage previousNextImage = await previousNextFile.TryLoadAsync(fileDatabase.FolderPath);
                        bool mismatched = unalteredImage.MismatchedOrNot32BitBgra(previousNextImage);

                        if (fileDatabase.IsFileRowInRange(otherImageRowForCombined))
                        {
                            ImageRow otherFileForCombined = fileDatabase.Files[otherImageRowForCombined];
                            MemoryImage otherImageForCombined = await otherFileForCombined.TryLoadAsync(fileDatabase.FolderPath);
                            mismatched |= unalteredImage.MismatchedOrNot32BitBgra(otherImageForCombined);
                        }

                        expectNullImage |= mismatched;
                    }

                    if (expectNullImage)
                    {
                        Assert.IsNull(currentImage, "Expected a null image for difference result {0} and state {1}.", result, cache.CurrentDifferenceState);
                    }
                    else
                    {
                        Assert.IsNotNull(currentImage, "Expected an image for difference result {0} and state {1}.", result, cache.CurrentDifferenceState);
                    }
                    break;
                case ImageDifferenceResult.Success:
                    Assert.IsNotNull(currentImage);
                    break;
                default:
                    throw new NotSupportedException(String.Format("Unhandled result {0}.", result));
            }
        }

        private IReadOnlyCollection<MetadataDirectory> LoadMetadata(FileDatabase fileDatabase, FileExpectations fileExpectation)
        {
            string filePath = Path.Combine(this.WorkingDirectory, fileExpectation.RelativePath, fileExpectation.FileName);
            Assert.IsTrue(JpegImage.IsJpeg(filePath));
            IReadOnlyCollection<MetadataDirectory> metadata = JpegImage.LoadMetadata(filePath);
            Assert.IsTrue(metadata.Count >= 5, "Expected at least 5 metadata directories to be retrieved from {0}", filePath);
            // example information returned from ExifTool
            // field name                   Bushnell                                    Reconyx
            // --- fields of likely interest for image analysis ---
            // Create Date                  2016.02.24 04:59:46                         [Bushnell only]
            // Date/Time Original           2016.02.24 04:59:46                         2015.01.28 11:17:34 [but located in Makernote rather than the standard EXIF field!]
            // Modify Date                  2016.02.24 04:59:46                         [Bushnell only]
            // --- fields possibly of interest interest for image analysis ---
            // Exposure Time                0                                           1/84
            // Shutter Speed                0                                           1/84
            // Software                     BS683BWYx05209                              [Bushnell only]
            // Firmware Version             [Reconyx only]                              3.3.0
            // Trigger Mode                 [Reconyx only]                              Motion Detection
            // Sequence                     [Reconyx only]                              1 of 5
            // Ambient Temperature          [Reconyx only]                              0 C
            // Serial Number                [Reconyx only]                              H500DE01120343
            // Infrared Illuminator         [Reconyx only]                              Off
            // User Label                   [Reconyx only]                              CCPF DS02
            // --- fields of little interest (Bushnell often uses placeholder values which don't change) ---
            // ExifTool Version Number      10.14                                       10.14
            // File Name                    BushnellTrophyHD-119677C-20160224-056.JPG   Reconyx-HC500-20150128-201.JPG
            // Directory                    Carnassial/UnitTests/bin/Debug               Carnassial/UnitTests/bin/Debug/CarnivoreTestImages
            // File Size                    731 kB                                      336 kB
            // File Modification Date/Time  <file time from last build>                 <file time from last build>
            // File Access Date/Time        <file time from last build>                 <file time from last build>
            // File Creation Date/Time      <file time from last build>                 <file time from last build>
            // File Permissions             rw-rw-rw-                                   rw-rw-rw-
            // File Type                    JPEG                                        JPEG
            // File Type Extension          jpg                                         jpg
            // MIME Type                    image/jpeg                                  image/jpeg
            // Exif Byte Order              Little-endian(Intel, II)                    Little-endian(Intel, II)
            // Image Description            M2E6L0-0R350B362                            [Bushnell only]
            // Make                         [blank]                                     [Bushnell only]
            // Camera Model Name            [blank]                                     [Bushnell only]
            // Orientation                  Horizontal(normal)                          [Bushnell only]
            // X Resolution                 96                                          72
            // Y Resolution                 96                                          72
            // Resolution Unit              inches                                      inches
            // Y Cb Cr Positioning          Co-sited                                    Co-sited
            // Copyright                    Copyright 2002                              [Bushnell only]
            // F Number                     2.8                                         [Bushnell only]
            // Exposure Program             Aperture-priority AE                        [Bushnell only]
            // ISO                          100                                         50
            // Exif Version                 0210                                        0220
            // Components Configuration     Y, Cb, Cr, -                                Y, Cb, Cr, -
            // Compressed Bits Per Pixel    0.7419992711                                [Bushnell only]
            // Shutter Speed Value          1                                           [Bushnell only]
            // Aperture Value               2.6                                         [Bushnell only]
            // Exposure Compensation        +2                                          [Bushnell only]
            // Max Aperture Value           2.6                                         [Bushnell only]
            // Metering Mode                Average                                     [Bushnell only]
            // Light Source                 Daylight                                    [Bushnell only]
            // Flash                        No Flash                                    [Bushnell only]
            // Warning                      [minor]Unrecognized MakerNotes              [Bushnell only]
            // Flashpix Version             0100                                        0100
            // Color Space                  sRGB                                        sRGB
            // Exif Image Width             3264                                        2048
            // Exif Image Height            2448                                        1536
            // Related Sound File           RelatedSound                                [Bushnell only]
            // Interoperability Index       R98 - DCF basic file(sRGB)                  [Bushnell only]
            // Interoperability Version     0100                                        [Bushnell only]
            // Exposure Index               1                                           [Bushnell only]
            // Sensing Method               One-chip color area                         [Bushnell only]
            // File Source                  Digital Camera                              [Bushnell only]
            // Scene Type                   Directly photographed                       [Bushnell only]
            // Compression                  JPEG(old - style)                           [Bushnell only]
            // Thumbnail Offset             1312                                        [Bushnell only]
            // Thumbnail Length             5768                                        [Bushnell only]
            // Image Width                  3264                                        2048
            // Image Height                 2448                                        1536
            // Encoding Process             Baseline DCT, Huffman coding                Baseline DCT, Huffman coding
            // Bits Per Sample              8                                           8
            // Color Components             3                                           3
            // Y Cb Cr Sub Sampling         YCbCr4:2:2 (2 1)                            YCbCr4:2:2 (2 1)
            // Aperture                     2.8                                         [Bushnell only]
            // Image Size                   3264x2448                                   2048x1536
            // Megapixels                   8.0                                         3.1
            // Thumbnail Image              <binary data>                               [Bushnell only]
            // Ambient Temperature Fahrenheit [Reconyx only]                            31 F
            // Battery Voltage              [Reconyx only]                              8.65 V
            // Contrast                     [Reconyx only]                              142
            // Brightness                   [Reconyx only]                              238
            // Event Number                 [Reconyx only]                              39
            // Firmware Date                [Reconyx only]                              2011:01:10
            // Maker Note Version           [Reconyx only]                              0xf101
            // Moon Phase                   [Reconyx only]                              First Quarter
            // Motion Sensitivity           [Reconyx only]                              100
            // Sharpness                    [Reconyx only]                              64
            // Saturation                   [Reconyx only]                              144
            return metadata;
        }

        private void VerifyCurrentImage(ImageCache cache)
        {
            Assert.IsNotNull(cache.Current);

            // don't dispose current image as it's owned by the cache
            MemoryImage currentImage = cache.GetCurrentImage();
            Assert.IsNotNull(currentImage);
            Assert.IsTrue(currentImage.DecodeError == false);
            Assert.IsTrue(currentImage.Format == MemoryImage.PreferredPixelFormat);
            Assert.IsTrue((1000 < currentImage.PixelHeight) && (currentImage.PixelHeight < 10000));
            Assert.IsTrue((1000 < currentImage.PixelWidth) && (currentImage.PixelWidth < 10000));
            Assert.IsTrue(currentImage.TotalPixels > 1000 * 1000);
        }
    }
}
