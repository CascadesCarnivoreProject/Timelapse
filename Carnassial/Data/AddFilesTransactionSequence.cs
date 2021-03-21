using Carnassial.Database;
using Carnassial.Images;
using Carnassial.Util;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Diagnostics;
using System.Globalization;

namespace Carnassial.Data
{
    public class AddFilesTransactionSequence : WindowedTransactionSequence<FileLoad>
    {
        private SQLiteCommand addFiles;
        private bool disposed;

        public AddFilesTransactionSequence(SQLiteDatabase database, ControlTable controls)
            : base(database)
        {
            this.disposed = false;

            List<string> userControlDataLabels = new();
            List<string> userControlDefaultValues = new();
            string deleteFlagDefaultValue = null;
            foreach (ControlRow control in controls)
            {
                if (control.IsUserControl())
                {
                    userControlDataLabels.Add(SQLiteDatabase.QuoteIdentifier(control.DataLabel));
                    string defaultValue;
                    switch (control.ControlType)
                    {
                        case ControlType.Counter:
                            Debug.Assert(Utilities.IsDigits(control.DefaultValue), "Default values for counters should be numeric.");
                            defaultValue = control.DefaultValue;
                            break;
                        case ControlType.Flag:
                            Debug.Assert(String.Equals(control.DefaultValue, Constant.Sql.FalseString, StringComparison.Ordinal) || String.Equals(control.DefaultValue, Constant.Sql.TrueString, StringComparison.Ordinal), "Default values for flags should be binary.");
                            defaultValue = control.DefaultValue;
                            break;
                        case ControlType.FixedChoice:
                        case ControlType.Note:
                            defaultValue = SQLiteDatabase.QuoteStringLiteral(control.DefaultValue);
                            break;
                        case ControlType.DateTime:
                        case ControlType.UtcOffset:
                        default:
                            throw new NotSupportedException(String.Format(CultureInfo.CurrentCulture, "Unhandled control type {0}.", control.ControlType));
                    }
                    userControlDefaultValues.Add(defaultValue);
                }
                else if (String.Equals(control.DataLabel, Constant.FileColumn.DeleteFlag, StringComparison.Ordinal))
                {
                    deleteFlagDefaultValue = control.DefaultValue;
                }
                // don't specify ID in the insert statement as it's an autoincrement primary key
                // don't generate parameters for standard controls they're coded explicitly
                this.IsInsert = true;
            }

            string dataLabelsConcatenated = null;
            string defaultValuesConcatenated = null;
            if (userControlDataLabels.Count > 0)
            {
                dataLabelsConcatenated = ", " + String.Join(", ", userControlDataLabels);
                defaultValuesConcatenated = ", " + String.Join(", ", userControlDefaultValues);
            }
            string fileInsertText = String.Format(CultureInfo.InvariantCulture,
                                                  "INSERT INTO {0} ({1}, {2}, {3}, {4}, {5}, {6}{7}) VALUES (@DateTime, {8}, @FileName, @Classification, @RelativePath, @UtcOffset{9})",
                                                  Constant.DatabaseTable.Files,
                                                  Constant.FileColumn.DateTime,
                                                  Constant.FileColumn.DeleteFlag,
                                                  Constant.FileColumn.File,
                                                  Constant.FileColumn.Classification,
                                                  Constant.FileColumn.RelativePath,
                                                  Constant.FileColumn.UtcOffset,
                                                  dataLabelsConcatenated,
                                                  deleteFlagDefaultValue,
                                                  defaultValuesConcatenated);

            this.Transaction = this.Database.Connection.BeginTransaction();
            this.addFiles = new SQLiteCommand(fileInsertText, this.Database.Connection, this.Transaction);
            // order must be kept in sync with parameter value sets AddFiles()
            this.addFiles.Parameters.Add(new SQLiteParameter("@Classification"));
            this.addFiles.Parameters.Add(new SQLiteParameter("@DateTime"));
            this.addFiles.Parameters.Add(new SQLiteParameter("@FileName"));
            this.addFiles.Parameters.Add(new SQLiteParameter("@RelativePath"));
            this.addFiles.Parameters.Add(new SQLiteParameter("@UtcOffset"));
        }

        /// <summary>
        /// Inserts files in the file table with their name, relative path, date time offset, and classification populated.
        /// Other fields are set to their default values.
        /// </summary>
        public override int AddToSequence(IList<FileLoad> files, int offset, int length)
        {
            Debug.Assert(files != null, nameof(files) + " is null.");
            Debug.Assert(offset >= 0, nameof(offset) + " is less than zero.");
            Debug.Assert(length >= 0, nameof(length) + " is less than zero.");
            Debug.Assert((offset + length) <= files.Count, String.Format(CultureInfo.CurrentCulture, "Offset {0} plus length {1} exceeds length of files ({2}.", offset, length, files.Count));

            // insert performance of early Carnassial 2.2.0.3 development (still using 2.2.0.2 schema)
            //                                   column defaults   specified defaults
            //                  unparameterized  parameterized     parameterized
            // 100 rows/call    245us/row        
            // 1000 rows/call   76us/row                           29us/row
            // single call      75us/row         27us/row          25us/row (40k rows/s)
            // Stopwatch stopwatch = new Stopwatch();
            // stopwatch.Start();
            int filesAdded = 0;
            int stopIndex = offset + length;
            for (int fileIndex = offset; fileIndex < stopIndex; ++fileIndex)
            {
                ImageRow file = files[fileIndex].File;
                if (file == null)
                {
                    continue;
                }
                this.addFiles.Parameters[0].Value = (int)file.Classification;
                this.addFiles.Parameters[1].Value = file.UtcDateTime;
                this.addFiles.Parameters[2].Value = file.FileName;
                this.addFiles.Parameters[3].Value = file.RelativePath;
                this.addFiles.Parameters[4].Value = DateTimeHandler.ToDatabaseUtcOffset(file.UtcOffset);

                this.addFiles.ExecuteNonQuery();
                file.AcceptChanges();
                ++filesAdded;

                ++this.RowsInCurrentTransaction;
                if (this.RowsInCurrentTransaction >= Constant.Database.RowsPerTransaction)
                {
                    this.addFiles = this.CommitAndBeginNew(this.addFiles);
                }
            }

            // stopwatch.Stop();
            // Trace.WriteLine(stopwatch.Elapsed.ToString("s\\.fffffff", CultureInfo.CurrentCulture));
            return filesAdded;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (this.disposed)
            {
                return;
            }

            if (disposing)
            {
                this.addFiles.Dispose();
            }

            this.disposed = true;
        }
    }
}
