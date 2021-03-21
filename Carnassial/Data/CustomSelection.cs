using Carnassial.Database;
using Carnassial.Util;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Carnassial.Data
{
    /// <summary>
    /// A list of search terms containing criteria for a given field.
    /// </summary>
    public class CustomSelection
    {
        public List<SearchTerm> SearchTerms { get; private set; }
        public LogicalOperator TermCombiningOperator { get; set; }

        public CustomSelection(ControlTable controls, LogicalOperator termCombiningOperator)
        {
            this.SearchTerms = new List<SearchTerm>();
            this.TermCombiningOperator = termCombiningOperator;

            // generate search terms as a starting point for custom selections
            // The initial set of search terms corresponds to
            // 
            // - controls which indicate they're likely to be important
            // - controls which are visible and copyable
            // 
            // Copyable is mild form of an importance hint, as copyable values are ones which are included in analysis slots
            // and may be bulk edited during data entry.  Hidden controls are skipped as they're not normally part of the user
            // experience (marking them hidden is often a form of deprecation).  Search terms for hidden or non-copyable controls
            // can be set up manually if  needed.
            foreach (ControlRow control in controls)
            {
                SearchTerm searchTerm = control.CreateSearchTerm();
                if (searchTerm.ImportanceHint || (control.Copyable && control.Visible))
                {
                    this.SearchTerms.Add(searchTerm);
                }
            }
        }

        public CustomSelection(CustomSelection other)
        {
            this.SearchTerms = new List<SearchTerm>(other.SearchTerms.Capacity);
            this.TermCombiningOperator = other.TermCombiningOperator;

            foreach (SearchTerm searchTerm in other.SearchTerms)
            {
                this.SearchTerms.Add(searchTerm.Clone());
            }
        }

        // create and return the query formed by the search term list
        public Select CreateSelect()
        {
            Select select = new(Constant.DatabaseTable.Files)
            {
                WhereCombiningOperator = this.TermCombiningOperator
            };
            foreach (SearchTerm searchTerm in this.SearchTerms.Where(term => term.UseForSearching))
            {
                select.Where.Add(searchTerm.GetWhereClause());
            }

            return select;
        }

        public override bool Equals(object? obj)
        {
            if (obj is CustomSelection customSelection)
            {
                return this.Equals(customSelection);
            }
            return false;
        }

        public bool Equals(CustomSelection? other)
        {
            if (other == null)
            {
                return false;
            }
            if (Object.ReferenceEquals(this, other))
            {
                return true;
            }

            if (this.SearchTerms.Count != other.SearchTerms.Count)
            {
                return false;
            }
            if (this.TermCombiningOperator != other.TermCombiningOperator)
            {
                return false;
            }

            for (int searchTermIndex = 0; searchTermIndex < this.SearchTerms.Count; ++searchTermIndex)
            {
                if (this.SearchTerms[searchTermIndex].Equals(other.SearchTerms[searchTermIndex]) == false)
                {
                    return false;
                }
            }

            return true;
        }

        public override int GetHashCode()
        {
            int hash = this.TermCombiningOperator.GetHashCode();
            foreach (SearchTerm searchTerm in this.SearchTerms)
            {
                Utilities.CombineHashCodes(hash, searchTerm.GetHashCode());
            }
            return hash;
        }

        public void SetDateTimesAndOffset(DateTimeOffset dateTimeOffset)
        {
            foreach (SearchTerm dateTimeTerm in this.SearchTerms.Where(term => String.Equals(term.DataLabel, Constant.FileColumn.DateTime, StringComparison.Ordinal)))
            {
                dateTimeTerm.DatabaseValue = dateTimeOffset.UtcDateTime;
            }

            SearchTerm? utcOffsetTerm = this.SearchTerms.FirstOrDefault(term => String.Equals(term.DataLabel, Constant.FileColumn.UtcOffset, StringComparison.Ordinal));
            if (utcOffsetTerm != null)
            {
                utcOffsetTerm.DatabaseValue = DateTimeHandler.ToDatabaseUtcOffset(dateTimeOffset.Offset);
            }
        }
    }
}
