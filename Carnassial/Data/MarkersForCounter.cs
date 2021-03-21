using Carnassial.Util;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Windows;

namespace Carnassial.Data
{
    public class MarkersForCounter : INotifyPropertyChanged
    {
        public int Count { get; private set; }
        public string DataLabel { get; private set; }
        public List<Marker> Markers { get; private set; }

        public event PropertyChangedEventHandler? PropertyChanged;

        public MarkersForCounter(string dataLabel, int count)
        {
            this.Count = count;
            this.DataLabel = dataLabel;
            this.Markers = new List<Marker>();
        }

        public void AddMarker(Marker marker)
        {
            Debug.Assert(String.Equals(marker.DataLabel, this.DataLabel, StringComparison.Ordinal), "Marker is associated with a different counter.");

            ++this.Count;
            this.Markers.Add(marker);
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.Markers)));
        }

        public byte[] MarkerPositionsToFloatArray()
        {
            if (this.Markers.Count < 1)
            {
                return null;
            }

            byte[] packedFloats = new byte[2 * this.Markers.Count * sizeof(float)];
            for (int byteIndex = -1, markerIndex = 0; markerIndex < this.Markers.Count; ++markerIndex)
            {
                Marker marker = this.Markers[markerIndex];
                byte[] xBytes = BitConverter.GetBytes((float)marker.Position.X);
                Debug.Assert(xBytes.Length == 4, "Expected 32 bit float for marker x position.");
                packedFloats[++byteIndex] = xBytes[0];
                packedFloats[++byteIndex] = xBytes[1];
                packedFloats[++byteIndex] = xBytes[2];
                packedFloats[++byteIndex] = xBytes[3];

                byte[] yBytes = BitConverter.GetBytes((float)marker.Position.Y);
                Debug.Assert(yBytes.Length == 4, "Expected 32 bit float for marker y position.");
                packedFloats[++byteIndex] = yBytes[0];
                packedFloats[++byteIndex] = yBytes[1];
                packedFloats[++byteIndex] = yBytes[2];
                packedFloats[++byteIndex] = yBytes[3];
            }
            return packedFloats;
        }

        public void MarkerPositionsFromFloatArray(byte[] packedFloats)
        {
            if (packedFloats == null)
            {
                // null argument indicates no positions
                return;
            }

            if (packedFloats.Length % (2 * sizeof(float)) != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(packedFloats), App.FindResource<string>(Constant.ResourceKey.MarkersForCounterFloatsUnpaired));
            }

            for (int index = 0; index < packedFloats.Length; index += 2 * sizeof(float))
            {
                float x = BitConverter.ToSingle(packedFloats, index);
                float y = BitConverter.ToSingle(packedFloats, index + sizeof(float));
                this.Markers.Add(new Marker(this.DataLabel, new Point(x, y)));
            }
        }

        public string MarkerPositionsToSpreadsheetString()
        {
            if (this.Markers.Count < 1)
            {
                return null;
            }

            Marker marker = this.Markers[0];
            StringBuilder pointList = new(marker.GetSpreadsheetPositionString());
            for (int index = 1; index < this.Markers.Count; ++index)
            {
                marker = this.Markers[index];
                pointList.Append(Constant.Excel.MarkerPositionSeparator + marker.GetSpreadsheetPositionString());
            }
            return pointList.ToString();
        }

        public static string MarkerPositionsToSpreadsheetString(byte[] packedFloats)
        {
            if ((packedFloats == null) || (packedFloats.Length == 0))
            {
                return null;
            }
            if (packedFloats.Length % (2 * sizeof(float)) != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(packedFloats), App.FindResource<string>(Constant.ResourceKey.MarkersForCounterFloatsUnpaired));
            }

            float x = BitConverter.ToSingle(packedFloats, 0);
            float y = BitConverter.ToSingle(packedFloats, sizeof(float));
            string xAsString = x.ToString(Constant.Excel.MarkerCoordinateFormat, Constant.InvariantCulture);
            string yAsString = y.ToString(Constant.Excel.MarkerCoordinateFormat, Constant.InvariantCulture);
            StringBuilder pointList = new(xAsString + Constant.Excel.MarkerCoordinateSeparator + yAsString);
            for (int index = 2 * sizeof(float); index < packedFloats.Length; index += 2 * sizeof(float))
            {
                x = BitConverter.ToSingle(packedFloats, index);
                y = BitConverter.ToSingle(packedFloats, index + sizeof(float));
                xAsString = x.ToString(Constant.Excel.MarkerCoordinateFormat, Constant.InvariantCulture);
                yAsString = y.ToString(Constant.Excel.MarkerCoordinateFormat, Constant.InvariantCulture);
                pointList.Append(Constant.Excel.MarkerPositionSeparator + xAsString + Constant.Excel.MarkerCoordinateSeparator + yAsString);
            }
            return pointList.ToString();
        }

        public void RemoveMarker(Marker marker)
        {
            Debug.Assert(String.Equals(marker.DataLabel, this.DataLabel, StringComparison.Ordinal), "Marker is associated with a different counter.");

            for (int index = 0; index < this.Markers.Count; ++index)
            {
                Marker candidate = this.Markers[index];
                if (marker.Position == candidate.Position)
                {
                    this.Markers.RemoveAt(index);
                    --this.Count;
                    this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.Markers)));
                    return;
                }
            }

            Debug.Fail("Attempt to remove marker not associated with counter.");
        }

        public static bool TryParseExcelStringToPackedFloats(string valueAsString, out byte[] packedFloats)
        {
            packedFloats = null;
            if (String.IsNullOrEmpty(valueAsString))
            {
                // position string might not contain any positions
                return true;
            }

            string[] positions = valueAsString.Split(Constant.Excel.MarkerPositionSeparator);
            packedFloats = new byte[2 * positions.Length * sizeof(float)];
            int byteIndex = -1;
            foreach (string position in positions)
            {
                string[] xy = position.Split(Constant.Excel.MarkerCoordinateSeparator);
                if (xy.Length != 2)
                {
                    return false;
                }
                if (Utilities.TryParseFixedPointInvariant(xy[0], out float x) == false)
                {
                    return false;
                }
                if (Utilities.TryParseFixedPointInvariant(xy[1], out float y) == false)
                {
                    return false;
                }

                byte[] xBytes = BitConverter.GetBytes(x);
                Debug.Assert(xBytes.Length == 4, "Expected 32 bit float for marker x position.");
                packedFloats[++byteIndex] = xBytes[0];
                packedFloats[++byteIndex] = xBytes[1];
                packedFloats[++byteIndex] = xBytes[2];
                packedFloats[++byteIndex] = xBytes[3];

                byte[] yBytes = BitConverter.GetBytes(y);
                Debug.Assert(yBytes.Length == 4, "Expected 32 bit float for marker y position.");
                packedFloats[++byteIndex] = yBytes[0];
                packedFloats[++byteIndex] = yBytes[1];
                packedFloats[++byteIndex] = yBytes[2];
                packedFloats[++byteIndex] = yBytes[3];
            }

            return true;
        }
    }
}
