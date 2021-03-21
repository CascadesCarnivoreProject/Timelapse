using Carnassial.Editor.Util;
using Carnassial.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Win32;
using System.IO;
using System.Windows;

namespace Carnassial.UnitTests
{
    [TestClass]
    public class RegistryTests : CarnassialTest
    {
        /// <summary>
        /// Sanity test against whatever the current state of Carnassial's registry keys is.
        /// </summary>
        [TestMethod]
        public void CarnassialProductionKeysRead()
        {
            CarnassialState state = new();
            state.ReadFromRegistry();
        }

        [TestMethod]
        public void CarnassialTestKeysCreateReuseUpdate()
        {
            string testRootKey = Constant.Registry.RootKey + "CarnassialUnitTest";
            using (RegistryKey testKey = Registry.CurrentUser.OpenSubKey(testRootKey))
            {
                if (testKey != null)
                {
                    Registry.CurrentUser.DeleteSubKeyTree(testRootKey);
                }
            }

            CarnassialUserRegistrySettings userSettings = new(testRootKey);
            this.VerifyDefaultState(userSettings);

            // write
            userSettings.WriteToRegistry();

            // loopback
            userSettings.ReadFromRegistry();
            this.VerifyDefaultState(userSettings);

            // overwrite
            userSettings.WriteToRegistry();

            // modify
            userSettings.AudioFeedback = true;
            int windowLocation = 100;
            int windowSize = 1000;
            userSettings.CarnassialWindowPosition = new Rect(windowLocation, windowLocation, windowSize, windowSize);
            int controlGridWidth = userSettings.ControlGridWidth + 22;
            userSettings.ControlGridWidth = controlGridWidth;
            userSettings.CustomSelectionTermCombiningOperator = Database.LogicalOperator.Or;
            double modifiedDarkPixelRatioThreshold = userSettings.DarkLuminosityThreshold + 1.0;
            userSettings.DarkLuminosityThreshold = modifiedDarkPixelRatioThreshold;
            string databasePath = Path.Combine(this.WorkingDirectory, Constant.File.DefaultFileDatabaseFileName);
            userSettings.MostRecentImageSets.SetMostRecent(databasePath);
            userSettings.OrderFilesByDateTime = true;
            userSettings.SkipFileClassification = true;
            userSettings.SuppressFileCountOnImportDialog = true;
            userSettings.SuppressImportPrompt = true;
            userSettings.Throttles.ImageClassificationChangeSlowdown = Constant.ThrottleValues.ImageClassificationSlowdownMinimum;
            userSettings.Throttles.SetDesiredImageRendersPerSecond(Constant.ThrottleValues.DesiredMaximumImageRendersPerSecondUpperBound);
            userSettings.Throttles.VideoSlowdown = Constant.ThrottleValues.VideoSlowdownMinimum;

            userSettings.WriteToRegistry();
            userSettings.ReadFromRegistry();

            Assert.IsTrue(userSettings.AudioFeedback);
            Assert.IsTrue(userSettings.CarnassialWindowPosition.X == windowLocation && userSettings.CarnassialWindowPosition.Y == windowLocation);
            Assert.IsTrue(userSettings.CarnassialWindowPosition.Width == windowSize && userSettings.CarnassialWindowPosition.Height == windowSize);
            Assert.IsTrue(userSettings.ControlGridWidth == controlGridWidth);
            Assert.IsTrue(userSettings.CustomSelectionTermCombiningOperator == Database.LogicalOperator.Or);
            Assert.IsTrue(userSettings.DarkLuminosityThreshold == modifiedDarkPixelRatioThreshold);
            Assert.IsNotNull(userSettings.MostRecentImageSets);
            Assert.IsTrue(userSettings.MostRecentImageSets.Count == 1);
            Assert.IsTrue(userSettings.MostRecentImageSets.TryGetMostRecent(out string mostRecentDatabasePath));
            Assert.IsTrue(mostRecentDatabasePath == databasePath);
            Assert.IsTrue(userSettings.OrderFilesByDateTime);
            Assert.IsTrue(userSettings.SkipFileClassification);
            Assert.IsTrue(userSettings.SuppressFileCountOnImportDialog);
            Assert.IsTrue(userSettings.SuppressImportPrompt);
            Assert.IsTrue(userSettings.Throttles.ImageClassificationChangeSlowdown == Constant.ThrottleValues.ImageClassificationSlowdownMinimum);
            Assert.IsTrue(userSettings.Throttles.DesiredImageRendersPerSecond == Constant.ThrottleValues.DesiredMaximumImageRendersPerSecondUpperBound);
            Assert.IsTrue(userSettings.Throttles.VideoSlowdown == Constant.ThrottleValues.VideoSlowdownMinimum);

            Registry.CurrentUser.DeleteSubKeyTree(testRootKey);
        }

        /// <summary>
        /// Sanity test against whatever the current state of the editor's registry keys is.
        /// </summary>
        [TestMethod]
        public void EditorProductionKeysRead()
        {
            EditorUserRegistrySettings editorRegistry = new();
        }

        [TestMethod]
        public void EditorTestKeysCreateReuseUpdate()
        {
            string testRootKey = Constant.Registry.RootKey + "EditorUnitTest";
            using (RegistryKey testKey = Registry.CurrentUser.OpenSubKey(testRootKey))
            {
                if (testKey != null)
                {
                    Registry.CurrentUser.DeleteSubKeyTree(testRootKey);
                }
            }

            EditorUserRegistrySettings editorRegistry = new(testRootKey);
            Assert.IsNotNull(editorRegistry.MostRecentTemplates);
            Assert.IsTrue(editorRegistry.MostRecentTemplates.Count == 0);

            // write
            editorRegistry.WriteToRegistry();

            // loopback
            editorRegistry.ReadFromRegistry();
            Assert.IsNotNull(editorRegistry.MostRecentTemplates);
            Assert.IsTrue(editorRegistry.MostRecentTemplates.Count == 0);
            Assert.IsFalse(editorRegistry.MostRecentTemplates.TryGetMostRecent(out string _));

            // overwrite
            editorRegistry.WriteToRegistry();

            // modify
            string templatePath = Path.Combine(this.WorkingDirectory, Constant.File.DefaultTemplateDatabaseFileName);
            editorRegistry.MostRecentTemplates.SetMostRecent(templatePath);
            editorRegistry.WriteToRegistry();
            editorRegistry.ReadFromRegistry();

            Assert.IsNotNull(editorRegistry.MostRecentTemplates);
            Assert.IsTrue(editorRegistry.MostRecentTemplates.Count == 1);
            Assert.IsTrue(editorRegistry.MostRecentTemplates.TryGetMostRecent(out string mostRecentTemplatePath));
            Assert.IsTrue(mostRecentTemplatePath == templatePath);

            Registry.CurrentUser.DeleteSubKeyTree(testRootKey);
        }

        private void VerifyDefaultState(CarnassialUserRegistrySettings userSettings)
        {
            Assert.IsFalse(userSettings.AudioFeedback);
            Assert.IsTrue(userSettings.CarnassialWindowPosition.X == 0 && userSettings.CarnassialWindowPosition.Y == 0);
            Assert.IsTrue(userSettings.CarnassialWindowPosition.Width == 1350 && userSettings.CarnassialWindowPosition.Height == 900);
            Assert.IsTrue(userSettings.DarkLuminosityThreshold == Constant.Images.DarkLuminosityThresholdDefault);
            Assert.IsNotNull(userSettings.MostRecentImageSets);
            Assert.IsTrue(userSettings.MostRecentImageSets.Count == 0);
            Assert.IsFalse(userSettings.MostRecentImageSets.TryGetMostRecent(out string mostRecentDatabasePath));
            Assert.IsNull(mostRecentDatabasePath);
            Assert.IsFalse(userSettings.OrderFilesByDateTime);
            Assert.IsFalse(userSettings.SkipFileClassification);
            Assert.IsFalse(userSettings.SuppressImportPrompt);
            Assert.IsFalse(userSettings.SuppressFileCountOnImportDialog);
            Assert.IsTrue(userSettings.Throttles.DesiredImageRendersPerSecond == Constant.ThrottleValues.DesiredMaximumImageRendersPerSecondDefault);
            Assert.IsTrue(userSettings.Throttles.ImageClassificationChangeSlowdown == Constant.ThrottleValues.ImageClassificationSlowdownDefault);
            Assert.IsTrue(userSettings.Throttles.VideoSlowdown == Constant.ThrottleValues.VideoSlowdownDefault);
        }
    }
}