﻿using Carnassial.Database;
using Carnassial.Images;
using Carnassial.Util;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Diagnostics;
using System.Linq;

namespace Carnassial.Data
{
    public class FileTable : SQLiteTable<ImageRow>
    {
        public FileTable()
        {
            this.UserControlIndicesByDataLabel = new Dictionary<string, int>();
        }

        public Dictionary<string, int> UserControlIndicesByDataLabel { get; private set; }

        public ImageRow CreateAndAppendFile(string fileName, string relativePath)
        {
            ImageRow file;
            if (FileTable.IsVideo(fileName))
            {
                file = new VideoRow(fileName, relativePath, this);
            }
            else if (JpegImage.IsJpeg(fileName))
            {
                file = new ImageRow(fileName, relativePath, this);
            }
            else
            {
                throw new NotSupportedException(String.Format("Unhandled extension for file '{0}'.", fileName));
            }

            this.Rows.Add(file);
            return file;
        }

        public SortedDictionary<string, List<string>> GetFileNamesByRelativePath()
        {
            SortedDictionary<string, List<string>> filesByRelativePath = new SortedDictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (ImageRow file in this.Rows)
            {
                if (filesByRelativePath.TryGetValue(file.RelativePath, out List<string> filesInFolder) == false)
                {
                    filesInFolder = new List<string>();
                    filesByRelativePath.Add(file.RelativePath, filesInFolder);
                }
                filesInFolder.Add(file.FileName);
            }
            return filesByRelativePath;
        }

        public Dictionary<string, HashSet<string>> HashFileNamesByRelativePath()
        {
            // for now, the primary purpose of this function is allow the caller to quickly check if a file is in the database
            // Therefore, assemble a Dictionary<, HashSet<>> as both these collection types have O(1) Contains().
            Dictionary<string, HashSet<string>> filesByRelativePath = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (ImageRow file in this.Rows)
            {
                if (filesByRelativePath.TryGetValue(file.RelativePath, out HashSet<string> filesInFolder) == false)
                {
                    filesInFolder = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    filesByRelativePath.Add(file.RelativePath, filesInFolder);
                }
                filesInFolder.Add(file.FileName);
            }
            return filesByRelativePath;
        }

        public static bool IsVideo(string fileName)
        {
            return fileName.EndsWith(Constant.File.AviFileExtension, StringComparison.OrdinalIgnoreCase) ||
                   fileName.EndsWith(Constant.File.Mp4FileExtension, StringComparison.OrdinalIgnoreCase);
        }

        public override void Load(SQLiteDataReader reader)
        {
            if (reader.FieldCount < 1)
            {
                throw new SQLiteException(SQLiteErrorCode.Schema, "Table has no columns.");
            }

            this.Rows.Clear();
            this.UserControlIndicesByDataLabel.Clear();

            // locate columns in schema
            // Standard columns usually appear first but, depending on the table's evolution, this may not always be the case.
            int dateTimeIndex = -1;
            int deleteFlagIndex = -1;
            int fileNameIndex = -1;
            int idIndex = -1;
            int imageQualityIndex = -1;
            int relativePathIndex = -1;
            int utcOffsetIndex = -1;
            int standardColumnCount = 0;
            int[] userControlSqlIndicies = new int[reader.FieldCount - Constant.Control.StandardControls.Count - 1]; // standard controls and ID
            for (int column = 0; column < reader.FieldCount; ++column)
            {
                string columnName = reader.GetName(column);
                switch (columnName)
                {
                    case Constant.DatabaseColumn.DateTime:
                        dateTimeIndex = column;
                        ++standardColumnCount;
                        break;
                    case Constant.DatabaseColumn.DeleteFlag:
                        deleteFlagIndex = column;
                        ++standardColumnCount;
                        break;
                    case Constant.DatabaseColumn.File:
                        fileNameIndex = column;
                        ++standardColumnCount;
                        break;
                    case Constant.DatabaseColumn.ID:
                        idIndex = column;
                        ++standardColumnCount;
                        break;
                    case Constant.DatabaseColumn.ImageQuality:
                        imageQualityIndex = column;
                        ++standardColumnCount;
                        break;
                    case Constant.DatabaseColumn.RelativePath:
                        relativePathIndex = column;
                        ++standardColumnCount;
                        break;
                    case Constant.DatabaseColumn.UtcOffset:
                        utcOffsetIndex = column;
                        ++standardColumnCount;
                        break;
                    default:
                        int userControlIndex = column - standardColumnCount;
                        this.UserControlIndicesByDataLabel.Add(columnName, userControlIndex);
                        userControlSqlIndicies[userControlIndex] = column;
                        break;
                }
            }
            Debug.Assert(standardColumnCount == Constant.Control.StandardControls.Count + 1, "Mismatch in locating standard controls and ID columns.");

            bool allStandardColumnsPresent = (dateTimeIndex != -1) &&
                                             (deleteFlagIndex != -1) &&
                                             (fileNameIndex != -1) &&
                                             (idIndex != -1) &&
                                             (imageQualityIndex != -1) &&
                                             (relativePathIndex != -1) &&
                                             (utcOffsetIndex != -1);
            if (allStandardColumnsPresent == false)
            {
                throw new SQLiteException(SQLiteErrorCode.Schema, "At least one standard column is missing from table " + reader.GetTableName(0));
            }

            while (reader.Read())
            {
                IDataRecord row = (IDataRecord)reader;
                string fileName = row.GetString(fileNameIndex);
                string relativePath = row.GetString(relativePathIndex);
                ImageRow file = this.CreateAndAppendFile(fileName, relativePath);

                // read file values
                // Carnassial versions prior to 2.2.0.3 had a bug where UTC offsets where written as TimeSpans rather than doubles
                // which, combined with column type real, produces an odd situation where IDataRecord.GetValue() returns a correct
                // double but GetDouble() throws on System.Data.SQLite 1.0.107.  As a workaround for backwards compatibility, 
                // GetValue() is called and cast to double.
                double utcOffset = (double)row.GetValue(utcOffsetIndex);
                file.DateTimeOffset = DateTimeHandler.FromDatabaseDateTimeOffset(row.GetDateTime(dateTimeIndex), DateTimeHandler.FromDatabaseUtcOffset(utcOffset));
                file.DeleteFlag = Boolean.Parse(row.GetString(deleteFlagIndex));
                file.ID = row.GetInt64(idIndex);
                string classificationAsString = row.GetString(imageQualityIndex);
                #pragma warning disable CS0618 // Type or member is obsolete
                if (String.Equals(classificationAsString, FileSelection.Ok.ToString(), StringComparison.Ordinal))
                #pragma warning restore CS0618 // Type or member is obsolete
                {
                    // legacy case for backwards compatibility with Carnassial 2.2.0.2 and earlier, where FileSelection was also
                    // used as FileClassification
                    file.Classification = FileClassification.Color;
                }
                else
                {
                    file.Classification = (FileClassification)Enum.Parse(typeof(FileClassification), classificationAsString);
                }
                foreach (KeyValuePair<string, int> userControl in this.UserControlIndicesByDataLabel)
                {
                    int sqlIndex = userControlSqlIndicies[userControl.Value];
                    file.UserControlValues[userControl.Value] = row.GetString(sqlIndex);
                }
                file.AcceptChanges();
            }
        }

        public ImageRow Single(string fileName, string relativePath)
        {
            return this.Rows.Single(file =>
            {
                return (String.Compare(file.RelativePath, relativePath, StringComparison.OrdinalIgnoreCase) == 0) &&
                       (String.Compare(file.FileName, fileName, StringComparison.OrdinalIgnoreCase) == 0);
            });
        }

        public bool TryFind(long id, out ImageRow file, out int fileIndex)
        {
            for (fileIndex = 0; fileIndex < this.Rows.Count; ++fileIndex)
            {
                file = this.Rows[fileIndex];
                if (file.ID == id)
                {
                    return true;
                }
            }
            file = null;
            return false;
        }

        private class UserControlIndices
        {
            public int SqlColumnIndex { get; private set; }
            public int UserControlArrayIndex { get; private set; }

            public UserControlIndices(int sqlColumnIndex, int userControlArrayIndex)
            {
                this.SqlColumnIndex = sqlColumnIndex;
                this.UserControlArrayIndex = userControlArrayIndex;
            }
        }
    }
}
