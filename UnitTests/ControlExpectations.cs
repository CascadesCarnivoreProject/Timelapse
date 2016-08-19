﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Data;
using Timelapse.Database;

namespace Timelapse.UnitTests
{
    internal class ControlExpectations
    {
        public long ControlOrder { get; set; }

        public bool Copyable { get; set; }

        public string DataLabel { get; set; }

        public string DefaultValue { get; set; }

        public long ID { get; set; }

        public string Label { get; set; }

        public string List { get; set; }

        public long SpreadsheetOrder { get; set; }

        public int TextBoxWidth { get; set; }

        public string Tooltip { get; set; }

        public string Type { get; set; }

        public bool Visible { get; set; }

        public static ControlExpectations CreateCounter(string dataLabel, long id)
        {
            return new ControlExpectations()
            {
                ControlOrder = id,
                Copyable = false,
                DataLabel = dataLabel,
                DefaultValue = Constants.ControlDefault.CounterValue,
                ID = id,
                Label = dataLabel,
                List = String.Empty,
                SpreadsheetOrder = id,
                TextBoxWidth = Constants.ControlDefault.CounterWidth,
                Tooltip = Constants.ControlDefault.CounterTooltip,
                Type = Constants.Control.Counter,
                Visible = true
            };
        }

        public static ControlExpectations CreateChoice(string dataLabel, long id)
        {
            return new ControlExpectations()
            {
                ControlOrder = id,
                Copyable = true,
                DataLabel = dataLabel,
                DefaultValue = Constants.ControlDefault.Value,
                ID = id,
                Label = dataLabel,
                List = String.Empty,
                SpreadsheetOrder = id,
                TextBoxWidth = Constants.ControlDefault.FixedChoiceWidth,
                Tooltip = Constants.ControlDefault.FixedChoiceTooltip,
                Type = Constants.Control.FixedChoice,
                Visible = true
            };
        }

        public static ControlExpectations CreateFlag(string dataLabel, long id)
        {
            return new ControlExpectations()
            {
                ControlOrder = id,
                Copyable = true,
                DataLabel = dataLabel,
                DefaultValue = Constants.ControlDefault.FlagValue,
                ID = id,
                Label = dataLabel,
                List = String.Empty,
                SpreadsheetOrder = id,
                TextBoxWidth = Constants.ControlDefault.FlagWidth,
                Tooltip = Constants.ControlDefault.FlagTooltip,
                Type = Constants.Control.Flag,
                Visible = true
            };
        }

        public static ControlExpectations CreateNote(string dataLabel, long id)
        {
            return new ControlExpectations()
            {
                ControlOrder = id,
                Copyable = true,
                DataLabel = dataLabel,
                DefaultValue = Constants.ControlDefault.Value,
                ID = id,
                Label = dataLabel,
                List = String.Empty,
                SpreadsheetOrder = id,
                TextBoxWidth = Constants.ControlDefault.NoteWidth,
                Tooltip = Constants.ControlDefault.NoteTooltip,
                Type = Constants.Control.Note,
                Visible = true
            };
        }

        public void Verify(ControlRow control)
        {
            Assert.IsTrue(control.ControlOrder == this.ControlOrder, "{0}: Expected ControlOrder '{1}' but found '{2}'.", this.DataLabel, this.ControlOrder, control.ControlOrder);
            Assert.IsTrue(control.Copyable == this.Copyable, "{0}: Expected Copyable '{1}' but found '{2}'.", this.DataLabel, this.Copyable, control.Copyable);
            Assert.IsTrue(control.DataLabel == this.DataLabel, "{0}: Expected DataLabel '{1}' but found '{2}'.", this.DataLabel, this.DataLabel, control.DataLabel);
            Assert.IsTrue(control.DefaultValue == this.DefaultValue, "{0}: Expected DefaultValue '{1}' but found '{2}'.", this.DataLabel, this.DefaultValue, control.DefaultValue);
            Assert.IsTrue(control.ID == this.ID, "{0}: Expected ID '{1}' but found '{2}'.", this.DataLabel, this.ID, control.ID);
            Assert.IsTrue(control.Label == this.Label, "{0}: Expected Label '{1}' but found '{2}'.", this.DataLabel, this.Label, control.Label);
            Assert.IsTrue(control.List == this.List, "{0}: Expected List '{1}' but found '{2}'.", this.DataLabel, this.List, control.List);
            Assert.IsTrue(control.SpreadsheetOrder == this.SpreadsheetOrder, "{0}: Expected SpreadsheetOrder '{1}' but found '{2}'.", this.DataLabel, this.SpreadsheetOrder, control.SpreadsheetOrder);
            Assert.IsTrue(control.TextBoxWidth == this.TextBoxWidth, "{0}: Expected TextBoxWidth '{1}' but found '{2}'.", this.DataLabel, this.TextBoxWidth, control.TextBoxWidth);
            Assert.IsTrue(control.Tooltip == this.Tooltip, "{0}: Expected Tooltip '{1}' but found '{2}'.", this.DataLabel, this.Tooltip, control.Tooltip);
            Assert.IsTrue(control.Type == this.Type, "{0}: Expected Type '{1}' but found '{2}'.", this.DataLabel, this.Type, control.Type);
            Assert.IsTrue(control.Visible == this.Visible, "{0}: Expected Visible '{1}' but found '{2}'.", this.DataLabel, this.Visible, control.Visible);
        }
    }
}