using Carnassial.Data;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfControl = System.Windows.Controls.Control;

namespace Carnassial.Control
{
    public abstract class DataEntryControl
    {
        /// <summary>Gets or sets a value indicating whether the control's content is user editable.</summary>
        public abstract bool ContentReadOnly { get; set; }

        /// <summary>Gets or sets a value indicating whether the control's content is copyable.</summary>
        public bool Copyable { get; set; }

        /// <summary>Gets the container that holds the control.</summary>
        public Grid Container { get; private set; }

        public long ContentMaxWidth
        {
            get { return (long)this.Container.ColumnDefinitions[1].MaxWidth; }
            set { this.Container.ColumnDefinitions[1].MaxWidth = value; }
        }

        /// <summary>Gets the context menu associated with the control.</summary>
        public ContextMenu ContextMenu
        {
            get { return this.Container.ContextMenu; }
        }

        public abstract ImageRow? DataContext { get; set; }

        /// <summary>Gets or sets the data label which corresponds to this control.</summary>
        public string DataLabel { get; protected set; }

        public abstract void Focus(DependencyObject focusScope);

        public abstract string Label { get; set; }

        public abstract string LabelTooltip { get; set; }

        public string PropertyName { get; private set; }

        public ControlType Type { get; private set; }

        protected DataEntryControl(ControlRow control, DataEntryControls styleProvider)
        {
            // populate properties from database definition of control
            // this.Content and Tooltip can't be set, however, as the caller hasn't instantiated the content control yet
            this.Copyable = control.Copyable;
            this.DataLabel = control.DataLabel;
            this.PropertyName = ImageRow.GetPropertyName(control.DataLabel);
            this.Type = control.ControlType;

            // create the grid which will contain the label and ontent
            this.Container = new Grid();
            this.Container.ColumnDefinitions.Add(new ColumnDefinition()
            {
                Width = GridLength.Auto
            });
            this.Container.ColumnDefinitions.Add(new ColumnDefinition()
            {
                MaxWidth = control.MaxWidth,
                Width = new GridLength(1.0, GridUnitType.Star)
            });
            this.Container.RowDefinitions.Add(new RowDefinition()
            {
                Height = GridLength.Auto
            });
            this.Container.Style = (Style)styleProvider.FindResource(DataEntryControls.ControlContainerStyleName);

            // use the containers's tag to point back to this so event handlers can access the DataEntryControl
            // this is needed by callbacks such as DataEntryHandler.Container_PreviewMouseRightButtonDown() and CarnassialWindow.CounterControl_MouseLeave()
            this.Container.Tag = this;
        }

        public void AppendToContextMenu(params MenuItem[] menuItems)
        {
            if (menuItems.Length < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(menuItems));
            }

            // if a context menu isn't already configured on the control's container create one
            // The context menu is attached to the containing StackPanel as this provides the context menu throughout the DataEntryControl's display area.  If
            // the content control defines its own context menu, as is the case for TextBoxes and derived classes by default, that takes priority within the
            // content control's area.  It would be desirable to also append the menu items here to such default menus but WCF makes this nontrivial as default 
            // menus are lazily instantiated and MenuItems can only be added to one ContextMenu, meaning it's probably best the caller pass multiple copies of 
            // menu items as C# is unfriendly towwards copying handlers from one event to another.
            ContextMenu menu = this.Container.ContextMenu;
            if (menu == null)
            {
                menu = new ContextMenu();
                this.Container.ContextMenu = menu;
            }
            menu.Tag = this;

            if (menu.HasItems)
            {
                menu.Items.Add(new Separator());
            }

            foreach (MenuItem menuItem in menuItems)
            {
                menu.Items.Add(menuItem);
            }
        }

        public virtual List<string> GetWellKnownValues()
        {
            // return empty set as flags and counters don't have choices
            return new List<string>();
        }

        public abstract void HighlightIfCopyable();

        public virtual bool IsCopyableValue(object? value)
        {
            string? valueAsString = (string?)value;
            if (String.IsNullOrEmpty(valueAsString))
            {
                return false;
            }

            return true;
        }

        public abstract void RemoveHighlightIfCopyable();

        public virtual void SetWellKnownValues(List<string> choices)
        {
            // do nothing as flags and counters don't have choices
        }

        public abstract void SetValue(object valueAsObject);
    }

    // A generic control comprises a stack panel containing 
    // - a control containing at least a descriptive label 
    // - another control for displaying / entering data at a given width
    public abstract class DataEntryControl<TContent, TLabel> : DataEntryControl
        where TContent : WpfControl, new()
        where TLabel : ContentControl, new()
    {
        private static readonly DependencyProperty Background = WpfControl.BackgroundProperty;

        public TContent ContentControl { get; private set; }

        public override ImageRow? DataContext
        {
            get { return (ImageRow)this.ContentControl.DataContext; }
            set { this.ContentControl.DataContext = value; }
        }

        /// <summary>Get or sets the control label's value.</summary>
        public override string Label
        {
            get { return (string)this.LabelControl.Content; }
            set { this.LabelControl.Content = value; }
        }

        public TLabel LabelControl { get; private set; }

        public override string LabelTooltip
        {
            get { return (string)this.LabelControl.ToolTip; }
            set { this.LabelControl.ToolTip = value; }
        }

        protected DataEntryControl(ControlRow control, DataEntryControls styleProvider, ControlContentStyle contentStyleName, ControlLabelStyle labelStyleName)
            : this(control, styleProvider, contentStyleName, labelStyleName, false)
        {
        }

        protected DataEntryControl(ControlRow control, DataEntryControls styleProvider, ControlContentStyle contentStyleName, ControlLabelStyle labelStyleName, bool readOnly)
            : base(control, styleProvider)
        {
            this.ContentControl = new TContent()
            {
                IsTabStop = true,
                Style = (Style)styleProvider.FindResource(contentStyleName.ToString())
            };
            this.ContentReadOnly = readOnly;
            this.SetValue(control.DefaultValue);

            // use the content's tag to point back to this so event handlers can access the DataEntryControl as well as just the content control
            this.ContentControl.Tag = this;

            // find a hotkey to assign to the control, if possible
            int hotkeyIndex = -1;
            if ((readOnly == false) && (control.Label != null))
            {
                for (int labelIndex = 0; labelIndex < control.Label.Length; ++labelIndex)
                {
                    if (!Constant.Control.ReservedHotKeys.Contains(control.Label[labelIndex]))
                    {
                        hotkeyIndex = labelIndex;
                        break;
                    }
                }
            }

            // create the label
            this.LabelControl = new TLabel();
            if (hotkeyIndex == -1)
            {
                // control doesn't need a hotkey as it's not editable or a hotkey cannot be assigned
                this.LabelControl.Content = control.Label;
            }
            else
            {
                // control can be assigned a hotkey
                Debug.Assert(control.Label != null);
                this.LabelControl.Content = control.Label.Substring(0, hotkeyIndex) + "_" + control.Label[hotkeyIndex..];
            }
            this.LabelControl.Style = (Style)styleProvider.FindResource(labelStyleName.ToString());
            this.LabelTooltip = control.Tooltip;

            if (this.LabelControl is Label labelControlAsLabel)
            {
                // if the label control is a Label there's no action which can be taken with it
                // In this case, set the hotkey to the first letter in the control's name and bind the label's target to the content control so that the 
                // hotkey moves focus to the content control rather than the label.
                labelControlAsLabel.Target = this.ContentControl;
            }

            // add the label and content to the control grid
            this.Container.Children.Add(this.LabelControl);
            this.Container.Children.Add(this.ContentControl);
        }

        public override void Focus(DependencyObject focusScope)
        {
            // request the focus manager figure out how to assign focus within the edit control as not all controls are focusable at their top level
            // This is not reliable at small focus scopes, possibly due to interaction with CarnassialWindow's focus management, but seems reasonably
            // well behaved at application scope.
            FocusManager.SetFocusedElement(focusScope, this.ContentControl);
        }

        public override void HighlightIfCopyable()
        {
            if (this.Copyable)
            {
                this.LabelControl.Background = SystemColors.InactiveSelectionHighlightBrush;
            }
        }

        public override void RemoveHighlightIfCopyable()
        {
            if (this.Copyable)
            {
                this.LabelControl.ClearValue(DataEntryControl<TContent, TLabel>.Background);
            }
        }
    }
}