using Carnassial.Data;
using Carnassial.Native;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace Carnassial.Images
{
    public class FileLoadAtom : IDisposable
    {
        private bool disposed;

        public FileLoad First { get; private set; }
        public int Offset { get; private set; }
        public string RelativePath { get; private set; }
        public FileLoad Second { get; private set; }

        public FileLoadAtom(string relativePath, FileLoad first, FileLoad second, int offset)
        {
            Debug.Assert(relativePath != null, "Relative path unexpectedly null.");
            Debug.Assert(first != null, "First file unexpectedly null.");
            Debug.Assert(second != null, "Second file unexpectedly null.");
            Debug.Assert(offset >= 0, "Offset unexpectedly negative.");

            this.disposed = false;
            this.First = first;
            this.Offset = offset;
            this.RelativePath = relativePath;
            this.Second = second;
        }

        public bool HasAtLeastOneFile
        {
            get { return (this.First.File != null) || (this.Second.File != null); }
        }

        public bool HasSecondFile
        {
            get { return this.Second.File != null; }
        }

        public ImageProperties Classify(double darkLuminosityThreshold, ref MemoryImage? preallocatedImage)
        {
            Debug.Assert(this.First.File != null, "First file unexpectedly null.");

            ImageProperties? firstProperties = null;
            if (this.First.File.IsVideo)
            {
                this.First.File.Classification = FileClassification.Video;
            }
            else
            {
                if (this.First.Jpeg != null)
                {
                    if (this.First.Jpeg.Metadata == null)
                    {
                        this.First.Jpeg.TryGetMetadata();
                    }
                    if (this.First.Jpeg.Metadata != null)
                    {
                        firstProperties = this.First.Jpeg.GetThumbnailProperties(ref preallocatedImage);
                        this.First.MetadataReadResult |= MetadataReadResults.Thumbnail;
                    }
                    if ((firstProperties == null) || (firstProperties.HasColorationAndLuminosity == false))
                    {
                        firstProperties = this.First.Jpeg.GetProperties(Constant.Images.NoThumbnailClassificationRequestedWidthInPixels, ref preallocatedImage);
                    }
                    if (firstProperties.HasColorationAndLuminosity)
                    {
                        this.First.File.Classification = firstProperties.EvaluateNewClassification(darkLuminosityThreshold);
                        this.First.MetadataReadResult |= MetadataReadResults.Classification;
                    }
                }
                else
                {
                    Debug.Assert((this.First.File.Classification == FileClassification.Corrupt) || (this.First.File.Classification == FileClassification.NoLongerAvailable), "First jpeg null but file not marked missing or corrupt.");
                }
            }

            if (this.HasSecondFile)
            {
                if (this.Second.File.IsVideo)
                {
                    this.Second.File.Classification = FileClassification.Video;
                }
                else
                {
                    if (this.Second.Jpeg != null)
                    {
                        if (this.Second.Jpeg.Metadata == null)
                        {
                            this.Second.Jpeg.TryGetMetadata();
                        }
                        if (this.Second.Jpeg.Metadata != null)
                        {
                            ImageProperties thumbnailProperties = this.Second.Jpeg.GetThumbnailProperties(ref preallocatedImage);
                            this.Second.MetadataReadResult |= MetadataReadResults.Thumbnail;
                            if (thumbnailProperties.HasColorationAndLuminosity)
                            {
                                this.Second.File.Classification = thumbnailProperties.EvaluateNewClassification(darkLuminosityThreshold);
                                this.Second.MetadataReadResult |= MetadataReadResults.Classification;
                            }
                        }
                    }
                    else
                    {
                        Debug.Assert((this.Second.File.Classification == FileClassification.Corrupt) || (this.Second.File.Classification == FileClassification.NoLongerAvailable), "Second jpeg null but file not marked missing or corrupt.");
                    }
                }
            }

            return firstProperties;
        }

        public void ClassifyFromThumbnails(double darkLuminosityThreshold, bool skipClassification, ref MemoryImage preallocatedThumbnail)
        {
            Debug.Assert(this.First.File != null, "First file unexpectedly null.");
            if (this.First.File.IsVideo)
            {
                this.First.File.Classification = FileClassification.Video;
            }
            else if (skipClassification)
            {
                this.First.File.Classification = FileClassification.Color;
            }
            else
            {
                if (this.First.Jpeg != null)
                {
                    if (this.First.Jpeg.Metadata == null)
                    {
                        this.First.Jpeg.TryGetMetadata();
                    }
                    if (this.First.Jpeg.Metadata != null)
                    {
                        ImageProperties thumbnailProperties = this.First.Jpeg.GetThumbnailProperties(ref preallocatedThumbnail);
                        this.First.MetadataReadResult |= MetadataReadResults.Thumbnail;
                        if (thumbnailProperties.HasColorationAndLuminosity)
                        {
                            this.First.File.Classification = thumbnailProperties.EvaluateNewClassification(darkLuminosityThreshold);
                            this.First.MetadataReadResult |= MetadataReadResults.Classification;
                        }
                    }
                }
                else
                {
                    Debug.Assert(this.First.File.Classification == FileClassification.Corrupt, "First jpeg null but file not marked corrupt.");
                }
            }

            if (this.HasSecondFile)
            {
                if (this.Second.File.IsVideo)
                {
                    this.Second.File.Classification = FileClassification.Video;
                }
                else if (skipClassification)
                {
                    this.Second.File.Classification = FileClassification.Color;
                }
                else
                {
                    if (this.Second.Jpeg != null)
                    {
                        if (this.Second.Jpeg.Metadata == null)
                        {
                            this.Second.Jpeg.TryGetMetadata();
                        }
                        if (this.Second.Jpeg.Metadata != null)
                        {
                            ImageProperties thumbnailProperties = this.Second.Jpeg.GetThumbnailProperties(ref preallocatedThumbnail);
                            this.Second.MetadataReadResult |= MetadataReadResults.Thumbnail;
                            if (thumbnailProperties.HasColorationAndLuminosity)
                            {
                                this.Second.File.Classification = thumbnailProperties.EvaluateNewClassification(darkLuminosityThreshold);
                                this.Second.MetadataReadResult |= MetadataReadResults.Classification;
                            }
                        }
                    }
                    else
                    {
                        Debug.Assert(this.Second.File.Classification == FileClassification.Corrupt, "Second jpeg null but file not marked corrupt.");
                    }
                }
            }
        }

        public bool CreateAndAppendFiles(Dictionary<string, HashSet<string>> fileNamesByRelativePath, FileTable files)
        {
            bool databaseHasFilesInFolder = fileNamesByRelativePath.TryGetValue(this.RelativePath, out HashSet<string> filesInFolder);
            if ((databaseHasFilesInFolder == false) || (filesInFolder.Contains(this.First.FileName) == false))
            {
                this.First.File = files.CreateAndAppendFile(this.First.FileName, this.RelativePath);
            }

            if (this.Second.FileName != null)
            {
                if ((databaseHasFilesInFolder == false) || (filesInFolder.Contains(this.Second.FileName) == false))
                {
                    this.Second.File = files.CreateAndAppendFile(this.Second.FileName, this.RelativePath);
                }
            }

            return this.HasAtLeastOneFile;
        }

        public void CreateJpegs(string imageSetFolderPath)
        {
            this.CreateJpegs(imageSetFolderPath, true);
        }

        public void CreateJpegs(string imageSetFolderPath, bool checkFilesExist)
        {
            if (this.First.File != null)
            {
                string firstFilePath = null;
                if (checkFilesExist)
                {
                    FileInfo firstFileInfo = this.First.File.GetFileInfo(imageSetFolderPath);
                    if (firstFileInfo.Exists)
                    {
                        firstFilePath = firstFileInfo.FullName;
                    }
                }
                else
                {
                    firstFilePath = this.First.File.GetFilePath(imageSetFolderPath);
                }

                if (firstFilePath != null)
                {
                    if (this.First.File.IsVideo)
                    {
                        this.First.File.Classification = FileClassification.Video;
                    }
                    else
                    {
                        try
                        {
                            this.First.Jpeg = new JpegImage(firstFilePath);
                        }
                        catch (IOException)
                        {
                            this.First.File.Classification = FileClassification.Corrupt;
                        }
                    }
                }
                else
                {
                    this.First.File.Classification = FileClassification.NoLongerAvailable;
                }
            }

            if (this.HasSecondFile)
            {
                string secondFilePath = null;
                if (checkFilesExist)
                {
                    FileInfo secondFileInfo = this.Second.File.GetFileInfo(imageSetFolderPath);
                    if (secondFileInfo.Exists)
                    {
                        secondFilePath = secondFileInfo.FullName;
                    }
                }
                else
                {
                    secondFilePath = this.First.File.GetFilePath(imageSetFolderPath);
                }

                if (secondFilePath != null)
                {
                    if (this.Second.File.IsVideo)
                    {
                        this.Second.File.Classification = FileClassification.Video;
                    }
                    else
                    {
                        try
                        {
                            this.Second.Jpeg = new JpegImage(this.Second.File.GetFilePath(imageSetFolderPath));
                        }
                        catch (IOException)
                        {
                            this.Second.File.Classification = FileClassification.Corrupt;
                        }
                    }
                }
                else
                {
                    this.Second.File.Classification = FileClassification.NoLongerAvailable;
                }
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
                if (this.First != null)
                {
                    this.First.Dispose();
                }
                if (this.Second != null)
                {
                    this.Second.Dispose();
                }
            }
            this.disposed = true;
        }

        public void DisposeJpegs()
        {
            if ((this.First != null) && (this.First.Jpeg != null))
            {
                this.First.Jpeg.Dispose();
            }
            if ((this.Second != null) && (this.Second.Jpeg != null))
            {
                this.Second.Jpeg.Dispose();
            }
        }

        public void ReadDateTimeOffsets(string imageSetFolderPath, TimeZoneInfo imageSetTimeZone)
        {
            Debug.Assert(this.First.File != null, "First file unexpectedly null.");
            if (this.First.File.IsVideo == false)
            {
                if (this.First.Jpeg != null)
                {
                    if ((this.First.Jpeg.Metadata != null) || this.First.Jpeg.TryGetMetadata())
                    {
                        this.First.MetadataReadResult = this.First.File.TryReadDateTimeFromMetadata(this.First.Jpeg.Metadata, imageSetTimeZone);
                    }
                }
                else
                {
                    Debug.Assert(this.First.File.Classification == FileClassification.Corrupt, "First jpeg null but file not marked corrupt.");
                }
            }
            if (this.First.MetadataReadResult.HasFlag(MetadataReadResults.DateTime) == false)
            {
                this.First.File.SetDateTimeOffsetFromFileInfo(this.First.File.GetFileInfo(imageSetFolderPath));
            }

            if (this.HasSecondFile)
            {
                if (this.Second.File.IsVideo)
                {
                    if (this.Second.File.IsPreviousJpegName(this.First.FileName))
                    {
                        this.Second.File.DateTimeOffset = this.First.File.DateTimeOffset + Constant.Images.DefaultHybridVideoLag;
                        this.Second.MetadataReadResult = MetadataReadResults.DateTimeInferredFromPrevious;
                    }
                    else
                    {
                        this.Second.File.SetDateTimeOffsetFromFileInfo(this.Second.File.GetFileInfo(imageSetFolderPath));
                    }
                }
                else
                {
                    if (this.Second.Jpeg != null)
                    {
                        if ((this.Second.Jpeg.Metadata != null) || this.Second.Jpeg.TryGetMetadata())
                        {
                            this.Second.MetadataReadResult = this.Second.File.TryReadDateTimeFromMetadata(this.Second.Jpeg.Metadata, imageSetTimeZone);
                        }
                    }
                    else
                    {
                        Debug.Assert(this.Second.File.Classification == FileClassification.Corrupt, "Second jpeg null but file not marked corrupt.");
                    }
                    if (this.Second.MetadataReadResult.HasFlag(MetadataReadResults.DateTime) == false)
                    {
                        this.Second.File.SetDateTimeOffsetFromFileInfo(this.Second.File.GetFileInfo(imageSetFolderPath));
                    }
                }
            }
        }

        // an adapter API to allow FileIOComputeTransactionManagers working on existing files to use file add infrastructure
        // An alternate implementation would bifurcate FileIOComputeTransactionManager to use ImageRows directly rather than FileLoad
        // and remove FileTable.GetFilesByRelativePathAndName().  Doing so would be cleaner, but requires maintaining largely
        // duplicate transaction and task code as well as an alternate FileLoadAtom implementation for relatively infrequent
        // reclassify, metadata, and datetime reread operations.  Carnassial's present implementation accepts the relatively small
        // runtime cost of adapting FileLoads to a populated file table over the cost of developing and maintaining the alternative.
        public void SetFiles(Dictionary<string, Dictionary<string, ImageRow>> filesByRelativePathAndName)
        {
            Dictionary<string, ImageRow> filesByName = filesByRelativePathAndName[this.RelativePath];

            this.First.File = filesByName[this.First.FileName];
            Debug.Assert(String.Equals(this.RelativePath, this.First.File.RelativePath, StringComparison.OrdinalIgnoreCase), String.Format(CultureInfo.InvariantCulture, "Relative path of atom '{0}' doesn't match relative path of first file '{1}'.", this.RelativePath, this.First.File.RelativePath));

            if (this.Second.FileName != null)
            {
                this.Second.File = filesByName[this.Second.FileName];
                Debug.Assert(String.Equals(this.RelativePath, this.Second.File.RelativePath, StringComparison.OrdinalIgnoreCase), String.Format(CultureInfo.InvariantCulture, "Relative path of atom '{0}' doesn't match relative path of first file '{1}'.", this.RelativePath, this.Second.File.RelativePath));
            }
        }
    }
}
