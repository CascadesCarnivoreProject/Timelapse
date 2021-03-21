using System;
using System.Collections.Generic;
using System.Data.SQLite;

namespace Carnassial.Database
{
    public class SQLiteTableSchema
    {
        public List<ColumnDefinition> ColumnDefinitions { get; private set; }
        public List<SecondaryIndex> Indices { get; private set; }
        public string Table { get; set; }

        public SQLiteTableSchema(string table)
        {
            this.ColumnDefinitions = new List<ColumnDefinition>();
            this.Indices = new List<SecondaryIndex>();
            this.Table = table;
        }

        public SQLiteTableSchema(SQLiteTableSchema other)
            : this(other.Table)
        {
            this.ColumnDefinitions.AddRange(other.ColumnDefinitions);
            this.Indices.AddRange(other.Indices);
        }

        public void CreateIndices(SQLiteConnection connection, SQLiteTransaction transaction)
        {
            foreach (SecondaryIndex index in this.Indices)
            {
                index.Create(connection, transaction);
            }
        }

        public void CreateTable(SQLiteConnection connection, SQLiteTransaction transaction)
        {
            string columnDefinitions = String.Join(", ", this.ColumnDefinitions.ConvertAll(columnDefinition => columnDefinition.ToString()));
            using SQLiteCommand command = new("CREATE TABLE " + this.Table + " (" + columnDefinitions + " )", connection, transaction);
            command.ExecuteNonQuery();
        }

        public void CreateTableAndIndicies(SQLiteConnection connection, SQLiteTransaction transaction)
        {
            this.CreateTable(connection, transaction);
            this.CreateIndices(connection, transaction);
        }
    }
}