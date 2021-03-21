using Carnassial.Data;
using Carnassial.Images;
using System;
using System.Threading;

namespace Carnassial.Dialog
{
    public class ReclassifyStatus : FileIOComputeTransactionStatus, IDisposable
    {
        private bool disposed;
        private CachedImage? image;

        public ImageRow? File { get; set; }
        public ImageProperties? ImageProperties { get; set; }
        public UInt64 MostRecentImageUpdate { get; set; }

        public ReclassifyStatus()
        {
            this.File = null;
            this.image = null;
            this.ImageProperties = null;
            this.MostRecentImageUpdate = 0;
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

        public void SetImage(CachedImage imageToDisplay)
        {
            // see remarks for FileLoadStatus.SetImage()
            CachedImage oldImage = Interlocked.Exchange(ref this.image, imageToDisplay);
            if (oldImage != null)
            {
                oldImage.Dispose();
            }
        }

        public bool TryDetachImage(out CachedImage image)
        {
            if (this.image == null)
            {
                image = null;
                return false;
            }

            // see remarks in FileLoadStatus.TryDetachImage()
            image = Interlocked.Exchange(ref this.image, null);
            return image != null;
        }
    }
}
