using Carnassial.Database;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Data.SQLite;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;

namespace Carnassial.Data
{
    // implements IList and INotifyCollectionChanged for use with data binding
    // WPF's DataGrid requires IList as it does not support IList<T>.
    public class ControlTable : SQLiteTable<ControlRow>, IList, INotifyCollectionChanged
    {
        private readonly Dictionary<string, ControlRow> controlsByDataLabel;

        public event NotifyCollectionChangedEventHandler? CollectionChanged;

        public ControlTable()
        {
            this.controlsByDataLabel = new Dictionary<string, ControlRow>(StringComparer.Ordinal);
        }

        int ICollection.Count
        {
            get { return this.RowCount; }
        }

        bool ICollection.IsSynchronized
        {
            get { return false; }
        }

        object ICollection.SyncRoot
        {
            get { throw new NotSupportedException(); }
        }

        object? IList.this[int index]
        {
            get
            {
                return this.Rows[index];
            }
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }
                ControlRow control = this.Rows[index];
                this.Rows[index] = (ControlRow)value;
                this.CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, value, control, index));
            }
        }

        bool IList.IsFixedSize
        {
            get { return false; }
        }

        public bool IsReadOnly
        {
            get { return false; }
        }

        void ICollection.CopyTo(Array array, int arrayIndex)
        {
            ((IList)this.Rows).CopyTo(array, arrayIndex);
        }

        public static SQLiteTableSchema CreateSchema()
        {
            SQLiteTableSchema schema = new(Constant.DatabaseTable.Controls);
            schema.ColumnDefinitions.Add(ColumnDefinition.CreatePrimaryKey());
            schema.ColumnDefinitions.Add(new ColumnDefinition(Constant.ControlColumn.Type, Constant.SQLiteAffinity.Integer)
            {
                NotNull = true
            });
            schema.ColumnDefinitions.Add(new ColumnDefinition(Constant.ControlColumn.DataLabel, Constant.SQLiteAffinity.Text)
            {
                NotNull = true
            });
            schema.ColumnDefinitions.Add(new ColumnDefinition(Constant.ControlColumn.Label, Constant.SQLiteAffinity.Text)
            {
                NotNull = true
            });
            schema.ColumnDefinitions.Add(new ColumnDefinition(Constant.ControlColumn.ControlOrder, Constant.SQLiteAffinity.Integer)
            {
                NotNull = true
            });
            schema.ColumnDefinitions.Add(new ColumnDefinition(Constant.ControlColumn.SpreadsheetOrder, Constant.SQLiteAffinity.Integer)
            {
                NotNull = true
            });
            schema.ColumnDefinitions.Add(new ColumnDefinition(Constant.ControlColumn.WellKnownValues, Constant.SQLiteAffinity.Text));
            schema.ColumnDefinitions.Add(new ColumnDefinition(Constant.ControlColumn.DefaultValue, Constant.SQLiteAffinity.Text));
            schema.ColumnDefinitions.Add(new ColumnDefinition(Constant.ControlColumn.Tooltip, Constant.SQLiteAffinity.Text));
            schema.ColumnDefinitions.Add(ColumnDefinition.CreateBoolean(Constant.ControlColumn.AnalysisLabel));
            schema.ColumnDefinitions.Add(ColumnDefinition.CreateBoolean(Constant.ControlColumn.Copyable));
            schema.ColumnDefinitions.Add(ColumnDefinition.CreateBoolean(Constant.ControlColumn.IndexInFileTable));
            schema.ColumnDefinitions.Add(ColumnDefinition.CreateBoolean(Constant.ControlColumn.Visible));
            schema.ColumnDefinitions.Add(new ColumnDefinition(Constant.ControlColumn.MaxWidth, Constant.SQLiteAffinity.Integer)
            {
                DefaultValue = Constant.ControlDefault.MaxWidth.ToString(Constant.InvariantCulture),
                NotNull = true
            });
            return schema;
        }

        int IList.Add(object? control)
        {
            int index = ((IList)this.Rows).Add(control);
            this.CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, control, index));
            return index;
        }

        void IList.Clear()
        {
            this.Rows.Clear();
            this.CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        bool IList.Contains(object? control)
        {
            if (control == null)
            {
                throw new ArgumentNullException(nameof(control));
            }
            return this.Rows.Contains((ControlRow)control);
        }

        int IList.IndexOf(object? control)
        {
            if (control == null)
            {
                throw new ArgumentNullException(nameof(control));
            }
            return this.Rows.IndexOf((ControlRow)control);
        }

        void IList.Insert(int index, object? control)
        {
            if (control == null)
            {
                throw new ArgumentNullException(nameof(control));
            }
            this.Rows.Insert(index, (ControlRow)control);
            this.CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, control, index));
        }

        void IList.Remove(object? control)
        {
            if (control == null)
            {
                throw new ArgumentNullException(nameof(control));
            }
            bool removed = this.Rows.Remove((ControlRow)control);
            if (removed)
            {
                this.CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, control));
            }
        }

        void IList.RemoveAt(int index)
        {
            ControlRow control = this.Rows[index];
            this.Rows.RemoveAt(index);
            this.CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, control, index));
        }

        public ControlRow this[string dataLabel]
        {
            get { return this.controlsByDataLabel[dataLabel]; }
        }

        public IEnumerable<ControlRow> InSpreadsheetOrder()
        {
            return this.OrderBy(control => control.SpreadsheetOrder);
        }

        public override void Load(SQLiteDataReader reader)
        {
            int analysisLabelIndex = -1;
            int controlOrderIndex = -1;
            int copyableIndex = -1;
            int dataLabelIndex = -1;
            int defaultValueIndex = -1;
            int idIndex = -1;
            int labelFileIndex = -1;
            int listIndex = -1;
            int indexIndex = -1;
            int maxWidthIndex = -1;
            int spreadsheetOrderIndex = -1;
            int tooltipIndex = -1;
            int typeIndex = -1;
            int visibleIndex = -1;
            for (int column = 0; column < reader.FieldCount; ++column)
            {
                string columnName = reader.GetName(column);
                switch (columnName)
                {
                    case Constant.ControlColumn.AnalysisLabel:
                        analysisLabelIndex = column;
                        break;
                    case Constant.ControlColumn.ControlOrder:
                        controlOrderIndex = column;
                        break;
                    case Constant.ControlColumn.Copyable:
                        copyableIndex = column;
                        break;
                    case Constant.ControlColumn.DataLabel:
                        dataLabelIndex = column;
                        break;
                    case Constant.ControlColumn.DefaultValue:
                        defaultValueIndex = column;
                        break;
                    case Constant.DatabaseColumn.ID:
                        idIndex = column;
                        break;
                    case Constant.ControlColumn.IndexInFileTable:
                        indexIndex = column;
                        break;
                    case Constant.ControlColumn.Label:
                        labelFileIndex = column;
                        break;
                    case Constant.ControlColumn.WellKnownValues:
                        listIndex = column;
                        break;
                    case Constant.ControlColumn.SpreadsheetOrder:
                        spreadsheetOrderIndex = column;
                        break;
                    case Constant.ControlColumn.Tooltip:
                        tooltipIndex = column;
                        break;
                    case Constant.ControlColumn.Type:
                        typeIndex = column;
                        break;
                    case Constant.ControlColumn.Visible:
                        visibleIndex = column;
                        break;
                    case Constant.ControlColumn.MaxWidth:
                        maxWidthIndex = column;
                        break;
                    default:
                        throw new NotSupportedException(String.Format(CultureInfo.CurrentCulture, "Unhandled column '{0}' in {1} table schema.", columnName, reader.GetTableName(0)));
                }
            }

            bool allStandardColumnsPresent = (analysisLabelIndex != -1) &&
                                             (controlOrderIndex != -1) &&
                                             (copyableIndex != -1) &&
                                             (dataLabelIndex != -1) &&
                                             (defaultValueIndex != -1) &&
                                             (idIndex != -1) &&
                                             (indexIndex != -1) &&
                                             (labelFileIndex != -1) &&
                                             (listIndex != -1) &&
                                             (maxWidthIndex != -1) &&
                                             (spreadsheetOrderIndex != -1) &&
                                             (tooltipIndex != -1) &&
                                             (typeIndex != -1) &&
                                             (visibleIndex != -1);
            if (allStandardColumnsPresent == false)
            {
                throw new SQLiteException(SQLiteErrorCode.Schema, "At least one standard column is missing from table " + reader.GetTableName(0));
            }

            this.controlsByDataLabel.Clear();
            this.Rows.Clear();

            while (reader.Read())
            {
                // read file values
                ControlRow control = new((ControlType)reader.GetInt32(typeIndex), reader.GetString(dataLabelIndex), reader.GetInt32(controlOrderIndex))
                {
                    AnalysisLabel = reader.GetBoolean(analysisLabelIndex),
                    Copyable = reader.GetBoolean(copyableIndex),
                    DefaultValue = reader.GetString(defaultValueIndex),
                    ID = reader.GetInt64(idIndex),
                    IndexInFileTable = reader.GetBoolean(indexIndex),
                    Label = reader.GetString(labelFileIndex),
                    WellKnownValues = reader.GetString(listIndex),
                    MaxWidth = reader.GetInt32(maxWidthIndex),
                    SpreadsheetOrder = reader.GetInt32(spreadsheetOrderIndex),
                    Tooltip = reader.GetString(tooltipIndex),
                    Visible = reader.GetBoolean(visibleIndex)
                };
                control.AcceptChanges();
                control.PropertyChanged += this.OnControlFieldChanged;

                this.controlsByDataLabel.Add(control.DataLabel, control);
                this.Rows.Add(control);
            }

            this.CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        private void OnControlFieldChanged(object? sender, PropertyChangedEventArgs controlChange)
        {
            if (String.Equals(controlChange.PropertyName, nameof(ControlRow.DataLabel), StringComparison.Ordinal) == false)
            {
                // nothing to do
                return;
            }
            if (sender == null)
            {
                throw new ArgumentNullException(nameof(sender));
            }

            // if data label changed, update index in controlsByDataLabel
            ControlRow control = (ControlRow)sender;
            string? oldDataLabel = null;
            foreach (KeyValuePair<string, ControlRow> controlAndDataLabel in this.controlsByDataLabel)
            {
                if (Object.ReferenceEquals(control, controlAndDataLabel.Value))
                {
                    oldDataLabel = controlAndDataLabel.Key;
                    break;
                }
            }
            Debug.Assert(oldDataLabel != null, "Receved data label change notification for control not in control dictionary.");
            this.controlsByDataLabel.Remove(oldDataLabel);
            this.controlsByDataLabel.Add(control.DataLabel, control);
        }

        public bool TryGet(string dataLabel, [NotNullWhen(true)] out ControlRow? control)
        {
            return this.controlsByDataLabel.TryGetValue(dataLabel, out control);
        }
    }
}
