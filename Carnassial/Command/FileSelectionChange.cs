﻿using Carnassial.Control;
using Carnassial.Data;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Carnassial.Command
{
    internal class FileSelectionChange : UndoableCommandAsync<CarnassialWindow>
    {
        private long newFileID;
        private FileSelection newSelection;

        private long previousFileID;
        private FileSelection previousSelection;

        public FileSelectionChange(DataEntryHandler dataHandler, FileSelection previousSelection, long previousFileID)
        {
            this.IsExecuted = true;

            this.newFileID = dataHandler.ImageCache.GetCurrentFileID();
            this.newSelection = dataHandler.FileDatabase.ImageSet.FileSelection;

            this.previousFileID = previousFileID;
            this.previousSelection = previousSelection;
        }

        public async override Task ExecuteAsync(CarnassialWindow carnassial)
        {
            await carnassial.SelectFilesAndShowFileAsync(this.newFileID, this.newSelection, false);
            this.IsExecuted = true;
        }

        public bool HasChange()
        {
            return this.previousSelection != this.newSelection;
        }

        public override string ToString()
        {
            if (this.newSelection == FileSelection.Custom)
            {
                return "custom selection";
            }
            return "selection of " + this.newSelection.ToString().ToLowerInvariant() + " files";
        }

        public async override Task UndoAsync(CarnassialWindow carnassial)
        {
            await carnassial.SelectFilesAndShowFileAsync(this.previousFileID, this.previousSelection, false);
            this.IsExecuted = false;
        }
    }
}