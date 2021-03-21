using System;
using System.Data.SQLite;

namespace Carnassial.Database
{
    public class TransactionSequence : IDisposable
    {
        private bool disposed;
        private readonly bool ownsTransaction;

        protected SQLiteDatabase Database { get; private set; }
        protected bool IsInsert { get; set; }
        protected int RowsInCurrentTransaction { get; set; }
        protected SQLiteTransaction? Transaction { get; set; }

        public int RowsCommitted { get; private set; }

        protected TransactionSequence(SQLiteDatabase database)
        {
            this.Database = database;
            this.disposed = false;
            this.IsInsert = false;
            this.ownsTransaction = true;
            this.RowsCommitted = 0;
            this.RowsInCurrentTransaction = 0;
            this.Transaction = null;
        }

        protected TransactionSequence(SQLiteDatabase database, SQLiteTransaction transaction)
            : this(database)
        {
            if (transaction == null)
            {
                this.Transaction = database.Connection.BeginTransaction();
            }
            else
            {
                this.ownsTransaction = false;
                this.Transaction = transaction;
            }
        }

        public void Commit()
        {
            this.Transaction.Commit();
            if (this.IsInsert)
            {
                this.Database.RowsInsertedSinceLastBackup += this.RowsInCurrentTransaction;
            }
            else
            {
                this.Database.RowsUpdatedSinceLastBackup += this.RowsInCurrentTransaction;
            }
            this.RowsCommitted += this.RowsInCurrentTransaction;
            this.RowsInCurrentTransaction = 0;
        }

        protected SQLiteCommand CommitAndBeginNew(SQLiteCommand command)
        {
            this.Commit();
            this.Transaction = this.Database.Connection.BeginTransaction();

            SQLiteCommand previousCommand = command;
            SQLiteCommand newCommand = new(previousCommand.CommandText, this.Database.Connection, this.Transaction);
            foreach (SQLiteParameter parameter in previousCommand.Parameters)
            {
                newCommand.Parameters.Add(parameter);
            }
            previousCommand.Dispose();
            return newCommand;
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
                if (this.ownsTransaction)
                {
                    this.Transaction.Dispose();
                }
            }

            this.disposed = true;
        }
    }
}