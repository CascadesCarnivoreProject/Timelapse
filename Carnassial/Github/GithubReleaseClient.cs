using Carnassial.Util;
using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using MessageBox = Carnassial.Dialog.MessageBox;

namespace Carnassial.Github
{
    public class GithubReleaseClient
    {
        private readonly string applicationName;
        private readonly Uri latestReleaseAddress;

        public GithubReleaseClient(string applicationName, Uri? latestRelease)
        {
            if (String.IsNullOrWhiteSpace(applicationName))
            {
                throw new ArgumentOutOfRangeException(nameof(applicationName));
            }

            this.applicationName = applicationName;
            this.latestReleaseAddress = latestRelease ?? throw new ArgumentNullException(nameof(latestRelease));
        }

        // Checks for updates by comparing the current version number with a version stored on the Carnassial website in an xml file.
        public bool TryGetAndParseRelease(bool showNoUpdatesMessage, out Version? publiclyAvailableVersion)
        {
            publiclyAvailableVersion = null;

            using (WebClient webClient = new())
            {
                webClient.Headers.Add(HttpRequestHeader.UserAgent, "Carnassial-GithubReleaseClient");
                try
                {
                    string releaseJson = webClient.DownloadString(this.latestReleaseAddress);
                    if (String.IsNullOrWhiteSpace(releaseJson) || (releaseJson.IndexOf("\"message\": \"Not Found\"", StringComparison.Ordinal) != -1))
                    {
                        // no releases, so nothing to do
                        return false;
                    }
                    JsonSerializer serializer = JsonSerializer.CreateDefault();
                    using StringReader reader = new(releaseJson);
                    using JsonReader jsonReader = new JsonTextReader(reader);
                    GithubRelease? latestRelease = serializer.Deserialize<GithubRelease>(jsonReader);
                    if (latestRelease == null || String.IsNullOrWhiteSpace(latestRelease.TagName))
                    {
                        return false;
                    }
                    // Version's parse implementation requires the leading v of Github convention be removed
                    if (Version.TryParse(latestRelease.TagName[1..], out publiclyAvailableVersion) == false)
                    {
                        return false;
                    }
                }
                catch (WebException exception)
                {
                    // 404 if no production releases (Github's latest endpoint doesn't return releases with prerelease = true)
                    if ((exception.Response is HttpWebResponse == false) || (((HttpWebResponse)exception.Response).StatusCode != HttpStatusCode.NotFound))
                    {
                        Debug.Fail(exception.ToString());
                    }
                    return false;
                }
                catch (Exception exception)
                {
                    Debug.Fail(exception.ToString());
                    throw;
                }
            }

            // get the running version  
            Version? currentVersion = Assembly.GetExecutingAssembly().GetName().Version;

            // compare the versions  
            if (currentVersion < publiclyAvailableVersion)
            {
                // ask the user if they would like to download the new version  
                MessageBox messageBox = MessageBox.FromResource(Constant.ResourceKey.GithubReleaseClientGetNewVersion, App.Current.MainWindow,
                                                                publiclyAvailableVersion,
                                                                this.applicationName,
                                                                currentVersion,
                                                                CarnassialConfigurationSettings.GetReleasesBrowserAddress());
                if (messageBox.ShowDialog() == true)
                {
                    Uri releasesAddress = CarnassialConfigurationSettings.GetReleasesBrowserAddress();
                    Process.Start(releasesAddress.AbsoluteUri);
                }
            }
            else if (showNoUpdatesMessage)
            {
                MessageBox messageBox = MessageBox.FromResource(Constant.ResourceKey.GithubReleaseClientNoUpdates, App.Current.MainWindow, this.applicationName, currentVersion);
                messageBox.ShowDialog();
            }

            return true;
        }
    }
}
