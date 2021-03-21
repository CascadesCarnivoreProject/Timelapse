using System;
using System.Configuration;
using System.Globalization;

namespace Carnassial.Util
{
    public static class CarnassialConfigurationSettings
    {
        public static Uri GetDevTeamEmailLink()
        {
            UriBuilder mailto = new(Uri.UriSchemeMailto + ":" + ConfigurationManager.AppSettings[Constant.ApplicationSettings.DevTeamEmail])
            {
                Query = String.Format(CultureInfo.InvariantCulture, "subject={0} {1}: feedback", Constant.ApplicationName, typeof(CarnassialConfigurationSettings).Assembly.GetName().Version)
            };
            return mailto.Uri;
        }

        public static Uri GetLatestReleaseApiAddress()
        {
            return CarnassialConfigurationSettings.GetUriSetting(Constant.GitHub.ApiBaseAddress, Constant.ApplicationSettings.GithubOrganizationAndRepo, "releases", "latest");
        }

        public static Uri GetIssuesBrowserAddress()
        {
            return CarnassialConfigurationSettings.GetUriSetting(Constant.GitHub.BaseAddress, Constant.ApplicationSettings.GithubOrganizationAndRepo, "issues");
        }

        public static Uri GetReleasesBrowserAddress()
        {
            return CarnassialConfigurationSettings.GetUriSetting(Constant.GitHub.BaseAddress, Constant.ApplicationSettings.GithubOrganizationAndRepo, "releases");
        }

        public static Uri GetTutorialBrowserAddress()
        {
            return CarnassialConfigurationSettings.GetUriSetting(Constant.GitHub.BaseAddress, Constant.ApplicationSettings.GithubOrganizationAndRepo, "wiki", "Tutorial");
        }

        private static Uri GetUriSetting(Uri baseAddress, string key, params string[] additionalPath)
        {
            UriBuilder uriBuilder = new(baseAddress);
            uriBuilder.Path += ConfigurationManager.AppSettings[key];
            foreach (string token in additionalPath)
            {
                uriBuilder.Path += "/" + token;
            }
            return uriBuilder.Uri;
        }
    }
}
