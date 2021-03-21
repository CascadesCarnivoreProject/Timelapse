using Carnassial.Database;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;

namespace Carnassial.Data
{
    public class FileFindReplace
    {
        private SearchTerm? findTerm1;
        private SearchTerm? findTerm2;
        private Func<object?, string, object, bool>? matchTerm1;
        private Func<object?, string, object, bool>? matchTerm2;
        private readonly Dictionary<string, SqlDataType> sqlDataTypeByLabel;

        public List<string> FindTerm1Labels { get; private set; }
        public List<string> FindTerm2Labels { get; private set; }
        public SearchTerm? ReplaceTerm { get; set; }

        public FileFindReplace(FileDatabase fileDatabase)
        {
            this.findTerm1 = null;
            this.FindTerm1Labels = new List<string>(fileDatabase.Controls.RowCount);
            this.findTerm2 = null;
            this.FindTerm2Labels = new List<string>(fileDatabase.Controls.RowCount + 1) { Constant.UserInterface.NoFindValue };
            this.matchTerm1 = null;
            this.matchTerm2 = null;
            this.ReplaceTerm = null;
            this.sqlDataTypeByLabel = new Dictionary<string, SqlDataType>(fileDatabase.Controls.RowCount);

            ControlRow? defaultControl = null;
            List<ControlRow> visibleControls = fileDatabase.Controls.Where(control => control.Visible).ToList();
            if (visibleControls.Count < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(fileDatabase), App.FindResource<string>(Constant.ResourceKey.FileFindReplaceNoControlsVisible));
            }

            foreach (ControlRow control in visibleControls)
            {
                if (this.FindTerm1Labels.Contains(control.Label, StringComparer.Ordinal) ||
                    String.Equals(control.Label, Constant.UserInterface.NoFindValue, StringComparison.Ordinal))
                {
                    // data labels are guaranteed to be unique but labels are not
                    // This creates a problem in that fields to find on are selected by label.  It is, however, unlikely duplicate labels
                    // exist as this creates a confusing analysis flow where two data entry controls are shown as having the same label.
                    // So, for now, treat labels as unique.  This can be revisited based on user need.
                    // Also, for now, reserve a special value for deselecting the second find term and ignore any colliding label.
                    continue;
                }

                this.FindTerm1Labels.Add(control.Label);
                this.FindTerm2Labels.Add(control.Label);
                if (control.AnalysisLabel && (defaultControl == null))
                {
                    defaultControl = control;
                }

                if (fileDatabase.Files.StandardColumnDataTypesByName.TryGetValue(control.DataLabel, out SqlDataType sqlDataType) == false)
                {
                    if (fileDatabase.Files.UserColumnsByName.TryGetValue(control.DataLabel, out FileTableColumn? fileColumn))
                    {
                        sqlDataType = fileColumn.DataType;
                    }
                    else
                    {
                        throw new ArgumentOutOfRangeException(nameof(fileDatabase), String.Format(CultureInfo.CurrentCulture, "Couldn't locate SQL data type for '{0}'.", control.DataLabel));
                    }
                }
                this.sqlDataTypeByLabel.Add(control.Label, sqlDataType);
            }
            if (defaultControl == null)
            {
                defaultControl = visibleControls[0];
            }

            SearchTerm findTerm1 = defaultControl.CreateSearchTerm();
            findTerm1.UseForSearching = true;
            this.FindTerm1 = findTerm1;
        }

        public SearchTerm? FindTerm1
        {
            get
            {
                return this.findTerm1;
            }
            set
            {
                Debug.Assert(value != null ? value.UseForSearching : true, "Search term is disabled.");
                this.findTerm1 = value;
                this.matchTerm1 = this.GetMatch(value);
            }
        }

        public SearchTerm? FindTerm2
        {
            get
            {
                return this.findTerm2;
            }
            set
            {
                Debug.Assert((value == null) || value.UseForSearching, "Search term is disabled.");
                this.findTerm2 = value;
                this.matchTerm2 = this.GetMatch(value);
            }
        }

        private Func<object?, string, object, bool>? GetMatch(SearchTerm? searchTerm)
        {
            if (searchTerm == null)
            {
                return null;
            }

            SqlDataType dataType = this.sqlDataTypeByLabel[searchTerm.Label];
            return dataType switch
            {
                SqlDataType.Boolean => this.MatchBoolean,
                SqlDataType.DateTime => this.MatchDateTime, // ideally datetimes, integers, and reals would share a Match<T> but this is not feasible with C# 7 generics
                SqlDataType.Integer => this.MatchInt32,
                SqlDataType.Real => this.MatchDouble,
                SqlDataType.String => this.MatchString,
                _ => throw new NotSupportedException(String.Format(CultureInfo.CurrentCulture, "Unhandled SQL data type {0}.", dataType)),
            };
        }

        public bool Matches(ImageRow file)
        {
            if (this.matchTerm1 == null)
            {
                return false;
            }

            Debug.Assert(this.FindTerm1 != null);
            if (this.matchTerm1(file.GetDatabaseValue(this.FindTerm1.DataLabel), this.FindTerm1.Operator, this.FindTerm1.DatabaseValue))
            {
                if (this.matchTerm2 == null)
                {
                    return true;
                }

                Debug.Assert(this.FindTerm2 != null);
                return this.matchTerm2(file.GetDatabaseValue(this.FindTerm2.DataLabel), this.FindTerm2.Operator, this.FindTerm2.DatabaseValue);
            }

            return false;
        }

        private bool MatchBoolean(object fileValueAsObject, string comparisonOperator, object findValueAsObject)
        {
            // keep in sync with FindDialog.GetOperators()
            bool fileValue = (bool)fileValueAsObject;
            bool findValue = (bool)findValueAsObject;
            if (String.Equals(comparisonOperator, Constant.SearchTermOperator.Equal, StringComparison.Ordinal))
            {
                return fileValue == findValue;
            }
            if (String.Equals(comparisonOperator, Constant.SearchTermOperator.NotEqual, StringComparison.Ordinal))
            {
                return fileValue != findValue;
            }
            throw new NotSupportedException(String.Format(CultureInfo.CurrentCulture, "Unhandled operator '{0}'.", comparisonOperator));
        }

        private bool MatchDateTime(object fileValueAsObject, string comparisonOperator, object findValueAsObject)
        {
            // keep in sync with FindDialog.GetOperators()
            DateTime fileValue = (DateTime)fileValueAsObject;
            DateTime findValue = (DateTime)findValueAsObject;
            if (String.Equals(comparisonOperator, Constant.SearchTermOperator.Equal, StringComparison.Ordinal))
            {
                return fileValue == findValue;
            }
            if (String.Equals(comparisonOperator, Constant.SearchTermOperator.NotEqual, StringComparison.Ordinal))
            {
                return fileValue != findValue;
            }
            if (String.Equals(comparisonOperator, Constant.SearchTermOperator.GreaterThan, StringComparison.Ordinal))
            {
                return fileValue > findValue;
            }
            if (String.Equals(comparisonOperator, Constant.SearchTermOperator.GreaterThanOrEqual, StringComparison.Ordinal))
            {
                return fileValue >= findValue;
            }
            if (String.Equals(comparisonOperator, Constant.SearchTermOperator.LessThan, StringComparison.Ordinal))
            {
                return fileValue < findValue;
            }
            if (String.Equals(comparisonOperator, Constant.SearchTermOperator.LessThanOrEqual, StringComparison.Ordinal))
            {
                return fileValue <= findValue;
            }
            throw new NotSupportedException(String.Format(CultureInfo.CurrentCulture, "Unhandled operator '{0}'.", comparisonOperator));
        }

        private bool MatchDouble(object fileValueAsObject, string comparisonOperator, object findValueAsObject)
        {
            // keep in sync with FindDialog.GetOperators()
            double fileValue = (double)fileValueAsObject;
            double findValue = (double)findValueAsObject;
            if (String.Equals(comparisonOperator, Constant.SearchTermOperator.Equal, StringComparison.Ordinal))
            {
                return fileValue == findValue;
            }
            if (String.Equals(comparisonOperator, Constant.SearchTermOperator.NotEqual, StringComparison.Ordinal))
            {
                return fileValue != findValue;
            }
            if (String.Equals(comparisonOperator, Constant.SearchTermOperator.GreaterThan, StringComparison.Ordinal))
            {
                return fileValue > findValue;
            }
            if (String.Equals(comparisonOperator, Constant.SearchTermOperator.GreaterThanOrEqual, StringComparison.Ordinal))
            {
                return fileValue >= findValue;
            }
            if (String.Equals(comparisonOperator, Constant.SearchTermOperator.LessThan, StringComparison.Ordinal))
            {
                return fileValue < findValue;
            }
            if (String.Equals(comparisonOperator, Constant.SearchTermOperator.LessThanOrEqual, StringComparison.Ordinal))
            {
                return fileValue <= findValue;
            }
            throw new NotSupportedException(String.Format(CultureInfo.CurrentCulture, "Unhandled operator '{0}'.", comparisonOperator));
        }

        private bool MatchInt32(object fileValueAsObject, string comparisonOperator, object findValueAsObject)
        {
            // keep in sync with FindDialog.GetOperators()
            int fileValue = (int)fileValueAsObject;
            int findValue = (int)findValueAsObject;
            if (String.Equals(comparisonOperator, Constant.SearchTermOperator.Equal, StringComparison.Ordinal))
            {
                return fileValue == findValue;
            }
            if (String.Equals(comparisonOperator, Constant.SearchTermOperator.NotEqual, StringComparison.Ordinal))
            {
                return fileValue != findValue;
            }
            if (String.Equals(comparisonOperator, Constant.SearchTermOperator.GreaterThan, StringComparison.Ordinal))
            {
                return fileValue > findValue;
            }
            if (String.Equals(comparisonOperator, Constant.SearchTermOperator.GreaterThanOrEqual, StringComparison.Ordinal))
            {
                return fileValue >= findValue;
            }
            if (String.Equals(comparisonOperator, Constant.SearchTermOperator.LessThan, StringComparison.Ordinal))
            {
                return fileValue < findValue;
            }
            if (String.Equals(comparisonOperator, Constant.SearchTermOperator.LessThanOrEqual, StringComparison.Ordinal))
            {
                return fileValue <= findValue;
            }
            throw new NotSupportedException(String.Format(CultureInfo.CurrentCulture, "Unhandled operator '{0}'.", comparisonOperator));
        }

        private bool MatchString(object fileValueAsObject, string comparisonOperator, object findValueAsObject)
        {
            // keep in sync with FindDialog.GetOperators()
            // For now, does not support GLOB.  Update FindReplace.RebuildFindField() if GLOB support is added.
            string fileValue = (string)fileValueAsObject;
            string findValue = (string)findValueAsObject;
            if (String.Equals(comparisonOperator, Constant.SearchTermOperator.Equal, StringComparison.Ordinal))
            {
                return String.Equals(fileValue, findValue, StringComparison.Ordinal);
            }
            if (String.Equals(comparisonOperator, Constant.SearchTermOperator.NotEqual, StringComparison.Ordinal))
            {
                return !String.Equals(fileValue, findValue, StringComparison.Ordinal);
            }
            if (String.Equals(comparisonOperator, Constant.SearchTermOperator.GreaterThan, StringComparison.Ordinal))
            {
                return String.Compare(fileValue, findValue, StringComparison.Ordinal) > 0;
            }
            if (String.Equals(comparisonOperator, Constant.SearchTermOperator.GreaterThanOrEqual, StringComparison.Ordinal))
            {
                return String.Compare(fileValue, findValue, StringComparison.Ordinal) >= 0;
            }
            if (String.Equals(comparisonOperator, Constant.SearchTermOperator.LessThan, StringComparison.Ordinal))
            {
                return String.Compare(fileValue, findValue, StringComparison.Ordinal) < 0;
            }
            if (String.Equals(comparisonOperator, Constant.SearchTermOperator.LessThanOrEqual, StringComparison.Ordinal))
            {
                return String.Compare(fileValue, findValue, StringComparison.Ordinal) <= 0;
            }
            throw new NotSupportedException(String.Format(CultureInfo.CurrentCulture, "Unhandled operator '{0}'.", comparisonOperator));
        }

        public bool TryReplace(ImageRow file)
        {
            if ((this.FindTerm1 == null) || (this.ReplaceTerm == null))
            {
                return false;
            }

            file[this.ReplaceTerm.DataLabel] = this.ReplaceTerm.DatabaseValue;
            return true;
        }
    }
}
