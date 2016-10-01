﻿using Carnassial.Controls;
using Carnassial.Database;
using Carnassial.Util;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Xceed.Wpf.Toolkit;

namespace Carnassial.Dialog
{
    /// <summary>
    /// A dialog allowing a user to create a custom selection by setting conditions on data fields.
    /// </summary>
    public partial class CustomViewSelection : Window
    {
        private const int DefaultControlWidth = 200;
        private const double DefaultSearchCriteriaWidth = Double.NaN; // Same as xaml Width = "Auto"

        private const int LabelColumn = 0;
        private const int OperatorColumn = 1;
        private const int ValueColumn = 2;
        private const int SearchCriteriaColumn = 3;

        private ImageDatabase database;
        private TimeZoneInfo imageSetTimeZone;

        public CustomViewSelection(ImageDatabase database, Window owner)
        {
            this.InitializeComponent();

            this.database = database;
            this.imageSetTimeZone = this.database.ImageSet.GetTimeZone();
            this.Owner = owner;
        }

        // When the window is loaded, add SearchTerm controls to it
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Adjust this dialog window position 
            Utilities.SetDefaultDialogPosition(this);
            Utilities.TryFitWindowInWorkingArea(this);

            // And vs Or conditional
            if (this.database.CustomSelection.TermCombiningOperator == CustomSelectionOperator.And)
            {
                this.TermCombiningAnd.IsChecked = true;
                this.TermCombiningOr.IsChecked = false;
            }
            else
            {
                this.TermCombiningAnd.IsChecked = false;
                this.TermCombiningOr.IsChecked = true;
            }
            this.TermCombiningAnd.Checked += this.AndOrRadioButton_Checked;
            this.TermCombiningOr.Checked += this.AndOrRadioButton_Checked;

            // Create a new row for each search term. 
            // Each row specifies a particular control and how it can be searched
            int gridRowIndex = 0;
            foreach (SearchTerm searchTerm in this.database.CustomSelection.SearchTerms)
            {
                // start at 1 as there is already a header row
                ++gridRowIndex;
                RowDefinition gridRow = new RowDefinition();
                gridRow.Height = GridLength.Auto;
                this.grid.RowDefinitions.Add(gridRow);

                // LABEL column: A checkbox to indicate whether the current search row should be used as part of the search
                Thickness thickness = new Thickness(5, 2, 5, 2);
                CheckBox controlLabel = new CheckBox();
                controlLabel.Content = "_" + searchTerm.Label;
                controlLabel.Margin = thickness;
                controlLabel.VerticalAlignment = VerticalAlignment.Center;
                controlLabel.HorizontalAlignment = HorizontalAlignment.Left;
                controlLabel.IsChecked = searchTerm.UseForSearching;
                controlLabel.Checked += this.Select_CheckedOrUnchecked;
                controlLabel.Unchecked += this.Select_CheckedOrUnchecked;
                Grid.SetRow(controlLabel, gridRowIndex);
                Grid.SetColumn(controlLabel, CustomViewSelection.LabelColumn);
                grid.Children.Add(controlLabel);

                // The operators allowed for each search term type
                string controlType = searchTerm.ControlType;
                string[] termOperators;
                if (controlType == Constants.Control.Counter ||
                    controlType == Constants.DatabaseColumn.DateTime ||
                    controlType == Constants.DatabaseColumn.ImageQuality ||
                    controlType == Constants.Control.FixedChoice)
                {
                    // No globs in Counters as that text field only allows numbers, we can't enter the special characters Glob required
                    // No globs in Dates the date entries are constrained by the date picker
                    // No globs in Fixed Choices as choice entries are constrained by menu selection
                    termOperators = new string[]
                    {
                        Constants.SearchTermOperator.Equal,
                        Constants.SearchTermOperator.NotEqual,
                        Constants.SearchTermOperator.LessThan,
                        Constants.SearchTermOperator.GreaterThan,
                        Constants.SearchTermOperator.LessThanOrEqual,
                        Constants.SearchTermOperator.GreaterThanOrEqual
                    };
                }
                else if (controlType == Constants.DatabaseColumn.DeleteFlag ||
                         controlType == Constants.Control.Flag)
                {
                    // Only equals and not equals in Flags, as other options don't make sense for booleans
                    termOperators = new string[]
                    {
                        Constants.SearchTermOperator.Equal,
                        Constants.SearchTermOperator.NotEqual
                    };
                }
                else
                {
                    termOperators = new string[]
                    {
                        Constants.SearchTermOperator.Equal,
                        Constants.SearchTermOperator.NotEqual,
                        Constants.SearchTermOperator.LessThan,
                        Constants.SearchTermOperator.GreaterThan,
                        Constants.SearchTermOperator.LessThanOrEqual,
                        Constants.SearchTermOperator.GreaterThanOrEqual,
                        Constants.SearchTermOperator.Glob
                    };
                }

                // term operator combo box
                ComboBox operatorsComboBox = new ComboBox();
                operatorsComboBox.IsEnabled = searchTerm.UseForSearching;
                operatorsComboBox.ItemsSource = termOperators;
                operatorsComboBox.Margin = thickness;
                operatorsComboBox.SelectedValue = searchTerm.Operator;
                operatorsComboBox.SelectionChanged += this.Operator_SelectionChanged; // Create the callback that is invoked whenever the user changes the expresison
                operatorsComboBox.Width = 60;

                Grid.SetRow(operatorsComboBox, gridRowIndex);
                Grid.SetColumn(operatorsComboBox, CustomViewSelection.OperatorColumn);
                this.grid.Children.Add(operatorsComboBox);

                // Value column: The value used for comparison in the search
                // Notes and Counters both uses a text field, so they can be constructed as a textbox
                // However, counter textboxes are modified to only allow integer input (both direct typing or pasting are checked)
                if (controlType == Constants.DatabaseColumn.DateTime)
                {
                    DateTimeOffset dateTime = this.database.CustomSelection.GetDateTime(gridRowIndex - 1, this.imageSetTimeZone);

                    DateTimePicker dateValue = new DateTimePicker();
                    dateValue.Format = DateTimeFormat.Custom;
                    dateValue.FormatString = Constants.Time.DateTimeDisplayFormat;
                    dateValue.IsEnabled = searchTerm.UseForSearching;
                    dateValue.Value = dateTime.DateTime;
                    dateValue.ValueChanged += this.DateTime_SelectedDateChanged;
                    dateValue.Width = CustomViewSelection.DefaultControlWidth;

                    Grid.SetRow(dateValue, gridRowIndex);
                    Grid.SetColumn(dateValue, CustomViewSelection.ValueColumn);
                    this.grid.Children.Add(dateValue);
                }
                else if (controlType == Constants.DatabaseColumn.File ||
                         controlType == Constants.Control.Counter ||
                         controlType == Constants.Control.Note ||
                         controlType == Constants.DatabaseColumn.RelativePath)
                {
                    TextBox textBoxValue = new TextBox();
                    textBoxValue.IsEnabled = searchTerm.UseForSearching;
                    textBoxValue.Text = searchTerm.DatabaseValue;
                    textBoxValue.Margin = thickness;
                    textBoxValue.Width = CustomViewSelection.DefaultControlWidth;
                    textBoxValue.Height = 22;
                    textBoxValue.TextWrapping = TextWrapping.NoWrap;
                    textBoxValue.VerticalAlignment = VerticalAlignment.Center;
                    textBoxValue.VerticalContentAlignment = VerticalAlignment.Center;

                    // The following is specific only to Counters
                    if (controlType == Constants.Control.Counter)
                    {
                        textBoxValue.PreviewTextInput += this.Counter_PreviewTextInput;
                        DataObject.AddPastingHandler(textBoxValue, this.Counter_Paste);
                    }
                    textBoxValue.TextChanged += this.NoteOrCounter_TextChanged;

                    Grid.SetRow(textBoxValue, gridRowIndex);
                    Grid.SetColumn(textBoxValue, CustomViewSelection.ValueColumn);
                    this.grid.Children.Add(textBoxValue);
                }
                else if (controlType == Constants.Control.FixedChoice ||
                         controlType == Constants.DatabaseColumn.ImageQuality)
                {
                    // FixedChoice and ImageQuality both present combo boxes, so they can be constructed the same way
                    ComboBox comboBoxValue = new ComboBox();
                    comboBoxValue.IsEnabled = searchTerm.UseForSearching;
                    comboBoxValue.Width = CustomViewSelection.DefaultControlWidth;
                    comboBoxValue.Margin = thickness;

                    // Create the dropdown menu 
                    comboBoxValue.ItemsSource = searchTerm.List;
                    comboBoxValue.SelectedItem = searchTerm.DatabaseValue;
                    comboBoxValue.SelectionChanged += this.FixedChoice_SelectionChanged;
                    Grid.SetRow(comboBoxValue, gridRowIndex);
                    Grid.SetColumn(comboBoxValue, CustomViewSelection.ValueColumn);
                    this.grid.Children.Add(comboBoxValue);
                }
                else if (controlType == Constants.DatabaseColumn.DeleteFlag ||
                         controlType == Constants.Control.Flag)
                {
                    // Flags present checkboxes
                    CheckBox flagCheckBox = new CheckBox();
                    flagCheckBox.Margin = thickness;
                    flagCheckBox.VerticalAlignment = VerticalAlignment.Center;
                    flagCheckBox.HorizontalAlignment = HorizontalAlignment.Left;
                    flagCheckBox.IsChecked = (searchTerm.DatabaseValue.ToLower() == Constants.Boolean.False) ? false : true;
                    flagCheckBox.IsEnabled = searchTerm.UseForSearching;
                    flagCheckBox.Checked += this.Flag_CheckedOrUnchecked;
                    flagCheckBox.Unchecked += this.Flag_CheckedOrUnchecked;

                    searchTerm.DatabaseValue = flagCheckBox.IsChecked.Value ? Constants.Boolean.True : Constants.Boolean.False;

                    Grid.SetRow(flagCheckBox, gridRowIndex);
                    Grid.SetColumn(flagCheckBox, CustomViewSelection.ValueColumn);
                    this.grid.Children.Add(flagCheckBox);
                }
                else if (controlType == Constants.DatabaseColumn.UtcOffset)
                {
                    UtcOffsetUpDown utcOffsetValue = new UtcOffsetUpDown();
                    utcOffsetValue.IsEnabled = searchTerm.UseForSearching;
                    utcOffsetValue.IsTabStop = true;
                    utcOffsetValue.Value = searchTerm.GetUtcOffset();
                    utcOffsetValue.ValueChanged += this.UtcOffset_SelectedDateChanged;
                    utcOffsetValue.Width = CustomViewSelection.DefaultControlWidth;

                    Grid.SetRow(utcOffsetValue, gridRowIndex);
                    Grid.SetColumn(utcOffsetValue, CustomViewSelection.ValueColumn);
                    this.grid.Children.Add(utcOffsetValue);
                }
                else
                {
                    throw new NotSupportedException(String.Format("Unhandled control type '{0}'.", controlType));
                }

                // Search Criteria Column: initially as an empty textblock. Indicates the constructed query expression for this row
                TextBlock searchCriteria = new TextBlock();
                searchCriteria.Width = CustomViewSelection.DefaultSearchCriteriaWidth;
                searchCriteria.Margin = thickness;
                searchCriteria.VerticalAlignment = VerticalAlignment.Center;
                searchCriteria.HorizontalAlignment = HorizontalAlignment.Left;

                Grid.SetRow(searchCriteria, gridRowIndex);
                Grid.SetColumn(searchCriteria, CustomViewSelection.SearchCriteriaColumn);
                this.grid.Children.Add(searchCriteria);
            }
            this.UpdateSearchCriteriaFeedback();
        }

        // Radio buttons for determing if we use And or Or
        private void AndOrRadioButton_Checked(object sender, RoutedEventArgs args)
        {
            RadioButton radioButton = sender as RadioButton;
            this.database.CustomSelection.TermCombiningOperator = (radioButton == this.TermCombiningAnd) ? CustomSelectionOperator.And : CustomSelectionOperator.Or;
            this.UpdateSearchCriteriaFeedback();
        }

        // Select: When the use checks or unchecks the checkbox for a row
        // - activate or deactivate the search criteria for that row
        // - update the searchterms to reflect the new status 
        // - update the UI to activate or deactivate (or show or hide) its various search terms
        private void Select_CheckedOrUnchecked(object sender, RoutedEventArgs args)
        {
            CheckBox select = sender as CheckBox;
            int row = Grid.GetRow(select);  // And you have the row number...
            bool state = select.IsChecked.Value;

            SearchTerm searchterms = this.database.CustomSelection.SearchTerms[row - 1];
            searchterms.UseForSearching = select.IsChecked.Value;

            CheckBox label = this.GetGridElement<CheckBox>(CustomViewSelection.LabelColumn, row);
            ComboBox expression = this.GetGridElement<ComboBox>(CustomViewSelection.OperatorColumn, row);
            UIElement value = this.GetGridElement<UIElement>(CustomViewSelection.ValueColumn, row);

            label.FontWeight = select.IsChecked.Value ? FontWeights.Bold : FontWeights.Normal;
            expression.IsEnabled = select.IsChecked.Value;
            value.IsEnabled = select.IsChecked.Value;

            this.UpdateSearchCriteriaFeedback();
        }

        // Operator: The user has selected a new expression
        // - set its corresponding search term in the searchList data structure
        // - update the UI to show the search criteria 
        private void Operator_SelectionChanged(object sender, SelectionChangedEventArgs args)
        {
            ComboBox comboBox = sender as ComboBox;
            int row = Grid.GetRow(comboBox);
            this.database.CustomSelection.SearchTerms[row - 1].Operator = comboBox.SelectedValue.ToString(); // Set the corresponding expression to the current selection
            this.UpdateSearchCriteriaFeedback();
        }

        // Value (Counters and Notes): The user has selected a new value
        // - set its corresponding search term in the searchList data structure
        // - update the UI to show the search criteria 
        private void NoteOrCounter_TextChanged(object sender, TextChangedEventArgs args)
        {
            TextBox textBox = sender as TextBox;
            int row = Grid.GetRow(textBox);
            this.database.CustomSelection.SearchTerms[row - 1].DatabaseValue = textBox.Text;
            this.UpdateSearchCriteriaFeedback();
        }

        // Value (Counter) Helper function: textbox accept only typed numbers 
        private void Counter_PreviewTextInput(object sender, TextCompositionEventArgs args)
        {
            args.Handled = IsNumbersOnly(args.Text);
        }

        // Value (DateTime): we need to construct a string DateTime from it
        private void DateTime_SelectedDateChanged(object sender, RoutedPropertyChangedEventArgs<object> args)
        {
            DateTimePicker datePicker = sender as DateTimePicker;
            if (datePicker.Value.HasValue)
            {
                int row = Grid.GetRow(datePicker);
                this.database.CustomSelection.SetDateTime(row - 1, datePicker.Value.Value, this.imageSetTimeZone);
                this.UpdateSearchCriteriaFeedback();
            }
        }

        // Value (FixedChoice): The user has selected a new value 
        // - set its corresponding search term in the searchList data structure
        // - update the UI to show the search criteria 
        private void FixedChoice_SelectionChanged(object sender, SelectionChangedEventArgs args)
        {
            ComboBox comboBox = sender as ComboBox;
            int row = Grid.GetRow(comboBox);
            this.database.CustomSelection.SearchTerms[row - 1].DatabaseValue = comboBox.SelectedValue.ToString(); // Set the corresponding value to the current selection
            this.UpdateSearchCriteriaFeedback();
        }

        // Value (Flags): The user has checked or unchecked a new value 
        // - set its corresponding search term in the searchList data structure
        // - update the UI to show the search criteria 
        private void Flag_CheckedOrUnchecked(object sender, RoutedEventArgs e)
        {
            CheckBox checkBox = sender as CheckBox;
            int row = Grid.GetRow(checkBox);
            this.database.CustomSelection.SearchTerms[row - 1].DatabaseValue = checkBox.IsChecked.ToString().ToLower(); // Set the corresponding value to the current selection
            this.UpdateSearchCriteriaFeedback();
        }

        // When this button is pressed, all the search terms checkboxes are cleared, which is equivalent to showing all images
        private void ShowAllButton_Click(object sender, RoutedEventArgs e)
        {
            for (int row = 1; row <= this.database.CustomSelection.SearchTerms.Count; row++)
            {
                CheckBox label = this.GetGridElement<CheckBox>(CustomViewSelection.LabelColumn, row);
                label.IsChecked = false;
            }
        }

        private void UtcOffset_SelectedDateChanged(object sender, RoutedPropertyChangedEventArgs<object> args)
        {
            UtcOffsetUpDown utcOffsetPicker = sender as UtcOffsetUpDown;
            if (utcOffsetPicker.Value.HasValue)
            {
                int row = Grid.GetRow(utcOffsetPicker);
                this.database.CustomSelection.SearchTerms[row - 1].SetDatabaseValue(utcOffsetPicker.Value.Value);
                this.UpdateSearchCriteriaFeedback();
            }
        }

        // Updates the search criteria shown across all rows to reflect the contents of the search list,
        // which also show or hides the search term feedback for that row.
        private void UpdateSearchCriteriaFeedback()
        {
            // We go backwards, as we don't want to print the AND or OR on the last expression
            bool lastExpression = true;
            for (int index = this.database.CustomSelection.SearchTerms.Count - 1; index >= 0; index--)
            {
                int row = index + 1; // we offset the row by 1 as row 0 is the header
                SearchTerm searchTerm = this.database.CustomSelection.SearchTerms[index];
                TextBlock searchCriteria = this.GetGridElement<TextBlock>(CustomViewSelection.SearchCriteriaColumn, row);

                if (searchTerm.UseForSearching == false)
                {
                    // The search term is not used for searching, so clear the feedback field
                    searchCriteria.Text = String.Empty;
                    continue;
                }

                // Construct the search term 
                string searchCriteriaText = searchTerm.DataLabel + " " + searchTerm.Operator + " "; // So far, we have "Data Label = "

                string value = searchTerm.DatabaseValue.Trim();    // the Value, but if its 
                if (value.Length == 0)
                {
                    value = "\"\"";  // an empty string, display it as ""
                }
                searchCriteriaText += value;

                // If it's not the last expression and if there are multiple queries (i.e., search terms) then show the And or Or at its end.
                if (!lastExpression)
                {
                    searchCriteriaText += " " + this.database.CustomSelection.TermCombiningOperator.ToString();
                }

                searchCriteria.Text = searchCriteriaText;
                lastExpression = false;
            }

            int count = this.database.GetImageCount(ImageSelection.Custom);
            this.OkButton.IsEnabled = count > 0 ? true : false;
            this.textBlockQueryMatches.Text = count > 0 ? count.ToString() : "0";

            this.btnShowAll.IsEnabled = lastExpression == false;
        }

        // Apply the selection if the Ok button is clicked
        private void OkButton_Click(object sender, RoutedEventArgs args)
        {
            this.database.SelectDataTableImages(ImageSelection.Custom);
            this.DialogResult = true;
        }

        // Cancel - exit the dialog without doing anythikng.
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }

        // Get the corresponding grid element from a given a column, row, 
        private TElement GetGridElement<TElement>(int column, int row) where TElement : UIElement
        {
            return (TElement)this.grid.Children.Cast<UIElement>().First(control => Grid.GetRow(control) == row && Grid.GetColumn(control) == column);
        }

        // Value (Counter) Helper function:  textbox accept only pasted numbers 
        private void Counter_Paste(object sender, DataObjectPastingEventArgs args)
        {
            bool isText = args.SourceDataObject.GetDataPresent(DataFormats.UnicodeText, true);
            if (!isText)
            {
                args.CancelCommand();
            }

            string text = args.SourceDataObject.GetData(DataFormats.UnicodeText) as string;
            if (CustomViewSelection.IsNumbersOnly(text))
            {
                args.CancelCommand();
            }
        }

        // Value(Counter) Helper function: checks if the text contains only numbers
        private static bool IsNumbersOnly(string text)
        {
            Regex regex = new Regex("[^0-9.-]+"); // regex that matches allowed text
            return regex.IsMatch(text);
        }
    }
}