using Carnassial.Command;
using Carnassial.Control;
using System;
using System.Collections.Generic;
using System.Windows.Input;
using System.Windows.Threading;

namespace Carnassial.Util
{
    public class CarnassialState : CarnassialUserRegistrySettings
    {
        private int keyRepeatCount;
        private KeyEventArgs? mostRecentKey;

        public List<Dictionary<string, object?>?> Analysis { get; private set; }
        public DispatcherTimer BackupTimer { get; private set; }
        public Dictionary<string, object?> CurrentFileSnapshot { get; set; }
        public byte DifferenceThreshold { get; set; }
        public bool FileNavigatorSliderDragging { get; set; }

        // timer for flushing FileNavigatorSlider drag events
        public DispatcherTimer FileNavigatorSliderTimer { get; private set; }

        public DateTime MostRecentRender { get; set; }
        public string? MostRecentFileAddFolderPath { get; set; }
        public int MostRecentlyFocusedControlIndex { get; set; }
        public long MouseHorizontalScrollDelta { get; set; }
        public string? MouseOverCounter { get; set; }
        public List<DataEntryNote> NoteControlsWithNewValues { get; private set; }
        public UndoRedoChain<CarnassialWindow> UndoRedoChain { get; private set; }

        public CarnassialState()
        {
            this.keyRepeatCount = 0;
            this.mostRecentKey = null;

            this.Analysis = new(Constant.AnalysisSlots);
            for (int analysisSlot = 0; analysisSlot < Constant.AnalysisSlots; ++analysisSlot)
            {
                this.Analysis.Add(null);
            }
            this.BackupTimer = new DispatcherTimer()
            {
                Interval = Constant.Database.BackupInterval
            };
            this.CurrentFileSnapshot = new(StringComparer.Ordinal);
            this.DifferenceThreshold = Constant.Images.DifferenceThresholdDefault;

            this.FileNavigatorSliderDragging = false;
            this.FileNavigatorSliderTimer = new DispatcherTimer()
            {
                Interval = TimeSpan.FromSeconds(1.0 / Constant.ThrottleValues.DesiredMaximumImageRendersPerSecondUpperBound)
            };

            this.MostRecentRender = DateTime.UtcNow - this.Throttles.DesiredIntervalBetweenRenders;
            this.MostRecentFileAddFolderPath = null;
            this.MostRecentlyFocusedControlIndex = -1;
            this.MouseHorizontalScrollDelta = 0;
            this.MouseOverCounter = null;
            this.NoteControlsWithNewValues = new List<DataEntryNote>();
            this.UndoRedoChain = new UndoRedoChain<CarnassialWindow>();
        }

        public int GetKeyRepeatCount(KeyEventArgs key)
        {
            // check mostRecentKey for null as key delivery is not entirely deterministic
            // It's possible WPF will send the first key as a repeat if the user holds a key down or starts typing while the main window is opening.
            if (key.IsRepeat && this.mostRecentKey != null && this.mostRecentKey.IsRepeat && (key.Key == this.mostRecentKey.Key))
            {
                ++this.keyRepeatCount;
            }
            else
            {
                this.keyRepeatCount = 0;
            }
            this.mostRecentKey = key;
            return this.keyRepeatCount;
        }

        public void ResetImageSetRelatedState()
        {
            // fields with Carnassial scope which therefore aren't reset here
            // this.keyRepeatCount
            // this.mostRecentKey
            // this.DifferenceThreshold
            // this.FileNavigatorSliderDragging
            // this.MostRecentFileAddFolderPath

            for (int analysisSlot = 0; analysisSlot < this.Analysis.Count; ++analysisSlot)
            {
                this.Analysis[analysisSlot] = null;
            }
            this.CurrentFileSnapshot.Clear();
            this.MostRecentlyFocusedControlIndex = -1;
            this.MouseHorizontalScrollDelta = 0;
            this.MouseOverCounter = null;
            this.NoteControlsWithNewValues.Clear();
            this.UndoRedoChain.Clear();
        }
    }
}
