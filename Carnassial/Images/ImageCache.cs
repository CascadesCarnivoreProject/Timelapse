using Carnassial.Data;
using Carnassial.Native;
using Carnassial.Util;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading.Tasks;

namespace Carnassial.Images
{
    public class ImageCache : FileTableEnumerator
    {
        private int combinedDifferencesCalculated;
        private TimeSpan combinedDifferenceTime;
        private readonly Dictionary<ImageDifference, CachedImage> differenceCache;
        private int differencesCalculated;
        private TimeSpan differenceTime;
        private bool disposed;
        private readonly MostRecentlyUsedList<long> mostRecentlyUsedIDs;
        private readonly ConcurrentDictionary<long, Task> prefetchesByID;
        private readonly ConcurrentDictionary<long, CachedImage> unalteredImagesByID;

        public ImageDifference CurrentDifferenceState { get; private set; }

        public ImageCache(FileDatabase fileDatabase)
            : base(fileDatabase)
        {
            this.CurrentDifferenceState = ImageDifference.Unaltered;
            this.differenceCache = new Dictionary<ImageDifference, CachedImage>(4);
            foreach (ImageDifference differenceState in Enum.GetValues(typeof(ImageDifference)))
            {
                this.differenceCache.Add(differenceState, null);
            }
            this.differencesCalculated = 0;
            this.differenceTime = TimeSpan.Zero;
            this.disposed = false;
            this.mostRecentlyUsedIDs = new MostRecentlyUsedList<long>(Constant.Images.ImageCacheSize);
            this.prefetchesByID = new ConcurrentDictionary<long, Task>();
            this.unalteredImagesByID = new ConcurrentDictionary<long, CachedImage>();
        }

        public double AverageCombinedDifferenceTimeInSeconds
        {
            get { return this.combinedDifferencesCalculated == 0 ? 0.0 : this.combinedDifferenceTime.TotalSeconds / this.combinedDifferencesCalculated; }
        }

        public double AverageDifferenceTimeInSeconds
        {
            get { return this.differencesCalculated == 0 ? 0.0 : this.differenceTime.TotalSeconds / this.differencesCalculated; }
        }

        protected override void Dispose(bool disposing)
        {
            if (this.disposed)
            {
                return;
            }

            if (disposing)
            {
                foreach (CachedImage image in this.unalteredImagesByID.Values)
                {
                    image.Dispose();
                }
                this.ResetDifferenceState();
            }

            base.Dispose(disposing);
            this.disposed = true;
        }

        public CachedImage GetCurrentImage()
        {
            lock (this.differenceCache)
            {
                return this.differenceCache[this.CurrentDifferenceState];
            }
        }

        private ImageDifference GetNextStateInCombinedDifferenceCycle()
        {
            Debug.Assert(this.IsFileAvailable && (this.Current.IsVideo == false), "No current file or current file is an image.");

            // can't calculate previous or next difference for the first or last file in the image set, respectively
            // if this method and MoveToNextStateInPreviousNextDifferenceCycle() returned bool they'd be consistent with MoveNext() and MovePrevious()
            // however, there's no way for them to fail and there's not value in always returning true
            if ((this.CurrentDifferenceState != ImageDifference.Unaltered) ||
                (this.CurrentRow == 0) ||
                (this.CurrentRow == this.FileDatabase.CurrentlySelectedFileCount - 1) ||
                !this.FileDatabase.IsFileDisplayable(this.CurrentRow + 1) ||
                !this.FileDatabase.IsFileDisplayable(this.CurrentRow - 1))
            {
                return ImageDifference.Unaltered;
            }

            return ImageDifference.Combined;
        }

        private ImageDifference GetNextStateInPreviousNextDifferenceCycle()
        {
            Debug.Assert(this.IsFileAvailable && (this.Current.IsVideo == false), "No current file or current file is an image.");

            // always go to unaltered from combined difference
            if (this.CurrentDifferenceState == ImageDifference.Combined)
            {
                return ImageDifference.Unaltered;
            }

            if (!this.Current.IsDisplayable())
            {
                // can't calculate differences for files which aren't displayble
                return ImageDifference.Unaltered;
            }

            // move to next state in cycle, wrapping around as needed
            ImageDifference nextDifference = (this.CurrentDifferenceState >= ImageDifference.Next) ? ImageDifference.Previous : this.CurrentDifferenceState + 1;

            // unaltered is always displayable; no more checks required
            if (nextDifference == ImageDifference.Unaltered)
            {
                return nextDifference;
            }

            // can't calculate previous or next difference for the first or last file in the image set, respectively
            // can't calculate difference if needed file isn't displayable
            if ((nextDifference == ImageDifference.Previous) && (this.CurrentRow == 0))
            {
                return this.GetNextStateInCombinedDifferenceCycle();
            }
            else if ((this.CurrentDifferenceState == ImageDifference.Next) && (this.CurrentRow == this.FileDatabase.CurrentlySelectedFileCount - 1))
            {
                return this.GetNextStateInCombinedDifferenceCycle();
            }
            else if ((this.CurrentDifferenceState == ImageDifference.Next) && !this.FileDatabase.IsFileDisplayable(this.CurrentRow + 1))
            {
                return this.GetNextStateInCombinedDifferenceCycle();
            }
            else if ((this.CurrentDifferenceState == ImageDifference.Previous) && !this.FileDatabase.IsFileDisplayable(this.CurrentRow - 1))
            {
                return this.GetNextStateInCombinedDifferenceCycle();
            }

            return nextDifference;
        }

        private void CacheImage(long id, CachedImage image)
        {
            lock (this.mostRecentlyUsedIDs)
            {
                // cache the image, replacing any existing image with the one passed
                this.unalteredImagesByID.AddOrUpdate(id,
                    (long newID) =>
                    {
                        // if the image cache is full make room for the incoming image
                        if (this.mostRecentlyUsedIDs.IsFull())
                        {
                            if (this.mostRecentlyUsedIDs.TryGetLeastRecent(out long fileIDToRemove))
                            {
                                if (this.unalteredImagesByID.TryRemove(fileIDToRemove, out CachedImage imageForID))
                                {
                                    imageForID.Dispose();
                                }
                            }
                        }

                        // indicate to add the image
                        return image;
                    },
                    (long existingID, CachedImage newImage) =>
                    {
                        // indicate to update the image
                        return newImage;
                    });
                this.mostRecentlyUsedIDs.SetMostRecent(id);
            }
        }

        // reset enumerator state but don't clear caches
        public override void Reset()
        {
            lock (this.differenceCache)
            {
                base.Reset();
                this.ResetDifferenceState();
            }
        }

        private void ResetDifferenceState()
        {
            this.CurrentDifferenceState = ImageDifference.Unaltered;
            // unaltered image is also contained in this.unalteredImagesByID and is disposed from that collection
            this.differenceCache[ImageDifference.Unaltered] = null;

            foreach (ImageDifference difference in new ImageDifference[] { ImageDifference.Previous, ImageDifference.Next, ImageDifference.Combined })
            {
                CachedImage differenceImage = this.differenceCache[difference];
                if (differenceImage != null)
                {
                    differenceImage.Dispose();
                    this.differenceCache[difference] = null;
                }
            }
        }

        private async Task<CachedImage> TryGetImageAsync(int fileRow)
        {
            if ((this.TryGetFile(fileRow, out ImageRow file) == false) || file.IsVideo)
            {
                return null;
            }

            return await this.TryGetImageAsync(file, 0).ConfigureAwait(true);
        }

        private async Task<CachedImage> TryGetImageAsync(ImageRow file, int prefetchStride)
        {
            // locate the requested image
            if (this.unalteredImagesByID.TryGetValue(file.ID, out CachedImage image) == false)
            {
                if (this.prefetchesByID.TryGetValue(file.ID, out Task prefetch))
                {
                    // image retrieval's already in progress, so wait for it to complete
                    await prefetch.ConfigureAwait(true);
                    image = this.unalteredImagesByID[file.ID];
                }
                else
                {
                    // load the requested image from disk as it isn't cached, doesn't have a prefetch running, and is needed right now 
                    // by the caller
                    image = await file.TryLoadImageAsync(this.FileDatabase.FolderPath).ConfigureAwait(true);
                    this.CacheImage(file.ID, image);
                }
            }

            // start prefetches of nearby images if requested
            if (prefetchStride != 0)
            {
                this.TryInitiatePrefetch(this.CurrentRow + prefetchStride);
            }
            return image;
        }

        private bool TryGetFile(int fileRow, out ImageRow file)
        {
            if (fileRow == this.CurrentRow)
            {
                file = this.Current;
                return true;
            }

            if (this.FileDatabase.IsFileRowInRange(fileRow) == false)
            {
                file = null;
                return false;
            }

            file = this.FileDatabase.Files[fileRow];
            return file.IsDisplayable();
        }

        private bool TryInitiatePrefetch(int fileIndex)
        {
            if (this.FileDatabase.IsFileRowInRange(fileIndex) == false)
            {
                return false;
            }

            ImageRow nextFile = this.FileDatabase.Files[fileIndex];
            if (nextFile.IsVideo || this.unalteredImagesByID.ContainsKey(nextFile.ID) || this.prefetchesByID.ContainsKey(nextFile.ID))
            {
                return false;
            }

            Task prefetch = Task.Run((Func<Task>)(async () =>
            {
                CachedImage nextImage = await nextFile.TryLoadImageAsync((string)this.FileDatabase.FolderPath).ConfigureAwait(true);
                this.CacheImage(nextFile.ID, nextImage);
                this.prefetchesByID.TryRemove(nextFile.ID, out Task _);
            }));
            this.prefetchesByID.AddOrUpdate(nextFile.ID, prefetch, (long id, Task newPrefetch) => { return newPrefetch; });
            return true;
        }

        public bool TryInvalidate(long id)
        {
            if (this.unalteredImagesByID.ContainsKey(id) == false)
            {
                return false;
            }

            if ((this.Current == null) || (this.Current.ID == id))
            {
                this.Reset();
            }

            if (this.unalteredImagesByID.TryRemove(id, out CachedImage imageForID))
            {
                imageForID.Dispose();
            }
            lock (this.mostRecentlyUsedIDs)
            {
                return this.mostRecentlyUsedIDs.TryRemove(id);
            }
        }

        public override bool TryMoveToFile(int fileIndex)
        {
            MoveToFileResult moveToFile = this.TryMoveToFileAsync(fileIndex, 0).GetAwaiter().GetResult();
            return moveToFile.Succeeded;
        }

        public async Task<MoveToFileResult> TryMoveToFileAsync(int fileIndex, int prefetchStride)
        {
            ImageRow afterMoveFile;
            bool movedToNewFile = false;
            lock (this.differenceCache)
            {
                long preMoveFileID = -1;
                if (this.IsFileAvailable)
                {
                    preMoveFileID = this.Current.ID;
                }

                if (base.TryMoveToFile(fileIndex) == false)
                {
                    return new MoveToFileResult();
                }

                afterMoveFile = this.Current;
                movedToNewFile = afterMoveFile.ID != preMoveFileID;
                if (movedToNewFile)
                {
                    // all moves are to display of unaltered images and invalidate any cached differences
                    // it is assumed images on disk are not altered while Carnassial is running and hence unaltered images can safely be cached by their IDs
                    this.ResetDifferenceState();
                }
            }

            // if this file is an image ensure it's loaded from disk and cached
            if (afterMoveFile.IsVideo == false)
            {
                this.differenceCache[ImageDifference.Unaltered] = await this.TryGetImageAsync(afterMoveFile, prefetchStride).ConfigureAwait(true);
            }

            return new MoveToFileResult(movedToNewFile);
        }

        public async Task<ImageDifferenceResult> TryMoveToNextCombinedDifferenceImageAsync(byte differenceThreshold)
        {
            ImageDifference initialDifferenceState;
            int initialRow;
            CachedImage unaltered;
            lock (this.differenceCache)
            {
                // three valid images are needed for combined differencing: the current one, the previous one, and the next one
                // If the current image is unavailable then neither unaltered or combined is display is feasible.
                if ((this.IsFileAvailable == false) || this.Current.IsVideo || (this.Current.IsDisplayable() == false))
                {
                    this.CurrentDifferenceState = ImageDifference.Unaltered;
                    return ImageDifferenceResult.CurrentImageNotAvailable;
                }
                unaltered = this.differenceCache[ImageDifference.Unaltered];
                if ((unaltered == null) || (unaltered.Image == null))
                {
                    return ImageDifferenceResult.CurrentImageNotAvailable;
                }

                ImageDifference nextDifferenceState = this.GetNextStateInCombinedDifferenceCycle();
                if (nextDifferenceState != ImageDifference.Combined)
                {
                    this.CurrentDifferenceState = nextDifferenceState;
                    return ImageDifferenceResult.Success;
                }

                CachedImage cachedDifference = this.differenceCache[nextDifferenceState];
                if ((cachedDifference != null) && (cachedDifference.Image != null))
                {
                    this.CurrentDifferenceState = nextDifferenceState;
                    return ImageDifferenceResult.Success;
                }

                initialDifferenceState = this.CurrentDifferenceState;
                initialRow = this.CurrentRow;
            }

            CachedImage previous = await this.TryGetImageAsync(initialRow - 1).ConfigureAwait(true);
            if ((previous == null) || (previous.Image == null))
            {
                return ImageDifferenceResult.PreviousImageNotAvailable;
            }

            CachedImage next = await this.TryGetImageAsync(initialRow + 1).ConfigureAwait(true);
            if ((next == null) || (next.Image == null))
            {
                return ImageDifferenceResult.NextImageNotAvailable;
            }

            // all three images are available, so calculate difference and cache result if it still applies at completion
            return await Task.Run(() =>
            {
                Stopwatch stopwatch = new();
                stopwatch.Start();
                bool success = unaltered.Image.TryDifference(previous.Image, next.Image, differenceThreshold, out MemoryImage difference);
                stopwatch.Stop();
                if (success)
                {
                    lock (this.differenceCache)
                    {
                        ++this.combinedDifferencesCalculated;
                        this.combinedDifferenceTime += stopwatch.Elapsed;

                        if ((this.CurrentRow == initialRow) && (this.CurrentDifferenceState == initialDifferenceState))
                        {
                            this.CurrentDifferenceState = ImageDifference.Combined;
                            this.differenceCache[ImageDifference.Combined] = new CachedImage(difference);
                            return ImageDifferenceResult.Success;
                        }
                        return ImageDifferenceResult.NoLongerValid;
                    }
                }
                return ImageDifferenceResult.NotCalculable;
            }).ConfigureAwait(true);
        }

        public async Task<ImageDifferenceResult> TryMoveToNextDifferenceImageAsync(byte differenceThreshold)
        {
            ImageDifferenceResult comparisonImageNotAvailable;
            int comparisonRow;
            ImageDifference initialDifferenceState;
            int initialRow;
            ImageDifference nextDifferenceState;
            CachedImage unaltered;
            lock (this.differenceCache)
            {
                if ((this.IsFileAvailable == false) || this.Current.IsVideo || (this.Current.IsDisplayable() == false))
                {
                    this.CurrentDifferenceState = ImageDifference.Unaltered;
                    return ImageDifferenceResult.CurrentImageNotAvailable;
                }

                unaltered = this.differenceCache[ImageDifference.Unaltered];
                if ((unaltered == null) || (unaltered.Image == null))
                {
                    this.CurrentDifferenceState = ImageDifference.Unaltered;
                    return ImageDifferenceResult.CurrentImageNotAvailable;
                }

                initialDifferenceState = this.CurrentDifferenceState;
                initialRow = this.CurrentRow;

                nextDifferenceState = this.GetNextStateInPreviousNextDifferenceCycle();
                switch (nextDifferenceState)
                {
                    case ImageDifference.Next:
                        comparisonImageNotAvailable = ImageDifferenceResult.NextImageNotAvailable;
                        comparisonRow = initialRow + 1;
                        break;
                    case ImageDifference.Unaltered:
                        this.CurrentDifferenceState = nextDifferenceState;
                        return ImageDifferenceResult.Success;
                    case ImageDifference.Previous:
                        comparisonImageNotAvailable = ImageDifferenceResult.PreviousImageNotAvailable;
                        comparisonRow = initialRow - 1;
                        break;
                    case ImageDifference.Combined:
                    default:
                        throw new NotSupportedException(String.Format(CultureInfo.CurrentCulture, "Unhandled difference state {0}.", nextDifferenceState));
                }

                CachedImage cachedDifference = this.differenceCache[nextDifferenceState];
                if ((cachedDifference != null) && (cachedDifference.Image != null))
                {
                    this.CurrentDifferenceState = nextDifferenceState;
                    return ImageDifferenceResult.Success;
                }
            }

            // determine which image to use for differencing
            CachedImage comparisonImage = await this.TryGetImageAsync(comparisonRow).ConfigureAwait(true);
            if ((comparisonImage == null) || (comparisonImage.Image == null))
            {
                return comparisonImageNotAvailable;
            }

            return await Task.Run(() =>
            {
                Stopwatch stopwatch = new();
                stopwatch.Start();
                bool success = unaltered.Image.TryDifference(comparisonImage.Image, differenceThreshold, out MemoryImage difference);
                if (success)
                {
                    lock (this.differenceCache)
                    {
                        ++this.differencesCalculated;
                        this.differenceTime += stopwatch.Elapsed;

                        if ((this.CurrentRow == initialRow) && (this.CurrentDifferenceState == initialDifferenceState))
                        {
                            this.CurrentDifferenceState = nextDifferenceState;
                            this.differenceCache[nextDifferenceState] = new CachedImage(difference);
                            return ImageDifferenceResult.Success;
                        }
                        return ImageDifferenceResult.NoLongerValid;
                    }
                }
                return ImageDifferenceResult.NotCalculable;
            }).ConfigureAwait(true);
        }
    }
}
