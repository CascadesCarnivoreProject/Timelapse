﻿using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace Carnassial.Images
{
    internal class MagnifyingGlass : Canvas
    {
        // current angle of the lens only
        private double lensAngle;
        private Canvas lensCanvas;

        private Ellipse magnifierLens;
        // current angle of the entire magnifying glass
        private double magnifyingGlassAngle;

        private RotateTransform rotation;
        private TranslateTransform translation;

        /// <summary>Gets or sets the diameter of the image shown in the magnifying glass's lens in pixels.</summary>
        /// <remarks>
        /// In a screen sense, the magnification is the lens diameter divided by the field of view (currently 1 - 17x).  Relative to the display image it's
        /// usually several times higher.
        /// </remarks>
        public double FieldOfView { get; set; }

        public new MarkableCanvas Parent { get; set; }

        public MagnifyingGlass(MarkableCanvas markableCanvas)
        {
            this.FieldOfView = Constant.MarkableCanvas.MagnifyingGlassDefaultFieldOfView;
            this.IsEnabled = false;
            this.IsHitTestVisible = false;
            this.HorizontalAlignment = HorizontalAlignment.Left;
            this.Parent = markableCanvas;
            this.VerticalAlignment = VerticalAlignment.Top;
            this.Visibility = Visibility.Collapsed;

            this.lensAngle = 0.0;
            this.magnifyingGlassAngle = 0.0;

            // Create the handle of the magnifying glass
            Line handle = new Line();
            handle.StrokeThickness = 5;
            handle.X1 = Constant.MarkableCanvas.MagnifyingGlassHandleStart;
            handle.Y1 = Constant.MarkableCanvas.MagnifyingGlassHandleStart;
            handle.X2 = Constant.MarkableCanvas.MagnifyingGlassHandleEnd;
            handle.Y2 = Constant.MarkableCanvas.MagnifyingGlassHandleEnd;
            LinearGradientBrush handleBrush = new LinearGradientBrush();
            handleBrush.StartPoint = new Point(0.78786, 1);
            handleBrush.EndPoint = new Point(1, 0.78786);
            handleBrush.GradientStops.Add(new GradientStop(Colors.DarkGreen, 0));
            handleBrush.GradientStops.Add(new GradientStop(Colors.LightGreen, 0.9));
            handleBrush.GradientStops.Add(new GradientStop(Colors.Green, 1));
            handle.Stroke = handleBrush;
            this.Children.Add(handle);

            // Create the lens of the magnifying glass
            this.lensCanvas = new Canvas();
            this.Children.Add(this.lensCanvas);

            // lens has a white backgound
            Ellipse lensBackground = new Ellipse();
            lensBackground.Width = Constant.MarkableCanvas.MagnifyingGlassDiameter;
            lensBackground.Height = Constant.MarkableCanvas.MagnifyingGlassDiameter;
            lensBackground.Fill = Brushes.White;
            this.lensCanvas.Children.Add(lensBackground);

            this.magnifierLens = new Ellipse();
            this.magnifierLens.Width = Constant.MarkableCanvas.MagnifyingGlassDiameter;
            this.magnifierLens.Height = Constant.MarkableCanvas.MagnifyingGlassDiameter;
            this.magnifierLens.StrokeThickness = 3;

            // fill the lens
            VisualBrush lensFill = new VisualBrush();
            lensFill.ViewboxUnits = BrushMappingMode.Absolute;
            lensFill.Viewbox = new Rect(0, 0, 50, 50);
            lensFill.ViewportUnits = BrushMappingMode.RelativeToBoundingBox;
            lensFill.Viewport = new Rect(0, 0, 1, 1);
            this.magnifierLens.Fill = lensFill;

            // outline the lens
            LinearGradientBrush outlineBrush = new LinearGradientBrush();
            outlineBrush.StartPoint = new Point(0, 0);
            outlineBrush.EndPoint = new Point(0, 1);
            ColorConverter cc = new ColorConverter();
            outlineBrush.GradientStops.Add(new GradientStop((Color)cc.ConvertFrom("#AAA"), 0));
            outlineBrush.GradientStops.Add(new GradientStop((Color)cc.ConvertFrom("#111"), 1));
            this.magnifierLens.Stroke = outlineBrush;
            this.lensCanvas.Children.Add(this.magnifierLens);

            Ellipse lensImage = new Ellipse();
            Canvas.SetLeft(lensImage, 2);
            Canvas.SetTop(lensImage, 2);
            lensImage.StrokeThickness = 4;
            lensImage.Width = Constant.MarkableCanvas.MagnifyingGlassDiameter - 4;
            lensImage.Height = Constant.MarkableCanvas.MagnifyingGlassDiameter - 4;
            this.lensCanvas.Children.Add(lensImage);

            // crosshairs
            Line verticalCrosshair = new Line();
            verticalCrosshair.StrokeThickness = 0.25;
            verticalCrosshair.X1 = 5;
            verticalCrosshair.Y1 = Constant.MarkableCanvas.MagnifyingGlassDiameter / 2;
            verticalCrosshair.X2 = Constant.MarkableCanvas.MagnifyingGlassDiameter - 5;
            verticalCrosshair.Y2 = Constant.MarkableCanvas.MagnifyingGlassDiameter / 2;
            verticalCrosshair.Stroke = Brushes.Black;
            verticalCrosshair.Opacity = 0.5;
            this.lensCanvas.Children.Add(verticalCrosshair);

            Line horizontalCrosshair = new Line();
            horizontalCrosshair.StrokeThickness = 0.25;
            horizontalCrosshair.X1 = Constant.MarkableCanvas.MagnifyingGlassDiameter / 2;
            horizontalCrosshair.Y1 = 5;
            horizontalCrosshair.X2 = Constant.MarkableCanvas.MagnifyingGlassDiameter / 2;
            horizontalCrosshair.Y2 = Constant.MarkableCanvas.MagnifyingGlassDiameter - 5;
            horizontalCrosshair.Stroke = Brushes.Black;
            horizontalCrosshair.Opacity = 0.5;
            this.lensCanvas.Children.Add(horizontalCrosshair);

            // set render transform
            // Rotate the glass before translating it as that ordering means translation calculations don't have to account for rotation.  If this is changed
            // RedrawIfVisible() must be updated.
            this.rotation = new RotateTransform(this.magnifyingGlassAngle, Constant.MarkableCanvas.MagnifyingGlassDiameter, Constant.MarkableCanvas.MagnifyingGlassDiameter);
            this.translation = new TranslateTransform();

            TransformGroup transformGroup = new TransformGroup();
            transformGroup.Children.Add(this.rotation);
            transformGroup.Children.Add(this.translation);
            this.RenderTransform = transformGroup;
        }

        // return the current angle if it matches one of the desired angle, or the desired angle closest to the current angle in degrees
        private double AdjustAngle(double currentAngle, double newAngleOption1, double newAngleOption2)
        {
            if (currentAngle == newAngleOption2)
            {
                return currentAngle;
            }
            else if (Math.Abs(currentAngle - newAngleOption1) > 180.0)
            {
                return newAngleOption2;
            }
            return newAngleOption1;
        }

        public void Hide()
        {
            this.Visibility = Visibility.Collapsed;
        }

        public void RedrawIfVisible(Canvas canvasToMagnify, Point newTranslation)
        {
            // not visible or nothing to draw
            if ((this.IsEnabled == false) || 
                (this.IsVisible == false) ||
                (this.Visibility != Visibility.Visible))
            {
                return;
            }

            // given the mouse position over the displayed image find the equivalent position in magnified image (which is a different size)
            Point mousePosition = Mouse.GetPosition(this.Parent.ImageToDisplay);
            Point mouseLocationRatio = Marker.ConvertPointToRatio(mousePosition, this.Parent.ImageToDisplay.ActualWidth, this.Parent.ImageToDisplay.ActualHeight);
            Point magnifiedLocation = Marker.ConvertRatioToPoint(mouseLocationRatio, canvasToMagnify.Width, canvasToMagnify.Height);

            // create a brush from the unaltered image in the magnification canvas and use it to fill the magnifying glass
            VisualBrush magnifierBrush = new VisualBrush(canvasToMagnify);
            magnifierBrush.ViewboxUnits = BrushMappingMode.Absolute;
            magnifierBrush.ViewportUnits = BrushMappingMode.RelativeToBoundingBox;
            magnifierBrush.Viewport = new Rect(0, 0, 1, 1);
            magnifierBrush.Viewbox = new Rect(magnifiedLocation.X - this.FieldOfView / 2.0, magnifiedLocation.Y - this.FieldOfView / 2.0, this.FieldOfView, this.FieldOfView);
            this.magnifierLens.Fill = magnifierBrush;

            // figure out the magnifying glass angle needed
            // The idea is that we will start rotating when the magnifying glass is near the top and the left of the display
            // The critical distance is size for the Y direction, and somewhat larger than size for the X direction (as we have to start
            // rotating earlier so it doesn't get clipped). xsize is somewhat arbitrary, i.e., determined by trial and error
            // positions of edges where angle should change 
            double leftEdge = Constant.MarkableCanvas.MagnifyingGlassDiameter;
            double rightEdge = this.Parent.ImageToDisplay.ActualWidth - Constant.MarkableCanvas.MagnifyingGlassDiameter;
            double topEdge = Constant.MarkableCanvas.MagnifyingGlassDiameter;
            double bottomEdge = this.Parent.ImageToDisplay.ActualHeight - Constant.MarkableCanvas.MagnifyingGlassDiameter;

            // In various cases, several angles can work so choose a new angle whose difference from the existing angle will cause the least amount of animation 
            double newMagnifyingGlassAngle;
            if ((mousePosition.X < leftEdge) && (mousePosition.Y < topEdge))
            {
                newMagnifyingGlassAngle = 180.0;                                                     // upper left corner
            }
            else if ((mousePosition.X < leftEdge) && (mousePosition.Y > bottomEdge))
            {
                newMagnifyingGlassAngle = 90.0;                                                      // lower left corner
            }
            else if (mousePosition.X < leftEdge)
            {
                newMagnifyingGlassAngle = this.AdjustAngle(this.magnifyingGlassAngle, 90.0, 180.0);  // middle left edge
            }
            else if ((mousePosition.X > rightEdge) && (mousePosition.Y < topEdge))
            {
                newMagnifyingGlassAngle = 270.0;                                                     // upper right corner
            }
            else if ((mousePosition.X > rightEdge) && (mousePosition.Y > bottomEdge))
            {
                newMagnifyingGlassAngle = 0.0;                                                       // lower right corner
            }
            else if (mousePosition.X > rightEdge)
            {
                newMagnifyingGlassAngle = this.AdjustAngle(this.magnifyingGlassAngle, 270.0, 0.0);   // middle right edge
            }
            else if (mousePosition.Y < topEdge)
            {
                newMagnifyingGlassAngle = this.AdjustAngle(this.magnifyingGlassAngle, 270.0, 180.0); // top edge, middle
            }
            else if (mousePosition.Y > bottomEdge)
            {
                newMagnifyingGlassAngle = this.AdjustAngle(this.magnifyingGlassAngle, 0.0, 90.0);    // bottom edge, middle
            }
            else
            {
                newMagnifyingGlassAngle = this.magnifyingGlassAngle; // far enough from edges the magnifer stays on the display image at any angle
            }

            // If the angle has changed, animate the magnifying glass and its contained image to the new angle
            if (this.magnifyingGlassAngle != newMagnifyingGlassAngle)
            {
                Debug.Assert(newMagnifyingGlassAngle == 0.0 || newMagnifyingGlassAngle == 90.0 || newMagnifyingGlassAngle == 180.0 || newMagnifyingGlassAngle == 270.0, String.Format("Unexpected magnifying glass angle {0}.", newMagnifyingGlassAngle));

                double newLensAngle = -newMagnifyingGlassAngle;
                // minimize the lens rotation in those cases where it would turn the long way around. 
                if (this.magnifyingGlassAngle == 270.0 && newMagnifyingGlassAngle == 0.0)
                {
                    newLensAngle = 0.0; // subtract the rotation of the magnifying glass to counter that rotational effect
                }
                else if (this.magnifyingGlassAngle == 0.0 && newMagnifyingGlassAngle == 270.0)
                {
                    newLensAngle = 90.0;
                }
                Debug.Assert(newLensAngle == 90.0 || newLensAngle == 0.0 || newLensAngle == -90.0 || newLensAngle == -180.0 || newLensAngle == -270.0, String.Format("Unexpected lens angle {0}.", newLensAngle));

                // rotate the lens within the magnifying glass to compensate for its overall rotation
                Duration animationDuration = new Duration(new TimeSpan(0, 0, 0, 0, 500));
                DoubleAnimation lensAnimation = new DoubleAnimation(this.lensAngle, newLensAngle, animationDuration);
                RotateTransform rotateTransformLens = new RotateTransform(this.magnifyingGlassAngle, Constant.MarkableCanvas.MagnifyingGlassDiameter / 2.0, Constant.MarkableCanvas.MagnifyingGlassDiameter / 2.0);
                rotateTransformLens.BeginAnimation(RotateTransform.AngleProperty, lensAnimation);
                this.lensCanvas.RenderTransform = rotateTransformLens;

                // rotate the entire magnifying glass to its new angle
                this.rotation.Angle = this.magnifyingGlassAngle;
                DoubleAnimation rotateAnimation = new DoubleAnimation(this.magnifyingGlassAngle, newMagnifyingGlassAngle, animationDuration);
                this.rotation.BeginAnimation(RotateTransform.AngleProperty, rotateAnimation);

                // Save angles for the next iteration.
                this.magnifyingGlassAngle = newMagnifyingGlassAngle;
                this.lensAngle = newLensAngle;
            }

            // set translation for current mouse location
            this.translation.X = newTranslation.X;
            this.translation.Y = newTranslation.Y;

            Canvas.SetLeft(this, mousePosition.X - Constant.MarkableCanvas.MagnifyingGlassDiameter);
            Canvas.SetTop(this, mousePosition.Y - Constant.MarkableCanvas.MagnifyingGlassDiameter);
        }

        public void Show()
        {
            this.Visibility = Visibility.Visible;
        }
    }
}
