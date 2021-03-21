using System.Windows;

namespace Carnassial.Images
{
    internal static class FrameworkElementExtensions
    {
        public static bool Contains(this FrameworkElement frameworkElement, Point point)
        {
            Rect extent = new(0.0, 0.0, frameworkElement.ActualWidth, frameworkElement.ActualHeight);
            return extent.Contains(point);
        }
    }
}
