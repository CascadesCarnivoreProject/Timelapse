using Carnassial.Data;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Windows.Controls;

namespace Carnassial.Control
{
    // A flag comprises
    // - a label containing the descriptive label) 
    // - checkbobox (the content) at the given width
    public class DataEntryFlag : DataEntryControl<CheckBox, Label>
    {
        public DataEntryFlag(ControlRow control, DataEntryControls styleProvider)
            : base(control, styleProvider, ControlContentStyle.FlagCheckBox, ControlLabelStyle.Label)
        {
            this.ContentControl.SetBinding(CheckBox.IsCheckedProperty, ImageRow.GetDataBindingPath(control));
        }

        public override bool ContentReadOnly
        {
            get { return !this.ContentControl.IsEnabled; }
            set { this.ContentControl.IsEnabled = !value; }
        }

        public override bool IsCopyableValue(object? value)
        {
            return value != null && (bool)value;
        }

        public override void SetValue(object valueAsObject)
        {
            if (valueAsObject is string valueAsString)
            {
                Debug.Assert(String.Equals(valueAsString, Constant.ControlDefault.FlagValue, StringComparison.Ordinal) || String.Equals(valueAsString, "1", StringComparison.Ordinal), String.Format(CultureInfo.InvariantCulture, "Unknown boolean value '{0}'.", valueAsString));
                this.ContentControl.IsChecked = String.Equals(valueAsString, Constant.ControlDefault.FlagValue, StringComparison.Ordinal) == false;
                this.ContentControl.ToolTip = valueAsString;
            }
            else if (valueAsObject is bool value)
            {
                this.ContentControl.IsChecked = value;
                this.ContentControl.ToolTip = value ? Boolean.TrueString : Boolean.FalseString;
            }
            else
            {
                if (valueAsObject == null)
                {
                    throw new ArgumentNullException(nameof(valueAsObject));
                }
                throw new ArgumentOutOfRangeException(nameof(valueAsObject), String.Format(CultureInfo.CurrentCulture, "Unexpected value type {0}.", valueAsObject.GetType()));
            }
        }
    }
}
