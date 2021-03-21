using Carnassial.Github;
using Carnassial.Util;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Navigation;

namespace Carnassial.Editor.Dialog
{
    public partial class AboutEditor : WindowWithSystemMenu
    {
        private readonly Uri latestReleaseAddress;
        private readonly Uri releasesAddress;

        public DateTime? MostRecentCheckForUpdate { get; private set; }

        public AboutEditor(Window owner)
        {
            this.InitializeComponent();
            this.Message.SetVisibility();

            this.latestReleaseAddress = CarnassialConfigurationSettings.GetLatestReleaseApiAddress();
            this.MostRecentCheckForUpdate = null;
            this.Owner = owner;
            this.releasesAddress = CarnassialConfigurationSettings.GetReleasesBrowserAddress();
            this.Version.Text = typeof(AboutEditor).Assembly.GetName().Version.ToString();

            this.CheckForNewerRelease.IsEnabled = this.latestReleaseAddress != null;
            this.ViewReleases.IsEnabled = this.releasesAddress != null;

            // configure hyperlinks
            // See remarks in Carnassial's About..ctor().
            Span termsOfUse = App.FindResource<Span>(EditorConstant.ResourceKey.AboutEditorTermsOfUse);
            if (termsOfUse.Tag == null)
            {
                Hyperlink emailLink = (Hyperlink)LogicalTreeHelper.FindLogicalNode(termsOfUse, Constant.DialogControlName.AboutEmailLink);
                emailLink.NavigateUri = CarnassialConfigurationSettings.GetDevTeamEmailLink();
                emailLink.Inlines.Clear();
                emailLink.Inlines.Add(emailLink.NavigateUri.ToEmailAddress());
                emailLink.RequestNavigate += this.Hyperlink_RequestNavigate;
                emailLink.ToolTip = emailLink.NavigateUri.ToEmailAddress();

                Hyperlink issuesLink = (Hyperlink)LogicalTreeHelper.FindLogicalNode(termsOfUse, Constant.DialogControlName.AboutIssuesLink);
                issuesLink.NavigateUri = CarnassialConfigurationSettings.GetIssuesBrowserAddress();
                issuesLink.Inlines.Clear();
                issuesLink.Inlines.Add(issuesLink.NavigateUri.AbsoluteUri);
                issuesLink.RequestNavigate += this.Hyperlink_RequestNavigate;
                issuesLink.ToolTip = issuesLink.NavigateUri.AbsoluteUri;

                termsOfUse.Tag = true;
            }
        }

        private void CheckForUpdate_Click(object sender, RoutedEventArgs e)
        {
            GithubReleaseClient updater = new(Constant.ApplicationName, this.latestReleaseAddress);
            if (updater.TryGetAndParseRelease(true, out Version _))
            {
                this.MostRecentCheckForUpdate = DateTime.UtcNow;
            }
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            if (e.Uri != null)
            {
                Process.Start(e.Uri.AbsoluteUri);
                e.Handled = true;
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }

        private void VersionChanges_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(this.releasesAddress.AbsoluteUri);
            e.Handled = true;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            CommonUserInterface.SetDefaultDialogPosition(this);
            CommonUserInterface.TryFitWindowInWorkingArea(this);
        }
    }
}
