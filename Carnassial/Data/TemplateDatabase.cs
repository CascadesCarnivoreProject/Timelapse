using Carnassial.Database;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using ColumnDefinition = Carnassial.Database.ColumnDefinition;

namespace Carnassial.Data
{
    /// <summary>
    /// Carnassial template database.
    /// </summary>
    public class TemplateDatabase : SQLiteDatabase
    {
        public ControlTable Controls { get; private set; }

        /// <summary>Gets the complete path to the folder containing the database.</summary>
        public string FolderPath { get; private set; }

        public ImageSetRow ImageSet { get; private set; }

        protected TemplateDatabase(string filePath)
            : base(filePath)
        {
            this.Controls = new ControlTable();
            
            string? folderPath = Path.GetDirectoryName(filePath);
            if (folderPath == null)
            {
                throw new ArgumentOutOfRangeException(nameof(filePath), String.Format(CultureInfo.CurrentCulture, "Unable to extract directory name from database file path '{0}'.", filePath));
            }
            this.FolderPath = folderPath;
        }

        public ControlRow AppendUserDefinedControl(ControlType controlType)
        {
            // create the row for the new control in the data table
            ControlRow newControl = new(controlType, this.GetNextUniqueDataLabel(controlType.ToString()), this.Controls.RowCount + 1);
            using (ControlTransactionSequence insertControl = ControlTransactionSequence.CreateInsert(this))
            {
                insertControl.AddControl(newControl);
                insertControl.Commit();
            }

            // refresh in memory table in order to get the new control's ID
            this.GetControlsSortedByControlOrder();
            return this.Controls[this.Controls.RowCount - 1];
        }

        protected void GetControlsSortedByControlOrder()
        {
            Select select = new(Constant.DatabaseTable.Controls)
            {
                OrderBy = Constant.ControlColumn.ControlOrder
            };

            this.LoadDataTableFromSelect(this.Controls, select);
        }

        public string GetNextUniqueDataLabel(string dataLabelPrefix)
        {
            // get all existing data labels
            List<string> dataLabels = new();
            foreach (ControlRow control in this.Controls)
            {
                dataLabels.Add(control.DataLabel);
            }

            // if the candidate data label exists, increment the count until a unique data label is found
            int dataLabelUniqueIdentifier = 0;
            string nextDataLabel = dataLabelPrefix + dataLabelUniqueIdentifier.ToString(Constant.InvariantCulture);
            while (dataLabels.Contains(nextDataLabel, StringComparer.Ordinal))
            {
                ++dataLabelUniqueIdentifier;
                nextDataLabel = dataLabelPrefix + dataLabelUniqueIdentifier.ToString(Constant.InvariantCulture);
            }

            return nextDataLabel;
        }

        protected void LoadImageSet()
        {
            ImageSetTable imageSetTable = new();
            this.LoadDataTableFromSelect(imageSetTable, new Select(Constant.DatabaseTable.ImageSet));
            this.ImageSet = imageSetTable[0];
        }

        public void RemoveUserDefinedControl(ControlRow controlToRemove)
        {
            if (controlToRemove.IsUserControl() == false)
            {
                throw new NotSupportedException(String.Format(CultureInfo.CurrentCulture, "Standard control {0} cannot be removed.", controlToRemove.DataLabel));
            }

            // capture state
            long removedControlOrder = controlToRemove.ControlOrder;
            long removedSpreadsheetOrder = controlToRemove.SpreadsheetOrder;

            // regenerate counter and spreadsheet orders; if they're greater than the one removed, decrement
            foreach (ControlRow control in this.Controls)
            {
                if (control.ControlOrder > removedControlOrder)
                {
                    --control.ControlOrder;
                }

                if (control.SpreadsheetOrder > removedSpreadsheetOrder)
                {
                    --control.SpreadsheetOrder;
                }
            }

            // drop the control from the database and data table
            using (SQLiteTransaction transaction = this.Connection.BeginTransaction())
            {
                using (SQLiteCommand removeControl = new("DELETE FROM " + Constant.DatabaseTable.Controls + Constant.Sql.Where + Constant.DatabaseColumn.ID + " = " + controlToRemove.ID, this.Connection))
                {
                    removeControl.ExecuteNonQuery();
                }
                using (ControlTransactionSequence remainingControlUpdate = ControlTransactionSequence.CreateUpdate(this, transaction))
                {
                    remainingControlUpdate.AddControls(this.Controls);
                }

                transaction.Commit();
            }
            ++this.RowsDroppedSinceLastBackup;

            // refresh in memory table
            // Using Remove() rather than rebuilding the table would be desirable but doing so changes row ordering and requires a resort.
            // This path is seldom used and isn't performance sensitive so simplicity is favored instead.
            this.GetControlsSortedByControlOrder();
        }

        public static bool TryCreateOrOpen(string filePath, out TemplateDatabase templateDatabase)
        {
            // check for an existing database before instantiating the databse as SQL wrapper instantiation creates the database file
            bool populateDatabase = !File.Exists(filePath);

            templateDatabase = new TemplateDatabase(filePath);
            if (populateDatabase)
            {
                // initialize the database if it's newly created
                templateDatabase.OnDatabaseCreated(null);
            }
            else
            {
                // if it's an existing database check if it needs updating to current structure and load data tables
                if (templateDatabase.OnExistingDatabaseOpened(null) == false)
                {
                    return false;
                }
            }

            // check all tables have been loaded from the database
            Debug.Assert(templateDatabase.Controls != null, "Controls wasn't loaded.");
            Debug.Assert(templateDatabase.ImageSet != null, "ImageSet wasn't loaded.");

            return true;
        }

        /// <summary>
        /// Update the database row for the control to the version currently in memory.
        /// </summary>
        public bool TrySyncControlToDatabase(ControlRow control)
        {
            if (control.HasChanges == false)
            {
                return false;
            }

            this.SyncControlToDatabase(control);
            return true;
        }

        public bool TrySyncImageSetToDatabase()
        {
            Debug.Assert(this.ImageSet != null);
            if (this.ImageSet.HasChanges == false)
            {
                return false;
            }

            // don't trigger backups on image set updates as none of the properties in the image set table is particularly important
            // For example, this avoids creating a backup when a custom selection is reverted to all when Carnassial exits.
            using (ImageSetTransactionSequence imageSetUpdate = ImageSetTransactionSequence.CreateUpdate(this))
            {
                imageSetUpdate.AddImageSet(this.ImageSet);
                imageSetUpdate.Commit();
            }
            return true;
        }

        public void UpdateDisplayOrder(string orderColumnName, Dictionary<string, int> newOrderByDataLabel)
        {
            // argument validation
            if (orderColumnName != Constant.ControlColumn.ControlOrder && orderColumnName != Constant.ControlColumn.SpreadsheetOrder)
            {
                throw new ArgumentOutOfRangeException(nameof(orderColumnName), String.Format(CultureInfo.CurrentCulture, "'{0}' is not a control order column.  Only '{1}' and '{2}' are order columns.", orderColumnName, Constant.ControlColumn.ControlOrder, Constant.ControlColumn.SpreadsheetOrder));
            }

            if (newOrderByDataLabel.Count != this.Controls.RowCount)
            {
                throw new NotSupportedException(String.Format(CultureInfo.CurrentCulture, "Partial order updates are not supported.  New ordering for {0} controls was passed but {1} controls are present for '{2}'.", newOrderByDataLabel.Count, this.Controls.RowCount, orderColumnName));
            }

            List<int> uniqueOrderValues = newOrderByDataLabel.Values.Distinct().ToList();
            if (uniqueOrderValues.Count != newOrderByDataLabel.Count)
            {
                throw new ArgumentException(String.Format(CultureInfo.CurrentCulture, "Each control must have a unique value for its order.  {0} duplicate values were passed for '{1}'.", newOrderByDataLabel.Count - uniqueOrderValues.Count, orderColumnName), nameof(newOrderByDataLabel));
            }

            uniqueOrderValues.Sort();
            for (int control = 0; control < uniqueOrderValues.Count; ++control)
            {
                int expectedOrder = control + 1;
                if (uniqueOrderValues[control] != expectedOrder)
                {
                    throw new ArgumentOutOfRangeException(nameof(newOrderByDataLabel), String.Format(CultureInfo.CurrentCulture, "Control order must be a ones based count.  An order of {0} was passed instead of the expected order {1} for '{2}'.", uniqueOrderValues[0], expectedOrder, orderColumnName));
                }
            }

            // update in memory table with new order
            foreach (ControlRow control in this.Controls)
            {
                string dataLabel = control.DataLabel;
                int newOrder = newOrderByDataLabel[dataLabel];
                switch (orderColumnName)
                {
                    case Constant.ControlColumn.ControlOrder:
                        control.ControlOrder = newOrder;
                        break;
                    case Constant.ControlColumn.SpreadsheetOrder:
                        control.SpreadsheetOrder = newOrder;
                        break;
                    default:
                        throw new NotSupportedException(String.Format(CultureInfo.CurrentCulture, "Unhandled column '{0}'.", orderColumnName));
                }
            }

            // sync new order to database
            using (ControlTransactionSequence controlUpdate = ControlTransactionSequence.CreateUpdate(this))
            {
                controlUpdate.AddControls(this.Controls);
                controlUpdate.Commit();
            }

            // if the control order changed rebuild the in memory table to sort it in the new order
            if (orderColumnName == Constant.ControlColumn.ControlOrder)
            {
                this.GetControlsSortedByControlOrder();
            }
        }

        protected virtual void OnDatabaseCreated(TemplateDatabase? other)
        {
            SQLiteTableSchema controlTableSchema = ControlTable.CreateSchema();
            SQLiteTableSchema imageSetTableSchema = ImageSetTable.CreateSchema();
            // set pragmas which don't have persistent effect when issued from within a transaction
            this.SetAutoVacuum(SQLiteAutoVacuum.Incremental);

            using (SQLiteTransaction transaction = this.Connection.BeginTransaction())
            {
                // set pragmas available within a transaction
                this.SetUserVersion(transaction, Constant.Release.V2_2_0_3);

                // create empty control table and image set tables
                controlTableSchema.CreateTableAndIndicies(this.Connection, transaction);
                imageSetTableSchema.CreateTableAndIndicies(this.Connection, transaction);

                if (other == null)
                {
                    // populate image set table
                    using ImageSetTransactionSequence insertImageSet = ImageSetTransactionSequence.CreateInsert(this, transaction);
                    insertImageSet.AddImageSet(new ImageSetRow()
                    {
                        InitialFolderName = Path.GetFileName(this.FolderPath),
                        Log = Constant.Database.ImageSetDefaultLog,
                        TimeZone = TimeZoneInfo.Local.Id
                    });
                }
                else
                {
                    Version otherVersion = other.GetUserVersion();
                    if (otherVersion != Constant.Release.V2_2_0_3)
                    {
                        throw new ArgumentOutOfRangeException(nameof(other), String.Format(CultureInfo.CurrentCulture, "Unexpected database version {0}.", otherVersion));
                    }

                    // if an existing template database was passed, clone its contents into this database
                    using (ControlTransactionSequence insertControls = ControlTransactionSequence.CreateInsert(this, transaction))
                    {
                        insertControls.AddControls(other.Controls);
                    }

                    Debug.Assert(other.ImageSet != null);
                    using ImageSetTransactionSequence insertImageSet = ImageSetTransactionSequence.CreateInsert(this, transaction);
                    insertImageSet.AddImageSet(other.ImageSet);
                }

                transaction.Commit();
            }

            if (other == null)
            {
                // no existing table to clone, so add standard controls to template table
                this.GetControlsSortedByControlOrder();
                int controlOrder = 0; // one based count incremented for every new control

                // standard controls
                ControlRow file = new(ControlType.Note, Constant.FileColumn.File, ++controlOrder)
                {
                    Copyable = false,
                    Label = Constant.FileColumn.File,
                    Tooltip = Constant.ControlDefault.FileTooltip
                };
                ControlRow relativePath = new(ControlType.Note, Constant.FileColumn.RelativePath, ++controlOrder)
                {
                    Copyable = false,
                    Tooltip = Constant.ControlDefault.RelativePathTooltip
                };
                ControlRow dateTime = new(ControlType.DateTime, Constant.FileColumn.DateTime, ++controlOrder)
                {
                    IndexInFileTable = true,
                    Tooltip = Constant.ControlDefault.DateTimeTooltip
                };
                ControlRow utcOffset = new(ControlType.UtcOffset, Constant.FileColumn.UtcOffset, ++controlOrder)
                {
                    Tooltip = Constant.ControlDefault.UtcOffsetTooltip
                };
                ControlRow classification = new(ControlType.FixedChoice, Constant.FileColumn.Classification, ++controlOrder)
                {
                    Copyable = false,
                    WellKnownValues = Constant.ControlDefault.ClassificationWellKnownValues,
                    Tooltip = Constant.ControlDefault.ClassificationTooltip
                };
                ControlRow deleteFlag = new(ControlType.Flag, Constant.FileColumn.DeleteFlag, ++controlOrder)
                {
                    Copyable = false,
                    Label = Constant.ControlDefault.DeleteFlagLabel,
                    Tooltip = Constant.ControlDefault.DeleteFlagTooltip
                };

                // insert standard controls into the controls table
                using ControlTransactionSequence transaction = ControlTransactionSequence.CreateInsert(this);
                transaction.AddControls(file, relativePath, dateTime, utcOffset, classification, deleteFlag);
                transaction.Commit();
            }

            // reload controls table to get updated IDs
            this.GetControlsSortedByControlOrder();
            // ensure image set is available
            this.LoadImageSet();
        }

        protected virtual bool OnExistingDatabaseOpened(TemplateDatabase? other)
        {
            List<string> tables = this.GetTableNames();
            if (tables.Contains(Constant.DatabaseTable.Controls, StringComparer.Ordinal) == false)
            {
                // if no table named Controls, then this is a database from Timelapse rather than Carnassial
                return false;
            }

            if (this.GetUserVersion() < Constant.Release.V2_2_0_3)
            {
                this.UpdateControlTableTo2203Schema();
                using SQLiteTransaction transaction = this.Connection.BeginTransaction();
                this.UpdateImageSetTo2203Schema(transaction, tables);

                if (other == null)
                {
                    // if this is a template database being opened go ahead and update its version
                    // If it's a file database, don't update the database as the caller also needs to perform updates to complete
                    // version migration.
                    this.SetUserVersion(transaction, Constant.Release.V2_2_0_3);
                }

                transaction.Commit();
            }
            else
            {
                this.GetControlsSortedByControlOrder();
            }

            this.LoadImageSet();
            return true;
        }

        private void SyncControlToDatabase(ControlRow control)
        {
            Debug.Assert(control.HasChanges, "Unexpected call to sync control without changes.");

            using ControlTransactionSequence updateControl = ControlTransactionSequence.CreateUpdate(this);
            updateControl.AddControl(control);
            updateControl.Commit();
        }

        private void UpdateControlTableTo2203Schema()
        {
            // if this is a .tdb from Carnassial 2.2.0.2 or earlier
            // - insert AnalysisLabel column in controls table
            // - convert Copyable and Visible columns from text to boolean integers
            bool addAnalysisLabel = false;
            bool addIndex = false;
            bool convertCopyableAndVisibleToInteger = false;
            bool convertTypeToInteger = false;
            bool renameList = false;
            bool renameWidth = false;
            SQLiteTableSchema currentSchema = this.GetTableSchema(Constant.DatabaseTable.Controls);
            if (currentSchema.ColumnDefinitions.SingleOrDefault(column => column.Name == Constant.ControlColumn.AnalysisLabel) == null)
            {
                addAnalysisLabel = true;
            }
            if (currentSchema.ColumnDefinitions.SingleOrDefault(column => column.Name == Constant.ControlColumn.IndexInFileTable) == null)
            {
                addIndex = true;
            }

            if (String.Equals(currentSchema.ColumnDefinitions.Single(column => String.Equals(column.Name, Constant.ControlColumn.Copyable, StringComparison.Ordinal)).Type, Constant.SQLiteAffinity.Text, StringComparison.OrdinalIgnoreCase))
            {
                convertCopyableAndVisibleToInteger = true;
            }
            if (String.Equals(currentSchema.ColumnDefinitions.Single(column => String.Equals(column.Name, Constant.ControlColumn.Type, StringComparison.Ordinal)).Type, Constant.SQLiteAffinity.Text, StringComparison.OrdinalIgnoreCase))
            {
                convertTypeToInteger = true;
            }

            #pragma warning disable CS0618 // Type or member is obsolete
            if (currentSchema.ColumnDefinitions.SingleOrDefault(column => String.Equals(column.Name, Constant.ControlColumn.List, StringComparison.Ordinal)) != null)
            #pragma warning restore CS0618 // Type or member is obsolete
            {
                renameList = true;
            }
            #pragma warning disable CS0618 // Type or member is obsolete
            if (currentSchema.ColumnDefinitions.SingleOrDefault(column => String.Equals(column.Name, Constant.ControlColumn.Width, StringComparison.Ordinal)) != null)
            #pragma warning restore CS0618 // Type or member is obsolete
            {
                renameWidth = true;
            }

            if (addAnalysisLabel || addIndex || convertCopyableAndVisibleToInteger || convertTypeToInteger || renameList || renameWidth)
            {
                using SQLiteTransaction transaction = this.Connection.BeginTransaction();
                if (addAnalysisLabel)
                {
                    ColumnDefinition analysisLabel = new(Constant.ControlColumn.AnalysisLabel, Constant.SQLiteAffinity.Integer)
                    {
                        DefaultValue = 0.ToString(Constant.InvariantCulture),
                        NotNull = true
                    };
                    this.AddColumnToTable(transaction, Constant.DatabaseTable.Controls, 6, analysisLabel);
                }
                if (addIndex)
                {
                    ColumnDefinition index = new(Constant.ControlColumn.IndexInFileTable, Constant.SQLiteAffinity.Integer)
                    {
                        DefaultValue = 0.ToString(Constant.InvariantCulture),
                        NotNull = true
                    };
                    this.AddColumnToTable(transaction, Constant.DatabaseTable.Controls, 11, index);
                }

                if (convertCopyableAndVisibleToInteger)
                {
                    this.ConvertBooleanStringColumnToInteger(transaction, Constant.DatabaseTable.Controls, Constant.ControlColumn.Copyable);
                    this.ConvertBooleanStringColumnToInteger(transaction, Constant.DatabaseTable.Controls, Constant.ControlColumn.Visible);
                }
                if (convertTypeToInteger)
                {
                    this.ConvertNonFlagEnumStringColumnToInteger<ControlType>(transaction, Constant.DatabaseTable.Controls, Constant.ControlColumn.Type);
                }

                if (renameList)
                {
#pragma warning disable CS0618 // Type or member is obsolete
                    this.RenameColumn(transaction, Constant.DatabaseTable.Controls, Constant.ControlColumn.List, Constant.ControlColumn.WellKnownValues, (ColumnDefinition columnWithNameChanged) => { });
                }
                if (renameWidth)
                {
#pragma warning disable CS0618 // Type or member is obsolete
                    this.RenameColumn(transaction, Constant.DatabaseTable.Controls, Constant.ControlColumn.Width, Constant.ControlColumn.MaxWidth, (ColumnDefinition columnWithNameChanged) =>
#pragma warning restore CS0618 // Type or member is obsolete
                    {
                        columnWithNameChanged.DefaultValue = Constant.ControlDefault.MaxWidth.ToString(Constant.InvariantCulture);
                        columnWithNameChanged.NotNull = true;
                    });
                }

                transaction.Commit();
            }

            this.GetControlsSortedByControlOrder();

            // rename image quality control to classification
            #pragma warning disable CS0618 // Type or member is obsolete
            ControlRow? imageQuality = this.Controls.SingleOrDefault(control => String.Equals(control.DataLabel, Constant.FileColumn.ImageQuality, StringComparison.Ordinal));
            #pragma warning restore CS0618 // Type or member is obsolete
            if (imageQuality != null)
            {
                imageQuality.DataLabel = Constant.FileColumn.Classification;
                this.SyncControlToDatabase(imageQuality);
            }

            // update choices and tooltip for file classification to Carnassial 2.2.0.3 schema if they're not already set as such
            ControlRow classification = this.Controls[Constant.FileColumn.Classification];
            if (String.Equals(classification.WellKnownValues, Constant.ControlDefault.ClassificationWellKnownValues, StringComparison.Ordinal) == false)
            {
                Debug.Assert(imageQuality != null);
                imageQuality.Label = Constant.FileColumn.Classification;
                classification.Tooltip = Constant.ControlDefault.ClassificationTooltip;
                classification.WellKnownValues = Constant.ControlDefault.ClassificationWellKnownValues;
                this.SyncControlToDatabase(classification);
            }

            // set index flag on date time control if it's not already set
            ControlRow dateTime = this.Controls[Constant.FileColumn.DateTime];
            if (dateTime.IndexInFileTable == false)
            {
                dateTime.IndexInFileTable = true;
                this.SyncControlToDatabase(dateTime);
            }

            // update defaults for flags to Carnassial 2.2.0.3 schema if they're not already set as such
            foreach (ControlRow flag in this.Controls.Where(control => control.ControlType == ControlType.Flag))
            {
                if (String.Equals(flag.DefaultValue, Boolean.FalseString, StringComparison.OrdinalIgnoreCase))
                {
                    flag.DefaultValue = Constant.Sql.FalseString;
                }
                else if (String.Equals(flag.DefaultValue, Boolean.TrueString, StringComparison.OrdinalIgnoreCase))
                {
                    flag.DefaultValue = Constant.Sql.TrueString;
                }
                if (String.IsNullOrEmpty(flag.WellKnownValues) || (String.Equals(flag.WellKnownValues, Constant.ControlDefault.FlagWellKnownValues, StringComparison.Ordinal) == false))
                {
                    flag.WellKnownValues = Constant.ControlDefault.FlagWellKnownValues;
                }
                if (flag.HasChanges)
                {
                    this.SyncControlToDatabase(flag);
                }
            }
        }

        private void UpdateImageSetTo2203Schema(SQLiteTransaction transaction, List<string> tables)
        {
            if (tables.Contains(Constant.DatabaseTable.ImageSet, StringComparer.Ordinal) == false)
            {
                // create default ImageSet table if this is a .tdb from Carnassial 2.2.0.1 or earlier
                SQLiteTableSchema imageSetTableSchema = ImageSetTable.CreateSchema();
                imageSetTableSchema.CreateTableAndIndicies(this.Connection, transaction);
                using ImageSetTransactionSequence insertImageSet = ImageSetTransactionSequence.CreateInsert(this, transaction);
                insertImageSet.AddImageSet(new ImageSetRow()
                {
                    InitialFolderName = Path.GetFileName(this.FolderPath),
                    Log = Constant.Database.ImageSetDefaultLog,
                    TimeZone = TimeZoneInfo.Local.Id
                });
            }
            else
            {
                // ImageSetOptions is a [Flags] enum but can be treated as a non-flag enum as of Carnassial 2.2.0.3 as
                // its only values are None (0x0) and Magnifier (0x1).
                this.ConvertNonFlagEnumStringColumnToInteger<FileSelection>(transaction, Constant.DatabaseTable.ImageSet, Constant.ImageSetColumn.FileSelection);
                this.ConvertNonFlagEnumStringColumnToInteger<ImageSetOptions>(transaction, Constant.DatabaseTable.ImageSet, Constant.ImageSetColumn.Options);
            }
        }
    }
}
