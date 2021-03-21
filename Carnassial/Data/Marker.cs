using System.Windows;

namespace Carnassial.Data
{
    public class Marker
    {
        /// <summary>
        /// Gets or sets the data label associated with this marker
        /// </summary>
        public string DataLabel { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to visually emphasize the marker
        /// </summary>
        public bool Emphasize { get; set; }

        public bool Highlight { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the label has already been displayed.
        /// </summary>
        public bool LabelShownPreviously { get; set; }

        /// <summary>
        /// Gets the marker's normalized location in the markable canvas, as a coordinate point on [0, 1], [0, 1].
        /// </summary>
        public Point Position { get; private set; }

        /// <summary>
        /// Gets or sets a value indicating whether to show the label next to the marker
        /// </summary>
        public bool ShowLabel { get; set; }

        /// <summary>
        /// Gets or sets the marker's tooltip text
        /// </summary>
        public string? Tooltip { get; set; } // The label (not datalabel) associated with this marker. To be put in the tooltip and for highlighting.

        public Marker(string dataLabel, Point point)
        {
            this.DataLabel = dataLabel;
            this.Emphasize = false;
            this.Highlight = false;
            this.LabelShownPreviously = true;
            this.Position = point;
            this.ShowLabel = false;
            this.Tooltip = null;
        }

        // find a point's relative location so it's size invariant
        public static Point ConvertPointToRatio(Point point, double width, double height)
        {
            Point ratio = new(point.X / width, point.Y / height);
            return ratio;
        }

        // convert a relative location to a specific screen location
        public static Point ConvertRatioToPoint(Point ratio, double width, double height)
        {
            Point point = new(ratio.X * width, ratio.Y * height);
            return point;
        }

        public string GetSpreadsheetPositionString()
        {
            string x = this.Position.X.ToString(Constant.Excel.MarkerCoordinateFormat, Constant.InvariantCulture);
            string y = this.Position.Y.ToString(Constant.Excel.MarkerCoordinateFormat, Constant.InvariantCulture);
            return x + Constant.Excel.MarkerCoordinateSeparator + y;
        }
    }
}
