using Carnassial.Interop;
using Carnassial.Native;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using MetadataExtractor.Formats.Jpeg;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using MetadataDirectory = MetadataExtractor.Directory;

namespace Carnassial.Images
{
    public class JpegImage : IDisposable
    {
        private static readonly List<IJpegSegmentMetadataReader> JpegReaders;
        private static readonly List<JpegSegmentType> JpegSegmentTypes;

        private bool disposed;
        private readonly UnbufferedSequentialReader reader;

        public IReadOnlyList<MetadataDirectory>? Metadata { get; private set; }

        static JpegImage()
        {
            JpegImage.JpegReaders = new List<IJpegSegmentMetadataReader>()
            {
                new JpegReader(),
                new ExifReader()
            };
            JpegImage.JpegSegmentTypes = new List<JpegSegmentType>(JpegImage.JpegReaders.SelectMany(reader => reader.SegmentTypes));
        }

        public JpegImage(string filePath)
        {
            this.disposed = false;
            this.Metadata = null;
            this.reader = new UnbufferedSequentialReader(filePath);
            this.reader.ExtendBuffer(Constant.Images.JpegInitialBufferSize);
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

            if (disposing && (this.reader != null))
            {
                this.reader.Dispose();
            }

            this.disposed = true;
        }

        public ImageProperties GetThumbnailProperties(ref MemoryImage? preallocatedThumbnail)
        {
            // determine image quality
            // Folder loading becomes jpeg decoding bound at larger decoding sizes.  Using a thumbnail is acceptable 
            // here because luminosity and coloration are average image properties and therefore well preserved even with
            // the aggressive subsampling of thubnails.
            MetadataReadResults thumbnailReadResult = this.TryGetThumbnail(ref preallocatedThumbnail);
            if (thumbnailReadResult != MetadataReadResults.Thumbnail)
            {
                return new ImageProperties(thumbnailReadResult);
            }

            this.TryGetInfoBarHeight(out int infoBarHeight);
            int imageHeight = 0;
            if (this.Metadata != null)
            {
                JpegDirectory? jpeg = this.Metadata.OfType<JpegDirectory>().FirstOrDefault();
                if ((jpeg != null) && jpeg.ContainsTag(JpegDirectory.TagImageHeight))
                {
                    imageHeight = jpeg.GetImageHeight();
                }
            }

            Debug.Assert(preallocatedThumbnail != null);
            double thumbnailScale = (double)preallocatedThumbnail.PixelHeight / (double)imageHeight;
            infoBarHeight = (int)Math.Round(thumbnailScale * infoBarHeight);
            double luminosity = preallocatedThumbnail.GetLuminosityAndColoration(infoBarHeight, out double coloration);
            return new ImageProperties(luminosity, coloration)
            {
                MetadataResult = MetadataReadResults.Thumbnail
            };
        }

        public ImageProperties GetProperties(int? requestedWidth, ref MemoryImage? preallocatedImage)
        {
            this.reader.ExtendBuffer((int)this.reader.Length - this.reader.Buffer.Length);
            if ((preallocatedImage == null) || (preallocatedImage.TryDecode(this.reader.Buffer, 0, this.reader.Buffer.Length, requestedWidth) == false))
            {
                preallocatedImage = new MemoryImage(this.reader.Buffer, 0, this.reader.Buffer.Length, requestedWidth);
            }

            this.TryGetInfoBarHeight(out int infoBarHeight);
            int imageHeight = 0;
            if (this.Metadata != null)
            {
                JpegDirectory? jpeg = this.Metadata.OfType<JpegDirectory>().FirstOrDefault();
                if ((jpeg != null) && jpeg.ContainsTag(JpegDirectory.TagImageHeight))
                {
                    imageHeight = jpeg.GetImageHeight();
                }
            }

            double decodingScale = (double)preallocatedImage.PixelHeight / (double)imageHeight;
            infoBarHeight = (int)Math.Round(decodingScale * infoBarHeight);
            double luminosity = preallocatedImage.GetLuminosityAndColoration(infoBarHeight, out double coloration);
            return new ImageProperties(luminosity, coloration);
        }

        public static bool IsJpeg(string filePath)
        {
            return filePath.EndsWith(Constant.File.JpgFileExtension, StringComparison.OrdinalIgnoreCase);
        }

        public static IReadOnlyList<MetadataDirectory> LoadMetadata(string filePath)
        {
            return JpegMetadataReader.ReadMetadata(filePath);
        }

        public bool TryGetInfoBarHeight(out int infoBarHeight)
        {
            if (this.Metadata == null)
            {
                throw new NotSupportedException(App.FormatResource(Constant.ResourceKey.JpegImageMetadataRequired, nameof(this.TryGetMetadata), nameof(this.TryGetInfoBarHeight)));
            }

            infoBarHeight = 0;
            ExifIfd0Directory? exifIfd0 = this.Metadata.OfType<ExifIfd0Directory>().FirstOrDefault();
            if ((exifIfd0 == null) || (exifIfd0.ContainsTag(ExifSubIfdDirectory.TagMake) == false))
            {
                return false;
            }

            string? make = exifIfd0.GetString(ExifSubIfdDirectory.TagMake);
            if (String.Equals(make, Constant.Manufacturer.Bushnell, StringComparison.OrdinalIgnoreCase))
            {
                infoBarHeight = Constant.Manufacturer.BushnellInfoBarHeight;
            }
            else if (String.Equals(make, Constant.Manufacturer.Reconyx, StringComparison.OrdinalIgnoreCase))
            {
                infoBarHeight = Constant.Manufacturer.ReconyxInfoBarHeight;
            }
            return infoBarHeight != 0;
        }

        [MemberNotNullWhen(true, nameof(Metadata))]
        public bool TryGetMetadata()
        {
            try
            {
                // follow internals of JpegMetadataReader to obtain control of the stream used for reading metadata
                List<JpegSegment> segments = JpegSegmentReader.ReadSegments(this.reader, JpegImage.JpegSegmentTypes).ToList();
                this.Metadata = JpegMetadataReader.ProcessJpegSegments(JpegImage.JpegReaders, segments);
            }
            catch (ImageProcessingException)
            {
                // typically this indicates a corrupt file
                // Most commonly this is last file in the add as opening cameras to turn them off triggers them, resulting in a race
                // condition between writing the file and the camera being turned off.
                this.Metadata = null;
            }

            return this.Metadata != null;
        }

        public MetadataReadResults TryGetThumbnail(ref MemoryImage? preallocatedThumbnail)
        {
            if (this.Metadata == null)
            {
                throw new NotSupportedException(App.FormatResource(Constant.ResourceKey.JpegImageMetadataRequired, nameof(this.TryGetMetadata), nameof(this.TryGetThumbnail) + "()."));
            }

            ExifThumbnailDirectory? thumbnailDirectory = this.Metadata.OfType<ExifThumbnailDirectory>().FirstOrDefault();
            if ((thumbnailDirectory == null) ||
                (thumbnailDirectory.ContainsTag(ExifThumbnailDirectory.TagCompression) == false) ||
                (thumbnailDirectory.ContainsTag(ExifThumbnailDirectory.TagThumbnailOffset) == false) ||
                (thumbnailDirectory.ContainsTag(ExifThumbnailDirectory.TagThumbnailLength) == false))
            {
                return MetadataReadResults.None;
            }
            string? compression = thumbnailDirectory.GetDescription(ExifThumbnailDirectory.TagCompression);
            if ((compression == null) || (compression.StartsWith(Constant.Exif.JpegCompression, StringComparison.OrdinalIgnoreCase) == false))
            {
                return MetadataReadResults.None;
            }

            int thumbnailOffset = thumbnailDirectory.GetInt32(ExifThumbnailDirectory.TagThumbnailOffset);
            int thumbnailLength = thumbnailDirectory.GetInt32(ExifThumbnailDirectory.TagThumbnailLength);
            if (thumbnailLength < Constant.Images.SmallestValidJpegSizeInBytes)
            {
                throw new ImageProcessingException(String.Format(CultureInfo.CurrentCulture, "Jpeg thumbnail sizeof {0} bytes is below smallest expected size {1}.", thumbnailLength, Constant.Images.SmallestValidJpegSizeInBytes));
            }
            if ((thumbnailOffset + thumbnailLength + Constant.Exif.MaxMetadataExtractorIssue35Offset) > this.reader.Buffer.Length)
            {
                throw new ImageProcessingException(String.Format(CultureInfo.CurrentCulture, "End position of thumbnail (byte {0}) may exceed file buffer length of '{1}'.", thumbnailOffset + thumbnailLength, this.reader.Buffer.Length));
            }

            // work around Metadata Extractor issue #35
            // https://github.com/drewnoakes/metadata-extractor-dotnet/issues/35
            if (thumbnailLength <= Constant.Exif.MaxMetadataExtractorIssue35Offset + 1)
            {
                throw new NotSupportedException(String.Format(CultureInfo.CurrentCulture, "Unhandled thumbnail length {0}.", thumbnailLength));
            }
            int issue35Offset = 0;
            for (int offset = 0; offset <= Constant.Exif.MaxMetadataExtractorIssue35Offset; ++offset)
            {
                // 0xffd8 is the JFIF start of image segment indicator
                // https://en.wikipedia.org/wiki/JPEG_File_Interchange_Format#File_format_structure
                int candidateThumbnailStartPosition = thumbnailOffset + offset;
                if ((this.reader.Buffer[candidateThumbnailStartPosition] == 0xff) && (this.reader.Buffer[candidateThumbnailStartPosition + 1] == 0xd8))
                {
                    issue35Offset = offset;
                    break;
                }
            }

            thumbnailOffset += issue35Offset;
            if ((thumbnailOffset + thumbnailLength) > this.reader.Buffer.Length)
            {
                throw new ImageProcessingException(String.Format(CultureInfo.CurrentCulture, "End position of thumbnail (byte {0}) is beyond the file's buffer length of '{1}'.", thumbnailOffset + thumbnailLength, this.reader.Buffer.Length));
            }

            if ((preallocatedThumbnail == null) || (preallocatedThumbnail.TryDecode(this.reader.Buffer, thumbnailOffset, thumbnailLength, null) == false))
            {
                preallocatedThumbnail = new MemoryImage(this.reader.Buffer, thumbnailOffset, thumbnailLength, null);
            }
            if (preallocatedThumbnail.DecodeError)
            {
                return MetadataReadResults.Failed;
            }
            return MetadataReadResults.Thumbnail;
        }
    }
}
