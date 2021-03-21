using Carnassial.Github;
using Carnassial.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace Carnassial.UnitTests
{
    [TestClass]
    public class VersionTests : CarnassialTest
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
        public void CheckForUpdates()
        {
            GithubReleaseClient carnassialUpdates = new(Constant.ApplicationName, CarnassialConfigurationSettings.GetLatestReleaseApiAddress());
            Assert.IsTrue(carnassialUpdates.TryGetAndParseRelease(false, out Version publiclyAvailableVersion));

            Version currentVersion = typeof(CarnassialWindow).Assembly.GetName().Version;
            Assert.IsTrue(publiclyAvailableVersion <= currentVersion);
        }
    }
}
