using Carnassial.Data;
using Carnassial.Util;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Carnassial.Control
{
    // A counter comprises
    // - a radio button containing the descriptive label
    // - an editable textbox (containing the content) at the given width
    public class DataEntryCounter : DataEntryControl<TextBox, RadioButton>
    {
        private bool labelControlIsChecked;

        public DataEntryCounter(ControlRow control, DataEntryControls styleProvider)
            : base(control, styleProvider, ControlContentStyle.NoteCounterTextBox, ControlLabelStyle.CounterButton)
        {
            this.ContentControl.PreviewTextInput += this.ContentControl_PreviewTextInput;

            // assign all counters to a single group so that selecting a new counter deselects any currently selected counter
            this.LabelControl.GroupName = "DataEntryCounter";
            this.LabelControl.Click += this.LabelControl_Click;
            this.labelControlIsChecked = false;

            this.ContentControl.SetBinding(TextBox.TextProperty, ImageRow.GetDataBindingPath(control));
        }

        public override bool ContentReadOnly
        {
            get { return this.ContentControl.IsReadOnly; }
            set { this.ContentControl.IsReadOnly = value; }
        }

        public override bool IsCopyableValue(object? value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }
            return (int)value != 0;
        }

        public bool IsSelected
        {
            get { return this.LabelControl.IsChecked.HasValue && (bool)this.LabelControl.IsChecked; }
        }

        /// <summary>Ensures only numbers are entered for counters.</summary>
        private void ContentControl_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !Utilities.IsDigits(e.Text);
        }

        private void LabelControl_Click(object sender, RoutedEventArgs e)
        {
            if (this.labelControlIsChecked)
            {
                this.LabelControl.IsChecked = false;
            }
            this.labelControlIsChecked = this.LabelControl.IsChecked ?? false;
        }

        public override void SetValue(object valueAsObject)
        {
            // counter values are unsigned integers but are are manipulated as strings
            string? valueAsString;
            if ((valueAsObject is string) || (valueAsObject == null))
            {
                valueAsString = (string?)valueAsObject;
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(valueAsObject), String.Format(CultureInfo.CurrentCulture, "Unsupported value type {0}.", valueAsObject.GetType()));
            }

            this.ContentControl.Text = valueAsString;
            this.ContentControl.ToolTip = valueAsString;
        }
    }
}