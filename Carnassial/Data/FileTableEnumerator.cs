using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Carnassial.Data
{
    public class FileTableEnumerator : IEnumerator<ImageRow?>
    {
        protected FileDatabase FileDatabase { get; private set; }

        // the current file, null if its not been set or if the database is empty
        public ImageRow? Current { get; private set; }
        public int CurrentRow { get; private set; }

        public FileTableEnumerator(FileDatabase fileDatabase)
        {
            this.Current = null;
            this.CurrentRow = Constant.Database.InvalidRow;
            this.FileDatabase = fileDatabase;
        }

        public FileTableEnumerator(FileDatabase fileDatabase, int startingPosition)
            : this(fileDatabase)
        {
            this.CurrentRow = startingPosition;
            this.TryMoveToFile(startingPosition);
        }

        [MemberNotNullWhen(true, nameof(Current))]
        public bool IsFileAvailable
        {
            get { return this.Current != null; }
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            // nothing to do but required by IEnumerator<T>
        }

        object? IEnumerator.Current
        {
            get { return this.Current; }
        }

        public long GetCurrentFileID()
        {
            return this.IsFileAvailable ? this.Current.ID : Constant.Database.InvalidID;
        }

        public bool MoveNext()
        {
            return this.TryMoveToFile(this.CurrentRow + 1);
        }

        public bool MovePrevious()
        {
            return this.TryMoveToFile(this.CurrentRow - 1);
        }

        public virtual void Reset()
        {
            this.Current = null;
            this.CurrentRow = Constant.Database.InvalidRow;
        }

        public virtual bool TryMoveToFile(int fileRowIndex)
        {
            if (this.FileDatabase.IsFileRowInRange(fileRowIndex))
            {
                this.Current = this.FileDatabase.Files[fileRowIndex];
                this.CurrentRow = fileRowIndex;
                return true;
            }

            return false;
        }
    }
}
