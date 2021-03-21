using Carnassial.Native;
using Microsoft.Win32.SafeHandles;
using System;
using System.Threading;

namespace Carnassial.Interop
{
    internal class HyperthreadSiblingAffinity : IDisposable
    {
        private bool disposed;
        private readonly bool isPinning;
        private readonly UInt64 originalAffinity;
        private readonly SafeAccessTokenHandle? unmanagedThread;

        public HyperthreadSiblingAffinity(int taskID)
        {
            this.disposed = false;
            // pinning of IO and compute task pairs to hyperthread siblings maximizes concurrent hardware use
            // However, it's not needed to pin on processors with hyperthreading disabled and pinning is not beneficial if it
            // prevents use of all physical cores.  Excluding virtual machines with odd numbers of cores, this produces the 
            // following hyperthreaded arrangements in combination with FileIOComputeTransactionManager:
            // total  physical  logical     core
            // tasks  cores     processors  pinning
            // 2      1         2           n/a
            // 4      2         4           {io + compute} + {io + compute}
            // 6      4         8           {io + compute} + {io + compute} + 2 unpinned compute
            this.isPinning = Processor.IsHyperthreaded && (taskID < Processor.PhysicalCores);
            if (this.isPinning)
            {
                if ((taskID < 0) || (taskID > 31))
                {
                    throw new ArgumentOutOfRangeException(nameof(taskID), App.FindResource<string>(Constant.ResourceKey.HyperthreadSiblingAffinityTaskID));
                }

                Thread.BeginThreadAffinity();
                this.unmanagedThread = NativeMethods.GetCurrentThread();

                UInt64 affinityMask = (UInt64)1 << (2 * taskID); // first hyperthread sibling
                affinityMask |= affinityMask << 1;        // second hyperthread sibling
                this.originalAffinity = NativeMethods.SetThreadAffinityMask(this.unmanagedThread, affinityMask);
            }
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

            if (disposing && this.isPinning)
            {
                if (this.unmanagedThread != null)
                {
                    NativeMethods.SetThreadAffinityMask(this.unmanagedThread, this.originalAffinity);
                    Thread.EndThreadAffinity();
                    this.unmanagedThread.Dispose();
                }
            }
            this.disposed = true;
        }
    }
}
