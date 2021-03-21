using Carnassial.Control;
using System.Diagnostics;

namespace Carnassial.Command
{
    public class FileSingleFieldChange : FileChange
    {
        public string DataLabel { get; private set; }
        public object? NewValue { get; private set; }
        public object? PreviousValue { get; private set; }
        public string PropertyName { get; private set; }

        public FileSingleFieldChange(long fileID, string dataLabel, string propertyName, object? previousValue, object? newValue, bool isExecuted)
            : base(fileID)
        {
            this.DataLabel = dataLabel;
            this.IsExecuted = isExecuted;
            this.NewValue = newValue;
            this.PreviousValue = previousValue;
            this.PropertyName = propertyName;
        }

        private void ApplyValueToCurrentFile(CarnassialWindow carnassial, object? value)
        {
            Debug.Assert(carnassial.IsFileAvailable(), "Attempt to edit file when no file is current.  Is a menu unexpectedly enabled?");
            Debug.Assert(this.FileID == carnassial.DataHandler.ImageCache.Current.ID, "Attempt to apply edit to a different file.");

            DataEntryControl control = carnassial.DataEntryControls.ControlsByDataLabel[this.DataLabel];
            carnassial.DataHandler.IsProgrammaticUpdate = true;
            FileSingleFieldChange.ApplyValueToFile(carnassial.DataHandler.ImageCache.Current, this.PropertyName, value, control, carnassial.State.CurrentFileSnapshot);
            carnassial.DataHandler.IsProgrammaticUpdate = false;
        }

        public override void Execute(CarnassialWindow carnassial)
        {
            this.ApplyValueToCurrentFile(carnassial, this.NewValue);
            this.IsExecuted = true;
        }

        public bool HasChange()
        {
            return this.NewValue != this.PreviousValue;
        }

        public override string ToString()
        {
            return "single field change";
        }

        public override void Undo(CarnassialWindow carnassial)
        {
            this.ApplyValueToCurrentFile(carnassial, this.PreviousValue);
            this.IsExecuted = false;
        }
    }
}
