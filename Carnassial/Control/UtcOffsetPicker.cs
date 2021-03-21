using System;
using System.Globalization;

namespace Carnassial.Control
{
    public class UtcOffsetPicker : TimeSpanPicker
    {
        public UtcOffsetPicker()
        {
            this.Format = Constant.Time.UtcOffsetDisplayFormat;
            this.Maximum = TimeSpan.FromHours(Constant.Time.MaximumUtcOffsetInHours);
            this.Minimum = TimeSpan.FromHours(Constant.Time.MinimumUtcOffsetInHours);
        }

        protected override TimeSpan ConvertIncrementOrDecrementToTimeSpan(char partFormat, int incrementOrDecrement)
        {
            return partFormat switch
            {
                'h' => TimeSpan.FromHours(incrementOrDecrement),
                'm' => TimeSpan.FromMinutes(incrementOrDecrement * Constant.Time.UtcOffsetGranularityInMinutes),
                _ => throw new NotSupportedException(String.Format(CultureInfo.CurrentCulture, "Unhandled part format {0}.", partFormat)),
            };
        }
    }
}
