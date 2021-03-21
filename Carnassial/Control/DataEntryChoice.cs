using Carnassial.Data;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Input;

namespace Carnassial.Control
{
    // A choice comprises
    // - a label containing the descriptive label) 
    // - a combobox (containing the content) at the given width
    public class DataEntryChoice : DataEntryControl<ComboBox, Label>
    {
        public override bool ContentReadOnly
        {
            get { return this.ContentControl.IsReadOnly; }
            set { this.ContentControl.IsReadOnly = value; }
        }

        public DataEntryChoice(ControlRow control, DataEntryControls styleProvider)
            : base(control, styleProvider, ControlContentStyle.ChoiceComboBox, ControlLabelStyle.Label)
        {
            // callback used to allow enter to select the highlit item
            this.ContentControl.PreviewKeyDown += this.ContentControl_PreviewKeyDown;

            // add items to the combo box
            this.SetWellKnownValues(control.GetWellKnownValues());

            this.ContentControl.SetBinding(ComboBox.SelectedItemProperty, ImageRow.GetDataBindingPath(control));
        }

        // Users may want to use the text search facility on the combobox, where they type the first letter and then enter
        // For some reason, it wasn't working on pressing 'enter' so this handler does that.
        // Whenever a return or enter is detected on the combobox, it finds the highlight item (i.e., that is highlit from the text search)
        // and sets the combobox to that value.
        private void ContentControl_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if ((e.Key == Key.Return) || (e.Key == Key.Enter))
            {
                ComboBox comboBox = sender as ComboBox;
                for (int i = 0; i < comboBox.Items.Count; i++)
                {
                    ComboBoxItem comboBoxItem = (ComboBoxItem)comboBox.ItemContainerGenerator.ContainerFromIndex(i);
                    if (comboBoxItem != null && comboBoxItem.IsHighlighted)
                    {
                        comboBox.SelectedValue = comboBoxItem.Content.ToString();
                    }
                }
            }
        }

        public override List<string> GetWellKnownValues()
        {
            List<string> choices = new();
            foreach (object item in this.ContentControl.Items)
            {
                if (item is string wellKnownString)
                {
                    choices.Add(wellKnownString);
                }
            }
            return choices;
        }

        public override void SetWellKnownValues(List<string> wellKnownValues)
        {
            this.ContentControl.Items.Clear();

            bool isClassification = String.Equals(this.DataLabel, Constant.FileColumn.Classification, StringComparison.Ordinal);
            bool emptyChoiceAllowed = false;
            foreach (string choice in wellKnownValues)
            {
                if (String.IsNullOrEmpty(choice))
                {
                    emptyChoiceAllowed = true;
                }
                else if (isClassification)
                {
                    // Data binding of a combo box's selected item property requires the type of the combo box's items match the
                    // type of the bound property.  For user defined choices in Carnassial (as of 2.2.0.3) both items and the
                    // bound ImageRow values are always strings.  This is not the case, however, for ImageRow.Classification as
                    // the property is an enum but well known values remain strings.  Either a converter has to be included in the
                    // binding to change the enum to a string or the well known values have to be parsed back to enums from strings.
                    // The latter is preferred as it's lower overhead.
                    bool success = ImageRow.TryParseFileClassification(choice, out FileClassification classification);
                    if (success == false)
                    {
                        throw new ArgumentOutOfRangeException(nameof(wellKnownValues), String.Format(CultureInfo.CurrentCulture, "'{0}' is not a valid FileClassification.", choice));
                    }
                    this.ContentControl.Items.Add(classification);
                }
                else
                {
                    this.ContentControl.Items.Add(choice);
                }
            }

            if (emptyChoiceAllowed)
            {
                // put empty choices at the end below a separator for visual clarity
                this.ContentControl.Items.Add(new Separator());
                this.ContentControl.Items.Add(String.Empty);
            }

            this.ContentControl.SelectedIndex = 0;
        }

        public override void SetValue(object valueAsObject)
        {
            string valueAsString;
            if ((valueAsObject is string) || (valueAsObject == null))
            {
                valueAsString = (string)valueAsObject;
            }
            else if (valueAsObject is FileSelection)
            {
                valueAsString = valueAsObject.ToString();
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(valueAsObject), String.Format(CultureInfo.CurrentCulture, "Unsupported value type {0}.", valueAsObject.GetType()));
            }

            this.ContentControl.SelectedValue = valueAsString;
            this.ContentControl.ToolTip = valueAsString;
        }
    }
}
