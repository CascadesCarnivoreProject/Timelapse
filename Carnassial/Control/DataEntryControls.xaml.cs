using Carnassial.Data;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Carnassial.Control
{
    /// <summary>
    /// This class generates data entry controls based upon the controls table.
    /// </summary>
    public partial class DataEntryControls : UserControl
    {
        public const string ControlContainerStyleName = "ContainerGrid";

        private HorizontalLineAdorner? dragAdorner;

        public List<DataEntryControl> Controls { get; private set; }
        public Dictionary<string, DataEntryControl> ControlsByDataLabel { get; private set; }

        public event Action<DataEntryControl, DataEntryControl>? ControlOrderChangedByDragDrop;

        public DataEntryControls()
        {
            this.InitializeComponent();
            this.Controls = new List<DataEntryControl>();
            this.ControlsByDataLabel = new Dictionary<string, DataEntryControl>(StringComparer.Ordinal);
            this.dragAdorner = null;
        }

        public void Clear()
        {
            this.Controls.Clear();
            this.ControlsByDataLabel.Clear();
            this.ControlsView.Items.Clear();
        }

        private void ControlsView_DragOver(object sender, DragEventArgs dragEvent)
        {
            if (this.dragAdorner == null)
            {
                this.dragAdorner = new HorizontalLineAdorner(this) { Margin = new Thickness(5.0, 0.0, 5.0, 0.0) };
            }
            this.dragAdorner.UpdatePosition(dragEvent);
        }

        private void ControlsView_Drop(object sender, DragEventArgs dropEvent)
        {
            if ((this.ControlsView.SelectedItem != null) &&
                this.TryFindDataEntryControl(dropEvent.GetPosition(this.ControlsView), out DataEntryControl? dropTargetControl))
            {
                if (this.dragAdorner != null)
                {
                    this.dragAdorner.Remove();
                    this.dragAdorner = null;
                }

                int dropTargetIndex = -1;
                for (int index = 0; index < this.ControlsView.Items.Count; ++index)
                {
                    FrameworkElement control = (FrameworkElement)this.ControlsView.Items[index];
                    if (control.Tag == dropTargetControl)
                    {
                        dropTargetIndex = index;
                        break;
                    }
                }
                Debug.Assert(dropTargetIndex >= 0, "Couldn't find index of drop target in controls list.");

                object draggedControlContainer = this.ControlsView.SelectedItem;
                int draggedControlIndex = this.ControlsView.SelectedIndex;
                if (draggedControlIndex == dropTargetIndex)
                {
                    // nothing to do since control was dropped in its current location
                    return;
                }

                this.ControlsView.Items.RemoveAt(draggedControlIndex);
                this.ControlsView.Items.Insert(dropTargetIndex, draggedControlContainer);
                this.ControlsView.SelectedIndex = dropTargetIndex;

                DataEntryControl draggedControl = this.Controls[draggedControlIndex];
                this.Controls.RemoveAt(draggedControlIndex);
                this.Controls.Insert(dropTargetIndex, draggedControl);

                this.ControlOrderChangedByDragDrop?.Invoke(draggedControl, dropTargetControl);

                dropEvent.Handled = true;
            }
        }

        private void ControlsView_MouseMove(object sender, MouseEventArgs e)
        {
            if (this.AllowDrop && (e.LeftButton == MouseButtonState.Pressed))
            {
                FrameworkElement controlContainer = (FrameworkElement)this.ControlsView.SelectedItem;
                if (controlContainer == null)
                {
                    if (this.TryFindDataEntryControl(e.GetPosition(this.ControlsView), out DataEntryControl? control) == false)
                    {
                        return;
                    }
                    controlContainer = control.Container;
                }

                // calling DoDragDrop() suppresses mouse move events until drag completes or cancels
                DragDrop.DoDragDrop((DependencyObject)sender, controlContainer, DragDropEffects.Move);
            }
        }

        private void ControlsViewItem_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            // select a data entry control when its contents receive keyboard focus
            // By default, ListView selections are driven by the mouse but not the keyboard.  Without an equivalent of this event
            // handler pressing a hotkey therefore bypasses selection, resulting in an inconsistent user experience.
            ((ListViewItem)sender).IsSelected = true;
        }

        public void CreateControls(TemplateDatabase database, DataEntryHandler dataEntryPropagator, Func<string, List<string>> getNoteAutocompletions)
        {
            // Depending on how the user interacts with the file import process image set loading can be aborted after controls are 
            // generated and then another image set loaded.  Any existing controls therefore need to be cleared.
            this.Clear();

            DataEntryDateTimeOffset? dateTimeControl = null;
            bool showUtcOffset = false;
            List<DataEntryControl> visibleControls = new();
            foreach (ControlRow control in database.Controls)
            {
                // no point in generating a control if it doesn't render in the UX
                if (control.Visible == false)
                {
                    continue;
                }

                if (control.ControlType == ControlType.DateTime)
                {
                    dateTimeControl = new DataEntryDateTimeOffset(control, this);
                    visibleControls.Add(dateTimeControl);
                }
                else if (control.ControlType == ControlType.Note)
                {
                    // standard controls rendering as notes aren't editable by the user 
                    List<string>? autocompletions = null;
                    bool readOnly = control.IsFilePathComponent();
                    if (readOnly == false)
                    {
                        autocompletions = new List<string>(getNoteAutocompletions.Invoke(control.DataLabel));
                    }
                    DataEntryNote noteControl = new(control, autocompletions, readOnly, this);
                    visibleControls.Add(noteControl);
                }
                else if (control.ControlType == ControlType.Flag)
                {
                    DataEntryFlag flagControl = new(control, this);
                    visibleControls.Add(flagControl);
                }
                else if (control.ControlType == ControlType.Counter)
                {
                    DataEntryCounter counterControl = new(control, this);
                    visibleControls.Add(counterControl);
                }
                else if (control.ControlType == ControlType.FixedChoice)
                {
                    DataEntryChoice choiceControl = new(control, this);
                    visibleControls.Add(choiceControl);
                }
                else if (control.ControlType == ControlType.UtcOffset)
                {
                    showUtcOffset = true;
                }
                else
                {
                    Debug.Fail(String.Format(CultureInfo.CurrentCulture, "Unhandled control type {0}.", control.ControlType));
                    continue;
                }
            }

            if (showUtcOffset && (dateTimeControl != null))
            {
                dateTimeControl.ShowUtcOffset();
            }

            for (int controlIndex = 0; controlIndex < visibleControls.Count; ++controlIndex)
            {
                DataEntryControl control = visibleControls[controlIndex];
                control.Container.SetValue(Grid.RowProperty, controlIndex);

                this.Controls.Add(control);
                this.ControlsByDataLabel.Add(control.DataLabel, control);

                this.ControlsView.Items.Add(control.Container);
            }

            if (dataEntryPropagator != null)
            {
                dataEntryPropagator.SetDataEntryCallbacks(this.Controls);
            }
        }

        public void SetDataContext(ImageRow? file)
        {
            foreach (DataEntryControl control in this.Controls)
            {
                control.DataContext = file;
            }
        }

        public bool TryFindDataEntryControl(Point mousePositionRelativeToControlsView, [NotNullWhen(true)] out DataEntryControl? control)
        {
            control = null;

            HitTestResult hitTest = VisualTreeHelper.HitTest(this.ControlsView, mousePositionRelativeToControlsView);
            if (hitTest == null)
            {
                return false;
            }

            FrameworkElement hitElement = (FrameworkElement)hitTest.VisualHit;
            if (hitElement == null)
            {
                return false;
            }

            while (hitElement.Tag is DataEntryControl == false)
            {
                FrameworkElement parent = (FrameworkElement)((FrameworkElement)hitElement).Parent;
                FrameworkElement templatedParent = (FrameworkElement)((FrameworkElement)hitElement).TemplatedParent;
                if (parent != null)
                {
                    hitElement = parent;
                }
                else if (templatedParent != null)
                {
                    hitElement = templatedParent;
                }
                else
                {
                    // fall back to visual search if no parent property is available
                    // As of .NET 4.6.1 WPF inserts TextBlocks under some labels but doesn't set their parent links.
                    hitElement = (FrameworkElement)VisualTreeHelper.GetParent(hitElement);
                    if (hitElement == null)
                    {
                        return false;
                    }
                }
            }

            control = (DataEntryControl)hitElement.Tag;
            return control != null;
        }
    }
}