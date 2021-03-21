using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Carnassial.Control
{
    public class AutocompleteTextBox : TextBox
    {
        public static readonly DependencyProperty AutocompletionsProperty = DependencyProperty.Register("Autocompletions", typeof(List<string>), typeof(AutocompleteTextBox));

        private string? mostRecentAutocompletion;

        public bool AllowLeadingWhitespace { get; set; }
        public bool SuppressAutocompletion { get; set; }

        /// <summary>
        /// Since autocompletion hooks the TextChanged event provide a follow on event to callers as event sequencing can be fragile.
        /// </summary>
        public event Action<object, TextChangedEventArgs>? TextAutocompleted;

        public AutocompleteTextBox()
        {
            this.AllowLeadingWhitespace = false;
            this.mostRecentAutocompletion = null;
            this.PreviewKeyDown += this.OnPreviewKeyDown;
            this.TextChanged += this.OnTextChanged;
        }

        // XamlWriter doesn't support generics so this property breaks anything triggering XamlWriter.Save(), such as clearing UI object collections
        // containing the text box since the clear triggers undo and undo relies on serialization.
        // If needed serialization support can be added via a TypeConverter.
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public List<string> Autocompletions
        {
            get { return (List<string>)this.GetValue(AutocompleteTextBox.AutocompletionsProperty); }
            set { this.SetValue(AutocompleteTextBox.AutocompletionsProperty, value); }
        }

        private int GetIndexOfCurrentAutocompletion()
        {
            if (this.Autocompletions == null)
            {
                return -1;
            }
            return this.Autocompletions.IndexOf(this.Text);
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (this.SuppressAutocompletion)
            {
                return;
            }

            switch (e.Key)
            {
                case Key.Down:
                    if (this.Autocompletions == null)
                    {
                        return;
                    }
                    int indexOfCurrentAutocompletion = this.GetIndexOfCurrentAutocompletion();
                    if (indexOfCurrentAutocompletion > this.Autocompletions.Count - 2)
                    {
                        this.Text = this.Autocompletions[0];
                    }
                    else
                    {
                        this.Text = this.Autocompletions[indexOfCurrentAutocompletion + 1];
                    }
                    e.Handled = true;
                    break;
                case Key.Up:
                    if (this.Autocompletions == null)
                    {
                        return;
                    }
                    indexOfCurrentAutocompletion = this.GetIndexOfCurrentAutocompletion();
                    if (indexOfCurrentAutocompletion < 1)
                    {
                        this.Text = this.Autocompletions[^1];
                    }
                    else
                    {
                        this.Text = this.Autocompletions[indexOfCurrentAutocompletion - 1];
                    }
                    e.Handled = true;
                    break;
                default:
                    return;
            }
        }

        private void OnTextChanged(object sender, TextChangedEventArgs eventArgs)
        {
            // if leading whitespace is disabled, reject it
            if (this.AllowLeadingWhitespace == false)
            {
                // Updating the text box moves the caret to the start position, which results in poor user experience when the text box initially contains only
                // whitespace and the user happens to move focus to the control in such a way that the first non-whitespace character entered follows some of the
                // whitespace---the result's the first character of the word ends up at the end rather than at the beginning.
                int cursorPosition = this.CaretIndex;
                string trimmedNote = this.Text.TrimStart();
                if (trimmedNote != this.Text)
                {
                    cursorPosition -= this.Text.Length - trimmedNote.Length;
                    if (cursorPosition < 0)
                    {
                        cursorPosition = 0;
                    }

                    this.Text = trimmedNote;
                    this.CaretIndex = cursorPosition;
                }
            }

            // check if autocompletion is possible when text is added
            // Don't attempt autocompletion on pure removals, such as backspace or delete, but do try when both add and remove changes are present as this
            // usually indicates the user's typing over the autocomplete suggestion.
            if ((this.SuppressAutocompletion == false) && (String.IsNullOrEmpty(this.Text) == false) && eventArgs.Changes.Any(change => change.AddedLength > 0))
            {
                int textLength = this.Text.Length;
                string? autocompletion = null;
                if (this.UseCompletion(this.mostRecentAutocompletion))
                {
                    // prefer the most recently used completion over others
                    // This tends to alleviate users' data entry effort as usually the data entered for the last file is more likely appropriate than the first
                    // hit found in the completions collection.
                    autocompletion = this.mostRecentAutocompletion;
                }
                else if (this.Autocompletions != null)
                {
                    autocompletion = this.Autocompletions.FirstOrDefault(value => this.UseCompletion(value));
                }

                if (String.IsNullOrEmpty(autocompletion) == false)
                {
                    bool originalSuppressAutocompletion = this.SuppressAutocompletion;
                    this.SuppressAutocompletion = true;

                    this.Text = autocompletion;
                    this.CaretIndex = textLength;
                    this.SelectionStart = textLength;
                    this.SelectionLength = autocompletion.Length - textLength;

                    this.SuppressAutocompletion = originalSuppressAutocompletion;
                    this.mostRecentAutocompletion = autocompletion;
                }
            }

            // synchronize tooltip with content
            this.ToolTip = this.Text;

            // fire follow on event
            if (this.SuppressAutocompletion == false)
            {
                this.TextAutocompleted?.Invoke(this, eventArgs);
            }
        }

        private bool UseCompletion(string? completion)
        {
            int textLength = this.Text.Length;
            if ((completion != null) && (completion.Length >= textLength) && completion.Substring(0, textLength).Equals(this.Text, StringComparison.Ordinal))
            {
                return true;
            }
            return false;
        }
    }
}
