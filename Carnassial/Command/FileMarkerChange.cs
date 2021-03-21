using Carnassial.Data;
using Carnassial.Images;
using System.Diagnostics;

namespace Carnassial.Command
{
    internal class FileMarkerChange : FileChange
    {
        private readonly bool isCreation;
        private readonly Marker marker;

        public FileMarkerChange(long fileID, MarkerCreatedOrDeletedEventArgs markerChange)
            : base(fileID)
        {
            this.isCreation = markerChange.IsCreation;
            this.marker = markerChange.Marker;
        }

        private void AddMarker(CarnassialWindow carnassial)
        {
            Debug.Assert(carnassial.IsFileAvailable(), "Attempt to change markers when no file displayed.");

            // insert the new marker and include it in the display list
            MarkersForCounter markersForCounter = carnassial.DataHandler.ImageCache.Current!.GetMarkersForCounter(this.marker.DataLabel);
            carnassial.DataHandler.IsProgrammaticUpdate = true;
            markersForCounter.AddMarker(this.marker);
            carnassial.DataHandler.IsProgrammaticUpdate = false;
            carnassial.RefreshDisplayedMarkers();
        }

        public override void Execute(CarnassialWindow carnassial)
        {
            Debug.Assert(carnassial.IsFileAvailable(), "Attempt to change markers when no file displayed.");
            Debug.Assert(this.FileID == carnassial.DataHandler.ImageCache.Current!.ID, "Attempt to apply edit to a different file.");

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
            MarkersForCounter markersForCounter = carnassial.DataHandler.ImageCache.Current.GetMarkersForCounter(this.marker.DataLabel);
            carnassial.DataHandler.IsProgrammaticUpdate = true;
            markersForCounter.RemoveMarker(this.marker);
            carnassial.DataHandler.IsProgrammaticUpdate = false;
            carnassial.RefreshDisplayedMarkers();
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
