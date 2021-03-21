using MetadataExtractor.IO;
using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using ClrBuffer = System.Buffer;

namespace Carnassial.Interop
{
    public class UnbufferedSequentialReader : SequentialReader, IDisposable
    {
        private static readonly ConcurrentDictionary<string, int> SectorSizeByPathRoot;

        private int bufferPosition;
        private bool disposed;
        private readonly SafeFileHandle file;
        private int filePosition;
        private readonly Lazy<long> length;
        private readonly Lazy<string> pathRoot;

        public byte[]? Buffer { get; private set; }

        static UnbufferedSequentialReader()
        {
            UnbufferedSequentialReader.SectorSizeByPathRoot = new ConcurrentDictionary<string, int>();
        }

        public UnbufferedSequentialReader(string filePath)
            : base(true)
        {
            this.Buffer = null;
            this.bufferPosition = 0;
            this.disposed = false;
            this.file = NativeMethods.CreateFileUnbuffered(filePath, false);
            this.filePosition = 0;
            this.length = new Lazy<long>(() => { return NativeMethods.GetFileSizeEx(this.file); });
            this.pathRoot = new Lazy<string>(() =>
            {
                string? root = Path.GetPathRoot(filePath);
                if (root == null)
                {
                    throw new NotSupportedException(String.Format(CultureInfo.CurrentCulture, "Could not obtain root of path '{0}'.", filePath));
                }
                return root;
            });
        }

        public long Length
        {
            get { return this.length.Value; }
        }

        public override long Position
        {
            get { return this.bufferPosition; }
        }

        public override int Available()
        {
            throw new NotImplementedException();
        }

        public void ExtendBuffer(int bytesToRead)
        {
            byte[]? existingBuffer = this.Buffer;
            int existingBufferLength = existingBuffer == null ? 0 : existingBuffer.Length;
            this.Buffer = new byte[existingBufferLength + bytesToRead];
            if (existingBufferLength > 0)
            {
                ClrBuffer.BlockCopy(existingBuffer!, 0, this.Buffer, 0, existingBufferLength);
            }

            int bytesRead = this.Read(this.Buffer, existingBufferLength, bytesToRead);
            if (bytesRead != bytesToRead)
            {
                throw new IOException(String.Format(CultureInfo.CurrentCulture, "Only {0} instead of {1} bytes were read.", bytesRead, bytesToRead));
            }
            this.filePosition += bytesRead;
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
                this.file.Dispose();
            }

            this.disposed = true;
        }

        public override byte GetByte()
        {
            if (this.Buffer == null)
            {
                throw new NotSupportedException(App.FormatResource(Constant.ResourceKey.UnbufferedSequentialReaderExtendBuffer, nameof(this.ExtendBuffer), nameof(this.GetByte)));
            }
            if (this.bufferPosition >= this.Buffer.Length)
            {
                // caller only requested one more byte but unbuffered IO requires at least a sector be read
                // This case is therefore reasonably robust against repeated calls to GetByte().  If needed for performance, multiple
                // sectors could be read instead of one.
                this.ExtendBuffer(this.GetSectorSize());
            }
            return this.Buffer[this.bufferPosition++];
        }

        public override byte[] GetBytes(int count)
        {
            byte[] bytes = new byte[count];
            this.GetBytes(bytes, 0, count);
            return bytes;
        }

        public override void GetBytes(byte[] buffer, int offset, int count)
        {
            if (this.Buffer == null)
            {
                throw new NotSupportedException(App.FormatResource(Constant.ResourceKey.UnbufferedSequentialReaderExtendBuffer, nameof(this.ExtendBuffer), nameof(this.GetBytes)));
            }

            int endPosition = this.bufferPosition + count;
            if (endPosition > this.Buffer.Length)
            {
                int minimumBytesRequired = endPosition - this.Buffer.Length;
                int bytesToRead = minimumBytesRequired;
                int sectorSize = this.GetSectorSize();
                if (minimumBytesRequired % sectorSize != 0)
                {
                    bytesToRead = sectorSize * (minimumBytesRequired / sectorSize + 1);
                }
                this.ExtendBuffer(bytesToRead);
            }
            ClrBuffer.BlockCopy(this.Buffer, this.bufferPosition, buffer, offset, count);
            this.bufferPosition += count;
        }

        private int GetSectorSize()
        {
            if (UnbufferedSequentialReader.SectorSizeByPathRoot.TryGetValue(this.pathRoot.Value, out int sectorSize) == false)
            {
                sectorSize = NativeMethods.GetSectorSizeInBytes(this.pathRoot.Value);
                UnbufferedSequentialReader.SectorSizeByPathRoot.AddOrUpdate(this.pathRoot.Value,
                    sectorSize,
                    (string pathRoot, int previouslyRetrievedSectorSize) =>
                    {
                        return previouslyRetrievedSectorSize;
                    });
            }
            return sectorSize;
        }

        private unsafe int Read(byte[] buffer, long offset, int bytesToRead)
        {
            fixed (byte* bufferPin = &buffer[offset])
            {
                int bytesRead = 0;
                int result = NativeMethods.ReadFile(this.file, bufferPin, bytesToRead, ref bytesRead, null);
                if (result == 0)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }
                return bytesRead;
            }
        }

        public override void Skip(long bytes)
        {
            if (bytes < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(bytes), App.FormatResource(Constant.ResourceKey.UnbufferedSequentialReaderBytesRequired, nameof(bytes)));
            }

            Debug.Assert(this.Buffer != null);
            int newPosition = this.bufferPosition + (int)bytes;
            if (newPosition > this.Buffer.Length)
            {
                throw new EndOfStreamException(App.FindResource<string>(Constant.ResourceKey.UnbufferedSequentialReaderEndOfFile));
            }
            this.bufferPosition = newPosition;
        }

        public override bool TrySkip(long bytes)
        {
            Debug.Assert(this.Buffer != null);
            int newPosition = this.bufferPosition + (int)bytes;
            if ((bytes < 0) || (newPosition > this.Buffer.Length))
            {
                return false;
            }

            this.bufferPosition = newPosition;
            return true;
        }

        public override SequentialReader WithByteOrder(bool isMotorolaByteOrder)
        {
            if (isMotorolaByteOrder)
            {
                return this;
            }
            throw new NotSupportedException(App.FindResource<string>(Constant.ResourceKey.UnbufferedSequentialReaderLittleEndian));
        }
    }
}
