using Carnassial.Data;
using Carnassial.Database;
using Carnassial.Interop;
using Carnassial.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Carnassial.Images
{
    /// <summary>
    /// Executes once a workload consisting of IO tasks loading data from files, compute tasks processing the data, and a database 
    /// transaction.
    /// </summary>
    /// <remarks>
    /// FileIOComputeTransactionManager provides common infrastructure for parallel execution of Carnassial operations involving 
    /// bulk processing of file metadata. Provision is made to run a number of IO and compute tasks suited to the processor 
    /// capability available, provide UI status updates as an operation proceeds, and accumulate the results of the compute tasks
    /// into a database transaction.
    /// </remarks>
    /// <typeparam name="TProgress">The type of the status object passed to the progress update callback.</typeparam>
    internal class FileIOComputeTransactionManager<TProgress> : IDisposable where TProgress : FileIOComputeTransactionStatus, new()
    {
        private bool addFilesInProgress;
        private int addFileStartIndex;
        private int addFileStopIndex;
        private int computeAtomIndex;
        private int computeFileIndex;
        private readonly int computeTaskCount;
        private readonly Task<int>?[] computeTasks;
        private bool disposed;
        private FileLoad[]? fileLoads;
        private int ioAtomIndex;
        private readonly int[] ioAtomsCompletedByTask;
        private int ioFileIndex;
        private SortedDictionary<string, List<string>>.Enumerator ioFilesByRelativePathEnumerator;
        private List<string>? ioFilesInCurrentFolder;
        private int ioFilesInCurrentFolderIndex;
        private readonly int ioTaskCount;
        private readonly Task?[] ioTasks;
        private int ioTasksActive;
        private bool isCompleted;
        private bool isExceptional;
        private FileLoadAtom[]? loadAtoms;
        private WindowedTransactionSequence<FileLoad>? transactionSequence;

        protected Func<int, int>? ComputeTaskBody { get; set; }
        protected Action<int>? IOTaskBody { get; set; }
        protected ExceptionPropagatingProgress<TProgress> Progress { get; private set; }
        protected TProgress Status { get; private set; }
        protected int TransactionFileCount { get; private set; }

        public TimeSpan ComputeDuration { get; private set; }
        public TimeSpan DatabaseDuration { get; private set; }
        public TimeSpan IODuration { get; private set; }
        public bool ShouldExitCurrentIteration { get; set; }

        protected FileIOComputeTransactionManager(Action<TProgress> onProgressUpdate, TimeSpan desiredProgressInterval)
        {
            this.addFilesInProgress = false;
            this.addFileStartIndex = 0;
            this.addFileStopIndex = 0;

            this.computeAtomIndex = 0;
            this.ComputeDuration = TimeSpan.Zero;
            this.computeFileIndex = 0;
            this.ComputeTaskBody = null;

            this.DatabaseDuration = TimeSpan.Zero;
            this.disposed = false;
            this.fileLoads = null;

            this.ioAtomIndex = 0;
            this.IODuration = TimeSpan.Zero;
            this.ioFilesInCurrentFolderIndex = 0;
            this.ioFileIndex = 0;
            this.ioFilesByRelativePathEnumerator = default;
            this.ioFilesInCurrentFolder = null;
            this.IOTaskBody = null;

            this.isCompleted = false;
            this.isExceptional = false;
            this.loadAtoms = null;
            this.Progress = new ExceptionPropagatingProgress<TProgress>(onProgressUpdate, desiredProgressInterval);
            this.ShouldExitCurrentIteration = false;
            this.Status = new TProgress();

            this.transactionSequence = null;
            this.TransactionFileCount = 0;

            // physical cores              1  1  2  2  ?  4  4  ?  6   6  ?  8+
            // logical processors          1  2  2  4  3  4  8  5  6  12  7  8+
            // hyperthreaded               n  y  n  y  ?  n  y  ?  n   y  ?  x
            // IO tasks                    1  1  1  2  1  2  2  2  2   2  2  2
            // compute tasks               1  1  1  2  2  2  4  3  4   4  4  4
            // total tasks                 2  2  2  4  3  4  6  5  6   6  6  6
            // thread pinning beneficial   n  ?  n  y  ?  n  y  ?  n   ?  ?  ?
            int logicalProcessors = Environment.ProcessorCount;
            this.ioTaskCount = logicalProcessors < 4 ? 1 : 2;
            this.computeTaskCount = Math.Min(4, logicalProcessors - 2);
            if (logicalProcessors < 4)
            {
                this.computeTaskCount = logicalProcessors == 3 ? 2 : 1;
            }
            this.ioTasksActive = 0;

            this.computeTasks = new Task<int>[this.computeTaskCount];
            for (int computeTaskIndex = 0; computeTaskIndex < this.computeTaskCount; ++computeTaskIndex)
            {
                this.computeTasks[computeTaskIndex] = null;
            }

            this.ioTasks = new Task[this.ioTaskCount];
            this.ioAtomsCompletedByTask = new int[this.ioTaskCount];
            for (int ioTaskIndex = 0; ioTaskIndex < this.ioTaskCount; ++ioTaskIndex)
            {
                this.ioAtomsCompletedByTask[ioTaskIndex] = -1;
                this.ioTasks[ioTaskIndex] = null;
            }
        }

        public int FilesCompleted
        {
            get { return this.computeFileIndex; }
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
                this.computeTasks.Dispose();
                this.ioFilesByRelativePathEnumerator.Dispose();
                this.ioTasks.Dispose();
                this.loadAtoms?.Dispose();
                if (this.transactionSequence != null)
                {
                    this.transactionSequence.Dispose();
                }

                this.Progress.End();
            }

            this.disposed = true;
        }

        protected void AddToSequence()
        {
            Debug.Assert((this.fileLoads != null) && (this.transactionSequence != null));
            this.TransactionFileCount += this.transactionSequence.AddToSequence(this.fileLoads, this.addFileStartIndex, this.addFileStopIndex - this.addFileStartIndex);
            this.addFilesInProgress = false;
            this.addFileStartIndex = this.addFileStopIndex;
        }

        protected FileLoadAtom? GetNextComputeAtom(int computeThreadID)
        {
            FileLoadAtom? atom = null;
            while (atom == null)
            {
                if (this.isExceptional || this.ShouldExitCurrentIteration)
                {
                    return null;
                }

                int atomIndex = -1;
                if (this.computeAtomIndex < this.ioAtomsCompletedByTask.Min())
                {
                    Debug.Assert(this.loadAtoms != null);
                    lock (this.loadAtoms)
                    {
                        if (this.computeAtomIndex < this.ioAtomsCompletedByTask.Min())
                        {
                            atomIndex = this.computeAtomIndex++;
                            atom = this.loadAtoms[atomIndex];
                            if (atom.HasSecondFile)
                            {
                                this.computeFileIndex += 2;
                            }
                            else
                            {
                                ++this.computeFileIndex;
                            }
                        }
                    }
                }
                else if (this.ioTasksActive == 0)
                {
                    if (this.computeAtomIndex < this.ioAtomIndex)
                    {
                        Debug.Assert(this.loadAtoms != null);
                        lock (this.loadAtoms)
                        {
                            if (this.computeAtomIndex < this.ioAtomIndex)
                            {
                                atomIndex = this.computeAtomIndex++;
                                atom = this.loadAtoms[atomIndex];
                                if (atom.HasSecondFile)
                                {
                                    this.computeFileIndex += 2;
                                }
                                else
                                {
                                    ++this.computeFileIndex;
                                }
                            }
                        }
                    }
                    else if (this.computeAtomIndex >= this.ioAtomIndex)
                    {
                        return null;
                    }
                }
                if (atomIndex < 0)
                {
                    Thread.Sleep(TimeSpan.FromMilliseconds(2.5));
                }
            }

            return atom;
        }

        protected FileLoadAtom? GetNextIOAtom(int ioThreadID)
        {
            if (this.isExceptional || this.ShouldExitCurrentIteration)
            {
                return null;
            }

            Debug.Assert((this.ioFilesInCurrentFolder != null) && (this.transactionSequence != null));
            int atomIndex;
            int atomOffset;
            string firstFileName;
            string relativePath;
            string? secondFileName = null;
            lock (this.transactionSequence)
            {
                while (this.ioFilesInCurrentFolderIndex >= this.ioFilesInCurrentFolder.Count)
                {
                    if (this.ioFilesByRelativePathEnumerator.MoveNext() == false)
                    {
                        return null;
                    }
                    this.ioFilesInCurrentFolder = this.ioFilesByRelativePathEnumerator.Current.Value;
                    Debug.Assert(this.ioFilesInCurrentFolder != null, "List of files in folder is unexpectedly null.");
                    this.ioFilesInCurrentFolderIndex = 0;
                }

                firstFileName = this.ioFilesInCurrentFolder[this.ioFilesInCurrentFolderIndex];
                Debug.Assert(firstFileName != null, "Unexpected null entry in collection of files to add.");

                if (this.ioFilesInCurrentFolderIndex + 1 < this.ioFilesInCurrentFolder.Count)
                {
                    secondFileName = this.ioFilesInCurrentFolder[this.ioFilesInCurrentFolderIndex + 1];
                    Debug.Assert(secondFileName != null, "Unexpected null entry in collection of files to add.");

                    // in the case of an alternating sequence of images and videos, try to align atom such that file N is a video
                    // This enables FolderLoadAtom to infer video date times from the metadata of preceeding .jpg files.  The
                    // possibilities here are
                    //
                    // n is video  n+1 is video  n+2 is video
                    // false       false         false         => two file atom ok
                    // false       false         true          => one file atom preferred
                    // false       true          false         => two file atom ok
                    // false       true          true          => two file atom ok
                    // true        false         false         => two file atom ok
                    // true        false         true          => one file atom preferred
                    // true        true          false         => two file atom ok
                    // true        true          true          => two file atom ok
                    //
                    // It follows that if n+1 is not a video then n+2 should be checked and an atom containing one file returned
                    // so that the next call aligns with n+2 being a video.
                    if ((FileTable.IsVideo(secondFileName) == false) && (this.ioFilesInCurrentFolderIndex + 2 < this.ioFilesInCurrentFolder.Count))
                    {
                        string fileNameNPlus2 = this.ioFilesInCurrentFolder[this.ioFilesInCurrentFolderIndex + 2];
                        if (FileTable.IsVideo(fileNameNPlus2))
                        {
                            secondFileName = null;
                        }
                    }
                }

                atomIndex = this.ioAtomIndex++;
                atomOffset = this.ioFileIndex;
                relativePath = this.ioFilesByRelativePathEnumerator.Current.Key;
                int increment = secondFileName != null ? 2 : 1;
                this.ioFileIndex += increment;
                this.ioFilesInCurrentFolderIndex += increment;
            }

            Debug.Assert((this.fileLoads != null) && (this.loadAtoms != null));
            FileLoad firstLoad = new(firstFileName);
            this.fileLoads[atomOffset] = firstLoad;
            FileLoad secondLoad = FileLoad.NoLoad;
            if (secondFileName != null)
            {
                secondLoad = new FileLoad(secondFileName);
                this.fileLoads[atomOffset + 1] = secondLoad;
            }
            this.ioAtomsCompletedByTask[ioThreadID] = atomIndex - 1;

            FileLoadAtom loadAtom = new(relativePath, firstLoad, secondLoad, atomOffset);
            this.loadAtoms[atomIndex] = loadAtom;
            return loadAtom;
        }

        protected async Task RunTasksAsync(WindowedTransactionSequence<FileLoad> transactionSequence, SortedDictionary<string, List<string>> filesToLoadByRelativePath, int filesToLoad)
        {
            if (this.ComputeTaskBody == null)
            {
                throw new InvalidOperationException(App.FormatResource(Constant.ResourceKey.FileIOComputeTransactionManagerNullTask, nameof(this.ComputeTaskBody)));
            }
            if (this.IOTaskBody == null)
            {
                throw new InvalidOperationException(App.FormatResource(Constant.ResourceKey.FileIOComputeTransactionManagerNullTask, nameof(this.IOTaskBody)));
            }
            if (this.isCompleted)
            {
                throw new InvalidOperationException(App.FormatResource(Constant.ResourceKey.FileIOComputeTransactionManagerCantRerun, nameof(this.RunTasksAsync)));
            }
            if (this.disposed)
            {
                throw new ObjectDisposedException(this.GetType().FullName);
            }

            Stopwatch stopwatch = new();
            stopwatch.Start();

            this.fileLoads = new FileLoad[filesToLoad];
            this.ioFilesByRelativePathEnumerator = filesToLoadByRelativePath.GetEnumerator();
            this.ioFilesByRelativePathEnumerator.MoveNext();
            this.ioFilesInCurrentFolder = this.ioFilesByRelativePathEnumerator.Current.Value;
            Debug.Assert(this.ioFilesInCurrentFolder != null, "List of files in folder is unexpectedly null.");
            this.loadAtoms = new FileLoadAtom[filesToLoad];
            this.transactionSequence = transactionSequence;

            this.ioTasksActive = this.ioTaskCount;
            for (int ioTask = 0; ioTask < this.ioTaskCount; ++ioTask)
            {
                int ioTaskNumber = ioTask;
                this.ioTasks[ioTask] = Task.Run(() =>
                {
                    try
                    {
                        using HyperthreadSiblingAffinity affinity = new(ioTaskNumber);
                        this.IOTaskBody.Invoke(ioTaskNumber);
                    }
                    catch
                    {
                        this.isExceptional = true;
                        throw;
                    }
                });
            }

            for (int computeTask = 0; computeTask < this.computeTaskCount; ++computeTask)
            {
                int computeTaskNumber = computeTask;
                this.computeTasks[computeTask] = Task.Run(() =>
                {
                    try
                    {
                        using HyperthreadSiblingAffinity affinity = new(computeTaskNumber);
                        return this.ComputeTaskBody.Invoke(computeTaskNumber);
                    }
                    catch
                    {
                        this.isExceptional = true;
                        throw;
                    }
                });
            }

            if (this.ioTasks != null)
            {
                await Task.WhenAll(this.ioTasks!).ConfigureAwait(false);
            }
            this.ioTasksActive = 0;
            this.IODuration = stopwatch.Elapsed;

            if (this.computeTasks != null)
            {
                await Task.WhenAll(this.computeTasks!).ConfigureAwait(false);
            }
            this.ComputeDuration = stopwatch.Elapsed;

            // if not aborted, flush any remaining commits to database on success
            if ((this.isExceptional == false) && (this.ShouldExitCurrentIteration == false))
            {
                this.addFileStopIndex = this.fileLoads.Length;
                this.AddToSequence();
                this.transactionSequence.Commit();
            }
            this.isCompleted = true;

            stopwatch.Stop();
            this.DatabaseDuration = stopwatch.Elapsed;
        }

        protected bool ShouldAddFilesToTransaction()
        {
            if (this.addFilesInProgress == true)
            {
                return false;
            }

            this.addFileStopIndex = this.FilesCompleted;
            this.addFilesInProgress = (this.addFileStopIndex - this.addFileStartIndex) > Constant.Database.NominalRowsPerTransactionFill;
            return this.addFilesInProgress;
        }
    }
}
