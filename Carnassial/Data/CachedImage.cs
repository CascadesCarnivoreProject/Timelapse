using Carnassial.Native;
using System;

namespace Carnassial.Data
{
    // a shim for distinguishing between image files which could not be found and those which could not be loaded
    // This class borders on unnecessary.  In general, a loadable image produces a MemoryImage (possibly with DecodeError = true,
    // implying corruption) and an image which cannot be loaded can be indicated by a null pointer.  However, a narrow range of cases
    // occurs where an image file exists on disk but is so corrupted or truncated no decoding is possible.  MemoryImage { DecodeError
    // = true } also applies to these cases and the special casing needed to produce an empty MemoryImage with the flag set is arguably
    // less complex than interposing the CachedImage class.  However, a C# surface for this purpose is somewhat lower cost to maintain
    // than a C++/CLI one.
    public class CachedImage : IDisposable
    {
        private bool disposed;

        public bool FileNoLongerAvailable { get; set; }
        public MemoryImage? Image { get; private set; }
        public bool ImageNotDecodable { get; set; }

        public CachedImage()
        {
            this.disposed = false;
            this.FileNoLongerAvailable = false;
            this.Image = null;
            this.ImageNotDecodable = false;
        }

        public CachedImage(MemoryImage image)
            : this()
        {
            this.Image = image;
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

            if (disposing && (this.Image != null))
            {
                this.Image.Dispose();
            }
            this.disposed = true;
        }
    }
}
