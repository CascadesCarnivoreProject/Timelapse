using Carnassial.Util;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Threading;
using MessageBox = Carnassial.Dialog.MessageBox;

namespace Carnassial
{
    public class ApplicationWindow : WindowWithSystemMenu
    {
        protected void Instructions_PreviewDrag(object sender, DragEventArgs dragEvent)
        {
            if (ApplicationWindow.IsSingleTemplateFileDrag(dragEvent, out string _))
            {
                dragEvent.Effects = DragDropEffects.All;
            }
            else
            {
                dragEvent.Effects = DragDropEffects.None;
            }
            dragEvent.Handled = true;
        }

        protected static bool IsSingleTemplateFileDrag(DragEventArgs dragEvent, [NotNullWhen(true)] out string? templateDatabasePath)
        {
            if (dragEvent.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] droppedFiles = (string[])dragEvent.Data.GetData(DataFormats.FileDrop);
                if (droppedFiles != null && droppedFiles.Length == 1)
                {
                    templateDatabasePath = droppedFiles[0];
                    if (templateDatabasePath.EndsWith(Constant.File.TemplateFileExtension, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            templateDatabasePath = null;
            return false;
        }

        [SupportedOSPlatform(Constant.Platform.Windows)]
        protected void ShowExceptionReportingDialog(string? alternativeTitle, string? databasePath, DispatcherUnhandledExceptionEventArgs e)
        {
            MessageBox exitNotification = MessageBox.FromResource(Constant.ResourceKey.ApplicationWindowException, this,
                                                                  CarnassialConfigurationSettings.GetIssuesBrowserAddress(),
                                                                  CarnassialConfigurationSettings.GetDevTeamEmailLink().ToEmailAddress(),
                                                                  typeof(CarnassialWindow).Assembly.GetName(),
                                                                  Environment.OSVersion,
                                                                  RuntimeInformation.FrameworkDescription,
                                                                  databasePath,
                                                                  e.Exception);
            if (alternativeTitle != null)
            {
                exitNotification.Title = alternativeTitle;
            }
            Clipboard.SetText(exitNotification.Message.GetWhat());
            exitNotification.ShowDialog();
        }
    }
}
