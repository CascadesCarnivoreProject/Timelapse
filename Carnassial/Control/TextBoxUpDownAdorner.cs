using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace Carnassial.Control
{
    public class TextBoxUpDownAdorner : Adorner
    {
        private static readonly Pen Outline = new(new SolidColorBrush(Color.FromArgb(64, 255, 255, 255)), 5);
        private static readonly Brush Fill = Brushes.Black;

        private double bottom;
        private bool textBoxHasSelection;
        private double top;
        private readonly StreamGeometry triangle;
        private double x;

        public event Action<TextBox, int>? Button_Clicked;

        public TextBoxUpDownAdorner(TextBox adornedTextBox)
            : base(adornedTextBox)
        {
            this.triangle = new StreamGeometry()
            {
                FillRule = FillRule.Nonzero
            };

            using (StreamGeometryContext geometryContext = this.triangle.Open())
            {
                geometryContext.BeginFigure(new Point(-6, 0), true /* filled */, true /* closed */);
                geometryContext.LineTo(new Point(6, 0), true, false);
                geometryContext.LineTo(new Point(0, 8), true, false);
            }
            this.triangle.Freeze();

            this.MouseDown += (s, e) =>
            {
                if (this.Button_Clicked != null)
                {
                    bool up = e.GetPosition(this.AdornedElement).Y < (this.top + this.bottom) / 2;
                    this.Button_Clicked((TextBox)this.AdornedElement, up ? 1 : -1);
                }
            };

            adornedTextBox.LostFocus += this.FocusLostOrSelectionChanged;
            adornedTextBox.SelectionChanged += this.FocusLostOrSelectionChanged;
        }

        private void FocusLostOrSelectionChanged(object sender, RoutedEventArgs e)
        {
            TextBox adornedTextBox = (TextBox)this.AdornedElement;
            if (adornedTextBox.IsFocused && adornedTextBox.IsVisible)
            {
                // during OnRender() GetRectFromCharacterIndex() may return infinite values so check there's a selection first
                this.textBoxHasSelection = adornedTextBox.SelectionLength > 0;
                if (this.textBoxHasSelection)
                {
                    Rect startCharacterBounds = adornedTextBox.GetRectFromCharacterIndex(adornedTextBox.SelectionStart);
                    Rect endCharacterBounds = adornedTextBox.GetRectFromCharacterIndex(adornedTextBox.SelectionStart + adornedTextBox.SelectionLength);
                    this.top = startCharacterBounds.Top - 2;
                    this.bottom = startCharacterBounds.Bottom + 2;
                    this.x = (startCharacterBounds.Left + endCharacterBounds.Left) / 2;
                }
            }
            else
            {
                this.textBoxHasSelection = false;
            }

            this.InvalidateVisual();
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            if (this.textBoxHasSelection)
            {
                // up button
                drawingContext.PushTransform(new TranslateTransform(this.x, this.top));
                drawingContext.PushTransform(new ScaleTransform(1, -1));
                drawingContext.DrawGeometry(TextBoxUpDownAdorner.Fill, TextBoxUpDownAdorner.Outline, this.triangle);
                drawingContext.Pop();
                drawingContext.Pop();

                // down button
                drawingContext.PushTransform(new TranslateTransform(this.x, this.bottom));
                drawingContext.DrawGeometry(TextBoxUpDownAdorner.Fill, TextBoxUpDownAdorner.Outline, this.triangle);
                drawingContext.Pop();
            }
        }
    }
}
