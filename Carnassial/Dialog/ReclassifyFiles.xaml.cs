using Carnassial.Data;
using Carnassial.Images;
using Carnassial.Native;
using Carnassial.Util;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace Carnassial.Dialog
{
    public partial class ReclassifyFiles : WindowWithSystemMenu, IDisposable
    {
        private bool disposed;
        private readonly FileDatabase fileDatabase;
        private readonly FileTableEnumerator fileEnumerator;
        private bool isProgramaticNavigatiorSliderUpdate;
        private ImageRow? previousFile;
        private ReclassifyIOComputeTransaction? reclassification;
        private readonly CarnassialUserRegistrySettings userSettings;

        public ReclassifyFiles(FileDatabase database, ImageCache imageCache, CarnassialUserRegistrySettings state, Window owner)
        {
            this.InitializeComponent();
            this.Message.SetVisibility();
            this.Owner = owner;

            this.fileDatabase = database;
            this.disposed = false;
            this.fileEnumerator = new FileTableEnumerator(database, imageCache.CurrentRow);
            this.isProgramaticNavigatiorSliderUpdate = false;
            this.previousFile = null;
            this.reclassification = null;
            this.userSettings = state;
        }

        /// <summary>
        /// Display classification of current file with the current threshold settings. Does not update the database.
        /// </summary>
        private void ClassifyCurrentFile()
        {
            ImageProperties? imageProperties = null;

            FileClassification newClassification;
            FileInfo fileInfo = this.fileEnumerator.Current.GetFileInfo(this.fileDatabase.FolderPath);
            if (fileInfo.Exists)
            {
                if (this.fileEnumerator.Current.IsVideo)
                {
                    newClassification = FileClassification.Video;
                }
                else
                {
                    using JpegImage jpeg = new(fileInfo.FullName);
                    if (jpeg.TryGetMetadata())
                    {
                        MemoryImage? preallocatedImage = null;
                        imageProperties = jpeg.GetThumbnailProperties(ref preallocatedImage);
                        if (imageProperties.MetadataResult.HasFlag(MetadataReadResults.Thumbnail) == false)
                        {
                            imageProperties = jpeg.GetProperties(Constant.Images.NoThumbnailClassificationRequestedWidthInPixels, ref preallocatedImage);
                        }
                        newClassification = imageProperties.EvaluateNewClassification(0.01 * this.DarkLuminosityThresholdPercent.Value);
                    }
                    else
                    {
                        newClassification = FileClassification.Corrupt;
                    }
                }
            }
            else
            {
                newClassification = FileClassification.NoLongerAvailable;
            }

            this.DisplayClassification(this.fileEnumerator.Current, imageProperties, newClassification);
        }

        private void DisplayClassification(ImageRow file, ImageProperties? imageProperties, FileClassification newClassificationToDisplay)
        {
            this.OriginalClassification.Content = file.Classification;
            this.NewClassification.Content = newClassificationToDisplay;

            if (newClassificationToDisplay == FileClassification.Video)
            {
                this.ClassificationInformation.Text = App.FindResource<string>(Constant.ResourceKey.ReclassifyFilesVideo);
            }
            else if ((newClassificationToDisplay == FileClassification.Corrupt) ||
                (newClassificationToDisplay == FileClassification.NoLongerAvailable) ||
                (imageProperties == null))
            {
                this.ClassificationInformation.Text = App.FindResource<string>(Constant.ResourceKey.ReclassifyFilesUnloadable);
            }
            else
            {
                this.ClassificationInformation.Text = imageProperties.GetClassificationDescription();
            }
        }

        private async Task DisplayFileAndClassificationAsync()
        {
            if (Object.ReferenceEquals(this.fileEnumerator.Current, this.previousFile))
            {
                // no change in the file displayed, so nothing to do
                return;
            }

            await this.FileDisplay.DisplayAsync(this.fileDatabase.FolderPath, this.fileEnumerator.Current).ConfigureAwait(true);
            this.FileName.Content = this.fileEnumerator.Current.FileName;
            this.FileName.ToolTip = this.FileName.Content;

            this.ClassifyCurrentFile();
            this.previousFile = this.fileEnumerator.Current;
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (this.disposed)
            {
                return;
            }

            if (disposing)
            {
                if (this.fileEnumerator != null)
                {
                    this.fileEnumerator.Dispose();
                }
                if (this.reclassification != null)
                {
                    this.reclassification.Dispose();
                }
            }

            this.disposed = true;
        }

        private async void ApplyDoneButton_Click(object sender, RoutedEventArgs e)
        {
            // second click - done
            if (this.reclassification != null)
            {
                this.DialogResult = true;
                return;
            }

            // first click - apply new threshold to selected files
            this.ApplyDoneButton.Content = "_Done";
            this.ApplyDoneButton.IsEnabled = false;
            this.CancelStopButton.Content = "_Stop";
            this.DarkLuminosityThresholdPercent.IsEnabled = false;
            this.FileNavigatorSlider.IsEnabled = false;
            this.MenuReset.IsEnabled = false;
            this.userSettings.DarkLuminosityThreshold = 0.01 * this.DarkLuminosityThresholdPercent.Value;

            using (this.reclassification = new ReclassifyIOComputeTransaction(this.UpdateClassificationStatus, this.userSettings.Throttles.GetDesiredProgressUpdateInterval()))
            {
                await this.reclassification.ReclassifyFilesAsync(this.fileDatabase, 0.01 * this.userSettings.DarkLuminosityThreshold, (int)this.ActualWidth).ConfigureAwait(true);
            }

            await this.DisplayFileAndClassificationAsync().ConfigureAwait(true);
            this.ApplyDoneButton.IsEnabled = true;
            this.CancelStopButton.IsEnabled = false;
        }

        // Cancel or Stop - exit the dialog
        private void CancelStopButton_Click(object sender, RoutedEventArgs e)
        {
            if (this.reclassification != null)
            {
                this.reclassification.ShouldExitCurrentIteration = true;
            }
            this.DialogResult = false;
        }

        private async void FileNavigatorSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (this.isProgramaticNavigatiorSliderUpdate)
            {
                return;
            }

            this.fileEnumerator.TryMoveToFile((int)this.FileNavigatorSlider.Value);
            await this.DisplayFileAndClassificationAsync().ConfigureAwait(true);
        }

        private void MenuResetCurrent_Click(object sender, RoutedEventArgs e)
        {
            this.ResetDarkLuminosityThreshold(this.userSettings.DarkLuminosityThreshold);
        }

        private void MenuResetDefault_Click(object sender, RoutedEventArgs e)
        {
            this.ResetDarkLuminosityThreshold(Constant.Images.DarkLuminosityThresholdDefault);
        }

        private void ResetDarkLuminosityThreshold(double darkLuminosityThreshold)
        {
            this.DarkLuminosityThresholdPercent.Value = 100.0 * darkLuminosityThreshold;
            this.ClassifyCurrentFile();
        }

        private async Task ShowFileWithoutSliderCallbackAsync(bool forward, ModifierKeys modifiers)
        {
            // determine how far to move and in which direction
            int increment = CommonUserInterface.GetIncrement(forward, modifiers);
            int newFileIndex = this.fileEnumerator.CurrentRow + increment;

            await this.ShowFileWithoutSliderCallbackAsync(newFileIndex).ConfigureAwait(true);
        }

        private async Task ShowFileWithoutSliderCallbackAsync(int newFileIndex)
        {
            // if no change the file is already being displayed
            // For example, the end of the image set has been reached but key repeat means right arrow events are still coming in as the user hasn't
            // reacted yet.
            if (newFileIndex == this.fileEnumerator.CurrentRow)
            {
                return;
            }
            if (newFileIndex >= this.fileDatabase.CurrentlySelectedFileCount)
            {
                newFileIndex = this.fileDatabase.CurrentlySelectedFileCount - 1;
            }
            else if (newFileIndex < 0)
            {
                newFileIndex = 0;
            }

            this.fileEnumerator.TryMoveToFile(newFileIndex);
            if (this.FileNavigatorSlider.Value != this.fileEnumerator.CurrentRow)
            {
                this.isProgramaticNavigatiorSliderUpdate = true;
                this.FileNavigatorSlider.Value = this.fileEnumerator.CurrentRow;
                this.isProgramaticNavigatiorSliderUpdate = false;
            }
            await this.DisplayFileAndClassificationAsync().ConfigureAwait(true);
        }

        private void UpdateClassificationStatus(ReclassifyStatus status)
        {
            if (status.File != null)
            {
                this.FileName.Content = status.File.FileName;
                this.FileName.ToolTip = this.FileName.Content;
                this.DisplayClassification(status.File, status.ImageProperties, status.File.Classification);
            }
            if (status.TryDetachImage(out CachedImage image))
            {
                this.FileDisplay.Display(image);
            }

            this.isProgramaticNavigatiorSliderUpdate = true;
            this.FileNavigatorSlider.Value = status.CurrentFileIndex - 1;
            this.isProgramaticNavigatiorSliderUpdate = false;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // make sure the title bar of the dialog box is on the screen. For small screens it may default to being off the screen
            CommonUserInterface.SetDefaultDialogPosition(this);
            CommonUserInterface.TryFitWindowInWorkingArea(this);

            this.DarkLuminosityThresholdPercent.Value = 100.0 * this.userSettings.DarkLuminosityThreshold;

            this.FileNavigatorSlider.Minimum = 0;
            this.FileNavigatorSlider.Maximum = this.fileDatabase.CurrentlySelectedFileCount - 1;
            CommonUserInterface.ConfigureNavigatorSliderTick(this.FileNavigatorSlider);
            this.FileNavigatorSlider.Value = this.fileEnumerator.CurrentRow;
            this.FileNavigatorSlider_ValueChanged(this, null);
            this.FileNavigatorSlider.ValueChanged += this.FileNavigatorSlider_ValueChanged;

            this.Focus(); // necessary for the left/right arrow keys to work
        }

        private async void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.End:
                    e.Handled = true;
                    await this.ShowFileWithoutSliderCallbackAsync(this.fileDatabase.CurrentlySelectedFileCount - 1).ConfigureAwait(true);
                    break;
                case Key.Home:
                    e.Handled = true;
                    await this.ShowFileWithoutSliderCallbackAsync(0).ConfigureAwait(true);
                    break;
                case Key.Left:  // previous file
                    e.Handled = true;
                    await this.ShowFileWithoutSliderCallbackAsync(false, Keyboard.Modifiers).ConfigureAwait(true);
                    break;
                case Key.Right: // next file
                    e.Handled = true;
                    await this.ShowFileWithoutSliderCallbackAsync(true, Keyboard.Modifiers).ConfigureAwait(true);
                    break;
                default:
                    return;
            }
        }
    }
}
