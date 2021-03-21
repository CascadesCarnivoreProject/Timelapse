using Carnassial.Data;
using Carnassial.Database;
using Carnassial.Images;
using Carnassial.Interop;
using Carnassial.Native;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows;

namespace Carnassial.UnitTests
{
    [TestClass]
    public class CarnassialTest
    {
        private static readonly object CultureLock;
        private static bool CultureChanged;
        private static CultureInfo CurrentCulture;
        private static CultureInfo CurrentUICulture;
        private static CultureInfo DefaultThreadCurrentCulture;
        private static CultureInfo DefaultThreadCurrentUICulture;

        protected static App App { get; private set; }

        static CarnassialTest()
        {
            // Carnassial and the editor need an application instance to be created to load resources from
            // WPF allows only one Application per app domain, so make instance persistent so it can be reused across multiple Carnassial
            // and editor window lifetimes.  This works because Carnassial and the editor use very similar styling, allowing the editor 
            // to consume Carnassial styles with negligible effect on test coverage.
            CarnassialTest.App = new App()
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown
            };

            Application.Current.Resources.MergedDictionaries.Add((ResourceDictionary)Application.LoadComponent(new Uri("/Carnassial;component/CarnassialWindowStyle.xaml", UriKind.Relative)));
            Application.Current.Resources.MergedDictionaries.Add((ResourceDictionary)Application.LoadComponent(new Uri("/Carnassial;component/Properties/SharedResources.xaml", UriKind.Relative)));
            Application.Current.Resources.MergedDictionaries.Add((ResourceDictionary)Application.LoadComponent(new Uri("/Carnassial;component/Properties/Resources.xaml", UriKind.Relative)));

            ResourceDictionary editorResourceDictionary = (ResourceDictionary)Application.LoadComponent(new Uri("/CarnassialTemplateEditor;component/EditorWindowStyle.xaml", UriKind.Relative));
            Application.Current.Resources.MergedDictionaries.Add((ResourceDictionary)Application.LoadComponent(new Uri("/CarnassialTemplateEditor;component/Properties/Resources.xaml", UriKind.Relative)));
            foreach (object key in editorResourceDictionary.Keys)
            {
                if (Application.Current.Resources.Contains(key) == false)
                {
                    Application.Current.Resources.Add(key, editorResourceDictionary[key]);
                }
            }

            CarnassialTest.CultureLock = new object();
            CarnassialTest.CultureChanged = false;
        }

        protected CarnassialTest()
        {
            this.WorkingDirectory = Environment.CurrentDirectory;

            // Constants.Images needs to load resources from Carnassial.exe and falls back to Application.ResourceAssembly if Application.Current isn't set
            // for unit tests neither Current or ResourceAssembly gets set as Carnassial.exe is not the entry point
            if (Application.ResourceAssembly == null)
            {
                Application.ResourceAssembly = typeof(Constant.Images).Assembly;
            }
        }

        public TestContext TestContext { get; set; }

        /// <summary>
        /// Gets the name of an optional subdirectory under the working directory from which all tests in the current class access database and image files.
        /// Classes wishing to use this mechanism should call EnsureTestClassSubdirectory() in their constructor to set up the subdirectory.
        /// </summary>
        protected string TestClassSubdirectory { get; private set; }

        /// <summary>
        /// Gets the path to the root folder under which all tests execute.
        /// </summary>
        protected string WorkingDirectory { get; private set; }

        [AssemblyCleanup]
        public static void AssemblyCleanup()
        {
            CarnassialTest.App.Shutdown();
        }

        /// <summary>
        /// Clones the specified template and image databases and opens the image database's clone.
        /// </summary>
        protected FileDatabase CloneFileDatabase(string templateDatabaseBaseFileName, string fileDatabaseFileName)
        {
            using TemplateDatabase templateDatabase = this.CloneTemplateDatabase(templateDatabaseBaseFileName);
            string fileDatabaseSourceFilePath = Path.Combine(this.WorkingDirectory, fileDatabaseFileName);
            string fileDatabaseCloneFilePath = this.GetUniqueFilePathForTest(fileDatabaseFileName);
            File.Copy(fileDatabaseSourceFilePath, fileDatabaseCloneFilePath, true);

            Assert.IsTrue(FileDatabase.TryCreateOrOpen(fileDatabaseCloneFilePath, templateDatabase, false, LogicalOperator.And, out FileDatabase fileDatabase));
            return fileDatabase;
        }

        /// <summary>
        /// Clones the specified template database and opens the clone.
        /// </summary>
        protected TemplateDatabase CloneTemplateDatabase(string templateDatabaseFileName)
        {
            string templateDatabaseSourceFilePath = Path.Combine(this.WorkingDirectory, templateDatabaseFileName);
            string templateDatabaseCloneFilePath = this.GetUniqueFilePathForTest(templateDatabaseFileName);
            File.Copy(templateDatabaseSourceFilePath, templateDatabaseCloneFilePath, true);

            bool result = TemplateDatabase.TryCreateOrOpen(templateDatabaseCloneFilePath, out TemplateDatabase clone);
            Assert.IsTrue(result, "Open of template database '{0}' failed.", templateDatabaseCloneFilePath);
            return clone;
        }

        /// <summary>
        /// Creates a file data table row from the specified FileExpectation.  Verifies the file isn't already in the database and does not add it to the database.
        /// </summary>
        protected ImageRow CreateFile(FileDatabase fileDatabase, TimeZoneInfo imageSetTimeZone, FileExpectations fileExpectation, out MetadataReadResults metadataReadResult)
        {
            FileInfo fileInfo = new(Path.Combine(this.WorkingDirectory, fileExpectation.RelativePath, fileExpectation.FileName));
            string relativePath = Path.GetDirectoryName(NativeMethods.GetRelativePathFromDirectoryToFile(fileDatabase.FolderPath, fileInfo));
            ImageRow file = fileDatabase.Files.CreateAndAppendFile(fileInfo.Name, relativePath);
            using (FileLoadAtom loadAtom = new(file.RelativePath, new FileLoad(file), new FileLoad((string)null), 0))
            {
                loadAtom.CreateJpegs(fileDatabase.FolderPath);
                MemoryImage thumbnail = null;
                loadAtom.ClassifyFromThumbnails(Constant.Images.DarkLuminosityThresholdDefault, false, ref thumbnail);
                loadAtom.ReadDateTimeOffsets(fileDatabase.FolderPath, imageSetTimeZone);
                metadataReadResult = loadAtom.First.MetadataReadResult;
            }
            return file;
        }

        /// <summary>
        /// Clones the specified template database and creates a file database unique to the calling test.
        /// </summary>
        protected FileDatabase CreateFileDatabase(string templateDatabaseBaseFileName, string fileDatabaseBaseFileName)
        {
            using TemplateDatabase templateDatabase = this.CloneTemplateDatabase(templateDatabaseBaseFileName);
            return this.CreateFileDatabase(templateDatabase, fileDatabaseBaseFileName);
        }

        /// <summary>
        /// Creates a file database unique to the calling test.
        /// </summary>
        protected FileDatabase CreateFileDatabase(TemplateDatabase templateDatabase, string fileDatabaseBaseFileName)
        {
            string fileDatabaseFilePath = this.GetUniqueFilePathForTest(fileDatabaseBaseFileName);
            if (File.Exists(fileDatabaseFilePath))
            {
                File.Delete(fileDatabaseFilePath);
            }

            Assert.IsTrue(FileDatabase.TryCreateOrOpen(fileDatabaseFilePath, templateDatabase, false, LogicalOperator.And, out FileDatabase fileDatabase));
            return fileDatabase;
        }

        /// <summary>
        /// Creates a template database unique to the calling test.
        /// </summary>
        protected TemplateDatabase CreateTemplateDatabase(string templateDatabaseBaseFileName)
        {
            // remove any previously existing database
            string templateDatabaseFilePath = this.GetUniqueFilePathForTest(templateDatabaseBaseFileName);
            if (File.Exists(templateDatabaseFilePath))
            {
                File.Delete(templateDatabaseFilePath);
            }

            // create the new database
            Assert.IsTrue(TemplateDatabase.TryCreateOrOpen(templateDatabaseFilePath, out TemplateDatabase templateDatabase));
            return templateDatabase;
        }

        protected void EnsureTestClassSubdirectory()
        {
            // ensure subdirectory exists
            this.TestClassSubdirectory = this.GetType().Name;
            string subdirectoryPath = Path.Combine(this.WorkingDirectory, this.TestClassSubdirectory);
            if (Directory.Exists(subdirectoryPath) == false)
            {
                Directory.CreateDirectory(subdirectoryPath);
            }

            // ensure subdirectory contains default files
            List<string> defaultFiles = new()
            {
                TestConstant.FileExpectation.DaylightBobcat.FileName,
                TestConstant.FileExpectation.InfraredMarten.FileName
            };
            foreach (string fileName in defaultFiles)
            {
                FileInfo sourceFile = new(Path.Combine(this.WorkingDirectory, fileName));
                FileInfo destinationFile = new(Path.Combine(this.WorkingDirectory, this.TestClassSubdirectory, fileName));
                if (destinationFile.Exists == false ||
                    destinationFile.LastWriteTimeUtc < sourceFile.LastWriteTimeUtc)
                {
                    sourceFile.CopyTo(destinationFile.FullName, true);
                }
            }
        }

        private byte[] HexStringToByteArray(string hex)
        {
            if ((hex.Length % 2) != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(hex), "Hex string must have an even number of characters to fully represent each byte.");
            }

            byte[] bytes = new byte[hex.Length / 2];
            for (int byteIndex = 0; byteIndex < bytes.Length; ++byteIndex)
            {
                bytes[byteIndex] = byte.Parse(hex.Substring(2 * byteIndex, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            }
            return bytes;
        }

        protected List<FileExpectations> PopulateDefaultDatabase(FileDatabase fileDatabase)
        {
            return this.PopulateDefaultDatabase(fileDatabase, false);
        }

        protected List<FileExpectations> PopulateDefaultDatabase(FileDatabase fileDatabase, bool excludeSubfolderFiles)
        {
            TimeZoneInfo imageSetTimeZone = fileDatabase.ImageSet.GetTimeZoneInfo();

            // files in same folder as .tdb and .ddb
            ImageRow martenImage = this.CreateFile(fileDatabase, imageSetTimeZone, TestConstant.FileExpectation.InfraredMarten, out MetadataReadResults martenMetadataRead);
            Assert.IsTrue(martenMetadataRead.HasFlag(MetadataReadResults.DateTime));

            ImageRow bobcatImage = this.CreateFile(fileDatabase, imageSetTimeZone, TestConstant.FileExpectation.DaylightBobcat, out MetadataReadResults bobcatMetadataRead);
            Assert.IsTrue(bobcatMetadataRead.HasFlag(MetadataReadResults.DateTime));

            using (AddFilesTransactionSequence addFiles = fileDatabase.CreateAddFilesTransaction())
            {
                addFiles.AddToSequence(new List<FileLoad>() { new FileLoad(martenImage), new FileLoad(bobcatImage) }, 0, 2);
                addFiles.Commit();
            }
            fileDatabase.SelectFiles(FileSelection.All);

            using FileTableEnumerator fileEnumerator = new(fileDatabase);
            Assert.IsTrue(fileEnumerator.TryMoveToFile(0));
            martenImage = fileEnumerator.Current;
            Assert.IsTrue(fileEnumerator.MoveNext());
            bobcatImage = fileEnumerator.Current;

            FileExpectations bobcatExpectation = new(TestConstant.FileExpectation.DaylightBobcat)
            {
                ID = 2
            };
            bobcatExpectation.UserControlsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.Choice0, "choice b");
            bobcatExpectation.UserControlsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.Choice3, fileDatabase.Controls[TestConstant.DefaultDatabaseColumn.Choice3].DefaultValue);
            bobcatExpectation.UserControlsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.ChoiceNotVisible, fileDatabase.Controls[TestConstant.DefaultDatabaseColumn.ChoiceNotVisible].DefaultValue);
            bobcatExpectation.UserControlsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.ChoiceWithCustomDataLabel, fileDatabase.Controls[TestConstant.DefaultDatabaseColumn.ChoiceWithCustomDataLabel].DefaultValue);
            bobcatExpectation.UserControlsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.Counter0, 1);
            bobcatExpectation.UserControlsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.Counter0Markers, TestConstant.DefaultMarkerPositions);
            bobcatExpectation.UserControlsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.Counter3, Int32.Parse(fileDatabase.Controls[TestConstant.DefaultDatabaseColumn.Counter3].DefaultValue, NumberStyles.None, Constant.InvariantCulture));
            bobcatExpectation.UserControlsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.Counter3Markers, TestConstant.DefaultMarkerPositions);
            bobcatExpectation.UserControlsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.CounterNotVisible, Int32.Parse(fileDatabase.Controls[TestConstant.DefaultDatabaseColumn.CounterNotVisible].DefaultValue, NumberStyles.None, Constant.InvariantCulture));
            bobcatExpectation.UserControlsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.CounterNotVisibleMarkers, TestConstant.DefaultMarkerPositions);
            bobcatExpectation.UserControlsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.CounterWithCustomDataLabel, Int32.Parse(fileDatabase.Controls[TestConstant.DefaultDatabaseColumn.CounterWithCustomDataLabel].DefaultValue, NumberStyles.None, Constant.InvariantCulture));
            bobcatExpectation.UserControlsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.CounterWithCustomDataLabelMarkers, TestConstant.DefaultMarkerPositions);
            bobcatExpectation.UserControlsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.Flag0, !String.Equals(fileDatabase.Controls[TestConstant.DefaultDatabaseColumn.Flag0].DefaultValue, Constant.ControlDefault.FlagValue, StringComparison.Ordinal));
            bobcatExpectation.UserControlsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.Flag3, !String.Equals(fileDatabase.Controls[TestConstant.DefaultDatabaseColumn.Flag3].DefaultValue, Constant.ControlDefault.FlagValue, StringComparison.Ordinal));
            bobcatExpectation.UserControlsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.FlagNotVisible, true);
            bobcatExpectation.UserControlsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.FlagWithCustomDataLabel, !String.Equals(fileDatabase.Controls[TestConstant.DefaultDatabaseColumn.FlagWithCustomDataLabel].DefaultValue, Constant.ControlDefault.FlagValue, StringComparison.Ordinal));
            bobcatExpectation.UserControlsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.Note0, fileDatabase.Controls[TestConstant.DefaultDatabaseColumn.Note0].DefaultValue);
            bobcatExpectation.UserControlsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.Note3, "bobcat");
            bobcatExpectation.UserControlsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.NoteNotVisible, "adult");
            bobcatExpectation.UserControlsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.NoteWithCustomDataLabel, fileDatabase.Controls[TestConstant.DefaultDatabaseColumn.NoteWithCustomDataLabel].DefaultValue);
            bobcatImage[TestConstant.DefaultDatabaseColumn.Choice0] = "choice b";
            bobcatImage[TestConstant.DefaultDatabaseColumn.Counter0] = 1;
            bobcatImage[TestConstant.DefaultDatabaseColumn.FlagNotVisible] = true;
            bobcatImage[TestConstant.DefaultDatabaseColumn.Note3] = "bobcat";
            bobcatImage[TestConstant.DefaultDatabaseColumn.NoteNotVisible] = "adult";
            Assert.IsTrue(fileDatabase.TrySyncFileToDatabase(bobcatImage));

            byte[] counter0MarkerPositions = this.HexStringToByteArray("7d31fc3ebeaa103f72a80b3f1db70f3fbb2c153f9ae80b3f");
            FileExpectations martenExpectation = new(TestConstant.FileExpectation.InfraredMarten)
            {
                ID = 1
            };
            martenExpectation.UserControlsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.Choice0, "choice b");
            martenExpectation.UserControlsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.Choice3, fileDatabase.Controls[TestConstant.DefaultDatabaseColumn.Choice3].DefaultValue);
            martenExpectation.UserControlsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.ChoiceNotVisible, fileDatabase.Controls[TestConstant.DefaultDatabaseColumn.ChoiceNotVisible].DefaultValue);
            martenExpectation.UserControlsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.ChoiceWithCustomDataLabel, fileDatabase.Controls[TestConstant.DefaultDatabaseColumn.ChoiceWithCustomDataLabel].DefaultValue);
            martenExpectation.UserControlsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.Counter0, 3);
            martenExpectation.UserControlsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.Counter0Markers, counter0MarkerPositions);
            martenExpectation.UserControlsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.Counter3, Int32.Parse(fileDatabase.Controls[TestConstant.DefaultDatabaseColumn.Counter3].DefaultValue, NumberStyles.None, Constant.InvariantCulture));
            martenExpectation.UserControlsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.Counter3Markers, TestConstant.DefaultMarkerPositions);
            martenExpectation.UserControlsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.CounterNotVisible, Int32.Parse(fileDatabase.Controls[TestConstant.DefaultDatabaseColumn.CounterNotVisible].DefaultValue, NumberStyles.None, Constant.InvariantCulture));
            martenExpectation.UserControlsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.CounterNotVisibleMarkers, TestConstant.DefaultMarkerPositions);
            martenExpectation.UserControlsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.CounterWithCustomDataLabel, Int32.Parse(fileDatabase.Controls[TestConstant.DefaultDatabaseColumn.CounterWithCustomDataLabel].DefaultValue, NumberStyles.None, Constant.InvariantCulture));
            martenExpectation.UserControlsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.CounterWithCustomDataLabelMarkers, TestConstant.DefaultMarkerPositions);
            martenExpectation.UserControlsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.Flag0, !String.Equals(fileDatabase.Controls[TestConstant.DefaultDatabaseColumn.Flag0].DefaultValue, Constant.ControlDefault.FlagValue, StringComparison.Ordinal));
            martenExpectation.UserControlsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.Flag3, !String.Equals(fileDatabase.Controls[TestConstant.DefaultDatabaseColumn.Flag3].DefaultValue, Constant.ControlDefault.FlagValue, StringComparison.Ordinal));
            martenExpectation.UserControlsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.FlagNotVisible, true);
            martenExpectation.UserControlsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.FlagWithCustomDataLabel, !String.Equals(fileDatabase.Controls[TestConstant.DefaultDatabaseColumn.FlagWithCustomDataLabel].DefaultValue, Constant.ControlDefault.FlagValue, StringComparison.Ordinal));
            martenExpectation.UserControlsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.Note0, fileDatabase.Controls[TestConstant.DefaultDatabaseColumn.Note0].DefaultValue);
            martenExpectation.UserControlsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.Note3, "American marten");
            martenExpectation.UserControlsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.NoteNotVisible, "adult");
            martenExpectation.UserControlsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.NoteWithCustomDataLabel, fileDatabase.Controls[TestConstant.DefaultDatabaseColumn.NoteWithCustomDataLabel].DefaultValue);
            ImageRow martenFile = fileDatabase.Files[0];
            martenFile[TestConstant.DefaultDatabaseColumn.Choice0] = "choice b";
            martenFile[TestConstant.DefaultDatabaseColumn.Counter0] = 3;
            martenFile[TestConstant.DefaultDatabaseColumn.Counter0Markers] = counter0MarkerPositions;
            martenFile[TestConstant.DefaultDatabaseColumn.FlagNotVisible] = true;
            martenFile[TestConstant.DefaultDatabaseColumn.Note3] = "American marten";
            martenFile[TestConstant.DefaultDatabaseColumn.NoteNotVisible] = "adult";
            Assert.IsTrue(martenFile.HasChanges);
            Assert.IsTrue(fileDatabase.TrySyncFileToDatabase(martenFile));
            Assert.IsFalse(martenFile.HasChanges);

            // assemble expectations
            List<FileExpectations> fileExpectations = new()
            {
                martenExpectation,
                bobcatExpectation
            };

            // files in subfolder
            if (excludeSubfolderFiles == false)
            {
                FileExpectations martenPairExpectation = new(TestConstant.FileExpectation.DaylightMartenPair)
                {
                    ID = 3
                };
                ImageRow martenPairImage = this.CreateFile(fileDatabase, imageSetTimeZone, TestConstant.FileExpectation.DaylightMartenPair, out MetadataReadResults martenPairMetadataRead);
                Assert.IsTrue(martenPairMetadataRead.HasFlag(MetadataReadResults.DateTime));

                FileExpectations coyoteExpectation = new(TestConstant.FileExpectation.DaylightCoyote)
                {
                    ID = 4
                };
                ImageRow coyoteImage = this.CreateFile(fileDatabase, imageSetTimeZone, TestConstant.FileExpectation.DaylightCoyote, out MetadataReadResults coyoteMetadataRead);
                Assert.IsTrue(coyoteMetadataRead.HasFlag(MetadataReadResults.DateTime));

                using (AddFilesTransactionSequence addFiles = fileDatabase.CreateAddFilesTransaction())
                {
                    addFiles.AddToSequence(new List<FileLoad>() { new FileLoad(martenPairImage), new FileLoad(coyoteImage) }, 0, 2);
                    addFiles.Commit();
                }
                fileDatabase.SelectFiles(FileSelection.All);
                Assert.IsTrue(String.Equals(fileDatabase.Files[2].FileName, martenPairImage.FileName, StringComparison.Ordinal));
                martenPairImage = fileDatabase.Files[2];
                Assert.IsTrue(String.Equals(fileDatabase.Files[3].FileName, coyoteImage.FileName, StringComparison.Ordinal));
                coyoteImage = fileDatabase.Files[3];

                fileExpectations.Add(martenPairExpectation);
                fileExpectations.Add(coyoteExpectation);

                Assert.IsTrue(fileEnumerator.MoveNext());
                Assert.IsTrue(fileEnumerator.MoveNext());
                Assert.IsTrue(fileEnumerator.Current.ID == coyoteExpectation.ID);
                coyoteImage = fileEnumerator.Current;
                coyoteImage[TestConstant.DefaultDatabaseColumn.Note3] = "coyote";
                coyoteImage[TestConstant.DefaultDatabaseColumn.NoteNotVisible] = "adult";
                coyoteImage[TestConstant.DefaultDatabaseColumn.NoteWithCustomDataLabel] = String.Empty;
                coyoteImage[TestConstant.DefaultDatabaseColumn.Note0] = "escaped field, because a comma is present";
                Assert.IsTrue(fileDatabase.TrySyncFileToDatabase(coyoteImage));
                Assert.IsFalse(coyoteImage.HasChanges);

                coyoteExpectation.UserControlsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.Choice0, fileDatabase.Controls[TestConstant.DefaultDatabaseColumn.Choice0].DefaultValue);
                coyoteExpectation.UserControlsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.Choice3, fileDatabase.Controls[TestConstant.DefaultDatabaseColumn.Choice3].DefaultValue);
                coyoteExpectation.UserControlsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.ChoiceNotVisible, fileDatabase.Controls[TestConstant.DefaultDatabaseColumn.ChoiceNotVisible].DefaultValue);
                coyoteExpectation.UserControlsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.ChoiceWithCustomDataLabel, fileDatabase.Controls[TestConstant.DefaultDatabaseColumn.ChoiceWithCustomDataLabel].DefaultValue);
                coyoteExpectation.UserControlsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.Counter0, Int32.Parse(fileDatabase.Controls[TestConstant.DefaultDatabaseColumn.Counter0].DefaultValue, NumberStyles.None, Constant.InvariantCulture));
                coyoteExpectation.UserControlsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.Counter0Markers, TestConstant.DefaultMarkerPositions);
                coyoteExpectation.UserControlsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.Counter3, Int32.Parse(fileDatabase.Controls[TestConstant.DefaultDatabaseColumn.Counter3].DefaultValue, NumberStyles.None, Constant.InvariantCulture));
                coyoteExpectation.UserControlsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.Counter3Markers, TestConstant.DefaultMarkerPositions);
                coyoteExpectation.UserControlsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.CounterNotVisible, Int32.Parse(fileDatabase.Controls[TestConstant.DefaultDatabaseColumn.CounterNotVisible].DefaultValue, NumberStyles.None, Constant.InvariantCulture));
                coyoteExpectation.UserControlsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.CounterNotVisibleMarkers, TestConstant.DefaultMarkerPositions);
                coyoteExpectation.UserControlsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.CounterWithCustomDataLabel, Int32.Parse(fileDatabase.Controls[TestConstant.DefaultDatabaseColumn.CounterWithCustomDataLabel].DefaultValue, NumberStyles.None, Constant.InvariantCulture));
                coyoteExpectation.UserControlsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.CounterWithCustomDataLabelMarkers, TestConstant.DefaultMarkerPositions);
                coyoteExpectation.UserControlsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.Flag0, !String.Equals(fileDatabase.Controls[TestConstant.DefaultDatabaseColumn.Flag0].DefaultValue, Constant.ControlDefault.FlagValue, StringComparison.Ordinal));
                coyoteExpectation.UserControlsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.Flag3, !String.Equals(fileDatabase.Controls[TestConstant.DefaultDatabaseColumn.Flag3].DefaultValue, Constant.ControlDefault.FlagValue, StringComparison.Ordinal));
                coyoteExpectation.UserControlsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.FlagNotVisible, !String.Equals(fileDatabase.Controls[TestConstant.DefaultDatabaseColumn.FlagNotVisible].DefaultValue, Constant.ControlDefault.FlagValue, StringComparison.Ordinal));
                coyoteExpectation.UserControlsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.FlagWithCustomDataLabel, !String.Equals(fileDatabase.Controls[TestConstant.DefaultDatabaseColumn.FlagWithCustomDataLabel].DefaultValue, Constant.ControlDefault.FlagValue, StringComparison.Ordinal));
                coyoteExpectation.UserControlsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.Note3, "coyote");
                coyoteExpectation.UserControlsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.NoteNotVisible, "adult");
                coyoteExpectation.UserControlsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.NoteWithCustomDataLabel, String.Empty);
                coyoteExpectation.UserControlsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.Note0, "escaped field, because a comma is present");

                Assert.IsTrue(fileDatabase.Files[2].ID == martenPairExpectation.ID);
                martenPairImage[TestConstant.DefaultDatabaseColumn.Note3] = "American marten";
                martenPairImage[TestConstant.DefaultDatabaseColumn.NoteNotVisible] = "adult";
                martenPairImage[TestConstant.DefaultDatabaseColumn.NoteWithCustomDataLabel] = String.Empty;
                martenPairImage[TestConstant.DefaultDatabaseColumn.Note0] = "escaped field due to presence of \",\"";
                Assert.IsTrue(fileDatabase.TrySyncFileToDatabase(martenPairImage));

                martenPairExpectation.UserControlsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.Choice0, fileDatabase.Controls[TestConstant.DefaultDatabaseColumn.Choice0].DefaultValue);
                martenPairExpectation.UserControlsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.Choice3, fileDatabase.Controls[TestConstant.DefaultDatabaseColumn.Choice3].DefaultValue);
                martenPairExpectation.UserControlsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.ChoiceNotVisible, fileDatabase.Controls[TestConstant.DefaultDatabaseColumn.ChoiceNotVisible].DefaultValue);
                martenPairExpectation.UserControlsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.ChoiceWithCustomDataLabel, fileDatabase.Controls[TestConstant.DefaultDatabaseColumn.ChoiceWithCustomDataLabel].DefaultValue);
                martenPairExpectation.UserControlsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.Counter0, Int32.Parse(fileDatabase.Controls[TestConstant.DefaultDatabaseColumn.Counter0].DefaultValue, NumberStyles.None, Constant.InvariantCulture));
                martenPairExpectation.UserControlsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.Counter0Markers, TestConstant.DefaultMarkerPositions);
                martenPairExpectation.UserControlsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.Counter3, Int32.Parse(fileDatabase.Controls[TestConstant.DefaultDatabaseColumn.Counter3].DefaultValue, NumberStyles.None, Constant.InvariantCulture));
                martenPairExpectation.UserControlsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.Counter3Markers, TestConstant.DefaultMarkerPositions);
                martenPairExpectation.UserControlsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.CounterNotVisible, Int32.Parse(fileDatabase.Controls[TestConstant.DefaultDatabaseColumn.CounterNotVisible].DefaultValue, NumberStyles.None, Constant.InvariantCulture));
                martenPairExpectation.UserControlsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.CounterNotVisibleMarkers, TestConstant.DefaultMarkerPositions);
                martenPairExpectation.UserControlsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.CounterWithCustomDataLabel, Int32.Parse(fileDatabase.Controls[TestConstant.DefaultDatabaseColumn.CounterWithCustomDataLabel].DefaultValue, NumberStyles.None, Constant.InvariantCulture));
                martenPairExpectation.UserControlsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.CounterWithCustomDataLabelMarkers, TestConstant.DefaultMarkerPositions);
                martenPairExpectation.UserControlsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.Flag0, !String.Equals(fileDatabase.Controls[TestConstant.DefaultDatabaseColumn.Flag0].DefaultValue, Constant.ControlDefault.FlagValue, StringComparison.Ordinal));
                martenPairExpectation.UserControlsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.Flag3, !String.Equals(fileDatabase.Controls[TestConstant.DefaultDatabaseColumn.Flag3].DefaultValue, Constant.ControlDefault.FlagValue, StringComparison.Ordinal));
                martenPairExpectation.UserControlsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.FlagNotVisible, !String.Equals(fileDatabase.Controls[TestConstant.DefaultDatabaseColumn.FlagNotVisible].DefaultValue, Constant.ControlDefault.FlagValue, StringComparison.Ordinal));
                martenPairExpectation.UserControlsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.FlagWithCustomDataLabel, !String.Equals(fileDatabase.Controls[TestConstant.DefaultDatabaseColumn.FlagWithCustomDataLabel].DefaultValue, Constant.ControlDefault.FlagValue, StringComparison.Ordinal));
                martenPairExpectation.UserControlsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.Note3, "American marten");
                martenPairExpectation.UserControlsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.NoteNotVisible, "adult");
                martenPairExpectation.UserControlsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.NoteWithCustomDataLabel, String.Empty);
                martenPairExpectation.UserControlsByDataLabel.Add(TestConstant.DefaultDatabaseColumn.Note0, "escaped field due to presence of \",\"");
            }

            // reload the file table so direct database updates are visible
            fileDatabase.SelectFiles(FileSelection.All);

            return fileExpectations;
        }

        protected string GetUniqueFilePathForTest(string baseFileName)
        {
            string uniqueTestIdentifier = String.Format(CultureInfo.InvariantCulture, "{0}.{1}", this.GetType().FullName, this.TestContext.TestName);
            string uniqueFileName = String.Format(CultureInfo.InvariantCulture, "{0}.{1}{2}", Path.GetFileNameWithoutExtension(baseFileName), uniqueTestIdentifier, Path.GetExtension(baseFileName));

            if (String.IsNullOrWhiteSpace(this.TestClassSubdirectory))
            {
                return Path.Combine(this.WorkingDirectory, uniqueFileName);
            }
            return Path.Combine(this.WorkingDirectory, this.TestClassSubdirectory, uniqueFileName);
        }

        protected static bool TryChangeToTestCulture()
        {
            lock (CarnassialTest.CultureLock)
            {
                if (CarnassialTest.CultureChanged)
                {
                    return false;
                }

                #if DEBUG
                LocalizedApplication.UseCurrentCulture = true;
                #endif

                CarnassialTest.CurrentCulture = CultureInfo.CurrentCulture;
                CarnassialTest.CurrentUICulture = CultureInfo.CurrentUICulture;
                CarnassialTest.DefaultThreadCurrentCulture = CultureInfo.DefaultThreadCurrentCulture;
                CarnassialTest.DefaultThreadCurrentUICulture = CultureInfo.DefaultThreadCurrentUICulture;

                // change to a culture other than the developer's to provide sanity coverage
                string cultureName = TestConstant.Globalization.DefaultUITestCultureNames[(int)DateTime.UtcNow.DayOfWeek];
                if (CultureInfo.CurrentCulture.Name.StartsWith(cultureName, StringComparison.Ordinal))
                {
                    cultureName = TestConstant.Globalization.AlternateUITestCultureName;
                }

                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
                CultureInfo.CurrentUICulture = CultureInfo.CurrentCulture;
                CultureInfo.DefaultThreadCurrentCulture = CultureInfo.CurrentCulture;
                CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.CurrentCulture;

                CarnassialTest.CultureChanged = true;
                return true;
            }
        }

        protected static bool TryRevertToDefaultCultures()
        {
            lock (CarnassialTest.CultureLock)
            {
                if (CarnassialTest.CultureChanged == false)
                {
                    return false;
                }

                CultureInfo.CurrentCulture = UserInterfaceTests.CurrentCulture;
                CultureInfo.CurrentUICulture = UserInterfaceTests.CurrentUICulture;
                CultureInfo.DefaultThreadCurrentCulture = UserInterfaceTests.DefaultThreadCurrentCulture;
                CultureInfo.DefaultThreadCurrentUICulture = UserInterfaceTests.DefaultThreadCurrentUICulture;
                CarnassialTest.CultureChanged = false;
                return true;
            }
        }
    }
}
