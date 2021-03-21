using Carnassial.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;

namespace Carnassial.Control
{
    /// <summary>
    /// A basic DateTimeOffset selection control.  Support is limited to custom format strings which use dd, f, ff, fff, ffff, fffff, ffffff, fffffff, F, FF, FFF,
    /// FFFF, FFFFF, FFFFFF, FFFFFF, hh, HH, mm, MM, ss, yy, yyyy, yyyyy, zz, ., :, /, spaces, T, and Z.  ddd can be used in cultures where it always indicates a 
    /// three letter abbreviation for a day.
    /// </summary>
    /// <remarks>
    /// The variable selection width required by d, dddd, h, H, m, M, s, y, yyy, and z is not supported.  Also not supported are formats using t, tt, %, \, zzz 
    /// (use K), other literals, and non-standard separators.
    /// </remarks>
    public partial class DateTimeOffsetPicker : UserControl
    {
        public static readonly DependencyProperty FormatProperty = DependencyProperty.Register("Format", typeof(string), typeof(DateTimeOffsetPicker), new FrameworkPropertyMetadata(Constant.Time.DateTimeDisplayFormat, DateTimeOffsetPicker.SetFormat));
        public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register("Maximum", typeof(DateTimeOffset), typeof(DateTimeOffsetPicker), new FrameworkPropertyMetadata(DateTimeOffset.MaxValue, null, DateTimeOffsetPicker.CoerceMaximum));
        public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register("Minimum", typeof(DateTimeOffset), typeof(DateTimeOffsetPicker), new FrameworkPropertyMetadata(Constant.ControlDefault.DateTimeValue, null, DateTimeOffsetPicker.CoerceMinimum));
        public static readonly DependencyProperty ValueProperty = DependencyProperty.Register("Value", typeof(DateTimeOffset), typeof(DateTimeOffsetPicker), new FrameworkPropertyMetadata(Constant.ControlDefault.DateTimeValue, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, DateTimeOffsetPicker.SetValue, DateTimeOffsetPicker.CoerceValue, false, UpdateSourceTrigger.LostFocus));

        private int currentPartIndex;
        private int monthPartIndex;
        private readonly List<IndexedDateTimePart> parts;
        private readonly TextBoxUpDownAdorner upDownButtons;

        public event Action<DateTimeOffsetPicker, DateTimeOffset>? ValueChanged;

        public DateTimeOffsetPicker()
        {
            this.currentPartIndex = -1;
            this.monthPartIndex = int.MaxValue;
            this.parts = new List<IndexedDateTimePart>();
            this.InitializeComponent();
            this.upDownButtons = new TextBoxUpDownAdorner(this.DateTimeDisplay);
            this.upDownButtons.Button_Clicked += (textBox, direction) => { this.IncrementOrDecrement(direction); };

            this.Calendar.SelectedDatesChanged += this.Calendar_SelectedDatesChanged;
            this.DateTimeDisplay.GotFocus += this.DateTimeDisplay_GotFocus;
            this.DateTimeDisplay.LostFocus += this.DateTimeDisplay_LostFocus;
            this.DateTimeDisplay.IsVisibleChanged += this.DateTimeDisplay_IsVisibleChanged;
            this.DateTimeDisplay.PreviewMouseUp += this.DateTimeDisplay_PreviewMouseUp;
            this.DateTimeDisplay.PreviewKeyDown += this.DateTimeDisplay_PreviewKeyDown;
            this.DateTimeDisplay.TextChanged += this.DateTimeDisplay_TextChanged;

            DateTimeOffsetPicker.SetFormat(this, new DependencyPropertyChangedEventArgs(DateTimeOffsetPicker.FormatProperty, Constant.Time.DateTimeDisplayFormat, Constant.Time.DateTimeDisplayFormat));

            this.GotFocus += this.DateTimeOffsetPicker_GotFocus;
            this.Focusable = true;
        }

        public string Format
        {
            get { return (string)this.GetValue(DateTimeOffsetPicker.FormatProperty); }
            set { this.SetValue(DateTimeOffsetPicker.FormatProperty, value); }
        }

        public DateTimeOffset Maximum
        {
            get { return (DateTimeOffset)this.GetValue(DateTimeOffsetPicker.MaximumProperty); }
            set { this.SetValue(DateTimeOffsetPicker.MaximumProperty, value); }
        }

        public DateTimeOffset Minimum
        {
            get { return (DateTimeOffset)this.GetValue(DateTimeOffsetPicker.MinimumProperty); }
            set { this.SetValue(DateTimeOffsetPicker.MinimumProperty, value); }
        }

        public bool ShowCalendarButton
        {
            get { return this.CalendarButton.Visibility == Visibility.Visible; }
            set { this.CalendarButton.Visibility = value ? Visibility.Visible : Visibility.Collapsed; }
        }

        public DateTimeOffset Value
        {
            get { return (DateTimeOffset)this.GetValue(DateTimeOffsetPicker.ValueProperty); }
            set { this.SetValue(DateTimeOffsetPicker.ValueProperty, value); }
        }

        private void Calendar_SelectedDatesChanged(object? sender, SelectionChangedEventArgs e)
        {
            this.CalendarButton.IsChecked = false;
            DateTime? selectedDate = this.Calendar.SelectedDate;
            Debug.Assert(selectedDate != null);
            this.Value = new DateTimeOffset((DateTime)selectedDate + this.Value.TimeOfDay, this.Value.Offset);
        }

        private static object CoerceMaximum(DependencyObject d, object value)
        {
            DateTimeOffsetPicker dateTimeOffsetPicker = (DateTimeOffsetPicker)d;
            DateTimeOffset maximumDateTime = (DateTimeOffset)value;
            if (maximumDateTime < dateTimeOffsetPicker.Minimum)
            {
                throw new ArgumentException(App.FindResource<string>(Constant.ResourceKey.DateTimeOffsetPickerCoerceMaximum), nameof(value));
            }

            if (maximumDateTime < dateTimeOffsetPicker.Value)
            {
                dateTimeOffsetPicker.Value = maximumDateTime;
            }

            return maximumDateTime;
        }

        private static object CoerceMinimum(DependencyObject d, object value)
        {
            DateTimeOffsetPicker dateTimeOffsetPicker = (DateTimeOffsetPicker)d;
            DateTimeOffset minimumDateTime = (DateTimeOffset)value;
            if (minimumDateTime > dateTimeOffsetPicker.Maximum)
            {
                throw new ArgumentException(App.FindResource<string>(Constant.ResourceKey.DateTimeOffsetPickerCoerceMinimum), nameof(value));
            }

            if (minimumDateTime > dateTimeOffsetPicker.Value)
            {
                dateTimeOffsetPicker.Value = minimumDateTime;
            }

            return minimumDateTime;
        }

        private static object CoerceValue(DependencyObject d, object value)
        {
            DateTimeOffsetPicker dateTimeOffsetPicker = (DateTimeOffsetPicker)d;
            DateTimeOffset dateTimeValue = (DateTimeOffset)value;
            if (dateTimeValue < dateTimeOffsetPicker.Minimum)
            {
                dateTimeValue = dateTimeOffsetPicker.Minimum;
            }
            if (dateTimeValue > dateTimeOffsetPicker.Maximum)
            {
                dateTimeValue = dateTimeOffsetPicker.Maximum;
            }
            return dateTimeValue;
        }

        private void DateTimeDisplay_GotFocus(object sender, RoutedEventArgs e)
        {
            this.TrySelectCurrentPart();
            e.Handled = true;
        }

        private void DateTimeDisplay_LostFocus(object sender, RoutedEventArgs e)
        {
            this.DateTimeDisplay.Text = this.Value.ToString(this.Format, CultureInfo.CurrentCulture);
        }

        private void DateTimeDisplay_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            AdornerLayer adornerLayer = AdornerLayer.GetAdornerLayer(this.DateTimeDisplay);
            if (adornerLayer != null)
            {
                if ((bool)e.NewValue)
                {
                    adornerLayer.Add(this.upDownButtons);
                }
                else
                {
                    adornerLayer.Remove(this.upDownButtons);
                }
            }
        }

        private void DateTimeDisplay_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                // navigation, increment/decrement
                case Key.Up:
                case Key.Down:
                case Key.Right:
                case Key.Left:
                    if (this.TryParseDateTimeOffset(out DateTimeOffset _) == false)
                    {
                        // action can't be be performed as the value isn't currently well formed
                        return;
                    }
                    switch (e.Key)
                    {
                        case Key.Up:
                            this.IncrementOrDecrement(CommonUserInterface.GetIncrement(true, Keyboard.Modifiers));
                            break;
                        case Key.Down:
                            this.IncrementOrDecrement(CommonUserInterface.GetIncrement(false, Keyboard.Modifiers));
                            break;
                        case Key.Left:
                            if (Keyboard.Modifiers != ModifierKeys.None)
                            {
                                return;
                            }
                            this.TrySelectPart(Direction.Previous);
                            break;
                        case Key.Right:
                            if (Keyboard.Modifiers != ModifierKeys.None)
                            {
                                return;
                            }
                            this.TrySelectPart(Direction.Next);
                            break;
                    }
                    break;
                // editing, focus changes, date separators, hotkeys
                case Key.Back:
                case Key.Decimal:
                case Key.Delete:
                case Key.Enter:
                case Key.Escape:
                case Key.OemMinus:
                case Key.OemPeriod:
                case Key.OemSemicolon:
                case Key.Space:
                case Key.System:
                case Key.T:
                case Key.Tab:
                case Key.Z:
                    // leave event unhandled so key is accepted as input
                    return;
                default:
                    if (this.parts[this.currentPartIndex].IsMonthName)
                    {
                        // allow characters in names of months
                        if (e.Key >= Key.A && e.Key <= Key.Z)
                        {
                            return;
                        }
                    }
                    else
                    {
                        // digits are allowed in all other fields
                        if ((e.Key >= Key.D0) && (e.Key <= Key.D9))
                        {
                            return;
                        }
                        if ((e.Key >= Key.NumPad0) && (e.Key <= Key.NumPad9))
                        {
                            return;
                        }
                    }
                    // block all other keys as they're neither navigation, editing, or digits
                    break;
            }

            e.Handled = true;
        }

        private void DateTimeDisplay_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (this.DateTimeDisplay.SelectionLength == 0)
            {
                this.TrySelectCurrentPart();
            }
        }

        private void DateTimeDisplay_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (this.TryParseDateTimeOffset(out DateTimeOffset dateTimeOffset))
            {
                this.CalendarButton.Visibility = Visibility.Visible;
                this.ErrorIcon.Visibility = Visibility.Hidden;
                this.Value = dateTimeOffset;
            }
            else
            {
                this.CalendarButton.Visibility = Visibility.Hidden;
                this.ErrorIcon.Visibility = Visibility.Visible;
            }
        }

        private void DateTimeOffsetPicker_GotFocus(object sender, RoutedEventArgs e)
        {
            // forward focus requests on this control to its date time display
            this.DateTimeDisplay.Focus();
            e.Handled = true;
        }

        private void IncrementOrDecrement(int increment)
        {
            if (this.TryParseDateTimeOffset(out DateTimeOffset newValue) == false)
            {
                newValue = this.Value;
            }

            char partFormat = this.parts[this.currentPartIndex].Format;
            switch (partFormat)
            {
                case 'd':
                    newValue = newValue.AddDays(increment);
                    break;
                case 'f':
                case 'F':
                    newValue = newValue.AddMilliseconds(increment);
                    break;
                case 'h':
                case 'H':
                    newValue = newValue.AddHours(increment);
                    break;
                case Constant.Time.DateTimeOffsetPart:
                case 'z':
                    bool nextPartAvailable = this.parts.Count > this.currentPartIndex + 1;
                    bool isOffsetHoursPart = (partFormat == 'z') || (nextPartAvailable && this.parts[this.currentPartIndex + 1].Format == Constant.Time.DateTimeOffsetPart);
                    TimeSpan incrementAsTimeSpan = isOffsetHoursPart ? TimeSpan.FromHours(increment) : TimeSpan.FromMinutes(increment * Constant.Time.UtcOffsetGranularityInMinutes);
                    TimeSpan offset = (newValue.Offset + incrementAsTimeSpan).Limit(TimeSpan.FromHours(Constant.Time.MinimumUtcOffsetInHours), TimeSpan.FromHours(Constant.Time.MaximumUtcOffsetInHours));
                    newValue = newValue.SetOffset(offset);
                    break;
                case 'm':
                    newValue = newValue.AddMinutes(increment);
                    break;
                case 'M':
                    newValue = newValue.AddMonths(increment);
                    break;
                case 's':
                    newValue = newValue.AddSeconds(increment);
                    break;
                case 'y':
                    newValue = newValue.AddYears(increment);
                    break;
                default:
                    throw new NotSupportedException(String.Format(CultureInfo.CurrentCulture, "Unhandled increment {0}.", partFormat));
            }

            if (newValue < this.Minimum)
            {
                this.Value = this.Minimum;
            }
            if (newValue > this.Maximum)
            {
                this.Value = this.Maximum;
            }
            this.Value = newValue;

            this.TrySelectCurrentPart();
        }

        private static void SetFormat(DependencyObject obj, DependencyPropertyChangedEventArgs args)
        {
            DateTimeOffsetPicker dateTimeOffsetPicker = (DateTimeOffsetPicker)obj;

            dateTimeOffsetPicker.monthPartIndex = Int32.MaxValue;
            dateTimeOffsetPicker.parts.Clear();

            IndexedDateTimePart currentPart = null;
            char previousFormatCharacter = '\0';
            for (int formatIndex = 0, selectionIndex = 0; formatIndex < dateTimeOffsetPicker.Format.Length; ++formatIndex, ++selectionIndex)
            {
                char formatCharacter = dateTimeOffsetPicker.Format[formatIndex];
                switch (formatCharacter)
                {
                    case 'd': // day
                    case 'f': // millisecond
                    case 'F':
                    case 'h': // hour
                    case 'H':
                    case Constant.Time.DateTimeOffsetPart: // offset
                    case 'm': // minute
                    case 'M': // month
                    case 's': // second
                    case 'y': // year
                    case 'z': // offset
                        break;
                    case '\\':
                        // backslashes aren't displayed and hence aren't part of selection
                        --selectionIndex;
                        continue;
                    case '.':
                    case ':':
                    case '/':
                    case '-':
                    case ' ':
                    case 'T':
                    case 'Z':
                        // skip delimiters and UTC indicator
                        continue;
                    default:
                        throw new NotSupportedException(String.Format(CultureInfo.CurrentCulture, "Unsupported format character '{0}'.", formatCharacter));
                }

                if (formatCharacter != previousFormatCharacter)
                {
                    if ((currentPart != null) && (currentPart.Format == 'M') && (currentPart.FormatLength == 3))
                    {
                        // part which just ended is an MMM month part and may render as two to eight or more characters depending on the current culture
                        // If needed this check can be done after the loop completes but there's no scenario for putting the month at the end of the date or
                        // not following it with some other part.  Lengths of zero are excluded as most calendars have 12 months.
                        List<int> abbreviatedMonthNameLengths = CultureInfo.CurrentCulture.DateTimeFormat.AbbreviatedMonthNames.Select(name => name.Length).Distinct().Where(length => length > 0).ToList();
                        int minimumSelectionLength = abbreviatedMonthNameLengths.Min();
                        currentPart.IsSelectionLengthVariable = abbreviatedMonthNameLengths.Count > 1;
                        currentPart.SelectionLength = minimumSelectionLength;

                        dateTimeOffsetPicker.monthPartIndex = dateTimeOffsetPicker.parts.Count - 1;
                        selectionIndex += minimumSelectionLength - 3;
                    }

                    // encountered new part
                    currentPart = new IndexedDateTimePart(formatCharacter, formatIndex, selectionIndex);
                    dateTimeOffsetPicker.parts.Add(currentPart);
                    previousFormatCharacter = formatCharacter;

                    if (formatCharacter == Constant.Time.DateTimeOffsetPart)
                    {
                        // current part is hours part, which may have a minus sign and therefore has a variable start
                        currentPart.IsSelectionStartVariable = true;
                        currentPart.SelectionLength = 2;

                        // add minutes part
                        IndexedDateTimePart minutesPart = new(formatCharacter, formatIndex, currentPart.SelectionStart + currentPart.SelectionLength + 1)
                        {
                            SelectionLength = 2
                        };
                        dateTimeOffsetPicker.parts.Add(minutesPart);
                    }
                }
                else
                {
                    // still in same part
                    ++currentPart.FormatLength;
                    ++currentPart.SelectionLength;
                }
            }

            // ensure part index is valid
            if (dateTimeOffsetPicker.parts.Count < 1)
            {
                dateTimeOffsetPicker.currentPartIndex = -1;
            }
            else if (dateTimeOffsetPicker.currentPartIndex < 0)
            {
                dateTimeOffsetPicker.currentPartIndex = 0;
            }
            else if (dateTimeOffsetPicker.currentPartIndex > dateTimeOffsetPicker.parts.Count - 1)
            {
                dateTimeOffsetPicker.currentPartIndex = dateTimeOffsetPicker.parts.Count - 1;
            }

            // ensure displayed value uses current format
            dateTimeOffsetPicker.DateTimeDisplay.Text = dateTimeOffsetPicker.Value.ToString(dateTimeOffsetPicker.Format, CultureInfo.CurrentCulture);
        }

        private static void SetValue(DependencyObject obj, DependencyPropertyChangedEventArgs args)
        {
            DateTimeOffsetPicker dateTimeOffsetPicker = (DateTimeOffsetPicker)obj;

            DateTimeOffset dateTimeOffset = (DateTimeOffset)args.NewValue;
            dateTimeOffsetPicker.Calendar.DisplayDate = dateTimeOffset.Date;
            dateTimeOffsetPicker.Calendar.SelectedDate = dateTimeOffset.Date;
            dateTimeOffsetPicker.DateTimeDisplay.Text = dateTimeOffset.ToString(dateTimeOffsetPicker.Format, CultureInfo.CurrentCulture);

            if (dateTimeOffsetPicker.ValueChanged != null)
            {
                dateTimeOffsetPicker.ValueChanged.Invoke(dateTimeOffsetPicker, dateTimeOffsetPicker.Value);
            }
        }

        private bool TryParseDateTimeOffset(out DateTimeOffset dateTimeOffset)
        {
            return DateTimeOffset.TryParseExact(this.DateTimeDisplay.Text, this.Format, CultureInfo.CurrentCulture, DateTimeStyles.None, out dateTimeOffset);
        }

        private bool TrySelectCurrentPart()
        {
            if (this.currentPartIndex < 0)
            {
                return false;
            }

            IndexedDateTimePart partToSelect = this.parts[this.currentPartIndex];
            int selectionStart = partToSelect.SelectionStart;
            if ((this.currentPartIndex > this.monthPartIndex) && this.parts[this.monthPartIndex].IsSelectionLengthVariable)
            {
                // move selection right if the currently displayed month abbreviation is of length greater than the default selection length
                selectionStart += CultureInfo.CurrentCulture.DateTimeFormat.AbbreviatedMonthNames[this.Value.Month - 1].Length - this.parts[this.monthPartIndex].SelectionLength;
            }

            if (this.TryParseDateTimeOffset(out DateTimeOffset dateTimeOffset) == false)
            {
                dateTimeOffset = this.Value;
            }
            if ((partToSelect.Format == Constant.Time.DateTimeOffsetPart) && (dateTimeOffset.Offset < TimeSpan.Zero))
            {
                // move selection right by one character due to leading minus sign on offset
                // This assumes offset parts are the last parts of the format as there's no scenario for placing the offset elsewhere.
                Debug.Assert(this.currentPartIndex >= this.parts.Count - 2, "Offset part in unsupported position.");
                ++selectionStart;
            }

            this.DateTimeDisplay.Select(selectionStart, partToSelect.SelectionLength);
            return true;
        }

        private bool TrySelectPart(Direction direction)
        {
            this.currentPartIndex = direction == Direction.Next ? this.currentPartIndex + 1 : this.currentPartIndex - 1;
            if (this.currentPartIndex < 0)
            {
                this.currentPartIndex = 0;
            }
            else if (this.currentPartIndex > this.parts.Count - 1)
            {
                this.currentPartIndex = this.parts.Count - 1;
            }

            return this.TrySelectCurrentPart();
        }
    }
}