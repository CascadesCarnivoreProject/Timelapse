using Carnassial.Data;
using Carnassial.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Windows;

namespace Carnassial.UnitTests
{
    public class FileExpectations
    {
        private readonly TimeZoneInfo timeZoneForDateTime;

        public FileClassification Classification { get; set; }
        public double Coloration { get; set; }
        public DateTimeOffset DateTime { get; set; }
        public bool DeleteFlag { get; set; }
        public string FileName { get; set; }
        public long ID { get; set; }
        public double Luminosity { get; set; }
        public string RelativePath { get; set; }
        public bool SkipDateTimeVerification { get; set; }
        public bool SkipMarkerByteVerification { get; set; }
        public bool SkipUserControlVerification { get; set; }
        public Dictionary<string, object> UserControlsByDataLabel { get; private set; }

        public FileExpectations(TimeZoneInfo timeZone)
        {
            this.Classification = (FileClassification)(-1);
            this.Coloration = -1.0;
            this.DateTime = DateTimeOffset.MinValue;
            this.DeleteFlag = false;
            this.FileName = null;
            this.ID = Constant.Database.InvalidID;
            this.Luminosity = -1.0;
            this.RelativePath = String.Empty;
            this.SkipDateTimeVerification = false;
            this.SkipMarkerByteVerification = false;
            this.SkipUserControlVerification = false;
            this.UserControlsByDataLabel = new Dictionary<string, object>(StringComparer.Ordinal);
            this.timeZoneForDateTime = timeZone;
        }

        public FileExpectations(FileExpectations other)
            : this(other.timeZoneForDateTime)
        {
            this.Classification = other.Classification;
            this.Coloration = other.Coloration;
            this.DateTime = other.DateTime;
            this.DeleteFlag = other.DeleteFlag;
            this.FileName = other.FileName;
            this.ID = other.ID;
            this.Luminosity = other.Luminosity;
            this.RelativePath = other.RelativePath;
            this.SkipDateTimeVerification = other.SkipDateTimeVerification;
            this.SkipMarkerByteVerification = other.SkipMarkerByteVerification;
            this.SkipUserControlVerification = other.SkipUserControlVerification;

            foreach (KeyValuePair<string, object> columnExpectation in other.UserControlsByDataLabel)
            {
                this.UserControlsByDataLabel.Add(columnExpectation.Key, columnExpectation.Value);
            }
        }

        private DateTimeOffset ConvertDateTimeToTimeZone(TimeZoneInfo imageSetDateTime)
        {
            bool currentlyDaylightSavings = this.timeZoneForDateTime.IsDaylightSavingTime(this.DateTime);
            TimeSpan utcOffsetChange = imageSetDateTime.BaseUtcOffset - this.timeZoneForDateTime.BaseUtcOffset;
            DateTimeOffset expectedDateTime = this.DateTime.ToNewOffset(utcOffsetChange);

            bool newlyDaylightSavings = imageSetDateTime.IsDaylightSavingTime(this.DateTime);
            if (newlyDaylightSavings != currentlyDaylightSavings)
            {
                TimeSpan daylightSavingsAdjustment = TimeSpan.FromHours(-1.0);
                if (newlyDaylightSavings && (currentlyDaylightSavings == false))
                {
                    daylightSavingsAdjustment = daylightSavingsAdjustment.Duration();
                }
                expectedDateTime = expectedDateTime.ToNewOffset(daylightSavingsAdjustment);
            }

            return expectedDateTime;
        }

        public static DateTimeOffset ParseDateTimeOffsetString(string dateTimeAsString)
        {
            return DateTimeOffset.ParseExact(dateTimeAsString, TestConstant.DateTimeWithOffsetFormat, Constant.InvariantCulture);
        }

        public void Verify(ImageRow file, TimeZoneInfo timeZone)
        {
            Assert.IsTrue(file.Classification == this.Classification, "{0}: Expected Classification '{1}' but found '{2}'.", this.FileName, this.Classification, file.Classification);
            Assert.IsTrue(file.DeleteFlag == this.DeleteFlag, "0}: Expected DeleteFlag '{1}' but found '{2}'.", this.FileName, this.DeleteFlag, file.DeleteFlag);
            Assert.IsTrue(file.FileName == this.FileName, "{0}: Expected FileName '{1}' but found '{2}'.", this.FileName, this.FileName, file.FileName);
            Assert.IsTrue(file.ID == this.ID, "{0}: Expected ID '{1}' but found '{2}'.", this.FileName, this.ID, file.ID);
            Assert.IsTrue((file.IsVideo == file is VideoRow) && (file.IsVideo == (file.Classification == FileClassification.Video)));
            Assert.IsTrue(file.RelativePath == this.RelativePath, "{0}: Expected RelativePath '{1}' but found '{2}'.", this.FileName, this.RelativePath, file.RelativePath);
            Assert.IsTrue(file.UtcDateTime.Kind == DateTimeKind.Utc, "{0}: Expected UtcDateTime.Kind '{1}' but found '{2}'.", this.FileName, DateTimeKind.Utc, file.UtcDateTime.Kind);

            // bypass checking of DateTimeOffset if requested, for example if the camera didn't generate image taken metadata
            if (this.SkipDateTimeVerification == false)
            {
                DateTimeOffset fileDateTime = file.DateTimeOffset;
                DateTimeOffset expectedDateTime = this.ConvertDateTimeToTimeZone(timeZone);
                Assert.IsTrue(fileDateTime.UtcDateTime == expectedDateTime.UtcDateTime, "{0}: Expected date time '{1}' but found '{2}'.", this.FileName, DateTimeHandler.ToDatabaseDateTimeString(expectedDateTime), DateTimeHandler.ToDatabaseDateTimeString(fileDateTime.UtcDateTime));
                Assert.IsTrue(fileDateTime.Offset == expectedDateTime.Offset, "{0}: Expected date time offset '{1}' but found '{2}'.", this.FileName, expectedDateTime.Offset, fileDateTime.Offset);
                Assert.IsTrue(file.UtcDateTime == expectedDateTime.UtcDateTime, "{0}: Expected DateTime '{1}' but found '{2}'.", this.FileName, DateTimeHandler.ToDatabaseDateTimeString(expectedDateTime), DateTimeHandler.ToDatabaseDateTimeString(file.UtcDateTime));
                Assert.IsTrue(file.UtcOffset == expectedDateTime.Offset, "{0}: Expected UtcOffset '{1}' but found '{2}'.", this.FileName, expectedDateTime.Offset, file.UtcOffset);
            }

            if (this.SkipUserControlVerification == false)
            {
                int actualUserControlCount = file.UserCounters.Length + file.UserFlags.Length + file.UserMarkerPositions.Length + file.UserNotesAndChoices.Length;
                Assert.IsTrue(actualUserControlCount == this.UserControlsByDataLabel.Count, "{0}: Expected '{1}' user control values but found '{2}'.", this.FileName, this.UserControlsByDataLabel.Count, actualUserControlCount);
                foreach (KeyValuePair<string, object> userControlExpectation in this.UserControlsByDataLabel)
                {
                    string dataLabel = userControlExpectation.Key;
                    object actualValue = file[dataLabel];
                    object expectedValue = userControlExpectation.Value;
                    if (expectedValue is bool expectedBool)
                    {
                        Assert.IsTrue((bool)actualValue == expectedBool, "{0}: Expected {1} to be '{2}' but found '{3}'.", this.FileName, userControlExpectation.Key, expectedBool, actualValue);
                    }
                    else if (expectedValue is byte[] expectedBytes)
                    {
                        byte[] actualBytes = (byte[])actualValue;
                        Assert.IsTrue(actualBytes.Length == expectedBytes.Length);
                        if (this.SkipMarkerByteVerification == false)
                        {
                            for (int byteIndex = 0; byteIndex < expectedBytes.Length; ++byteIndex)
                            {
                                Assert.IsTrue(actualBytes[byteIndex] == expectedBytes[byteIndex]);
                            }
                        }
                    }
                    else if (expectedValue is int expectedInt)
                    {
                        Assert.IsTrue((int)actualValue == expectedInt, "{0}: Expected {1} to be '{2}' but found '{3}'.", this.FileName, userControlExpectation.Key, expectedInt, actualValue);
                        MarkersForCounter markersForCounter = file.GetMarkersForCounter(dataLabel);
                        this.Verify(dataLabel, markersForCounter);
                    }
                    else
                    {
                        Assert.IsTrue(String.Equals((string)actualValue, (string)expectedValue, StringComparison.Ordinal), "{0}: Expected {1} to be '{2}' but found '{3}'.", this.FileName, userControlExpectation.Key, expectedValue, actualValue);
                    }
                }
            }
        }

        private void Verify(string dataLabel, MarkersForCounter markersForCounter)
        {
            // basic checks
            int expectedCount = (int)this.UserControlsByDataLabel[dataLabel];
            Assert.IsTrue(markersForCounter.Count == expectedCount);

            string spreadsheetPositions = markersForCounter.MarkerPositionsToSpreadsheetString();
            string[] spreadsheetTokens = spreadsheetPositions == null ? Array.Empty<string>() : spreadsheetPositions.Split(Constant.Excel.MarkerPositionSeparator);

            string markerColummn = FileTable.GetMarkerPositionColumnName(dataLabel);
            byte[] expectedPositions = (byte[])this.UserControlsByDataLabel[markerColummn];
            MarkersForCounter expectedMarkersForCounter = new(dataLabel, expectedCount);
            expectedMarkersForCounter.MarkerPositionsFromFloatArray(expectedPositions);

            Assert.IsTrue(expectedMarkersForCounter.Markers.Count == spreadsheetTokens.Length);
            Assert.IsTrue(expectedMarkersForCounter.Markers.Count == markersForCounter.Markers.Count);

            // marker positions
            for (int markerIndex = 0; markerIndex < expectedMarkersForCounter.Markers.Count; ++markerIndex)
            {
                Marker expectedMarker = expectedMarkersForCounter.Markers[markerIndex];
                Marker marker = markersForCounter.Markers[markerIndex];

                Assert.IsTrue(String.Equals(marker.DataLabel, dataLabel, StringComparison.Ordinal));
                Assert.IsFalse(marker.Emphasize);
                Assert.IsFalse(marker.Highlight);
                Assert.IsTrue(marker.LabelShownPreviously);
                Vector positionDifference = marker.Position - expectedMarker.Position;
                Assert.IsTrue(positionDifference.Length < 0.5E-6);
                Assert.IsFalse(marker.ShowLabel);
                Assert.IsNull(marker.Tooltip);
            }
        }
    }
}
