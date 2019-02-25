﻿using Carnassial.Interop;
using System;
using System.Diagnostics;

namespace Carnassial.Data
{
    public abstract class DataImportExportStatus<TProgress> 
    {
        private readonly ulong progressUpdateIntervalInMilliseconds;

        protected double CurrentPosition { get; set; }
        protected double EndPosition { get; set; }
        protected bool IsRead { get; set; }
        protected bool IsTransactionCommit { get; set; }
        protected ulong MostRecentStatusUpdate { get; set; }
        protected IProgress<TProgress> Progress { get; private set; }

        protected DataImportExportStatus(Action<TProgress> onProgressUpdate, TimeSpan progressUpdateInterval)
        {
            this.progressUpdateIntervalInMilliseconds = (UInt64)progressUpdateInterval.TotalMilliseconds;

            this.CurrentPosition = 0.0;
            this.EndPosition = -1.0;
            this.IsRead = false;
            this.IsTransactionCommit = false;
            this.MostRecentStatusUpdate = 0;
            this.Progress = new Progress<TProgress>(onProgressUpdate);
        }

        public virtual void BeginRead(long entitiesToRead)
        {
            Debug.Assert(entitiesToRead >= 0, "Expected entities to read.");

            this.ClearFlags();
            this.EndPosition = (double)entitiesToRead;
            this.IsRead = true;

            this.Report(0);
        }

        public void BeginTransactionCommit(int totalFilesToInsertOrUpdate)
        {
            Debug.Assert(totalFilesToInsertOrUpdate >= 0, "Expected files to transact.");

            this.ClearFlags();
            this.EndPosition = (double)totalFilesToInsertOrUpdate;
            this.IsTransactionCommit = true;

            this.Report(0);
        }

        protected virtual void ClearFlags()
        {
            this.IsRead = false;
            this.IsTransactionCommit = false;
        }

        public abstract string GetMessage();

        public double GetPercentage()
        {
            Debug.Assert(this.EndPosition >= 0.0, "Set EndPosition before initiating a status report.");

            if (this.EndPosition <= Double.Epsilon)
            {
                // avoid divide by zero as ProgressBar.Value rejects values larger than 100%
                // Positioning is ambiguous in this case.  Since currentPosition should not be greater than endPosition and positions
                // are zero or positive, a zero end position implies a zero current position.  In such no op cases, the progress
                // value which produces the best user experience likely depends on previous and subsequent operations, information
                // which is not available in this context.  In general, it's least confusing to indicate an operation in progress so,
                // while any value from 0 to 100% is valid, report 50% as a best effort.
                return 50.0;
            }
            return 100.0 * this.CurrentPosition / this.EndPosition;
        }

        public void Report(long currentPosition)
        {
            this.CurrentPosition = (double)currentPosition;
            Debug.Assert(this.CurrentPosition <= this.EndPosition, "Current position past end position.");

            this.ReportProgress();
            this.MostRecentStatusUpdate = NativeMethods.GetTickCount64();
        }

        protected abstract void ReportProgress();

        public bool ShouldReport()
        {
            UInt64 tickNow = NativeMethods.GetTickCount64();
            return (tickNow - this.MostRecentStatusUpdate) > this.progressUpdateIntervalInMilliseconds;
        }
    }
}
