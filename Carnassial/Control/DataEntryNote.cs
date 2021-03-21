using Carnassial.Data;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Controls;

namespace Carnassial.Control
{
    // A note lays out as
    // - a label containing the descriptive label) 
    // - an editable textbox (containing the content) at the given width
    public class DataEntryNote : DataEntryControl<AutocompleteTextBox, Label>
    {
        private List<string> autocompletionsFromList;

        public override bool ContentReadOnly
        {
            get { return this.ContentControl.IsReadOnly; }
            set { this.ContentControl.IsReadOnly = value; }
        }

        public override ImageRow? DataContext
        {
            get
            {
                return (ImageRow)this.ContentControl.DataContext;
            }
            set
            {
                this.ContentControl.SuppressAutocompletion = true;
                this.ContentControl.DataContext = value;
                this.ContentControl.SuppressAutocompletion = false;
            }
        }

        public DataEntryNote(ControlRow control, List<string>? autocompletionsFromDatabase, bool readOnly, DataEntryControls styleProvider)
            : base(control, styleProvider, ControlContentStyle.NoteCounterTextBox, ControlLabelStyle.Label, readOnly)
        {
            this.SetWellKnownValues(control.GetWellKnownValues());
            this.autocompletionsFromList = control.GetWellKnownValues();
            if (this.autocompletionsFromList.Contains(control.DefaultValue, StringComparer.Ordinal) == false)
            {
                this.autocompletionsFromList.Add(control.DefaultValue);
            }
            this.MergeAutocompletions(autocompletionsFromDatabase);

            this.ContentControl.SetBinding(AutocompleteTextBox.TextProperty, ImageRow.GetDataBindingPath(control));
        }

        public override List<string> GetWellKnownValues()
        {
            return this.ContentControl.Autocompletions;
        }

        public void MergeAutocompletions(List<string>? autocompletionsFromDatabase)
        {
            if (autocompletionsFromDatabase == null)
            {
                this.ContentControl.Autocompletions = this.autocompletionsFromList;
            }
            else
            {
                this.ContentControl.Autocompletions = this.autocompletionsFromList.Union(autocompletionsFromDatabase).Distinct().ToList();
            }
        }

        public override void SetWellKnownValues(List<string> wellKnownValues)
        {
            this.autocompletionsFromList = wellKnownValues;
            this.MergeAutocompletions(null);
        }

        public override void SetValue(object valueAsObject)
        {
            string? valueAsString;
            if ((valueAsObject is string) || (valueAsObject == null))
            {
                valueAsString = (string?)valueAsObject;
            }
            else
            {
                if (valueAsObject == null)
                {
                    throw new ArgumentNullException(nameof(valueAsObject));
                }
                throw new ArgumentOutOfRangeException(nameof(valueAsObject), String.Format(CultureInfo.CurrentCulture, "Unsupported value type {0}.", valueAsObject.GetType()));
            }

            this.ContentControl.SuppressAutocompletion = true;
            this.ContentControl.Text = valueAsString;
            this.ContentControl.ToolTip = valueAsString;
            this.ContentControl.SuppressAutocompletion = false;
        }
    }
}
