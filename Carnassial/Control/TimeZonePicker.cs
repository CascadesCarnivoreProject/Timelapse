using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace Carnassial.Control
{
    public class TimeZonePicker : ComboBox
    {
        public ReadOnlyDictionary<string, TimeZoneInfo> TimeZonesByDisplayIdentifier { get; private set; }

        public TimeZonePicker()
        {
            this.FontWeight = FontWeights.Normal;

            Dictionary<string, TimeZoneInfo> timeZones = new(StringComparer.Ordinal);
            foreach (TimeZoneInfo timeZone in TimeZoneInfo.GetSystemTimeZones())
            {
                string timeZoneDisplayIdentifier = timeZone.DisplayName;
                if (timeZone.SupportsDaylightSavingTime == false)
                {
                    timeZoneDisplayIdentifier += " [no daylight savings]";
                }
                timeZones.Add(timeZoneDisplayIdentifier, timeZone);
                this.Items.Add(timeZoneDisplayIdentifier);
            }

            this.TimeZonesByDisplayIdentifier = new ReadOnlyDictionary<string, TimeZoneInfo>(timeZones);
        }

        public void SelectTimeZone(TimeZoneInfo timeZone)
        {
            foreach (string item in this.Items)
            {
                if (item != null && item.StartsWith(timeZone.DisplayName, StringComparison.Ordinal))
                {
                    this.SelectedItem = item;
                }
            }
        }
    }
}
