using Carnassial.Database;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Text;

namespace Carnassial.Data
{
    public class ImageSetTransactionSequence : TransactionSequence
    {
        private bool disposed;
        private readonly SQLiteCommand insertOrUpdateImageSet;

        protected ImageSetTransactionSequence(StringBuilder command, SQLiteDatabase database, SQLiteTransaction transaction)
            : base(database, transaction)
        {
            this.disposed = false;
            this.insertOrUpdateImageSet = new SQLiteCommand(command.ToString(), this.Database.Connection, this.Transaction);
            this.insertOrUpdateImageSet.Parameters.Add(new SQLiteParameter("@" + Constant.ImageSetColumn.FileSelection));
            this.insertOrUpdateImageSet.Parameters.Add(new SQLiteParameter("@" + Constant.ImageSetColumn.InitialFolderName));
            this.insertOrUpdateImageSet.Parameters.Add(new SQLiteParameter("@" + Constant.ImageSetColumn.Log));
            this.insertOrUpdateImageSet.Parameters.Add(new SQLiteParameter("@" + Constant.ImageSetColumn.MostRecentFileID));
            this.insertOrUpdateImageSet.Parameters.Add(new SQLiteParameter("@" + Constant.ImageSetColumn.Options));
            this.insertOrUpdateImageSet.Parameters.Add(new SQLiteParameter("@" + Constant.ImageSetColumn.TimeZone));
            this.insertOrUpdateImageSet.Parameters.Add(new SQLiteParameter("@" + Constant.DatabaseColumn.ID));
            this.IsInsert = this.insertOrUpdateImageSet.CommandText.StartsWith("INSERT", StringComparison.Ordinal);
        }

        public void AddImageSet(ImageSetRow imageSet)
        {
            if ((this.IsInsert == false) && (imageSet.HasChanges == false))
            {
                return;
            }

            this.insertOrUpdateImageSet.Parameters[0].Value = (int)imageSet.FileSelection;
            this.insertOrUpdateImageSet.Parameters[1].Value = imageSet.InitialFolderName;
            this.insertOrUpdateImageSet.Parameters[2].Value = imageSet.Log;
            this.insertOrUpdateImageSet.Parameters[3].Value = imageSet.MostRecentFileID;
            this.insertOrUpdateImageSet.Parameters[4].Value = (int)imageSet.Options;
            this.insertOrUpdateImageSet.Parameters[5].Value = imageSet.TimeZone;
            this.insertOrUpdateImageSet.Parameters[6].Value = imageSet.ID;

            this.insertOrUpdateImageSet.ExecuteNonQuery();
            imageSet.AcceptChanges();
        }

        public static ImageSetTransactionSequence CreateInsert(SQLiteDatabase database)
        {
            return ImageSetTransactionSequence.CreateInsert(database, null);
        }

        public static ImageSetTransactionSequence CreateInsert(SQLiteDatabase database, SQLiteTransaction transaction)
        {
            List<string> columns = new(Constant.ImageSetColumn.Columns.Count);
            List<string> parameterNames = new(Constant.ImageSetColumn.Columns.Count);
            foreach (string column in Constant.ImageSetColumn.Columns)
            {
                columns.Add(column);
                parameterNames.Add("@" + column);
            }

            StringBuilder insertCommand = new("INSERT INTO " + Constant.DatabaseTable.ImageSet + " (" + String.Join(", ", columns) + ") VALUES (" + String.Join(", ", parameterNames) + ")");
            return new ImageSetTransactionSequence(insertCommand, database, transaction);
        }

        public static ImageSetTransactionSequence CreateUpdate(SQLiteDatabase database)
        {
            return ImageSetTransactionSequence.CreateUpdate(database, null);
        }

        public static ImageSetTransactionSequence CreateUpdate(SQLiteDatabase database, SQLiteTransaction transaction)
        {
            StringBuilder updateCommand = new("UPDATE " + Constant.DatabaseTable.ImageSet + " SET ");
            List<string> parameters = new(Constant.ImageSetColumn.Columns.Count);
            foreach (string column in Constant.ImageSetColumn.Columns)
            {
                parameters.Add(column + "=@" + column);
            }
            updateCommand.Append(String.Join(", ", parameters));
            updateCommand.Append(" WHERE " + Constant.DatabaseColumn.ID + "=@" + Constant.DatabaseColumn.ID);

            return new ImageSetTransactionSequence(updateCommand, database, transaction);
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
                this.insertOrUpdateImageSet.Dispose();
            }

            this.disposed = true;
        }
    }
}