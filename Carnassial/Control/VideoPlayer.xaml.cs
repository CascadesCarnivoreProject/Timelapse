using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace Carnassial.Control
{
    public partial class VideoPlayer : UserControl
    {
        private readonly DispatcherTimer positionUpdateTimer;

        private bool isProgrammaticUpdate;

        public VideoPlayer()
        {
            this.InitializeComponent();
            this.isProgrammaticUpdate = false;
            this.positionUpdateTimer = new DispatcherTimer()
            {
                Interval = TimeSpan.FromMilliseconds(250.0)
            };
            this.positionUpdateTimer.Tick += this.Timer_Tick;
        }

        public void Pause()
        {
            this.positionUpdateTimer.Stop();
            this.Video.Pause();

            this.PlayOrPause.IsChecked = false;
            this.ShowPosition();
        }

        private void Play()
        {
            this.PlayOrPause.IsChecked = true;

            // start over from beginning if at end of video
            if (this.Video.NaturalDuration.HasTimeSpan && this.Video.Position == this.Video.NaturalDuration.TimeSpan)
            {
                this.Video.Position = TimeSpan.Zero;
                this.ShowPosition();
            }

            this.positionUpdateTimer.Start();
            this.Video.Play();
        }

        private void PlayOrPause_Click(object sender, RoutedEventArgs e)
        {
            if (this.PlayOrPause.IsChecked == true)
            {
                this.Play();
            }
            else
            {
                this.Pause();
            }
        }

        public void SetSource(Uri source)
        {
            this.Video.Source = source;

            // MediaElement seems only deterministic about displaying the first frame when LoadedBehaviour is set to Pause, which isn't helpful as calls to
            // Play() then have no effect.  This is a well known issue with various folks getting results.  The below combination of Play(), Pause() and Position
            // seems to work, though neither Pause() or Position is sufficent on its own and black frames still get rendered if Position is set to zero or
            // an especially small value.
            double originalVolume = this.Video.Volume;
            this.Video.Volume = 0.0;
            this.Video.Play();
            this.Video.Pause();
            this.Video.Position = TimeSpan.FromMilliseconds(1.0);
            this.Video.Volume = originalVolume;

            // position updated through the media opened event
        }

        private void ShowPosition()
        {
            this.isProgrammaticUpdate = true;
            if (this.Video.NaturalDuration.HasTimeSpan)
            {
                this.VideoPosition.Maximum = this.Video.NaturalDuration.TimeSpan.TotalSeconds;
                this.TimeFromEnd.Text = (this.Video.NaturalDuration.TimeSpan - this.Video.Position).ToString(Constant.Time.VideoPositionFormat, CultureInfo.CurrentCulture);
                this.VideoPosition.TickFrequency = this.VideoPosition.Maximum / 10.0;
            }
            this.TimeFromStart.Text = this.Video.Position.ToString(Constant.Time.VideoPositionFormat, CultureInfo.CurrentCulture);
            this.VideoPosition.Value = this.Video.Position.TotalSeconds;
            this.isProgrammaticUpdate = false;
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            if (this.Video.Source != null)
            {
                this.ShowPosition();
            }
        }

        public bool TryPlayOrPause()
        {
            if (this.Visibility != Visibility.Visible)
            {
                return false;
            }

            // WPF doesn't offer a way to fire a toggle button's click event programatically (ToggleButtonAutomationPeer -> IToggleProvider -> Toggle()
            // changes the state of the button but fails to trigger the click event) so do the equivalent in code
            this.PlayOrPause.IsChecked = !this.PlayOrPause.IsChecked;
            this.PlayOrPause_Click(this, null);
            return true;
        }

        private void Video_MediaEnded(object sender, RoutedEventArgs e)
        {
            this.Pause();
        }

        private void Video_MediaOpened(object sender, RoutedEventArgs e)
        {
            this.ShowPosition();
        }

        private void Video_Unloaded(object sender, RoutedEventArgs e)
        {
            this.positionUpdateTimer.Stop();
        }

        // pause video when user starts moving the slider so the two actions don't interfere with eachother
        private void VideoPosition_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            this.Pause();
        }

        // Scrub the video to the current slider position
        private void VideoPosition_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (this.isProgrammaticUpdate)
            {
                return;
            }

            TimeSpan videoPosition = TimeSpan.FromSeconds(this.VideoPosition.Value);
            this.Video.Position = videoPosition;
            this.ShowPosition();
        }
    }
}