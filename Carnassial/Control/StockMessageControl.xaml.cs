using Carnassial.Dialog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace Carnassial.Control
{
    public partial class StockMessageControl : UserControl
    {
        private MessageBoxImage statusImage;

        public StockMessageControl()
        {
            this.InitializeComponent();
            this.Image = MessageBoxImage.Warning;
        }

        public MessageBoxImage Image
        {
            get
            {
                return this.statusImage;
            }
            set
            {
                this.statusImage = value;
                this.StatusImage.Source = value switch
                {
                    // the MessageBoxImage enum has some duplicate values, so not all of them needed cases
                    //   - Hand = Stop = Error
                    //   - Exclamation = Warning
                    //   - Asterisk = Information
                    MessageBoxImage.Question => Constant.Images.StatusHelp.Value,
                    MessageBoxImage.Warning => Constant.Images.StatusWarning.Value,
                    MessageBoxImage.None => null,
                    MessageBoxImage.Information => Constant.Images.StatusInformation.Value,
                    MessageBoxImage.Error => Constant.Images.StatusError.Value,
                    _ => throw new NotSupportedException(String.Format(CultureInfo.CurrentCulture, "Unhandled icon type {0}.", value)),
                };
            }
        }

        public bool DisplayHideExplanation
        {
            get
            {
                return this.HideExplanation.Visibility == Visibility.Visible;
            }
            set
            {
                this.HideExplanation.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private static IEnumerable<object> Format(IEnumerable<Inline> textElements, object?[] args)
        {
            if (textElements == null)
            {
                yield break;
            }

            foreach (TextElement textElement in textElements)
            {
                Debug.Assert(textElement != null, "Inline unexpectedly null.");
                if (textElement is LineBreak)
                {
                    yield return new LineBreak();
                }
                else if (textElement is Run run)
                {
                    yield return new Run(String.Format(CultureInfo.CurrentCulture, run.Text, args));
                }
                else
                {
                    throw new NotSupportedException(String.Format(CultureInfo.CurrentCulture, "Unhandled inline of type {0}.", textElement.GetType()));
                }
            }
        }

        public static string GetHint(StockMessageControl message)
        {
            return message.Hint.Text;
        }

        public static string GetProblem(StockMessageControl message)
        {
            return message.Problem.Text;
        }

        public static string GetReason(StockMessageControl message)
        {
            return message.Reason.Text;
        }

        public static string GetResult(StockMessageControl message)
        {
            return message.Result.Text;
        }

        public static string GetSolution(StockMessageControl message)
        {
            return message.Solution.Text;
        }

        public static string GetTitle(StockMessageControl message)
        {
            return message.Title.Text;
        }

        public string GetWhat()
        {
            StringBuilder what = new();
            foreach (Inline inline in this.What.Inlines)
            {
                if (inline is Run run)
                {
                    what.Append(run.Text);
                }
                else if (inline is LineBreak)
                {
                    what.Append(Environment.NewLine);
                }
                else
                {
                    // best effort as this method is called for reporting on unhandled exceptions
                    Debug.Fail(String.Format(CultureInfo.InvariantCulture, "Unhandled inline type {0}.", inline.GetType()));
                }
            }
            return what.ToString();
        }

        public static string GetWhat(StockMessageControl message)
        {
            return message.GetWhat();
        }

        private void HideExplanation_CheckedChanged(object sender, RoutedEventArgs e)
        {
            this.SetVisibility();
        }

        public void Initialize(Message message, params object?[] args)
        {
            Debug.Assert(String.IsNullOrWhiteSpace(message.Title) == false, "Message title unexpectedly empty.");

            this.Image = message.Image;
            this.Title.Text = String.Format(CultureInfo.CurrentCulture, message.Title, args);
            this.DisplayHideExplanation = message.DisplayHideExplanation;

            this.Problem.Inlines.AddRange(StockMessageControl.Format(message.Problem, args));
            this.What.Inlines.AddRange(StockMessageControl.Format(message.What, args));
            this.Reason.Inlines.AddRange(StockMessageControl.Format(message.Reason, args));
            this.Solution.Inlines.AddRange(StockMessageControl.Format(message.Solution, args));
            this.Result.Inlines.AddRange(StockMessageControl.Format(message.Result, args));
            this.Hint.Inlines.AddRange(StockMessageControl.Format(message.Hint, args));

            this.SetVisibility();
        }

        public void SetVisibility()
        {
            if (this.HideExplanation.IsChecked == true)
            {
                this.Hint.Visibility = Visibility.Collapsed;
                this.Problem.Visibility = Visibility.Collapsed;
                this.Reason.Visibility = Visibility.Collapsed;
                this.Result.Visibility = Visibility.Collapsed;
                this.Solution.Visibility = Visibility.Collapsed;
                this.What.Visibility = Visibility.Collapsed;
            }
            else
            {
                this.Hint.Visibility = this.Hint.Inlines.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
                this.Problem.Visibility = this.Problem.Inlines.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
                this.Reason.Visibility = this.Reason.Inlines.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
                this.Result.Visibility = this.Result.Inlines.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
                this.Solution.Visibility = this.Solution.Inlines.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
                this.What.Visibility = this.What.Inlines.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        public static void SetHint(StockMessageControl message, string value)
        {
            message.Hint.Text = value;
        }

        public static void SetProblem(StockMessageControl message, string value)
        {
            message.Problem.Text = value;
        }

        public static void SetReason(StockMessageControl message, string value)
        {
            message.Reason.Text = value;
        }

        public static void SetResult(StockMessageControl message, string value)
        {
            message.Result.Text = value;
        }

        public static void SetSolution(StockMessageControl message, string value)
        {
            message.Solution.Text = value;
        }

        public static void SetTitle(StockMessageControl message, string value)
        {
            message.Title.Text = value;
        }

        public static void SetWhat(StockMessageControl message, string value)
        {
            message.What.Text = value;
        }
    }
}
