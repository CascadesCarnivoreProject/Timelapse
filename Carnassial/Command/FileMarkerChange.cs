﻿using Carnassial.Control;
using Carnassial.Data;
using Carnassial.Images;
using System.Diagnostics;

namespace Carnassial.Command
{
    internal class FileMarkerChange : FileChange
    {
        private bool isCreation;
        private Marker marker;

        public FileMarkerChange(long fileID, MarkerCreatedOrDeletedEventArgs markerChange)
            : base(fileID)
        {
            this.isCreation = markerChange.IsCreation;
            this.marker = markerChange.Marker;
        }

        private void AddMarker(CarnassialWindow carnassial)
        {
            // insert the new marker and include it in the display list
            DataEntryCounter counter = (DataEntryCounter)carnassial.DataEntryControls.ControlsByDataLabel[this.marker.DataLabel];
            MarkersForCounter markersForCounter = carnassial.DataHandler.ImageCache.CurrentMarkers[this.marker.DataLabel];
            markersForCounter.AddMarker(this.marker);
            carnassial.RefreshDisplayedMarkers();

            // increment the counter to reflect the new marker
            carnassial.DataHandler.IsProgrammaticUpdate = true;
            carnassial.DataHandler.IncrementOrResetCounter(counter);
            carnassial.DataHandler.IsProgrammaticUpdate = false;
        }

        public override void Execute(CarnassialWindow carnassial)
        {
            Debug.Assert(carnassial.IsFileAvailable(), "Attempt to change markers when no file is current.");
            Debug.Assert(this.FileID == carnassial.DataHandler.ImageCache.Current.ID, "Attempt to apply edit to a different file.");

            if (this.isCreation)
            {
                this.AddMarker(carnassial);
            }
            else
            {
                this.RemoveMarker(carnassial);
            }

            this.IsExecuted = true;
        }

        private void RemoveMarker(CarnassialWindow carnassial)
        {
            // remove the marker from in memory data and from the display list
            MarkersForCounter markersForCounter = carnassial.DataHandler.ImageCache.CurrentMarkers[this.marker.DataLabel];
            markersForCounter.RemoveMarker(this.marker);
            carnassial.RefreshDisplayedMarkers();

            // decrement the counter to reflect the new marker
            DataEntryCounter counter = (DataEntryCounter)carnassial.DataEntryControls.ControlsByDataLabel[this.marker.DataLabel];
            carnassial.DataHandler.IsProgrammaticUpdate = true;
            carnassial.DataHandler.DecrementOrResetCounter(counter);
            carnassial.DataHandler.IsProgrammaticUpdate = false;
        }

        public override string ToString()
        {
            if (this.isCreation)
            {
                return "marker addition";
            }
            return "marker removal";
        }

        public override void Undo(CarnassialWindow carnassial)
        {
            Debug.Assert(carnassial.IsFileAvailable(), "Attempt to change markers when no file is current.");
            Debug.Assert(this.FileID == carnassial.DataHandler.ImageCache.Current.ID, "Attempt to apply edit to a different file.");

            if (this.isCreation)
            {
                this.RemoveMarker(carnassial);
            }
            else
            {
                this.AddMarker(carnassial);
            }

            this.IsExecuted = false;
        }
    }
}