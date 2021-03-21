using Carnassial.Data;
using System;
using System.Globalization;
using System.Threading;

namespace Carnassial.Images
{
    internal class FileLoadStatus : FileIOComputeTransactionStatus, IDisposable
    {
        private bool disposed;
        private CachedImage? image;

        public ImageRow? CurrentFile { get; set; }
        public int ImageRenderWidth { get; private set; }
        public UInt64 MostRecentImageUpdate { get; set; }
        public int TotalFiles { get; set; }

        public FileLoadStatus()
        {
            this.CurrentFile = null;
            this.disposed = false;
            this.image = null;
            this.MaybeUpdateImageRenderWidth(0);
            this.MostRecentImageUpdate = UInt64.MinValue;
            this.TotalFiles = 0;
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

            if (disposing && (this.image != null))
            {
                this.image.Dispose();
            }
            this.disposed = true;
        }

        public string GetMessage()
        {
            if (this.CurrentFileIndex == 0)
            {
                return String.Format(CultureInfo.CurrentCulture, "File {0} of {1}...", this.CurrentFileIndex, this.TotalFiles);
            }
            return String.Format(CultureInfo.CurrentCulture, "Loading file {0} of {1} ({2})...", this.CurrentFileIndex, this.TotalFiles, this.CurrentFile.FileName);
        }

        public double GetPercentage()
        {
            return 100.0 * this.CurrentFileIndex / (double)this.TotalFiles;
        }

        public void MaybeUpdateImageRenderWidth(int possibleNewWidth)
        {
            this.ImageRenderWidth = Math.Max(Constant.Images.MinimumRenderWidth, possibleNewWidth);
        }

        public void SetImage(CachedImage imageToDisplay)
        {
            // a race condition potentially exists between calls to this function and calls to TryDetachImage()
            // To avoid, both functions use Interlocked.Exchange() and, in this function, the released image is diposed after
            // this.image is set to the new image so that callers can't obtain an image which in the process of or is scheduled
            // for disposal.
            CachedImage oldImage = Interlocked.Exchange(ref this.image, imageToDisplay);
            if (oldImage != null)
            {
                oldImage.Dispose();
            }
        }

        public bool TryDetachImage(out CachedImage? image)
        {
            if (this.image == null)
            {
                image = null;
                return false;
            }

            // see remarks in SetImage()
            image = Interlocked.Exchange(ref this.image, null);
            return image != null;
        }
    }
}
