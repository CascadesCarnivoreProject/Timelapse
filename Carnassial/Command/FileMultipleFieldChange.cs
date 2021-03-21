using Carnassial.Control;
using Carnassial.Data;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;

namespace Carnassial.Command
{
    internal class FileMultipleFieldChange : FileChange
    {
        private readonly Dictionary<string, object?> newValuesByPropertyName;
        private readonly Dictionary<string, object?> previousValuesByPropertyName;

        public FileMultipleFieldChange(FileTableEnumerator fileEnumerator, Dictionary<string, object?> newValuesByPropertyName)
            : base(fileEnumerator.Current.ID)
        {
            Debug.Assert(fileEnumerator.IsFileAvailable, "File enumerator is not positioned on a file.");
            this.newValuesByPropertyName = newValuesByPropertyName;
            this.newValuesByPropertyName[Constant.DatabaseColumn.ID] = fileEnumerator.Current.ID;
            this.previousValuesByPropertyName = fileEnumerator.Current.GetValues();
            this.RemoveUnchangedFields();
        }

        public FileMultipleFieldChange(Dictionary<string, object?> previousValuesByPropertyName, FileTableEnumerator fileEnumerator)
            : base(fileEnumerator.Current.ID)
        {
            Debug.Assert(fileEnumerator.IsFileAvailable, "File enumerator is not positioned on a file.");
            Debug.Assert(previousValuesByPropertyName != null, "Previous state is empty.");
            object? previousID = previousValuesByPropertyName[Constant.DatabaseColumn.ID];
            Debug.Assert(previousID != null);
            if ((long)previousID != fileEnumerator.Current.ID)
            {
                throw new ArgumentOutOfRangeException(nameof(previousValuesByPropertyName), String.Format(CultureInfo.CurrentCulture, "Previous state is for file {0} rather than {1}.", previousValuesByPropertyName[Constant.DatabaseColumn.ID], fileEnumerator.Current.ID));
            }

            this.IsExecuted = true;
            this.newValuesByPropertyName = fileEnumerator.Current.GetValues();
            this.previousValuesByPropertyName = previousValuesByPropertyName;
            this.RemoveUnchangedFields();
        }

        public int Changes
        {
            get { return this.newValuesByPropertyName.Count; }
        }

        private void ApplyValuesToCurrentFile(CarnassialWindow carnassial, Dictionary<string, object?> valuesByPropertyName)
        {
            Debug.Assert(carnassial.IsFileAvailable(), "Attempt to edit file when no file is current.  Is a menu unexpectedly enabled?");
            Debug.Assert(this.FileID == carnassial.DataHandler.ImageCache.Current.ID, "Attempt to apply edit to a different file.");

            // OK if file ID doesn't match as values may be coming from the clipboard, the previous file, or analysis
            carnassial.DataHandler.IsProgrammaticUpdate = true;
            foreach (KeyValuePair<string, object?> singleFieldChange in valuesByPropertyName)
            {
                string dataLabel = ImageRow.GetDataLabel(singleFieldChange.Key);
                DataEntryControl control = carnassial.DataEntryControls.ControlsByDataLabel[dataLabel];
                FileMultipleFieldChange.ApplyValueToFile(carnassial.DataHandler.ImageCache.Current, singleFieldChange.Key, singleFieldChange.Value, control, carnassial.State.CurrentFileSnapshot);
            }
            carnassial.DataHandler.IsProgrammaticUpdate = false;
        }

        public FileSingleFieldChange AsSingleChange()
        {
            if (this.Changes != 1)
            {
                throw new InvalidOperationException(String.Format(CultureInfo.CurrentCulture, "A multiple field change with {0} changes cannot be converted to a single field change.", this.Changes));
            }

            KeyValuePair<string, object?> newValue = this.newValuesByPropertyName.First();
            object? previousValue = this.previousValuesByPropertyName[newValue.Key];
            FileSingleFieldChange singleChange = new(this.FileID, ImageRow.GetDataLabel(newValue.Key), newValue.Key, previousValue, newValue.Value, this.IsExecuted);
            return singleChange;
        }

        public override void Execute(CarnassialWindow carnassial)
        {
            this.ApplyValuesToCurrentFile(carnassial, this.newValuesByPropertyName);
            this.IsExecuted = true;
        }

        private void RemoveUnchangedFields()
        {
            // remove any fields which have the same value in both the previous and new states
            List<string> unchangedFields = new();
            foreach (KeyValuePair<string, object?> newValue in this.newValuesByPropertyName)
            {
                object? previousValue = this.previousValuesByPropertyName[newValue.Key];
                bool previousValueNull = previousValue == null;
                bool newValueNull = newValue.Value == null;
                if (previousValueNull ^ newValueNull)
                {
                    continue;
                }
                if ((previousValueNull && newValueNull) || previousValue!.Equals(newValue.Value))
                {
                    unchangedFields.Add(newValue.Key);
                }
            }

            foreach (string propertyName in unchangedFields)
            {
                this.newValuesByPropertyName.Remove(propertyName);
                this.previousValuesByPropertyName.Remove(propertyName);
            }

            // remove any fields which aren't present in the new state and therefore aren't modified
            if (this.previousValuesByPropertyName.Count != this.newValuesByPropertyName.Count)
            {
                foreach (string propertyName in this.previousValuesByPropertyName.Keys)
                {
                    if (this.newValuesByPropertyName.ContainsKey(propertyName) == false)
                    {
                        unchangedFields.Add(propertyName);
                    }
                }

                foreach (string propertyName in unchangedFields)
                {
                    this.previousValuesByPropertyName.Remove(propertyName);
                }

                Debug.Assert(this.previousValuesByPropertyName.Count == this.newValuesByPropertyName.Count, "Mismatch between new and previous states.");
            }
        }

        public override string ToString()
        {
            return "multiple field change";
        }

        public override void Undo(CarnassialWindow carnassial)
        {
            this.ApplyValuesToCurrentFile(carnassial, this.previousValuesByPropertyName);
            this.IsExecuted = false;
        }
    }
}
