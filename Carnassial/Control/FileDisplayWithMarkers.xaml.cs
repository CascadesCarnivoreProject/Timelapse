using Carnassial.Data;
using Carnassial.Images;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Carnassial.Control
{
    public partial class FileDisplayWithMarkers : UserControl
    {
        private readonly ZoomBookmark bookmark;

        // the canvas to magnify contains both an image and markers so the magnifying glass view matches the display image
        private readonly Canvas magnifierCanvas;

        // the image displayed in the magnifying glass
        // Unlike the display image, this image is rendered at 1:1 and the scaled or translated in the magnifier's view of it.
        private readonly Image imageToMagnify;

        // render transforms for the display image and markers
        // RedrawMagnifyingGlassIfVisible() must be kept in sync with these.  See comments there.
        private readonly ScaleTransform displayImageScale;
        private readonly TranslateTransform displayImageTranslation;

        private readonly MagnifyingGlass magnifyingGlass;

        private List<Marker>? markers;

        // mouse state used to discriminate clicks from drags
        private UIElement? mouseDownSender;
        private DateTime mouseDownTime;
        private Point mouseDownLocation;
        private Point previousMousePosition;

        public event EventHandler<MarkerCreatedOrDeletedEventArgs>? MarkerCreatedOrDeleted;

        /// <summary>
        /// Gets or sets the maximum zoom of the display image.
        /// </summary>
        public double ZoomMaximum { get; set; }

        public FileDisplayWithMarkers()
        {
            this.InitializeComponent();
            this.markers = new List<Marker>();
            this.ResetMaximumZoom();

            // initialize render transforms
            // scale transform's center is set during layout once the image size is known
            // default bookmark is default zoomed out, normal pan state
            this.bookmark = new ZoomBookmark();
            this.displayImageScale = new ScaleTransform(this.bookmark.Scale.X, this.bookmark.Scale.Y);
            this.displayImageTranslation = new TranslateTransform(this.bookmark.Translation.X, this.bookmark.Translation.Y);

            TransformGroup transformGroup = new();
            transformGroup.Children.Add(this.displayImageScale);
            transformGroup.Children.Add(this.displayImageTranslation);

            this.FileDisplay.Image.RenderTransform = transformGroup;
            this.FileDisplay.Image.MouseDown += this.FileDisplayImage_MouseDown;
            this.FileDisplay.Image.MouseUp += this.FileDisplayImage_MouseUp;
            this.FileDisplay.Image.MouseWheel += this.FileDisplayImage_MouseWheel;
            this.FileDisplay.Image.SizeChanged += this.FileDisplayImage_SizeChanged;

            this.imageToMagnify = new Image();
            this.imageToMagnify.SizeChanged += this.ImageToMagnify_SizeChanged;

            this.magnifierCanvas = new Canvas();
            this.magnifierCanvas.SizeChanged += this.CanvasToMagnify_SizeChanged;
            this.magnifierCanvas.Children.Add(this.imageToMagnify);

            // set up the magnifying glass
            this.magnifyingGlass = new MagnifyingGlass();

            Canvas.SetZIndex(this.magnifyingGlass, 1); // should always be on top
            this.DisplayCanvas.Children.Add(this.magnifyingGlass);

            // event handlers for image interaction: keys, mouse handling for markers
            // this.mouseDownLocation not initialized as it's set from the display image's mouse down handler
            // this.mouseDownSender left as null as it's set from the display image's mouse down handler
            // this.mouseDownTime not initialized as it's not consumed until after being set from the display image's mouse down handler
            this.MouseLeave += this.MarkableCanvas_MouseLeave;
            this.MouseMove += this.MarkableCanvas_MouseMove;

            this.SizeChanged += this.MarkableCanvas_SizeChanged;
        }

        /// <summary>
        /// Gets or sets the markers on the image.
        /// </summary>
        public List<Marker>? Markers
        {
            get
            {
                return this.markers;
            }
            set
            {
                // update markers
                this.markers = value;
                // render new markers and update display image
                this.RedrawMarkers();
                // update magnifying glass content
                this.RedrawMagnifyingGlassIfVisible();
            }
        }

        /// <summary>
        /// Gets or sets the amount of ImageToMagnify shown in the magnifying glass.  A higher zoom is a smaller field of view.
        /// </summary>
        private double MagnifyingGlassFieldOfView
        {
            get
            {
                return this.magnifyingGlass.FieldOfView;
            }
            set
            {
                // clamp the value
                if (value < Constant.ImageDisplay.MagnifyingGlassMinimumFieldOfView)
                {
                    value = Constant.ImageDisplay.MagnifyingGlassMinimumFieldOfView;
                }
                else if (value > Constant.ImageDisplay.MagnifyingGlassMaximumFieldOfView)
                {
                    value = Constant.ImageDisplay.MagnifyingGlassMaximumFieldOfView;
                }
                this.magnifyingGlass.FieldOfView = value;

                // update magnifier content if there is something to magnify
                if (this.imageToMagnify.Source != null && this.FileDisplay.Image.ActualWidth > 0)
                {
                    this.RedrawMagnifyingGlassIfVisible();
                }
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the magnifying glass is generally visible or hidden, and returns its state.
        /// </summary>
        public bool MagnifyingGlassEnabled
        {
            get
            {
                return this.magnifyingGlass.IsEnabled;
            }
            set
            {
                this.magnifyingGlass.IsEnabled = value;
                if (value && (this.FileDisplay.Image.Visibility == Visibility.Visible))
                {
                    // draw the magnifying glass if it was just enabled and an image is being displayed
                    // Note: the magnifying glass may immediately be hidden again if the mouse isn't over the display image.
                    this.magnifyingGlass.Show();
                    this.RedrawMagnifyingGlassIfVisible();
                }
                else
                {
                    this.magnifyingGlass.Hide();
                }
            }
        }

        private Canvas AddMarker(Marker marker, Size canvasRenderSize, bool imageToDisplayMarkers)
        {
            Canvas markerCanvas = new()
            {
                ToolTip = marker.Tooltip,
                Tag = marker
            };

            // create a marker
            Ellipse mark = new()
            {
                Width = Constant.ImageDisplay.MarkerDiameter,
                Height = Constant.ImageDisplay.MarkerDiameter,
                Stroke = marker.Highlight ? Brushes.MediumBlue : Brushes.Gold,
                StrokeThickness = Constant.ImageDisplay.MarkerStrokeThickness,
                Fill = Constant.ImageDisplay.MarkerFillBrush
            };
            markerCanvas.Children.Add(mark);

            // draw another ellipse as a black outline around it
            Ellipse blackOutline = new()
            {
                Stroke = Brushes.Black,
                Width = mark.Width + 1,
                Height = mark.Height + 1,
                StrokeThickness = 1
            };
            markerCanvas.Children.Add(blackOutline);

            // and another ellipse as a white outline around it
            Ellipse whiteOutline = new()
            {
                Stroke = Brushes.White,
                Width = blackOutline.Width + 1,
                Height = blackOutline.Height + 1,
                StrokeThickness = 1
            };
            markerCanvas.Children.Add(whiteOutline);

            // maybe add emphasis
            double outerDiameter = whiteOutline.Width;
            Ellipse? glow = null;
            if (marker.Emphasize)
            {
                glow = new Ellipse()
                {
                    Width = whiteOutline.Width + Constant.ImageDisplay.MarkerGlowDiameterIncrease,
                    Height = whiteOutline.Height + Constant.ImageDisplay.MarkerGlowDiameterIncrease,
                    StrokeThickness = Constant.ImageDisplay.MarkerGlowStrokeThickness,
                    Stroke = mark.Stroke,
                    Opacity = Constant.ImageDisplay.MarkerGlowOpacity
                };
                markerCanvas.Children.Add(glow);

                outerDiameter = glow.Width;
            }

            markerCanvas.Width = outerDiameter;
            markerCanvas.Height = outerDiameter;

            double position = (markerCanvas.Width - mark.Width) / 2.0;
            Canvas.SetLeft(mark, position);
            Canvas.SetTop(mark, position);

            position = (markerCanvas.Width - blackOutline.Width) / 2.0;
            Canvas.SetLeft(blackOutline, position);
            Canvas.SetTop(blackOutline, position);

            position = (markerCanvas.Width - whiteOutline.Width) / 2.0;
            Canvas.SetLeft(whiteOutline, position);
            Canvas.SetTop(whiteOutline, position);

            if (marker.Emphasize)
            {
                Debug.Assert(glow != null);
                position = (markerCanvas.Width - glow.Width) / 2.0;
                Canvas.SetLeft(glow, position);
                Canvas.SetTop(glow, position);
            }

            if (marker.ShowLabel)
            {
                Label label = new()
                {
                    Content = marker.Tooltip,
                    Opacity = 0.6,
                    Background = Brushes.White,
                    Padding = new Thickness(0, 0, 0, 0),
                    Margin = new Thickness(0, 0, 0, 0)
                };
                markerCanvas.Children.Add(label);

                position = (markerCanvas.Width / 2.0) + (whiteOutline.Width / 2.0);
                Canvas.SetLeft(label, position);
                Canvas.SetTop(label, markerCanvas.Height / 2);
            }

            // get the point from the marker, and convert it so that the marker will be in the right place
            Point screenPosition = Marker.ConvertRatioToPoint(marker.Position, canvasRenderSize.Width, canvasRenderSize.Height);
            if (imageToDisplayMarkers)
            {
                screenPosition = this.FileDisplay.Image.RenderTransform.Transform(screenPosition);
            }

            Canvas.SetLeft(markerCanvas, screenPosition.X - markerCanvas.Width / 2.0);
            Canvas.SetTop(markerCanvas, screenPosition.Y - markerCanvas.Height / 2.0);
            markerCanvas.MouseDown += this.FileDisplayImage_MouseDown;
            markerCanvas.MouseMove += this.MarkableCanvas_MouseMove;
            markerCanvas.MouseRightButtonUp += this.Marker_MouseRightButtonUp;
            markerCanvas.MouseUp += this.FileDisplayImage_MouseUp;
            markerCanvas.MouseWheel += this.FileDisplayImage_MouseWheel; // Make the mouse wheel work over marks as well as the image
            return markerCanvas;
        }

        private void AddMarkers(Panel panel, Size canvasRenderSize, bool doTransform)
        {
            if (this.Markers != null)
            {
                foreach (Marker marker in this.Markers)
                {
                    Canvas markerCanvas = this.AddMarker(marker, canvasRenderSize, doTransform);
                    panel.Children.Add(markerCanvas);
                }
            }
        }

        // return to the zoom / pan levels saved as a bookmark
        public void ApplyBookmark()
        {
            this.bookmark.Apply(this.displayImageScale, this.displayImageTranslation);
            this.RedrawMarkers();
        }

        private void CanvasToMagnify_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // redraw markers so they're in the right place to appear in the magnifying glass
            this.RedrawMagnifierMarkers();

            // update the magnifying glass's contents
            this.RedrawMagnifyingGlassIfVisible();
        }

        private void ClearMarkers(Panel panel)
        {
            for (int index = panel.Children.Count - 1; index >= 0; index--)
            {
                if ((panel.Children[index] is Canvas) && (panel.Children[index] != this.magnifyingGlass))
                {
                    panel.Children.RemoveAt(index);
                }
            }
        }

        public void Display(FileDisplayMessage message)
        {
            this.FileDisplay.Display(message);
        }

        /// <summary>
        /// Sets only the display image and leaves markers and the magnifier image unchanged.  Used by the differencing routines to set the difference image.
        /// </summary>
        public void Display(CachedImage image)
        {
            this.FileDisplay.Display(image);
        }

        /// <summary>
        /// Set a wholly new image.  Clears any existing markers and syncs the magnifier image to the display image.
        /// </summary>
        public void Display(CachedImage image, List<Marker>? markers)
        {
            // initate render of new image for display
            this.Display(image);

            // initiate render of magnified image
            // The asynchronous chain behind this is not entirely trivial.  The links are
            //   1) ImageToMagnify_SizeChanged fires and updates canvasToMagnify's size to match
            //   2) CanvasToMagnify_SizeChanged fires and redraws the magnified markers since the cavas size is now known and marker positions can update
            //   3) CanvasToMagnify_SizeChanged initiates a render on the magnifying glass to show the new image and marker positions
            //   4) if it's visible the magnifying glass content updates
            // This synchronization to WPF render opertations is necessary as, despite their appearance, properties like Source, Width, and Height are 
            // asynchronous.  Other approaches therefore tend to be subject to race conditions in render order which hide or misplace markers in the 
            // magnified view and also have a proclivity towards leaving incorrect or stale magnifying glass content on screen.
            // 
            // Another race exists as this.Markers can be set during the above rendering, initiating a second, concurrent marker render.  This is unavoidable
            // due to the need to expose a marker property but is mitigated by accepting new markers through this API and performing the set above as 
            // this.markers rather than this.Markers.
            if (image.Image != null)
            {
                image.Image.SetSource(this.imageToMagnify);
            }

            // change to new markers
            // Assign property so any existing markers are cleared.
            this.Markers = markers;

            // ensure magnifying glass is visible if it's enabled
            if (this.MagnifyingGlassEnabled)
            {
                this.magnifyingGlass.Show();
            }
        }

        public void Display(string folderPath, ImageCache imageCache, List<Marker> displayMarkers)
        {
            if (imageCache.Current.IsVideo)
            {
                FileInfo fileInfo = imageCache.Current.GetFileInfo(folderPath);
                if (fileInfo.Exists == false)
                {
                    this.Display(Constant.Images.FileNoLongerAvailableMessage);
                }

                // leave the magnifying glass's enabled state unchanged so user doesn't have to constantly keep re-enabling it in hybrid image sets
                this.FileDisplay.Display(fileInfo);
                this.markers = displayMarkers;
            }
            else
            {
                this.Display(imageCache.GetCurrentImage(), displayMarkers);
            }
        }

        // record the location, who sent it, and the time.
        // This information is used on move and up events to discriminate between marker placement and panning. 
        private void FileDisplayImage_MouseDown(object sender, MouseButtonEventArgs e)
        {
            this.previousMousePosition = e.GetPosition(this);
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                this.mouseDownLocation = e.GetPosition(this.FileDisplay.Image);
                this.mouseDownSender = (UIElement)sender;
                this.mouseDownTime = DateTime.UtcNow;
            }
        }

        private void FileDisplayImage_MouseUp(object sender, MouseButtonEventArgs e)
        {
            // make sure the cursor reverts to the normal arrow cursor
            this.Cursor = Cursors.Arrow;

            // get the current position
            Point mousePosition = e.GetPosition(this.FileDisplay.Image);

            // is this the end of a translate operation or of placing a maker?
            // Create a marker if the left button has been released, the mouse movement is below threshold, and less than 200 ms have passed since the original
            // mouse down.
            if ((e.LeftButton == MouseButtonState.Released) &&
                (sender == this.mouseDownSender) &&
                (this.mouseDownLocation - mousePosition).Length <= 2.0)
            {
                TimeSpan timeSinceDown = DateTime.UtcNow - this.mouseDownTime;
                if (timeSinceDown.TotalMilliseconds < 200)
                {
                    // get the current point, and create a marker on it.
                    Point position = e.GetPosition(this.FileDisplay.Image);
                    position = Marker.ConvertPointToRatio(position, this.FileDisplay.Image.ActualWidth, this.FileDisplay.Image.ActualHeight);
                    Marker marker = new(null, position)
                    {
                        ShowLabel = true, // show label on creation, cleared on next refresh
                        LabelShownPreviously = false
                    };

                    // don't add the new marker to the marker list as CarnassialWindow is responsible for filling in remaining properties and then adding it
                    this.MarkerCreatedOrDeleted?.Invoke(this, new MarkerCreatedOrDeletedEventArgs(marker, true));
                }
            }

            // show the magnifying glass again if it was hidden during a pan
            this.RedrawMagnifyingGlassIfVisible();
        }

        // use the mouse wheel to scale the image
        private void FileDisplayImage_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            // zoom in if delta is positive, otherwise out
            this.ScaleImage(e.Delta > 0);
        }

        private void FileDisplayImage_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // when the display image size changes refresh the markers so they appear in the correct place
            this.RedrawDisplayMarkers();
        }

        private void ImageToMagnify_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // keep the magnifying glass canvas in sync with the magnified image size
            // this update triggers a call to CanvasToMagnify_SizeChanged
            this.magnifierCanvas.Width = this.imageToMagnify.ActualWidth;
            this.magnifierCanvas.Height = this.imageToMagnify.ActualHeight;
        }

        /// <summary>
        /// Enlarge the magnifying glass image.
        /// </summary>
        public void MagnifierZoomIn()
        {
            this.MagnifyingGlassFieldOfView /= Constant.ImageDisplay.MagnifyingGlassFieldOfViewIncrement;
        }

        /// <summary>
        /// Show more area in the magnifying glass image.
        /// </summary>
        public void MagnifierZoomOut()
        {
            this.MagnifyingGlassFieldOfView *= Constant.ImageDisplay.MagnifyingGlassFieldOfViewIncrement;
        }

        // hide the magnifying glass when the mouse leaves the canvas
        private void MarkableCanvas_MouseLeave(object sender, MouseEventArgs e)
        {
            this.magnifyingGlass.Hide();
        }

        private void MarkableCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            // magnifying glass should be visible only if the current mouse position is over the display image
            if (this.magnifyingGlass.IsEnabled)
            {
                bool mouseOverDisplayImage = false;
                if (this.FileDisplay.Image.IsVisible)
                {
                    mouseOverDisplayImage = this.FileDisplay.Image.Contains(e.GetPosition(this.FileDisplay.Image));
                }

                if (mouseOverDisplayImage)
                {
                    this.magnifyingGlass.Visibility = Visibility.Visible;
                }
                else
                {
                    this.magnifyingGlass.Hide();
                }
            }

            // panning isn't supported on videos
            if (this.FileDisplay.Image.IsVisible == false)
            {
                return;
            }

            // pan or update magnifying glass
            // if the left button is pressed pan the image 
            Point mousePosition = e.GetPosition(this);
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                // change to a panning cursor
                this.Cursor = Cursors.ScrollAll;
                // also hide the magnifying glass so it won't be distracting
                this.magnifyingGlass.Hide();
                // pan
                this.TranslateImage(mousePosition);
            }
            else
            {
                // update the magnifying glass
                this.RedrawMagnifyingGlassIfVisible();
            }

            this.previousMousePosition = mousePosition;
        }

        private void MarkableCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // clear the bookmark (if any) as it will no longer be correct
            // if needed, the bookmark could be rescaled instead
            this.bookmark.Reset();
        }

        // remove a marker on a right mouse button up event
        // Note: There is currently no filter on this callback, so markers can be removed by any right mouse click even if they're
        // not associated with the selected counter or no counter is selected.
        private void Marker_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            Debug.Assert(this.Markers != null);

            Canvas canvas = (Canvas)sender;
            Marker marker = (Marker)canvas.Tag;
            this.Markers.Remove(marker);
            this.MarkerCreatedOrDeleted?.Invoke(this, new MarkerCreatedOrDeletedEventArgs(marker, false));
            this.RedrawMarkers();
        }

        private void RedrawMagnifyingGlassIfVisible()
        {
            // nothing to magnify
            if ((this.IsVisible == false) ||
                (this.imageToMagnify.Source == null))
            {
                this.magnifyingGlass.Hide();
                return;
            }

            // if mouse is off of display image and magnifying glass shouldn't be shown
            Point mouseImagePosition = Mouse.GetPosition(this.FileDisplay.Image);
            if (this.FileDisplay.Image.Contains(mouseImagePosition) == false)
            {
                this.magnifyingGlass.Hide();
                return;
            }

            // update magnifier's view of the image to magnify and position for current mouse location
            this.magnifyingGlass.RedrawIfVisible(this.magnifierCanvas, this.FileDisplay.Image, mouseImagePosition);
        }

        /// <summary>
        /// Remove all currently drawn markers and then redraw all markers in the current list.
        /// </summary>
        private void RedrawMarkers()
        {
            this.RedrawDisplayMarkers();
            this.RedrawMagnifierMarkers();
        }

        private void RedrawDisplayMarkers()
        {
            this.ClearMarkers(this.DisplayCanvas);
            this.AddMarkers(this.DisplayCanvas, this.FileDisplay.Image.RenderSize, true);
        }

        private void RedrawMagnifierMarkers()
        {
            this.ClearMarkers(this.magnifierCanvas);
            this.AddMarkers(this.magnifierCanvas, this.magnifierCanvas.RenderSize, false);
        }

        public void ResetMaximumZoom()
        {
            this.ZoomMaximum = Constant.ImageDisplay.ImageZoomMaximum;
        }

        // scale the image around the given position
        private void ScaleImage(bool zoomIn)
        {
            // nothing to do if at maximum or minimum scaling value whilst zooming in or out, respectively 
            if ((zoomIn && this.displayImageScale.ScaleX >= this.ZoomMaximum) ||
                (!zoomIn && this.displayImageScale.ScaleX <= Constant.ImageDisplay.ImageZoomMinimum))
            {
                return;
            }

            lock (this.FileDisplay.Image)
            {
                // ordering of changes to scale transform is significant
                // See Min's explanation of the interactions in https://social.msdn.microsoft.com/Forums/vstudio/en-US/63ebc273-89bc-431e-a5bd-c014128c7879/scaletransform-and-translatetransform-what-the?forum=wpf.
                // If the mouse is outside of the display image's area Mouse.GetPosition() returns corresponding coordinates, though its behavior is
                // formally undefined.  In the interest of useful behavior out of area positions are clamped to scale centers along the image's edges.
                double previousScaleCenterX = this.displayImageScale.CenterX;
                double previousScaleCenterY = this.displayImageScale.CenterY;

                Point constrainedMousePosition = Mouse.GetPosition(this.FileDisplay.Image);
                constrainedMousePosition.X = Math.Max(0, Math.Min(constrainedMousePosition.X, this.FileDisplay.Image.ActualWidth));
                constrainedMousePosition.Y = Math.Max(0, Math.Min(constrainedMousePosition.Y, this.FileDisplay.Image.ActualHeight));

                this.displayImageScale.CenterX = constrainedMousePosition.X;
                this.displayImageScale.CenterY = constrainedMousePosition.Y;

                this.displayImageTranslation.X += (this.displayImageScale.CenterX - previousScaleCenterX) * (this.displayImageScale.ScaleX - 1);
                this.displayImageTranslation.Y += (this.displayImageScale.CenterY - previousScaleCenterY) * (this.displayImageScale.ScaleY - 1);

                if (zoomIn)
                {
                    this.displayImageScale.ScaleX *= Constant.ImageDisplay.MagnifyingGlassFieldOfViewIncrement;
                    this.displayImageScale.ScaleX = Math.Min(this.ZoomMaximum, this.displayImageScale.ScaleX);
                }
                else
                {
                    this.displayImageScale.ScaleX /= Constant.ImageDisplay.MagnifyingGlassFieldOfViewIncrement;
                    this.displayImageScale.ScaleX = Math.Max(Constant.ImageDisplay.ImageZoomMinimum, this.displayImageScale.ScaleX);
                }
                this.displayImageScale.ScaleY = this.displayImageScale.ScaleX;

                // clear center and translation when scale factor is unity
                // This is a convenience to automatically recenter an image as, if the user pans while zoomed, zoom out to unity would otherwise leave the
                // image panned.  If the user's intent is to zoom out to unity it's desirable to constrian the center and translation so the image occupies
                // all display area available.  However, doing so would push the scale center off the mouse location and result in inconsistent behavior
                // if the intent's not to go to unity.
                if (this.displayImageScale.ScaleX <= Constant.ImageDisplay.ImageZoomMinimum)
                {
                    this.displayImageScale.CenterX = 0.0;
                    this.displayImageScale.CenterY = 0.0;
                    this.displayImageTranslation.X = 0.0;
                    this.displayImageTranslation.Y = 0.0;
                }

                this.RedrawMarkers();
            }
        }

        public void SetBookmark()
        {
            // a user may want to flip between zoom all and a remembered zoom / pan setting that focuses in on a particular region
            this.bookmark.Set(this.displayImageScale, this.displayImageTranslation);
        }

        // given the mouse location on the image, translate the image
        // This is normally called from a left mouse drag event.
        private void TranslateImage(Point mousePosition)
        {
            // get the center point on the image
            Point center = this.PointFromScreen(this.FileDisplay.Image.PointToScreen(new Point(this.FileDisplay.Image.Width / 2.0, this.FileDisplay.Image.Height / 2.0)));

            // calculate the delta position from the last location relative to the center
            double newX = center.X + mousePosition.X - this.previousMousePosition.X;
            double newY = center.Y + mousePosition.Y - this.previousMousePosition.Y;

            // get the translated image width
            double imageWidth = this.FileDisplay.Image.Width * this.displayImageScale.ScaleX;
            double imageHeight = this.FileDisplay.Image.Height * this.displayImageScale.ScaleY;

            // limit the delta position so that the image stays on the screen
            if (newX - imageWidth / 2.0 >= 0.0)
            {
                newX = imageWidth / 2.0;
            }
            else if (newX + imageWidth / 2.0 <= this.ActualWidth)
            {
                newX = this.ActualWidth - imageWidth / 2.0;
            }

            if (newY - imageHeight / 2.0 >= 0.0)
            {
                newY = imageHeight / 2.0;
            }
            else if (newY + imageHeight / 2.0 <= this.ActualHeight)
            {
                newY = this.ActualHeight - imageHeight / 2.0;
            }

            // translate the canvas and redraw the markers
            this.displayImageTranslation.X += newX - center.X;
            this.displayImageTranslation.Y += newY - center.Y;

            this.RedrawMarkers();
        }

        public bool TryPlayOrPauseVideo()
        {
            return this.FileDisplay.Video.TryPlayOrPause();
        }

        // whenever the image size changes, refresh the markers so they appear in the correct place
        private void VideoToDisplay_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            this.RedrawMarkers();
        }

        public void ZoomIn()
        {
            this.ScaleImage(true);
        }

        public void ZoomOut()
        {
            this.ScaleImage(false);
        }

        // return to the zoomed out level, with no panning
        public void ZoomToFit()
        {
            this.displayImageScale.ScaleX = 1.0;
            this.displayImageScale.ScaleY = 1.0;
            this.displayImageTranslation.X = 0.0;
            this.displayImageTranslation.Y = 0.0;
            this.RedrawMarkers();
        }
    }
}
