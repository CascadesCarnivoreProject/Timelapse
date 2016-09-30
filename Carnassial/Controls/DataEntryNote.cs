﻿using Carnassial.Database;
using System.Collections.Generic;
using System.Windows.Controls;

namespace Carnassial.Controls
{
    // A note lays out as a stack panel containing
    // - a label containing the descriptive label) 
    // - an editable textbox (containing the content) at the given width
    public class DataEntryNote : DataEntryControl<TextBox, Label>
    {
        public List<string> Autocompletions { get; set; }

        /// <summary>Gets the content of the note</summary>
        public override string Content
        {
            get { return this.ContentControl.Text; }
        }

        public bool ContentChanged { get; set; }

        public override bool ContentReadOnly
        {
            get { return this.ContentControl.IsReadOnly; }
            set { this.ContentControl.IsReadOnly = value; }
        }

        public DataEntryNote(ControlRow control, List<string> autocompletions, DataEntryControls styleProvider) : 
            base(control, styleProvider, ControlContentStyle.TextBoxCodeBar, ControlLabelStyle.LabelCodeBar)
        {
            this.Autocompletions = autocompletions;
            this.ContentChanged = false;
        }

        public override void SetContentAndTooltip(string value)
        {
            this.ContentChanged = this.ContentControl.Text != value;
            this.ContentControl.Text = value;
            this.ContentControl.ToolTip = value;
        }
    }
}
