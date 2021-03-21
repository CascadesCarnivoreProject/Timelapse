using Carnassial.Interop;
using System;

namespace Carnassial.Util
{
    public class ExceptionPropagatingProgress<TProgress>
    {
        private ulong mostRecentStatusUpdate;
        private readonly Action<TProgress> onProgressUpdateCore;
        private readonly IProgress<TProgress> progress;
        private readonly ulong progressUpdateIntervalInMilliseconds;

        public Exception? Exception { get; private set; }
        public bool UpdatePending { get; private set; }

        public ExceptionPropagatingProgress(Action<TProgress> handler, TimeSpan progressUpdateInterval)
        {
            this.onProgressUpdateCore = handler ?? throw new ArgumentNullException(nameof(handler));
            this.progress = new Progress<TProgress>(this.OnProgressUpdate);
            this.progressUpdateIntervalInMilliseconds = (UInt64)progressUpdateInterval.TotalMilliseconds;

            this.Exception = null;
            this.UpdatePending = false;

            this.mostRecentStatusUpdate = 0;
        }

        public void End()
        {
            if (this.Exception != null)
            {
                throw new AggregateException(this.Exception);
            }
        }

        private void OnProgressUpdate(TProgress value)
        {
            try
            {
                this.onProgressUpdateCore(value);
                this.mostRecentStatusUpdate = NativeMethods.GetTickCount64();
                this.UpdatePending = false;
            }
            catch (Exception exception)
            {
                lock (this.onProgressUpdateCore)
                {
                    if (this.Exception == null)
                    {
                        this.Exception = exception;
                    }
                    else
                    {
                        this.Exception = new AggregateException(this.Exception, exception);
                    }
                }
            }
        }

        public void QueueProgressUpdate(TProgress value)
        {
            if (this.Exception != null)
            {
                throw new AggregateException(this.Exception);
            }
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            this.UpdatePending = true;
            this.progress.Report(value);
        }

        public bool ShouldUpdateProgress()
        {
            if (this.Exception != null)
            {
                throw new AggregateException(this.Exception);
            }

            if (this.UpdatePending)
            {
                return false;
            }

            UInt64 timeSinceLastUpdate = NativeMethods.GetTickCount64() - this.mostRecentStatusUpdate;
            return timeSinceLastUpdate > this.progressUpdateIntervalInMilliseconds;
        }
    }
}
