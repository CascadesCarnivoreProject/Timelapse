using Carnassial.Data;
using System;
using System.Windows;
using System.Windows.Threading;

namespace Carnassial.Util
{
    public class Throttles
    {
        public double DesiredImageRendersPerSecond { get; private set; }
        public TimeSpan DesiredIntervalBetweenRenders { get; private set; }
        public DispatcherTimer FilePlayTimer { get; private set; }
        public double ImageClassificationChangeSlowdown { get; set; }
        public int RepeatedKeyAcceptanceInterval { get; private set; }
        public double VideoSlowdown { get; set; }

        public Throttles()
        {
            this.FilePlayTimer = new DispatcherTimer();
            this.SetDesiredImageRendersPerSecond(Constant.ThrottleValues.DesiredMaximumImageRendersPerSecondDefault);
            this.ImageClassificationChangeSlowdown = Constant.ThrottleValues.ImageClassificationSlowdownDefault;
            this.VideoSlowdown = Constant.ThrottleValues.VideoSlowdownDefault;
        }

        public TimeSpan GetDesiredProgressUpdateInterval()
        {
            if (this.DesiredIntervalBetweenRenders > Constant.ThrottleValues.DesiredIntervalBetweenStatusUpdates)
            {
                return this.DesiredIntervalBetweenRenders;
            }
            return Constant.ThrottleValues.DesiredIntervalBetweenStatusUpdates;
        }

        public void SetDesiredImageRendersPerSecond(double rendersPerSecond)
        {
            if (rendersPerSecond < Constant.ThrottleValues.DesiredMaximumImageRendersPerSecondLowerBound ||
                rendersPerSecond > Constant.ThrottleValues.DesiredMaximumImageRendersPerSecondUpperBound)
            {
                throw new ArgumentOutOfRangeException(nameof(rendersPerSecond));
            }

            this.DesiredImageRendersPerSecond = rendersPerSecond;
            this.DesiredIntervalBetweenRenders = TimeSpan.FromSeconds(1.0 / rendersPerSecond);
            this.RepeatedKeyAcceptanceInterval = (int)(((double)SystemParameters.KeyboardSpeed + 0.5 * rendersPerSecond) / rendersPerSecond);
            this.FilePlayTimer.Interval = this.DesiredIntervalBetweenRenders;
        }

        public void SetFilePlayInterval(ImageRow? previousFile, ImageRow currentFile)
        {
            if (currentFile.Classification == FileClassification.Video)
            {
                this.FilePlayTimer.Interval = TimeSpan.FromTicks((long)(this.VideoSlowdown * this.DesiredIntervalBetweenRenders.Ticks));
            }
            else if ((previousFile != null) && (previousFile.Classification != currentFile.Classification))
            {
                this.FilePlayTimer.Interval = TimeSpan.FromTicks((long)(this.ImageClassificationChangeSlowdown * this.DesiredIntervalBetweenRenders.Ticks));
            }
            else
            {
                this.FilePlayTimer.Interval = this.DesiredIntervalBetweenRenders;
            }
        }

        public void StartFilePlayTimer(ImageRow? previousFile, ImageRow currentFile)
        {
            this.SetFilePlayInterval(previousFile, currentFile);
            this.FilePlayTimer.Start();
        }

        public void StopFilePlayTimer()
        {
            this.FilePlayTimer.Stop();
            this.FilePlayTimer.Interval = this.DesiredIntervalBetweenRenders;
        }
    }
}
