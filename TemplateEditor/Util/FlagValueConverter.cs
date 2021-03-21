using Carnassial.Util;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Data;

namespace Carnassial.Editor.Util
{
    internal class FlagValueConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
            {
                return null;
            }

            List<string> values = WellKnownValueConverter.Convert((string)value);
            if (values.Count == 1)
            {
                // default value case
                return this.Convert((string)value);
            }

            // well known values
            List<string> displayWellKnownValues = new(values.Count);
            foreach (string wellKnownValue in values)
            {
                displayWellKnownValues.Add(this.Convert(wellKnownValue));
            }
            return displayWellKnownValues;
        }

        private string Convert(string value)
        {
            return value switch
            {
                Constant.Sql.FalseString => EditorConstant.Resources.DisplayFalseString,
                Constant.Sql.TrueString => EditorConstant.Resources.DisplayTrueString,
                _ => throw new NotSupportedException(String.Format(CultureInfo.CurrentCulture, "Unhandled value '{0}'.", value)),
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
            {
                return null;
            }
            if (value is string valueAsString)
            {
                return this.ConvertBack(valueAsString);
            }

            List<string> displayWellKnownValues = (List<string>)value;
            List<string> wellKnownValues = new(displayWellKnownValues.Count);
            foreach (string displayWellKnownValue in displayWellKnownValues)
            {
                wellKnownValues.Add(this.ConvertBack(displayWellKnownValue));
            }
            return WellKnownValueConverter.ConvertBack(wellKnownValues);
        }

        private string ConvertBack(string value)
        {
            return value switch
            {
                EditorConstant.Resources.DisplayFalseString => Constant.Sql.FalseString,
                EditorConstant.Resources.DisplayTrueString => Constant.Sql.TrueString,
                _ => throw new NotSupportedException(String.Format(CultureInfo.CurrentCulture, "Unhandled value '{0}'.", value)),
            };
        }
    }
}
