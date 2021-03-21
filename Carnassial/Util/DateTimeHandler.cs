using System;
using System.Diagnostics;
using System.Globalization;

namespace Carnassial.Util
{
    public class DateTimeHandler
    {
        private static readonly char[] DateTimeToSpreadsheetBuffer;
        private static readonly char[] UtcOffsetToDatabaseBuffer;
        private static readonly int[] MaximumDaysInMonth;

        static DateTimeHandler()
        {
            Debug.Assert(String.Equals(Constant.Time.DateTimeDatabaseFormat, "yyyy-MM-ddTHH:mm:ss.fffZ", StringComparison.Ordinal), nameof(DateTimeHandler.ToDatabaseDateTimeString) + "() and " + nameof(DateTimeHandler.TryParseDatabaseDateTime) + "() need to be updated for change in spreadsheet date time format.");

            DateTimeHandler.DateTimeToSpreadsheetBuffer = new char[] { 'y', 'y', 'y', 'y', '-', 'M', 'M', '-', 'd', 'd', 'T', 'H', 'H', ':', 'm', 'm', ':', 's', 's', '.', 'f', 'f', 'f', 'Z' };
            ////                                             1   2   3   4   5   6   7   8   9   10  11  12
            DateTimeHandler.MaximumDaysInMonth = new int[] { 31, 29, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31 };
            DateTimeHandler.UtcOffsetToDatabaseBuffer = new char[6];
        }

        public static DateTimeOffset CreateDateTimeOffset(DateTime dateTime, TimeZoneInfo imageSetTimeZone)
        {
            if (dateTime.Kind == DateTimeKind.Unspecified)
            {
                TimeSpan utcOffset = imageSetTimeZone.GetUtcOffset(dateTime);
                return new DateTimeOffset(dateTime, utcOffset);
            }
            return new DateTimeOffset(dateTime);
        }

        public static DateTimeOffset FromDatabaseDateTimeOffset(DateTime dateTime, TimeSpan utcOffset)
        {
            return new DateTimeOffset((dateTime + utcOffset).Ticks, utcOffset);
        }

        public static TimeSpan FromDatabaseUtcOffset(double hours)
        {
            return TimeSpan.FromHours(hours);
        }

        public static DateTime ParseDatabaseDateTime(string dateTimeAsString)
        {
            if (DateTimeHandler.TryParseDatabaseDateTime(dateTimeAsString, out DateTime dateTime))
            {
                return dateTime;
            }
            throw new ArgumentOutOfRangeException(nameof(dateTimeAsString), String.Format(CultureInfo.CurrentCulture, "'{0}' is not in the format '{1}'.", dateTimeAsString, Constant.Time.DateTimeDatabaseFormat));
        }

        public static DateTime ParseDisplayDateTimeString(string dateTimeAsString)
        {
            return DateTime.ParseExact(dateTimeAsString, Constant.Time.DateTimeDisplayFormat, CultureInfo.CurrentCulture);
        }

        public static TimeSpan ParseDisplayUtcOffsetString(string utcOffsetAsString)
        {
            bool negative = false;
            if (utcOffsetAsString.StartsWith("-", StringComparison.Ordinal))
            {
                negative = true;
                utcOffsetAsString = utcOffsetAsString[1..];
            }

            TimeSpan utcOffset = TimeSpan.ParseExact(utcOffsetAsString, Constant.Time.UtcOffsetDisplayFormat, Constant.InvariantCulture);
            if (negative)
            {
                utcOffset = utcOffset.Negate();
            }
            return utcOffset;
        }

        public static unsafe string ToDatabaseDateTimeString(DateTimeOffset dateTime)
        {
            fixed (char* dateTimeCharacters = DateTimeHandler.DateTimeToSpreadsheetBuffer)
            {
                DateTime utcDateTime = dateTime.UtcDateTime;
                int year = utcDateTime.Year;
                *(dateTimeCharacters + 0) = (char)(year / 1000 + '0');
                *(dateTimeCharacters + 1) = (char)(year / 100 % 10 + '0');
                *(dateTimeCharacters + 2) = (char)(year / 10 % 10 + '0');
                *(dateTimeCharacters + 3) = (char)(year % 10 + '0');

                int month = utcDateTime.Month;
                *(dateTimeCharacters + 5) = (char)(month / 10 + '0');
                *(dateTimeCharacters + 6) = (char)(month % 10 + '0');

                int day = utcDateTime.Day;
                *(dateTimeCharacters + 8) = (char)(day / 10 + '0');
                *(dateTimeCharacters + 9) = (char)(day % 10 + '0');

                int hour = utcDateTime.Hour;
                *(dateTimeCharacters + 11) = (char)(hour / 10 + '0');
                *(dateTimeCharacters + 12) = (char)(hour % 10 + '0');

                int minute = utcDateTime.Minute;
                *(dateTimeCharacters + 14) = (char)(minute / 10 + '0');
                *(dateTimeCharacters + 15) = (char)(minute % 10 + '0');

                int second = utcDateTime.Second;
                *(dateTimeCharacters + 17) = (char)(second / 10 + '0');
                *(dateTimeCharacters + 18) = (char)(second % 10 + '0');

                int millisecond = utcDateTime.Millisecond;
                *(dateTimeCharacters + 20) = (char)(millisecond / 100 + '0');
                *(dateTimeCharacters + 21) = (char)(millisecond / 10 % 10 + '0');
                *(dateTimeCharacters + 22) = (char)(millisecond % 10 + '0');
            }
            return new String(DateTimeHandler.DateTimeToSpreadsheetBuffer);
        }

        public static double ToDatabaseUtcOffset(TimeSpan utcOffset)
        {
            return utcOffset.TotalHours;
        }

        public static unsafe string ToDatabaseUtcOffsetString(TimeSpan utcOffset)
        {
            int length = 1;
            fixed (char* utcOffsetCharacters = DateTimeHandler.UtcOffsetToDatabaseBuffer)
            {
                int index = 0;

                bool isNegative = utcOffset.TotalHours < 0.0;
                if (isNegative)
                {
                    ++length;
                    *(utcOffsetCharacters + index++) = '-';
                }

                int hours = utcOffset.Hours;
                if (hours < 0)
                {
                    hours *= -1;
                }
                if (hours > 9)
                {
                    *(utcOffsetCharacters + index++) = (char)(hours / 10 + '0');
                    hours -= 10;
                    ++length;
                }
                *(utcOffsetCharacters + index++) = (char)(hours + '0');

                int minutes = utcOffset.Minutes;
                if (minutes != 0)
                {
                    length += 3;
                    int minutesAsFractionOfHour = 100 * utcOffset.Minutes / 60;
                    *(utcOffsetCharacters + index++) = '.';
                    *(utcOffsetCharacters + index++) = (char)(minutesAsFractionOfHour / 10 + '0');
                    *(utcOffsetCharacters + index++) = (char)(minutesAsFractionOfHour % 10 + '0');
                }
            }
            return new String(DateTimeHandler.UtcOffsetToDatabaseBuffer, 0, length);
        }

        /// <summary>
        /// Given a date as a DateTimeOffset, return it as a string in dd-MMM-yyyy format, e.g., 05-Apr-2016, with the offset.
        /// </summary>
        public static string ToDisplayDateString(DateTimeOffset date)
        {
            return date.DateTime.ToString(Constant.Time.DateFormat, CultureInfo.CurrentCulture);
        }

        public static string ToDisplayDateTimeString(DateTimeOffset dateTime)
        {
            return dateTime.DateTime.ToString(Constant.Time.DateTimeDisplayFormat, CultureInfo.CurrentCulture);
        }

        public static string ToDisplayDateTimeUtcOffsetString(DateTimeOffset dateTime)
        {
            return dateTime.DateTime.ToString(Constant.Time.DateTimeDisplayFormat, CultureInfo.CurrentCulture) + " " + DateTimeHandler.ToDisplayUtcOffsetString(dateTime.Offset);
        }

        public static string ToDisplayTimeSpanString(TimeSpan timeSpan)
        {
            string sign = (timeSpan < TimeSpan.Zero) ? "-" : null;
            string timeSpanAsString = sign + timeSpan.ToString(Constant.Time.TimeSpanDisplayFormat, CultureInfo.CurrentCulture);

            TimeSpan duration = timeSpan.Duration();
            if (duration.Days == 0)
            {
                return timeSpanAsString;
            }
            if (duration.Days == 1)
            {
                return sign + "1 day " + timeSpanAsString;
            }

            return sign + duration.Days.ToString("D", CultureInfo.CurrentCulture) + " days " + timeSpanAsString;
        }

        public static string ToDisplayUtcOffsetString(TimeSpan utcOffset)
        {
            string displayString = utcOffset.ToString(Constant.Time.UtcOffsetDisplayFormat, CultureInfo.CurrentCulture);
            if (utcOffset < TimeSpan.Zero)
            {
                displayString = "-" + displayString;
            }
            return displayString;
        }

        public static unsafe bool TryParseDatabaseDateTime(string dateTimeAsString, out DateTime dateTime)
        {
            // called once per row during spreadsheet import
            // Profiling shows DateTime.ParseExact(Constant.Time.DateTimeDatabaseFormat) consumes 65% of the time spent in parsing
            // row data (roughly equivalent to the time spent in Carnassial code per Excel row rather than in XML parsing and shared
            // string lookup).  In the interests of efficiency, use dedicated code instead.
            // The UTC date time format is guarded by a Debug.Assert() in DateTimeHandler..ctor() and doesn't need to be checked here.
            // 0         1         2
            // 012345678901234567890123
            // yyyy-MM-ddTHH:mm:ss.fffZ
            if ((dateTimeAsString == null) ||
                (dateTimeAsString.Length != 24))
            {
                dateTime = DateTime.MinValue;
                return false;
            }

            // unsafe fixed profiles about six times faster than using dateTimeAsString[]
            fixed (char* dateTimeCharacters = dateTimeAsString)
            {
                if ((*(dateTimeCharacters + 0) < '0') || (*(dateTimeCharacters + 0) > '9') || // yyyy
                    (*(dateTimeCharacters + 1) < '0') || (*(dateTimeCharacters + 1) > '9') ||
                    (*(dateTimeCharacters + 2) < '0') || (*(dateTimeCharacters + 2) > '9') ||
                    (*(dateTimeCharacters + 3) < '0') || (*(dateTimeCharacters + 3) > '9') ||
                    (*(dateTimeCharacters + 4) != '-') ||
                    (*(dateTimeCharacters + 5) < '0') || (*(dateTimeCharacters + 5) > '2') || // mm
                    (*(dateTimeCharacters + 6) < '0') || (*(dateTimeCharacters + 6) > '9') ||
                    (*(dateTimeCharacters + 7) != '-') ||
                    (*(dateTimeCharacters + 8) < '0') || (*(dateTimeCharacters + 8) > '3') || // dd
                    (*(dateTimeCharacters + 9) < '0') || (*(dateTimeCharacters + 9) > '9') ||
                    (*(dateTimeCharacters + 10) != 'T') ||
                    (*(dateTimeCharacters + 11) < '0') || (*(dateTimeCharacters + 11) > '2') || // HH
                    (*(dateTimeCharacters + 12) < '0') || (*(dateTimeCharacters + 12) > '9') ||
                    (*(dateTimeCharacters + 13) != ':') ||
                    (*(dateTimeCharacters + 14) < '0') || (*(dateTimeCharacters + 14) > '5') || // mm
                    (*(dateTimeCharacters + 15) < '0') || (*(dateTimeCharacters + 15) > '9') ||
                    (*(dateTimeCharacters + 16) != ':') ||
                    (*(dateTimeCharacters + 17) < '0') || (*(dateTimeCharacters + 17) > '5') || // ss - leap seconds not supported consistent with
                    (*(dateTimeCharacters + 18) < '0') || (*(dateTimeCharacters + 18) > '9') || // https://github.com/Microsoft/referencesource/blob/master/mscorlib/system/datetime.cs : TimeToTicks()
                    (*(dateTimeCharacters + 19) != '.') ||
                    (*(dateTimeCharacters + 20) < '0') || (*(dateTimeCharacters + 20) > '9') || // fff
                    (*(dateTimeCharacters + 21) < '0') || (*(dateTimeCharacters + 21) > '9') ||
                    (*(dateTimeCharacters + 22) < '0') || (*(dateTimeCharacters + 22) > '9') ||
                    (*(dateTimeCharacters + 23) != 'Z'))
                {
                    dateTime = DateTime.MinValue;
                    return false;
                }

                int year = 1000 * (*(dateTimeCharacters + 0) - '0') + 100 * (*(dateTimeCharacters + 1) - '0') + 10 * (*(dateTimeCharacters + 2) - '0') + (*(dateTimeCharacters + 3) - '0');
                int month = 10 * (*(dateTimeCharacters + 5) - '0') + (*(dateTimeCharacters + 6) - '0');
                int day = 10 * (*(dateTimeCharacters + 8) - '0') + (*(dateTimeCharacters + 9) - '0');
                int hour = 10 * (*(dateTimeCharacters + 11) - '0') + (*(dateTimeCharacters + 12) - '0');
                int minute = 10 * (*(dateTimeCharacters + 14) - '0') + (*(dateTimeCharacters + 15) - '0');
                int second = 10 * (*(dateTimeCharacters + 17) - '0') + (*(dateTimeCharacters + 18) - '0');
                int millisecond = 100 * (*(dateTimeCharacters + 20) - '0') + 10 * (*(dateTimeCharacters + 21) - '0') + (*(dateTimeCharacters + 22) - '0');

                if ((month == 0) || (month > 12) ||
                    (day > DateTimeHandler.MaximumDaysInMonth[month - 1]) || // allows day 29 for February
                    ((month == 2) && (DateTime.IsLeapYear(year) == false) && day > 28) || // exclude day 29 of February
                    (hour > 24))
                {
                    // minutes and seconds are constrained to 0 to 59 by above check and don't need to be rechecked here
                    dateTime = DateTime.MaxValue;
                    return false;
                }

                dateTime = new DateTime(year, month, day, hour, minute, second, millisecond, DateTimeKind.Utc);
                return true;
            }
        }

        public static unsafe bool TryParseDatabaseUtcOffset(string utcOffsetAsString, out TimeSpan utcOffset)
        {
            if ((utcOffsetAsString == null) || (utcOffsetAsString.Length < 1))
            {
                utcOffset = TimeSpan.Zero;
                return false;
            }

            // while the UTC offset string should be Constant.Time.UtcOffsetDatabaseFormat it may be reduced
            // Therefore, perform a conventional floating point number parse.
            fixed (char* utcOffsetCharacters = utcOffsetAsString)
            {
                bool isNegative = *utcOffsetCharacters == '-';
                int index = isNegative ? 1 : 0;
                int length = utcOffsetAsString.Length;
                int hour = 0;
                while (index < length)
                {
                    if (*(utcOffsetCharacters + index) == '.')
                    {
                        ++index;
                        break;
                    }
                    if ((*(utcOffsetCharacters + index) < '0') || (*(utcOffsetCharacters + index) > '9'))
                    {
                        utcOffset = TimeSpan.MinValue;
                        return false;
                    }
                    hour = 10 * hour + *(utcOffsetCharacters + index) - '0';
                    ++index;
                }

                int minuteDivisor = 1;
                int minutes = 0;
                while (index < length)
                {
                    if ((*(utcOffsetCharacters + index) < '0') || (*(utcOffsetCharacters + index) > '9'))
                    {
                        utcOffset = TimeSpan.MinValue;
                        return false;
                    }
                    minuteDivisor *= 10;
                    minutes = 10 * minutes + *(utcOffsetCharacters + index) - '0';
                    ++index;
                }

                double utcOffsetInHours = (double)(hour * (isNegative ? -1 : 1)) + (double)minutes / (double)minuteDivisor;
                if ((utcOffsetInHours > Constant.Time.MaximumUtcOffsetInHours) ||
                    (utcOffsetInHours < Constant.Time.MinimumUtcOffsetInHours) ||
                    (minutes % Constant.Time.UtcOffsetGranularityInMinutes != 0))
                {
                    utcOffset = TimeSpan.MaxValue;
                    return false;
                }

                utcOffset = TimeSpan.FromHours(utcOffsetInHours);
                return true;
            }
        }

        public static bool TryParseDateTimeOffset(string dateTimeOffsetAsString, out DateTimeOffset dateTimeOffset)
        {
            return DateTimeOffset.TryParseExact(dateTimeOffsetAsString, Constant.Time.DateTimeDatabaseFormat, Constant.InvariantCulture, DateTimeStyles.None, out dateTimeOffset);
        }

        public static bool TryParseDisplayDateTime(string dateTimeAsString, out DateTimeOffset dateTimeOffset)
        {
            return DateTimeOffset.TryParseExact(dateTimeAsString, Constant.Time.DateTimeDisplayFormat, Constant.InvariantCulture, DateTimeStyles.None, out dateTimeOffset);
        }

        public static bool TryParseMetadataDateTaken(string dateTimeAsString, TimeZoneInfo imageSetTimeZone, out DateTimeOffset dateTimeOffset)
        {
            if (DateTime.TryParseExact(dateTimeAsString, Constant.Time.DateTimeMetadataFormats, Constant.InvariantCulture, DateTimeStyles.None, out DateTime dateTime) == false)
            {
                dateTimeOffset = DateTimeOffset.MinValue;
                return false;
            }

            dateTimeOffset = DateTimeHandler.CreateDateTimeOffset(dateTime, imageSetTimeZone);
            return true;
        }

        public static bool TrySwapDayMonth(DateTimeOffset imageDate, out DateTimeOffset swappedDate)
        {
            swappedDate = DateTimeOffset.MinValue;
            if (imageDate.Day > Constant.Time.MonthsInYear)
            {
                return false;
            }
            swappedDate = new DateTimeOffset(imageDate.Year, imageDate.Day, imageDate.Month, imageDate.Hour, imageDate.Minute, imageDate.Second, imageDate.Millisecond, imageDate.Offset);
            return true;
        }
    }
}
