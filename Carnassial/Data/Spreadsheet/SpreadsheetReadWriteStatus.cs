﻿using System;
using System.Diagnostics;

namespace Carnassial.Data.Spreadsheet
{
    public class SpreadsheetReadWriteStatus : DataImportExportStatus<SpreadsheetReadWriteStatus>
    {
        private bool isExcelLoad;
        private bool isExcelSave;
        private double spreadsheetReadPositionDivisor;
        private string spreadsheetReadPositionUnit;

        public SpreadsheetReadWriteStatus(Action<SpreadsheetReadWriteStatus> onProgressUpdate, TimeSpan progressUpdateInterval)
            : base(onProgressUpdate, progressUpdateInterval)
        {
            this.isExcelLoad = false;
            this.isExcelSave = false;
            this.spreadsheetReadPositionDivisor = -1.0;
            this.spreadsheetReadPositionUnit = null;
        }

        public override void BeginRead(long bytesToRead)
        {
            this.spreadsheetReadPositionDivisor = 1024.0;
            this.spreadsheetReadPositionUnit = "kB";

            if (bytesToRead > 1024 * 1024)
            {
                this.spreadsheetReadPositionDivisor = 1024.0 * 1024.0;
                this.spreadsheetReadPositionUnit = "MB";
            }

            base.BeginRead(bytesToRead);
        }

        public void BeginExcelLoad(int sharedStringsToRead)
        {
            this.ClearFlags();
            this.EndPosition = sharedStringsToRead;
            this.isExcelLoad = true;

            this.Report(0);
        }

        public void BeginExcelSave()
        {
            this.ClearFlags();
            // the OpenXML SDK doesn't provide progress information during save so the choice of end position is rather arbitrary
            // However, progress is available from SharedStringIndex.Write().  For now, Write() calls Report(1) when it finishes
            // and shared string writing is assumed to be 15% of the total save cost.  Uncompressed, shared strings are estimated
            // as 8% of the total footprint of a workbook based on actual files.  It's assumed the XML writes to the shared string 
            // table doubles this cost compared to that of writing completed shared string and workbook parts to disk.
            this.EndPosition = 6.0;
            this.isExcelSave = true;

            this.Report(0);
        }

        public void BeginWrite(int rowsToWrite)
        {
            Debug.Assert(rowsToWrite >= 0, "Expected rows to write.");

            this.ClearFlags();
            this.EndPosition = (double)rowsToWrite;

            this.Report(0);
        }

        protected override void ClearFlags()
        {
            base.ClearFlags();

            this.isExcelSave = false;
            this.isExcelLoad = false;
        }

        public void EndExcelWorkbookSave()
        {
            this.Report(1);
        }

        public override string GetMessage()
        {
            if (this.isExcelSave)
            {
                return "Saving Excel file...";
            }
            if (this.isExcelLoad)
            {
                return "Loading Excel file...";
            }
            if (this.IsRead)
            {
                return String.Format("Read {0:0.0} of {1:0.0}{2}...", this.CurrentPosition / this.spreadsheetReadPositionDivisor, this.EndPosition / this.spreadsheetReadPositionDivisor, this.spreadsheetReadPositionUnit);
            }
            if (this.IsTransactionCommit)
            {
                return "Updating Carnassial database...";
            }
            return String.Format("Writing row {0} of {1}...", this.CurrentPosition, this.EndPosition);
        }

        protected override void ReportProgress()
        {
            this.Progress.Report(this);
        }
    }
}
