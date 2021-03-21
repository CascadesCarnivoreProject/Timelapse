using Carnassial.Database;
using System;
using System.Data.SQLite;
using System.Globalization;

namespace Carnassial.Data
{
    internal class ImageSetTable : SQLiteTable<ImageSetRow>
    {
        public static SQLiteTableSchema CreateSchema()
        {
            SQLiteTableSchema schema = new(Constant.DatabaseTable.ImageSet);
            schema.ColumnDefinitions.Add(ColumnDefinition.CreatePrimaryKey());
            schema.ColumnDefinitions.Add(new ColumnDefinition(Constant.ImageSetColumn.FileSelection, Constant.SQLiteAffinity.Integer)
            {
                DefaultValue = ((int)default(FileSelection)).ToString(Constant.InvariantCulture),
                NotNull = true
            });
            schema.ColumnDefinitions.Add(new ColumnDefinition(Constant.ImageSetColumn.InitialFolderName, Constant.SQLiteAffinity.Text)
            {
                NotNull = true
            });
            schema.ColumnDefinitions.Add(new ColumnDefinition(Constant.ImageSetColumn.Log, Constant.SQLiteAffinity.Text));
            schema.ColumnDefinitions.Add(new ColumnDefinition(Constant.ImageSetColumn.Options, Constant.SQLiteAffinity.Integer)
            {
                DefaultValue = ((int)default(ImageSetOptions)).ToString(Constant.InvariantCulture),
                NotNull = true
            });
            schema.ColumnDefinitions.Add(new ColumnDefinition(Constant.ImageSetColumn.MostRecentFileID, Constant.SQLiteAffinity.Integer)
            {
                DefaultValue = Constant.Database.DefaultFileID.ToString(Constant.InvariantCulture),
                NotNull = true
            });
            schema.ColumnDefinitions.Add(new ColumnDefinition(Constant.ImageSetColumn.TimeZone, Constant.SQLiteAffinity.Text)
            {
                NotNull = true
            });
            return schema;
        }

        public override void Load(SQLiteDataReader reader)
        {
            int fileSelectionIndex = -1;
            int idIndex = -1;
            int initialFolderIndex = -1;
            int logIndex = -1;
            int mostRecentFileIndex = -1;
            int optionsIndex = -1;
            int timeZoneIndex = -1;
            for (int column = 0; column < reader.FieldCount; ++column)
            {
                string columnName = reader.GetName(column);
                switch (columnName)
                {
                    case Constant.ImageSetColumn.FileSelection:
                        fileSelectionIndex = column;
                        break;
                    case Constant.DatabaseColumn.ID:
                        idIndex = column;
                        break;
                    case Constant.ImageSetColumn.InitialFolderName:
                        initialFolderIndex = column;
                        break;
                    case Constant.ImageSetColumn.Log:
                        logIndex = column;
                        break;
                    case Constant.ImageSetColumn.MostRecentFileID:
                        mostRecentFileIndex = column;
                        break;
                    case Constant.ImageSetColumn.Options:
                        optionsIndex = column;
                        break;
                    case Constant.ImageSetColumn.TimeZone:
                        timeZoneIndex = column;
                        break;
                    default:
                        throw new NotSupportedException(String.Format(CultureInfo.CurrentCulture, "Unhandled column '{0}' in {1} table schema.", columnName, reader.GetTableName(0)));
                }
            }

            bool allStandardColumnsPresent = (fileSelectionIndex != -1) &&
                                             (idIndex != -1) &&
                                             (initialFolderIndex != -1) &&
                                             (logIndex != -1) &&
                                             (mostRecentFileIndex != -1) &&
                                             (optionsIndex != -1) &&
                                             (timeZoneIndex != -1);
            if (allStandardColumnsPresent == false)
            {
                throw new SQLiteException(SQLiteErrorCode.Schema, "At least one standard column is missing from table " + reader.GetTableName(0));
            }

            this.Rows.Clear();

            while (reader.Read())
            {
                // read file values
                ImageSetRow imageSet = new()
                {
                    FileSelection = (FileSelection)reader.GetInt32(fileSelectionIndex),
                    ID = reader.GetInt64(idIndex),
                    InitialFolderName = reader.GetString(initialFolderIndex),
                    Log = reader.GetString(logIndex),
                    MostRecentFileID = reader.GetInt64(mostRecentFileIndex),
                    Options = (ImageSetOptions)reader.GetInt32(optionsIndex),
                    TimeZone = reader.GetString(timeZoneIndex)
                };
                imageSet.AcceptChanges();

                this.Rows.Add(imageSet);
            }
        }
    }
}
