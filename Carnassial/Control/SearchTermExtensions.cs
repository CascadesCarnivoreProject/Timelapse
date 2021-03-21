using Carnassial.Data;
using Carnassial.Util;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using WpfControl = System.Windows.Controls.Control;

namespace Carnassial.Control
{
    public static class SearchTermExtensions
    {
        // accept only numbers in counter textboxes
        private static void Counter_Paste(object sender, DataObjectPastingEventArgs args)
        {
            bool isText = args.SourceDataObject.GetDataPresent(DataFormats.UnicodeText, true);
            if (!isText)
            {
                args.CancelCommand();
            }

            if ((args.SourceDataObject.GetData(DataFormats.UnicodeText) is not string text) || (Utilities.IsDigits(text) == false))
            {
                args.CancelCommand();
            }
        }

        private static void Counter_PreviewTextInput(object sender, TextCompositionEventArgs args)
        {
            // counters accept only numbers
            args.Handled = !Utilities.IsDigits(args.Text);
        }

        public static WpfControl CreateValueControl(this SearchTerm searchTerm, AutocompletionCache autocompletionCache)
        {
            ControlType controlType = searchTerm.ControlType;
            switch (controlType)
            {
                case ControlType.Counter:
                case ControlType.Note:
                    AutocompleteTextBox textBox = new()
                    {
                        AllowLeadingWhitespace = true,
                        Autocompletions = autocompletionCache[searchTerm.DataLabel],
                        DataContext = searchTerm,
                        Margin = App.FindResource<Thickness>(Constant.ResourceKey.SearchTermListCellMargin),
                        TextWrapping = TextWrapping.NoWrap,
                        VerticalAlignment = VerticalAlignment.Stretch,
                        VerticalContentAlignment = VerticalAlignment.Center,
                        Width = Constant.UserInterface.FindValueWidth
                    };

                    BindingOperations.SetBinding(textBox, CheckBox.IsEnabledProperty, new Binding(nameof(searchTerm.UseForSearching)));
                    Binding textBinding = new(nameof(searchTerm.DatabaseValue))
                    {
                        UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                    };
                    if (searchTerm.ControlType == ControlType.Counter)
                    {
                        textBinding.Converter = new IntegerConverter();
                    }
                    BindingOperations.SetBinding(textBox, AutocompleteTextBox.TextProperty, textBinding);

                    if (controlType == ControlType.Counter)
                    {
                        // accept only numbers in counter text boxes
                        textBox.PreviewTextInput += SearchTermExtensions.Counter_PreviewTextInput;
                        DataObject.AddPastingHandler(textBox, SearchTermExtensions.Counter_Paste);
                    }
                    return textBox;
                case ControlType.DateTime:
                    DateTimeOffsetPicker dateTimeOffset = new()
                    {
                        DataContext = searchTerm,
                        Value = (DateTime)searchTerm.DatabaseValue,
                        Width = Constant.UserInterface.FindValueWidth
                    };
                    BindingOperations.SetBinding(dateTimeOffset, DateTimeOffsetPicker.ValueProperty, new Binding(nameof(searchTerm.DatabaseValue))
                    {
                        Converter = new DateTimeOffsetConverter()
                    });
                    BindingOperations.SetBinding(dateTimeOffset, CheckBox.IsEnabledProperty, new Binding(nameof(searchTerm.UseForSearching)));
                    return dateTimeOffset;
                case ControlType.FixedChoice:
                    ComboBox comboBox = new()
                    {
                        DataContext = searchTerm,
                        ItemsSource = searchTerm.WellKnownValues,
                        Margin = App.FindResource<Thickness>(Constant.ResourceKey.SearchTermListCellMargin),
                        SelectedItem = searchTerm.DatabaseValue,
                        Width = Constant.UserInterface.FindValueWidth
                    };
                    BindingOperations.SetBinding(comboBox, ComboBox.SelectedItemProperty, new Binding(nameof(searchTerm.DatabaseValue)));
                    BindingOperations.SetBinding(comboBox, CheckBox.IsEnabledProperty, new Binding(nameof(searchTerm.UseForSearching)));
                    return comboBox;
                case ControlType.Flag:
                    CheckBox checkBox = new()
                    {
                        DataContext = searchTerm,
                        HorizontalAlignment = HorizontalAlignment.Left,
                        Margin = App.FindResource<Thickness>(Constant.ResourceKey.SearchTermListCellMargin),
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    BindingOperations.SetBinding(checkBox, CheckBox.IsCheckedProperty, new Binding(nameof(searchTerm.DatabaseValue))
                    {
                        Converter = new BooleanConverter()
                    });
                    BindingOperations.SetBinding(checkBox, CheckBox.IsEnabledProperty, new Binding(nameof(searchTerm.UseForSearching)));
                    return checkBox;
                case ControlType.UtcOffset:
                    UtcOffsetPicker utcOffset = new()
                    {
                        DataContext = searchTerm,
                        IsTabStop = true,
                        Value = DateTimeHandler.FromDatabaseUtcOffset((double)searchTerm.DatabaseValue),
                        VerticalContentAlignment = VerticalAlignment.Stretch,
                        Width = Constant.UserInterface.FindValueWidth
                    };
                    BindingOperations.SetBinding(utcOffset, UtcOffsetPicker.ValueProperty, new Binding(nameof(searchTerm.DatabaseValue))
                    {
                        Converter = new UtcOffsetConverter()
                    });
                    BindingOperations.SetBinding(utcOffset, CheckBox.IsEnabledProperty, new Binding(nameof(searchTerm.UseForSearching)));
                    return utcOffset;
                default:
                    throw new NotSupportedException(String.Format(CultureInfo.CurrentCulture, "Unhandled control type '{0}'.", controlType));
            }
        }

        protected class BooleanConverter : IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            {
                Debug.Assert(targetType == typeof(bool?), "Expected to convert to bool?.");
                return new bool?((bool)value);
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            {
                Debug.Assert(targetType == typeof(object), "Expected request to convert bool to object.");
                return ((bool?)value).Value;
            }
        }

        protected class DateTimeOffsetConverter : IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            {
                Debug.Assert(targetType == typeof(DateTimeOffset), "Expected to convert to DateTimeOffset.");
                return new DateTimeOffset((DateTime)value);
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            {
                Debug.Assert(targetType == typeof(object), "Expected request to convert DateTimeOffset to object.");
                return ((DateTimeOffset)value).UtcDateTime;
            }
        }

        protected class IntegerConverter : IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            {
                Debug.Assert(targetType == typeof(string), "Expected to convert to string.");
                // if needed, check parameter
                return ((int)value).ToString(culture);
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            {
                Debug.Assert(targetType == typeof(object), "Expected request to convert string to object.");
                // if needed, check parameter

                string valueAsString = (string)value;
                if (String.IsNullOrEmpty(valueAsString))
                {
                    // while counter input is limited to numbers a user can delete all text during editing
                    valueAsString = Constant.ControlDefault.CounterValue;
                }
                return Int32.Parse(valueAsString, NumberStyles.AllowLeadingSign, CultureInfo.CurrentCulture);
            }
        }

        protected class UtcOffsetConverter : IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            {
                Debug.Assert(targetType == typeof(TimeSpan), "Expected to convert to TimeSpan.");
                if (value is TimeSpan timeSpan)
                {
                    return timeSpan;
                }
                return DateTimeHandler.FromDatabaseUtcOffset((double)value);
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            {
                Debug.Assert(targetType == typeof(double), "Expected to convert to double.");
                return DateTimeHandler.ToDatabaseUtcOffset((TimeSpan)value);
            }
        }
    }
}
