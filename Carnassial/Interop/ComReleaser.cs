using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Carnassial.Interop
{
    // adapted from Stephen Toub's .NET Matters: IFileOperation in Windows Vista column, MSDN Magazine December 2007
    [SupportedOSPlatform(Constant.Platform.Windows)]
    internal class ComReleaser<T> : IDisposable where T : class
    {
        private bool disposed;

        public T Item { get; private set; }

        public ComReleaser(T comObject)
        {
            if (comObject == null)
            {
                throw new ArgumentNullException(nameof(comObject));
            }
            if (!comObject.GetType().IsCOMObject)
            {
                throw new ArgumentOutOfRangeException(nameof(comObject));
            }

            this.disposed = false;
            this.Item = comObject;
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
                Marshal.FinalReleaseComObject(this.Item);
            }

            this.disposed = true;
        }
    }
}
