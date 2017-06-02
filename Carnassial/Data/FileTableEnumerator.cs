﻿using System;
using System.Collections;
using System.Collections.Generic;

namespace Carnassial.Data
{
    public class FileTableEnumerator : IEnumerator<ImageRow>
    {
        protected FileDatabase Database { get; private set; }

        // the current file, null if its not been set or if the database is empty
        public ImageRow Current { get; private set; }
        public int CurrentRow { get; private set; }

        public FileTableEnumerator(FileDatabase fileDatabase) :
            this(fileDatabase, Constant.Database.InvalidRow)
        {
        }

        public FileTableEnumerator(FileDatabase fileDatabase, int startingPosition)
        {
            this.CurrentRow = startingPosition;
            this.Database = fileDatabase;

            // OK if this fails as FileTableEnumerator..ctor(FileDatabase) passes -1 to match default enumerator behaviour
            this.TryMoveToFile(startingPosition);
        }

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

        object IEnumerator.Current
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
            if (this.Database.IsFileRowInRange(fileRowIndex))
            {
                this.CurrentRow = fileRowIndex;
                // rebuild ImageProperties regardless of whether the row changed or not as this seek may be a refresh after a database change
                this.Current = this.Database.Files[fileRowIndex];
                return true;
            }

            return false;
        }
    }
}
