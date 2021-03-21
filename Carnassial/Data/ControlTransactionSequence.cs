using Carnassial.Database;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Text;

namespace Carnassial.Data
{
    public class ControlTransactionSequence : TransactionSequence
    {
        private bool disposed;
        private readonly SQLiteCommand insertOrUpdateControls;

        protected ControlTransactionSequence(StringBuilder command, SQLiteDatabase database, SQLiteTransaction transaction)
            : base(database, transaction)
        {
            this.disposed = false;
            this.insertOrUpdateControls = new SQLiteCommand(command.ToString(), this.Database.Connection, this.Transaction);
            this.insertOrUpdateControls.Parameters.Add(new SQLiteParameter("@" + Constant.ControlColumn.AnalysisLabel));
            this.insertOrUpdateControls.Parameters.Add(new SQLiteParameter("@" + Constant.ControlColumn.ControlOrder));
            this.insertOrUpdateControls.Parameters.Add(new SQLiteParameter("@" + Constant.ControlColumn.Copyable));
            this.insertOrUpdateControls.Parameters.Add(new SQLiteParameter("@" + Constant.ControlColumn.DataLabel));
            this.insertOrUpdateControls.Parameters.Add(new SQLiteParameter("@" + Constant.ControlColumn.DefaultValue));
            this.insertOrUpdateControls.Parameters.Add(new SQLiteParameter("@" + Constant.ControlColumn.IndexInFileTable));
            this.insertOrUpdateControls.Parameters.Add(new SQLiteParameter("@" + Constant.ControlColumn.Label));
            this.insertOrUpdateControls.Parameters.Add(new SQLiteParameter("@" + Constant.ControlColumn.SpreadsheetOrder));
            this.insertOrUpdateControls.Parameters.Add(new SQLiteParameter("@" + Constant.ControlColumn.MaxWidth));
            this.insertOrUpdateControls.Parameters.Add(new SQLiteParameter("@" + Constant.ControlColumn.Tooltip));
            this.insertOrUpdateControls.Parameters.Add(new SQLiteParameter("@" + Constant.ControlColumn.Type));
            this.insertOrUpdateControls.Parameters.Add(new SQLiteParameter("@" + Constant.ControlColumn.Visible));
            this.insertOrUpdateControls.Parameters.Add(new SQLiteParameter("@" + Constant.ControlColumn.WellKnownValues));
            this.insertOrUpdateControls.Parameters.Add(new SQLiteParameter("@" + Constant.DatabaseColumn.ID));
            this.IsInsert = this.insertOrUpdateControls.CommandText.StartsWith("INSERT", StringComparison.Ordinal);
        }

        public void AddControl(ControlRow control)
        {
            this.AddControls(new List<ControlRow>() { control });
        }

        public void AddControls(IEnumerable<ControlRow> controls)
        {
            foreach (ControlRow control in controls)
            {
                if ((this.IsInsert == false) && (control.HasChanges == false))
                {
                    continue;
                }

                this.insertOrUpdateControls.Parameters[0].Value = control.AnalysisLabel;
                this.insertOrUpdateControls.Parameters[1].Value = control.ControlOrder;
                this.insertOrUpdateControls.Parameters[2].Value = control.Copyable;
                this.insertOrUpdateControls.Parameters[3].Value = control.DataLabel;
                this.insertOrUpdateControls.Parameters[4].Value = control.DefaultValue;
                this.insertOrUpdateControls.Parameters[5].Value = control.IndexInFileTable;
                this.insertOrUpdateControls.Parameters[6].Value = control.Label;
                this.insertOrUpdateControls.Parameters[7].Value = control.SpreadsheetOrder;
                this.insertOrUpdateControls.Parameters[8].Value = control.MaxWidth;
                this.insertOrUpdateControls.Parameters[9].Value = control.Tooltip;
                this.insertOrUpdateControls.Parameters[10].Value = (int)control.ControlType;
                this.insertOrUpdateControls.Parameters[11].Value = control.Visible;
                this.insertOrUpdateControls.Parameters[12].Value = control.WellKnownValues;
                this.insertOrUpdateControls.Parameters[13].Value = control.ID;

                this.insertOrUpdateControls.ExecuteNonQuery();
                control.AcceptChanges();
            }
        }

        public void AddControls(params ControlRow[] controls)
        {
            this.AddControls((IEnumerable<ControlRow>)controls);
        }

        public static ControlTransactionSequence CreateInsert(SQLiteDatabase database)
        {
            return ControlTransactionSequence.CreateInsert(database, null);
        }

        public static ControlTransactionSequence CreateInsert(SQLiteDatabase database, SQLiteTransaction transaction)
        {
            List<string> columns = new(Constant.ControlColumn.Columns.Count);
            List<string> parameterNames = new(Constant.ControlColumn.Columns.Count);
            foreach (string column in Constant.ControlColumn.Columns)
            {
                columns.Add(column);
                parameterNames.Add("@" + column);
            }

            StringBuilder insertCommand = new("INSERT INTO " + Constant.DatabaseTable.Controls + " (" + String.Join(", ", columns) + ") VALUES (" + String.Join(", ", parameterNames) + ")");
            return new ControlTransactionSequence(insertCommand, database, transaction);
        }

        public static ControlTransactionSequence CreateUpdate(SQLiteDatabase database)
        {
            return ControlTransactionSequence.CreateUpdate(database, null);
        }

        public static ControlTransactionSequence CreateUpdate(SQLiteDatabase database, SQLiteTransaction transaction)
        {
            StringBuilder updateCommand = new("UPDATE " + Constant.DatabaseTable.Controls + " SET ");
            List<string> parameters = new(Constant.ControlColumn.Columns.Count);
            foreach (string column in Constant.ControlColumn.Columns)
            {
                parameters.Add(column + "=@" + column);
            }
            updateCommand.Append(String.Join(", ", parameters));
            updateCommand.Append(" WHERE " + Constant.DatabaseColumn.ID + "=@" + Constant.DatabaseColumn.ID);

            return new ControlTransactionSequence(updateCommand, database, transaction);
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
                this.insertOrUpdateControls.Dispose();
            }

            this.disposed = true;
        }
    }
}
