using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace Carnassial.Control
{
    internal class HorizontalLineAdorner : Adorner
    {
        private readonly AdornerLayer adornerLayer;
        private readonly DataEntryControls dataEntryControls;
        private Point position;

        public HorizontalLineAdorner(DataEntryControls dataEntryControls)
            : base(dataEntryControls.ControlsView)
        {
            this.adornerLayer = AdornerLayer.GetAdornerLayer(this.AdornedElement);
            this.dataEntryControls = dataEntryControls;
            this.position = new Point(0.0, 0.0);

            this.adornerLayer.Add(this);
        }

        public override GeneralTransform GetDesiredTransform(GeneralTransform transform)
        {
            GeneralTransformGroup result = new();
            result.Children.Add(new TranslateTransform(this.position.X, this.position.Y));
            return result;
        }

        public void UpdatePosition(DragEventArgs dragEvent)
        {
            if (this.dataEntryControls.TryFindDataEntryControl(dragEvent.GetPosition(this.AdornedElement), out DataEntryControl control))
            {
                Point controlPosition = control.Container.TranslatePoint(new Point(0.0, 0.0), this.AdornedElement);
                this.position.Y = controlPosition.Y + control.Container.ActualHeight;
                this.adornerLayer.Update(this.AdornedElement);
            }
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            drawingContext.DrawLine(new Pen(Brushes.Black, 2.0), new Point(this.Margin.Left, 0.0), new Point(((FrameworkElement)this.AdornedElement).ActualWidth - this.Margin.Right, 0.0));
        }

        public void Remove()
        {
            this.adornerLayer.Remove(this);
        }
    }
}
