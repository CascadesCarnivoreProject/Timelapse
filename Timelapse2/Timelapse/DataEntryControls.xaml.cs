﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Windows.Controls;
using Timelapse.Database;
using Timelapse.Util;

namespace Timelapse
{
    /// <summary>
    /// This class generates controls based upon the information passed into it from the data grid templateTable
    /// </summary>
    public partial class DataEntryControls : UserControl
    {
        // Given a key, return its associated control
        public Dictionary<string, DataEntryControl> ControlsByDataLabel { get; private set; }
        public List<DataEntryControl> Controls { get; private set; } // list of all our counter controls
        // The wrap panel will contain all our controls. If we want to reparent things, we do it by reparenting the wrap panel
        public PropagateControl Propagate { get; private set; }

        public DataEntryControls()
        {
            this.InitializeComponent();
            this.ControlsByDataLabel = new Dictionary<string, DataEntryControl>();
            this.Controls = new List<DataEntryControl>();
        }

        public void Generate(ImageDatabase database, ImageTableEnumerator imageEnumerator)
        {
            this.Propagate = new PropagateControl(database, imageEnumerator);

            for (int row = 0; row < database.TemplateTable.Rows.Count; row++)
            {
                // no point in generating a control if it doesn't render in the UX
                DataRow dataRow = database.TemplateTable.Rows[row];
                string visiblityAsString = dataRow[Constants.Control.Visible].ToString();
                bool visible = String.Equals(Boolean.TrueString, visiblityAsString, StringComparison.OrdinalIgnoreCase) ? true : false;
                if (visible == false)
                {
                    continue;
                }

                // get the values for the control
                string copyableAsString = dataRow[Constants.Control.Copyable].ToString();
                bool copyable = String.Equals(Boolean.TrueString, copyableAsString, StringComparison.OrdinalIgnoreCase) ? true : false;
                string dataLabel = dataRow.GetStringField(Constants.Control.DataLabel);
                string defaultValue = dataRow[Constants.Control.DefaultValue].ToString();
                long id = (long)dataRow[Constants.DatabaseColumn.ID]; // TODO Need to use this ID to pass between controls and data
                string label = dataRow[Constants.Control.Label].ToString();
                string list = dataRow[Constants.Control.List].ToString();
                string tooltip = dataRow[Constants.Control.Tooltip].ToString();
                string type = dataRow[Constants.Control.Type].ToString();
                string widthAsString = dataRow[Constants.Control.TextBoxWidth].ToString();
                int width = (widthAsString == String.Empty) ? 0 : Int32.Parse(widthAsString);

                DataEntryControl controlToAdd;
                if (type == Constants.DatabaseColumn.File ||
                    type == Constants.DatabaseColumn.RelativePath ||
                    type == Constants.DatabaseColumn.Folder ||
                    type == Constants.DatabaseColumn.Date ||
                    type == Constants.DatabaseColumn.Time ||
                    type == Constants.Control.Note)
                {
                    bool createContextMenu = (type == Constants.Control.Note) ? true : false;
                    DataEntryNote noteControl = new DataEntryNote(dataLabel, this, createContextMenu);
                    noteControl.Label = label;
                    noteControl.Width = width;
                    if (type == Constants.DatabaseColumn.Folder || 
                        type == Constants.DatabaseColumn.RelativePath ||
                        type == Constants.DatabaseColumn.File)
                    {
                        // File name and path aren't editable by the user 
                        noteControl.ReadOnly = true;
                    }
                    controlToAdd = noteControl;
                }
                else if (type == Constants.Control.Flag || type == Constants.Control.DeleteFlag)
                {
                    DataEntryFlag flagControl = new DataEntryFlag(dataLabel, this, true);
                    flagControl.Label = label;
                    flagControl.Width = width;
                    controlToAdd = flagControl;
                }
                else if (type == Constants.Control.Counter)
                {
                    DataEntryCounter counterControl = new DataEntryCounter(dataLabel, this, true);
                    counterControl.Label = label;
                    counterControl.Width = width;
                    controlToAdd = counterControl;
                }
                else if (type == Constants.Control.FixedChoice || type == Constants.DatabaseColumn.ImageQuality)
                {
                    bool createContextMenu = (type == Constants.Control.FixedChoice) ? true : false;
                    DataEntryChoice choiceControl = new DataEntryChoice(dataLabel, this, createContextMenu, list);
                    choiceControl.Label = label;
                    choiceControl.Width = width;
                    controlToAdd = choiceControl;
                }
                else
                {
                    Debug.Assert(false, String.Format("Unhandled control type {0}.", type));
                    continue;
                }

                controlToAdd.Content = defaultValue;
                controlToAdd.Copyable = copyable;
                controlToAdd.Tooltip = tooltip;
                this.ControlGrid.Inlines.Add(controlToAdd.Container);
                this.Controls.Add(controlToAdd);
                this.ControlsByDataLabel.Add(dataLabel, controlToAdd);
            }
        }

        public void AddButton(Control button)
        {
            this.ButtonLocation.Child = button;
        }
    }
}