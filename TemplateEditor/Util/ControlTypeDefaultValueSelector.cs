using Carnassial.Data;
using System;
using System.Windows;
using System.Windows.Controls;

namespace Carnassial.Editor.Util
{
    internal class ControlTypeDefaultValueSelector : DataTemplateSelector
    {
        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            if (item is ControlRow control)
            {
                return control.ControlType switch
                {
                    ControlType.FixedChoice => (DataTemplate)App.Current.MainWindow.FindResource(EditorConstant.Resources.DefaultValueChoiceComboBox),
                    ControlType.Flag => (DataTemplate)App.Current.MainWindow.FindResource(EditorConstant.Resources.DefaultValueFlagComboBox),
                    ControlType.Counter or 
                    ControlType.DateTime or 
                    ControlType.Note or 
                    ControlType.UtcOffset => (DataTemplate)App.Current.MainWindow.FindResource(EditorConstant.Resources.DefaultValueTextBox),
                    _ => throw new NotSupportedException("Unhandled control type " + control.ControlType + "."),
                };
            }

            return base.SelectTemplate(item, container);
        }
    }
}
