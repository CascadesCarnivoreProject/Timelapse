using Carnassial.Util;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;

namespace Carnassial.Control
{
    /// <summary>
    /// A basic TimeSpan selection control.  Support is limited to custom format strings which use f, ff, fff, ffff, fffff, ffffff, fffffff, F, FF, FFF, FFFF,
    /// FFFFF, FFFFFF, FFFFFF, hh, mm, ss, ., :, and \.
    /// </summary>
    /// <remarks>
    /// The variable selection width required by h, %h, m, %m, s, and %s is not supported.  Also not supported are formats using d, dd, ddd, dddd, ddddd, dddddd, 
    /// ddddddd, dddddddd, %f, %F, other literals, and non-standard separators.
    /// </remarks>
    public partial class TimeSpanPicker
    {
        public static readonly DependencyProperty FormatProperty = DependencyProperty.Register("Format", typeof(string), typeof(TimeSpanPicker), new FrameworkPropertyMetadata(Constant.Time.TimeSpanDisplayFormat, TimeSpanPicker.SetFormat));
        public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register("Maximum", typeof(TimeSpan), typeof(TimeSpanPicker), new FrameworkPropertyMetadata(TimeSpan.MaxValue, null, TimeSpanPicker.CoerceMaximum));
        public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register("Minimum", typeof(TimeSpan), typeof(TimeSpanPicker), new FrameworkPropertyMetadata(TimeSpan.MinValue, null, TimeSpanPicker.CoerceMinimum));
        public static readonly DependencyProperty ValueProperty = DependencyProperty.Register("Value", typeof(TimeSpan), typeof(TimeSpanPicker), new FrameworkPropertyMetadata(TimeSpan.Zero, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, TimeSpanPicker.SetValue, TimeSpanPicker.CoerceValue, false, UpdateSourceTrigger.LostFocus));

        private readonly List<IndexedDateTimePart> parts;
        private int currentPartIndex;
        private readonly TextBoxUpDownAdorner upDownButtons;

        public event Action<TimeSpanPicker, TimeSpan>? ValueChanged;

        public TimeSpanPicker()
        {
            this.currentPartIndex = -1;
            this.parts = new List<IndexedDateTimePart>();
            this.InitializeComponent();
            this.upDownButtons = new TextBoxUpDownAdorner(this.TimeSpanDisplay);
            this.upDownButtons.Button_Clicked += (textBox, direction) => { this.IncrementOrDecrement(direction); };

            this.TimeSpanDisplay.GotFocus += this.TimeSpanPicker_GotFocus;
            this.TimeSpanDisplay.LostFocus += this.TimeSpanPicker_LostFocus;
            this.TimeSpanDisplay.IsVisibleChanged += this.TimeSpanPicker_IsVisibleChanged;
            this.TimeSpanDisplay.PreviewMouseUp += this.TimeSpanPicker_PreviewMouseUp;
            this.TimeSpanDisplay.PreviewKeyDown += this.TimeSpanPicker_PreviewKeyDown;
            this.TimeSpanDisplay.TextChanged += this.TimeSpanPicker_TextChanged;

            TimeSpanPicker.SetFormat(this, new DependencyPropertyChangedEventArgs(TimeSpanPicker.FormatProperty, Constant.Time.TimeSpanDisplayFormat, Constant.Time.TimeSpanDisplayFormat));
            TimeSpanPicker.SetValue(this, new DependencyPropertyChangedEventArgs(TimeSpanPicker.ValueProperty, TimeSpan.Zero, TimeSpan.Zero));
        }

        public string Format
        {
            get { return (string)this.GetValue(TimeSpanPicker.FormatProperty); }
            set { this.SetValue(TimeSpanPicker.FormatProperty, value); }
        }

        public TimeSpan Maximum
        {
            get { return (TimeSpan)this.GetValue(TimeSpanPicker.MaximumProperty); }
            set { this.SetValue(TimeSpanPicker.MaximumProperty, value); }
        }

        public TimeSpan Minimum
        {
            get { return (TimeSpan)this.GetValue(TimeSpanPicker.MinimumProperty); }
            set { this.SetValue(TimeSpanPicker.MinimumProperty, value); }
        }

        public TimeSpan Value
        {
            get { return (TimeSpan)this.GetValue(TimeSpanPicker.ValueProperty); }
            set { this.SetValue(TimeSpanPicker.ValueProperty, value); }
        }

        private static object CoerceMaximum(DependencyObject d, object value)
        {
            TimeSpanPicker timeSpanPicker = (TimeSpanPicker)d;
            TimeSpan maximumTimeSpan = (TimeSpan)value;
            if (maximumTimeSpan < timeSpanPicker.Minimum)
            {
                throw new ArgumentException(App.FindResource<string>(Constant.ResourceKey.TimeSpanPickerCoerceMaximum), nameof(value));
            }

            if (maximumTimeSpan < timeSpanPicker.Value)
            {
                timeSpanPicker.Value = maximumTimeSpan;
            }

            return maximumTimeSpan;
        }

        private static object CoerceMinimum(DependencyObject d, object value)
        {
            TimeSpanPicker timeSpanPicker = (TimeSpanPicker)d;
            TimeSpan minimumTimeSpan = (TimeSpan)value;
            if (minimumTimeSpan > timeSpanPicker.Maximum)
            {
                throw new ArgumentException(App.FindResource<string>(Constant.ResourceKey.TimeSpanPickerCoerceMinimum), nameof(value));
            }

            if (minimumTimeSpan > timeSpanPicker.Value)
            {
                timeSpanPicker.Value = minimumTimeSpan;
            }

            return minimumTimeSpan;
        }

        private static object CoerceValue(DependencyObject d, object value)
        {
            TimeSpanPicker timeSpanPicker = (TimeSpanPicker)d;
            return ((TimeSpan)value).Limit(timeSpanPicker.Minimum, timeSpanPicker.Maximum);
        }

        protected virtual TimeSpan ConvertIncrementOrDecrementToTimeSpan(char partFormat, int incrementOrDecrement)
        {
            return partFormat switch
            {
                'd' => TimeSpan.FromDays(incrementOrDecrement),
                'f' or 
                'F' => TimeSpan.FromMilliseconds(incrementOrDecrement),
                'h' => TimeSpan.FromHours(incrementOrDecrement),
                'm' => TimeSpan.FromMinutes(incrementOrDecrement),
                's' => TimeSpan.FromSeconds(incrementOrDecrement),
                _ => throw new NotSupportedException(String.Format(CultureInfo.CurrentCulture, "Unhandled part format {0}.", partFormat)),
            };
        }

        private void IncrementOrDecrement(int incrementOrDecrement)
        {
            if (this.TryParseTimeSpan(out TimeSpan timeSpan) == false)
            {
                timeSpan = this.Value;
            }
            timeSpan += this.ConvertIncrementOrDecrementToTimeSpan(this.parts[this.currentPartIndex].Format, incrementOrDecrement);
            this.Value = timeSpan.Limit(this.Minimum, this.Maximum);
            this.TrySelectCurrentPart();
        }

        private static void SetFormat(DependencyObject obj, DependencyPropertyChangedEventArgs args)
        {
            TimeSpanPicker timeSpanPicker = (TimeSpanPicker)obj;

            timeSpanPicker.parts.Clear();
            IndexedDateTimePart currentPart = null;
            char previousFormatCharacter = '\0';
            for (int formatIndex = 0, selectionIndex = 0; formatIndex < timeSpanPicker.Format.Length; ++formatIndex, ++selectionIndex)
            {
                char formatCharacter = timeSpanPicker.Format[formatIndex];
                switch (formatCharacter)
                {
                    case 'd': // days
                    case 'f': // milliseconds
                    case 'F':
                    case 'h': // hours
                    case 'm': // minutes
                    case 's': // seconds
                        break;
                    case '\\':
                        // backslashes aren't displayed and hence aren't part of selection
                        --selectionIndex;
                        continue;
                    case '.':
                    case ':':
                    case ' ':
                        // skip delimiters and spaces
                        continue;
                    default:
                        throw new NotSupportedException(String.Format(CultureInfo.CurrentCulture, "Unsupported format character '{0}'.", formatCharacter));
                }

                if (formatCharacter != previousFormatCharacter)
                {
                    // encountered new part
                    currentPart = new IndexedDateTimePart(formatCharacter, formatIndex, selectionIndex);
                    timeSpanPicker.parts.Add(currentPart);
                    previousFormatCharacter = formatCharacter;
                }
                else
                {
                    // still in same part
                    ++currentPart.FormatLength;
                    ++currentPart.SelectionLength;
                }
            }

            // the first part may have a minus sign and therefore has variable selection indices
            if (timeSpanPicker.parts.Count > 0)
            {
                timeSpanPicker.parts[0].IsSelectionStartVariable = true;
            }

            // ensure part index is valid
            if (timeSpanPicker.parts.Count < 1)
            {
                timeSpanPicker.currentPartIndex = -1;
            }
            else if (timeSpanPicker.currentPartIndex < 0)
            {
                timeSpanPicker.currentPartIndex = 0;
            }
            else if (timeSpanPicker.currentPartIndex > timeSpanPicker.parts.Count - 1)
            {
                timeSpanPicker.currentPartIndex = timeSpanPicker.parts.Count - 1;
            }

            // ensure displayed value uses current format
            timeSpanPicker.TimeSpanDisplay.Text = timeSpanPicker.Value.ToString(timeSpanPicker.Format, CultureInfo.CurrentCulture);
        }

        private static void SetValue(DependencyObject obj, DependencyPropertyChangedEventArgs args)
        {
            TimeSpanPicker timeSpanPicker = (TimeSpanPicker)obj;
            TimeSpan timeSpan = (TimeSpan)args.NewValue;
            timeSpanPicker.TimeSpanDisplay.Text = TimeSpanPicker.TimeSpanToString(timeSpan, timeSpanPicker.Format);

            if (timeSpanPicker.ValueChanged != null)
            {
                timeSpanPicker.ValueChanged.Invoke(timeSpanPicker, timeSpanPicker.Value);
            }
        }

        private void TimeSpanPicker_GotFocus(object sender, RoutedEventArgs e)
        {
            this.TrySelectCurrentPart();
            e.Handled = true;
        }

        private void TimeSpanPicker_LostFocus(object sender, RoutedEventArgs e)
        {
            this.TimeSpanDisplay.Text = TimeSpanPicker.TimeSpanToString(this.Value, this.Format);
        }

        private void TimeSpanPicker_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            AdornerLayer adornerLayer = AdornerLayer.GetAdornerLayer(this);
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

        private void TimeSpanPicker_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                // navigation, increment/decrement
                case Key.Up:
                case Key.Down:
                case Key.Right:
                case Key.Left:
                    if (this.TryParseTimeSpan(out TimeSpan _) == false)
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
                // editing, focus changes
                case Key.Back:
                case Key.D0:
                case Key.D1:
                case Key.D2:
                case Key.D3:
                case Key.D4:
                case Key.D5:
                case Key.D6:
                case Key.D7:
                case Key.D8:
                case Key.D9:
                case Key.Decimal:
                case Key.Delete:
                case Key.Enter:
                case Key.Escape:
                case Key.NumPad0:
                case Key.NumPad1:
                case Key.NumPad2:
                case Key.NumPad3:
                case Key.NumPad4:
                case Key.NumPad5:
                case Key.NumPad6:
                case Key.NumPad7:
                case Key.NumPad8:
                case Key.NumPad9:
                case Key.OemPeriod:
                case Key.OemSemicolon:
                case Key.System:
                case Key.Tab:
                    // leave event unhandled so key is accepted as input
                    return;
                default:
                    // block all other keys as they're neither navigation, editing, or digits
                    break;
            }
            e.Handled = true;
        }

        private void TimeSpanPicker_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (this.TimeSpanDisplay.SelectionLength == 0)
            {
                this.TrySelectCurrentPart();
            }
        }

        private void TimeSpanPicker_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (this.TryParseTimeSpan(out TimeSpan timeSpan))
            {
                this.ErrorIcon.Visibility = Visibility.Collapsed;
                this.Value = timeSpan;
            }
            else
            {
                this.ErrorIcon.Visibility = Visibility.Visible;
            }
        }

        private static string TimeSpanToString(TimeSpan value, string format)
        {
            return (value < TimeSpan.Zero ? "-" : String.Empty) + value.ToString(format, CultureInfo.CurrentCulture);
        }

        private bool TryParseTimeSpan(out TimeSpan timeSpan)
        {
            if (TimeSpan.TryParseExact(this.TimeSpanDisplay.Text, this.Format, CultureInfo.CurrentCulture, TimeSpanStyles.None, out timeSpan))
            {
                return true;
            }
            return TimeSpan.TryParseExact(this.TimeSpanDisplay.Text, @"\-" + this.Format, CultureInfo.CurrentCulture, TimeSpanStyles.AssumeNegative, out timeSpan);
        }

        private bool TrySelectCurrentPart()
        {
            if (this.currentPartIndex < 0)
            {
                return false;
            }

            IndexedDateTimePart partToSelect = this.parts[this.currentPartIndex];
            int selectionStart = partToSelect.SelectionStart;
            if (this.TryParseTimeSpan(out TimeSpan timeSpan) == false)
            {
                timeSpan = this.Value;
            }
            if (timeSpan < TimeSpan.Zero)
            {
                // move selection right by one character due to leading minus sign
                ++selectionStart;
            }
            this.TimeSpanDisplay.Select(selectionStart, partToSelect.SelectionLength);
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