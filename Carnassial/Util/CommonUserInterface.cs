using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;

namespace Carnassial.Util
{
    public static class CommonUserInterface
    {
        public static void ConfigureNavigatorSliderTick(Slider slider)
        {
            if (slider.Maximum <= 50)
            {
                slider.IsSnapToTickEnabled = true;
                slider.TickFrequency = 1.0;
            }
            else
            {
                slider.IsSnapToTickEnabled = false;
                slider.TickFrequency = 0.02 * slider.Maximum;
            }
        }

        public static int GetIncrement(bool forward, ModifierKeys modifiers)
        {
            int increment = forward ? 1 : -1;
            if (modifiers.HasFlag(ModifierKeys.Shift))
            {
                increment *= 5;
            }
            if (modifiers.HasFlag(ModifierKeys.Control))
            {
                increment *= 10;
            }
            return increment;
        }

        public static void SetDefaultDialogPosition(Window window)
        {
            Debug.Assert(window.Owner != null, "Window's owner property is null.  Is a set of it prior to calling ShowDialog() missing?");
            window.Left = window.Owner.Left + (window.Owner.Width - window.ActualWidth) / 2;
            window.Top = window.Owner.Top + 20;
        }

        public static bool TryFitWindowInWorkingArea(Window window)
        {
            if (Double.IsNaN(window.Left))
            {
                window.Left = 0;
            }
            if (Double.IsNaN(window.Top))
            {
                window.Top = 0;
            }

            Rectangle windowPosition = new((int)window.Left, (int)window.Top, (int)window.Width, (int)window.Height);
            Rectangle workingArea = Screen.GetWorkingArea(windowPosition);
            bool windowFitsInWorkingArea = true;

            // move window up if it extends below the working area
            if (windowPosition.Bottom > workingArea.Bottom)
            {
                int pixelsToMoveUp = windowPosition.Bottom - workingArea.Bottom;
                if (pixelsToMoveUp > windowPosition.Top)
                {
                    // window is too tall and has to shorten to fit screen
                    window.Top = 0;
                    window.Height = workingArea.Bottom;
                    windowFitsInWorkingArea = false;
                }
                else if (pixelsToMoveUp > 0)
                {
                    // move window up
                    window.Top -= pixelsToMoveUp;
                }
            }

            // move window left if it extends right of the working area
            if (windowPosition.Right > workingArea.Right)
            {
                int pixelsToMoveLeft = windowPosition.Right - workingArea.Right;
                if (pixelsToMoveLeft > windowPosition.Left)
                {
                    // window is too wide and has to narrow to fit screen
                    window.Left = 0;
                    window.Width = workingArea.Width;
                    windowFitsInWorkingArea = false;
                }
                else if (pixelsToMoveLeft > 0)
                {
                    // move window left
                    window.Left -= pixelsToMoveLeft;
                }
            }

            return windowFitsInWorkingArea;
        }

        public static bool TryGetFileFromUser(string title, string defaultFilePath, string filter, out string selectedFilePath)
        {
            using (OpenFileDialog openFileDialog = new())
            {
                openFileDialog.AutoUpgradeEnabled = true;
                openFileDialog.CheckFileExists = true;
                openFileDialog.CheckPathExists = true;
                openFileDialog.DefaultExt = Constant.File.TemplateFileExtension;
                openFileDialog.Filter = filter;
                openFileDialog.Multiselect = false;
                openFileDialog.Title = title;

                if (String.IsNullOrWhiteSpace(defaultFilePath))
                {
                    openFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                }
                else
                {
                    // it would be ideal to reapply the filter and assign a new default file name when the folder changes
                    // Unfortunately this is not supported by CommonOpenFileDialog, the WinForms OpenFileDialog, or the WPF OpenFileDialog.
                    openFileDialog.InitialDirectory = Path.GetDirectoryName(defaultFilePath);
                    openFileDialog.FileName = Path.GetFileName(defaultFilePath);
                }

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    selectedFilePath = openFileDialog.FileName;
                    return true;
                }
            }

            selectedFilePath = null;
            return false;
        }
    }
}
