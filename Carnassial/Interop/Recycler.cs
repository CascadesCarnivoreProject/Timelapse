using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Carnassial.Interop
{
    // reduced from Stephen Toub's .NET Matters: IFileOperation in Windows Vista column, MSDN Magazine December 2007
    [SupportedOSPlatform(Constant.Platform.Windows)]
    internal class Recycler : IDisposable
    {
        private static readonly Type ShellFileOperationType = Type.GetTypeFromCLSID(Constant.ComGuid.IFileOperationClsid) ?? throw new NotSupportedException(String.Format(CultureInfo.CurrentCulture, "Unable to obtain type for COM CLS ID {0}.", Constant.ComGuid.IFileOperationClsid));

        private bool disposed;
        private readonly IFileOperation? shellFileOperation;

        public Recycler()
        {
            this.disposed = false;

            // move to recycle bin using IFileOperation on Windows 8 RTM and newer as FOFX_RECYCLEONDELETE is available
            if (Environment.OSVersion.Version >= Constant.Windows8MinimumVersion)
            {
                this.shellFileOperation = (IFileOperation?)Activator.CreateInstance(Recycler.ShellFileOperationType);
                if (this.shellFileOperation == null)
                {
                    throw new NotSupportedException(String.Format(CultureInfo.CurrentCulture, "Unable to instantiate shell file operation."));
                }
                this.shellFileOperation.SetOperationFlags(FileOperationFlags.FOFX_RECYCLEONDELETE | FileOperationFlags.FOF_SILENT | FileOperationFlags.FOF_NOERRORUI);
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

            if (disposing)
            {
                if (this.shellFileOperation != null)
                {
                    Marshal.FinalReleaseComObject(this.shellFileOperation);
                }
            }

            this.disposed = true;
        }

        [SupportedOSPlatform(Constant.Platform.Windows)]
        public void MoveToRecycleBin(string filePath)
        {
            this.ThrowIfDisposed();

            // legacy Windows 7 support
            if (this.shellFileOperation == null)
            {
                NativeMethods.MoveToRecycleBin(filePath);
                return;
            }

            using (ComReleaser<IShellItem> file = NativeMethods.CreateShellItem(filePath))
            {
                this.shellFileOperation.DeleteItem(file.Item, null);
            }
            this.shellFileOperation.PerformOperations();
        }

        [SupportedOSPlatform(Constant.Platform.Windows)]
        public void MoveToRecycleBin(IList<FileInfo> files)
        {
            this.ThrowIfDisposed();
            if (files.Count < 1)
            {
                return;
            }

            // legacy Windows 7 support
            if (this.shellFileOperation == null)
            {
                foreach (FileInfo file in files)
                {
                    NativeMethods.MoveToRecycleBin(file.FullName);
                }
                return;
            }

            foreach (FileInfo file in files)
            {
                if (file == null)
                {
                    continue;
                }

                using ComReleaser<IShellItem> shellFile = NativeMethods.CreateShellItem(file.FullName);
                this.shellFileOperation.DeleteItem(shellFile.Item, null);
            }
            this.shellFileOperation.PerformOperations();
        }

        private void ThrowIfDisposed()
        {
            if (this.disposed)
            {
                throw new ObjectDisposedException(nameof(Recycler));
            }
        }
    }
}
