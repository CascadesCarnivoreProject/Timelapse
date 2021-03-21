using Carnassial.Database;
using Carnassial.Util;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Carnassial.Data
{
    /// <summary>
    /// A SearchTerm stores the search criteria for a field.
    /// </summary>
    public class SearchTerm : INotifyPropertyChanged
    {
        private ControlType controlType;
        private Type databaseColumnType;
        private object? databaseValue;
        private string? dataLabel;
        private string? label;
        private bool importanceHint;
        private string op;
        private Lazy<ObservableCollection<object>> wellKnownValues;
        private bool useForSearching;

        public event PropertyChangedEventHandler? PropertyChanged;

        public SearchTerm(ControlRow control)
        {
            this.useForSearching = false;
            // all other fields are initialized in SetControl()

            this.SetControl(control);
        }

        protected SearchTerm(SearchTerm other)
        {
            this.ControlType = other.ControlType;
            this.databaseColumnType = other.databaseColumnType;
            this.databaseValue = other.databaseValue;
            this.DataLabel = other.DataLabel;
            this.Label = other.Label;
            this.Operator = other.Operator;
            this.UseForSearching = other.UseForSearching;
            this.wellKnownValues = other.wellKnownValues;
        }

        public ControlType ControlType
        {
            get
            {
                return this.controlType;
            }
            private set
            {
                if (this.controlType != value)
                {
                    this.controlType = value;
                    this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.ControlType)));
                }
            }
        }

        public object DatabaseValue
        {
            get
            {
                return this.databaseValue;
            }
            set
            {
                object databaseValue = this.ConvertDatabaseValue(value);
                if (Object.Equals(this.databaseValue, databaseValue) == false)
                {
                    this.databaseValue = databaseValue;
                    this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.DatabaseValue)));
                }
            }
        }

        public string DataLabel
        {
            get
            {
                return this.dataLabel;
            }
            private set
            {
                if (String.Equals(this.dataLabel, value, StringComparison.Ordinal) == false)
                {
                    this.dataLabel = value;
                    this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.DataLabel)));
                }
            }
        }

        public string Label
        {
            get
            {
                return this.label;
            }
            private set
            {
                if (String.Equals(this.label, value, StringComparison.Ordinal) == false)
                {
                    this.label = value;
                    this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.Label)));
                }
            }
        }

        public bool ImportanceHint
        {
            get
            {
                return this.importanceHint;
            }
            private set
            {
                if (this.importanceHint != value)
                {
                    this.importanceHint = value;
                    this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.ImportanceHint)));
                }
            }
        }

        public string Operator
        {
            get
            {
                return this.op;
            }
            set
            {
                if (String.Equals(this.op, value, StringComparison.Ordinal) == false)
                {
                    this.op = value;
                    this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.Operator)));
                }
            }
        }

        public bool UseForSearching
        {
            get
            {
                return this.useForSearching;
            }
            set
            {
                if (this.useForSearching != value)
                {
                    this.useForSearching = value;
                    this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.UseForSearching)));
                }
            }
        }

        public ObservableCollection<object> WellKnownValues
        {
            get { return this.wellKnownValues.Value; }
        }

        public SearchTerm Clone()
        {
            return new SearchTerm(this);
        }

        protected object ConvertDatabaseValue(object value)
        {
            if (value is string valueAsString)
            {
                return this.ConvertWellKnownValue(valueAsString);
            }

            if (this.databaseColumnType == typeof(DateTime))
            {
                DateTime dateTime = (DateTime)value;
                if (dateTime.Kind != DateTimeKind.Utc)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), App.FindResource<string>(Constant.ResourceKey.SearchTermDateTimeNotUtc));
                }
                return dateTime;
            }

            if (value == null)
            {
                if (this.databaseColumnType.IsByRef)
                {
                    return null;
                }
                throw new ArgumentOutOfRangeException(nameof(value), String.Format(CultureInfo.CurrentCulture, "Cannot convert null to database column type {0}.", this.databaseColumnType.Name));
            }

            Type typeOfValue = value.GetType();
            if (this.databaseColumnType != typeOfValue)
            {
                throw new ArgumentOutOfRangeException(nameof(value), String.Format(CultureInfo.CurrentCulture, "Cannot convert {0} to database column type {1}.", typeOfValue, this.databaseColumnType.Name));
            }

            return value;
        }

        protected object ConvertWellKnownValue(string value)
        {
            if (this.databaseColumnType == typeof(bool))
            {
                int valueAsInt = Int32.Parse(value, NumberStyles.None, Constant.InvariantCulture);
                if (valueAsInt == 0)
                {
                    return false;
                }
                if (valueAsInt == 1)
                {
                    return true;
                }
                throw new ArgumentOutOfRangeException(nameof(value), App.FindResource<string>(Constant.ResourceKey.SearchTermInvalidBoolean));
            }
            if (this.databaseColumnType == typeof(DateTime))
            {
                return DateTimeHandler.ParseDatabaseDateTime(value);
            }
            if (this.databaseColumnType == typeof(FileClassification))
            {
                if (ImageRow.TryParseFileClassification(value, out FileClassification classification))
                {
                    return classification;
                }
                throw new ArgumentOutOfRangeException(nameof(value), String.Format(CultureInfo.CurrentCulture, "Unknown file classification '{0}'.", value));
            }
            if (this.databaseColumnType == typeof(int))
            {
                return Int32.Parse(value, NumberStyles.AllowLeadingSign, Constant.InvariantCulture);
            }
            if (this.databaseColumnType == typeof(string))
            {
                return value;
            }
            if (this.databaseColumnType == typeof(double))
            {
                return DateTimeHandler.ToDatabaseUtcOffset(DateTimeHandler.ParseDisplayUtcOffsetString(value));
            }

            throw new NotSupportedException(String.Format(CultureInfo.CurrentCulture, "Unhandled type {0}.", this.databaseColumnType));
        }

        public override bool Equals(object? obj)
        {
            if (obj is SearchTerm searchTerm)
            {
                return this.Equals(searchTerm);
            }
            return false;
        }

        public bool Equals(SearchTerm other)
        {
            if (other == null)
            {
                return false;
            }
            if (Object.ReferenceEquals(this, other))
            {
                return true;
            }

            if (this.ControlType != other.ControlType)
            {
                return false;
            }
            if (this.DatabaseValue != other.DatabaseValue)
            {
                return false;
            }
            if (String.Equals(this.DataLabel, other.DataLabel, StringComparison.Ordinal) == false)
            {
                return false;
            }
            if (String.Equals(this.Label, other.Label, StringComparison.Ordinal) == false)
            {
                return false;
            }
            if (String.Equals(this.Operator, other.Operator, StringComparison.Ordinal) == false)
            {
                return false;
            }
            if (this.UseForSearching != other.UseForSearching)
            {
                return false;
            }
            // this.WellKnownValues is excluded as doesn't affect the query fragment the term generates

            return true;
        }

        public override int GetHashCode()
        {
            // this.WellKnownValues is excluded as doesn't affect the query fragment the term generates
            return Utilities.CombineHashCodes(this.ControlType, this.DatabaseValue, this.DataLabel, this.Label, this.Operator, this.UseForSearching);
        }

        public List<string> GetOperators()
        {
            // keep in sync with FileFindReplace.Match*() and SearchTermPicker.LabelBox_SelectionChanged()
            return this.ControlType switch
            {
                ControlType.Counter or 
                ControlType.DateTime or 
                ControlType.FixedChoice => new List<string>()
                    {
                        Constant.SearchTermOperator.Equal,
                        Constant.SearchTermOperator.NotEqual,
                        Constant.SearchTermOperator.LessThan,
                        Constant.SearchTermOperator.GreaterThan,
                        Constant.SearchTermOperator.LessThanOrEqual,
                        Constant.SearchTermOperator.GreaterThanOrEqual
                    },
                ControlType.Flag => new List<string>()
                    {
                        Constant.SearchTermOperator.Equal,
                        Constant.SearchTermOperator.NotEqual
                    },
                _ => new List<string>()
                    {
                        Constant.SearchTermOperator.Equal,
                        Constant.SearchTermOperator.NotEqual,
                        Constant.SearchTermOperator.LessThan,
                        Constant.SearchTermOperator.GreaterThan,
                        Constant.SearchTermOperator.LessThanOrEqual,
                        Constant.SearchTermOperator.GreaterThanOrEqual,
                        Constant.SearchTermOperator.Glob
                    },// notes
            };
        }

        public WhereClause GetWhereClause()
        {
            string sqlOperator = this.Operator switch
            {
                Constant.SearchTermOperator.Equal => Constant.SqlOperator.Equal,
                Constant.SearchTermOperator.Glob => Constant.SqlOperator.Glob,
                Constant.SearchTermOperator.GreaterThan => Constant.SqlOperator.GreaterThan,
                Constant.SearchTermOperator.GreaterThanOrEqual => Constant.SqlOperator.GreaterThanOrEqual,
                Constant.SearchTermOperator.LessThan => Constant.SqlOperator.LessThan,
                Constant.SearchTermOperator.LessThanOrEqual => Constant.SqlOperator.LessThanOrEqual,
                Constant.SearchTermOperator.NotEqual => Constant.SqlOperator.NotEqual,
                _ => throw new NotSupportedException(String.Format(CultureInfo.CurrentCulture, "Unhandled search term operator '{0}'.", this.Operator)),
            };
            return new WhereClause(this.DataLabel, sqlOperator, this.DatabaseValue);
        }

        [MemberNotNull(nameof(databaseColumnType))]
        [MemberNotNull(nameof(op))]
        [MemberNotNull(nameof(wellKnownValues))]
        public void SetControl(ControlRow control)
        {
            // set fields directly rather than through properties to suppress per-field change notifications
            this.controlType = control.ControlType;
            this.databaseColumnType = control.GetDatabaseColumnType();
            this.dataLabel = control.DataLabel;
            this.databaseValue = control.GetDefaultDatabaseValue();
            this.label = control.Label;
            this.importanceHint = control.AnalysisLabel;
            this.op = Constant.SearchTermOperator.Equal;
            if (this.controlType == ControlType.Counter)
            {
                this.op = Constant.SearchTermOperator.GreaterThan;
            }
            else if (this.controlType == ControlType.DateTime)
            {
                this.op = Constant.SearchTermOperator.GreaterThanOrEqual;
            }
            // leave this.useForSearching unchanged
            // This method is only called at construction and when the user retargets a SearchTerm to a different database column. 
            // In the latter case, modifying UseForSearching is most likely not the user's intent.
            this.wellKnownValues = new Lazy<ObservableCollection<object>>(() =>
            {
                List<string> wellKnownStrings = control.GetWellKnownValues();
                if (String.Equals(this.DataLabel, Constant.FileColumn.Classification, StringComparison.Ordinal))
                {
                    // remove duplicate Color entry due to Ok being included as a well known value
                    wellKnownStrings.Remove(FileClassification.Color.ToString());
                }

                ObservableCollection<object> wellKnownValues = new();
                foreach (string wellKnownString in wellKnownStrings)
                {
                    wellKnownValues.Add(this.ConvertWellKnownValue(wellKnownString));
                }
                return wellKnownValues;
            });

            // send whole object changed notification
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
        }

        public override string ToString()
        {
            string displayValue;
            if (this.databaseColumnType == typeof(bool))
            {
                displayValue = (bool)this.DatabaseValue ? Boolean.TrueString : Boolean.FalseString;
            }
            else if (this.databaseColumnType == typeof(DateTime))
            {
                displayValue = DateTimeHandler.ToDisplayDateTimeString((DateTime)this.DatabaseValue);
            }
            else if ((this.databaseColumnType == typeof(FileClassification)) ||
                     (this.databaseColumnType == typeof(int)))
            {
                displayValue = this.DatabaseValue.ToString();
            }
            else if (this.databaseColumnType == typeof(string))
            {
                displayValue = (string)this.DatabaseValue;
            }
            else if (this.databaseColumnType == typeof(double))
            {
                displayValue = DateTimeHandler.ToDisplayUtcOffsetString(DateTimeHandler.FromDatabaseUtcOffset((double)this.DatabaseValue));
            }
            else
            {
                throw new NotSupportedException(String.Format(CultureInfo.CurrentCulture, "Unhandled type {0}.", this.databaseColumnType));
            }

            return this.DataLabel + " " + this.Operator + " \"" + displayValue + "\"";
        }
    }
}
