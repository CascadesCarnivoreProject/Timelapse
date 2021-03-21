using System;
using System.Collections.Generic;

namespace Carnassial.Data
{
    public class FileImportResult
    {
        public Exception? Exception { get; set; }
        public List<string> Errors { get; private set; }
        public int FilesAdded { get; set; }
        public int FilesProcessed { get; set; }
        public int FilesUpdated { get; set; }

        public FileImportResult()
        {
            this.Exception = null;
            this.Errors = new List<string>();
            this.FilesAdded = 0;
            this.FilesProcessed = 0;
            this.FilesUpdated = 0;
        }

        public int FilesChanged
        {
            get { return this.FilesAdded + this.FilesUpdated; }
        }
    }
}
