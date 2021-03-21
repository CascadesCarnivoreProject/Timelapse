using Carnassial.Util;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace Carnassial.Dialog
{
    public partial class RenameFileDatabaseFile : WindowWithSystemMenu
    {
        public string? NewFileName { get; private set; }

        public RenameFileDatabaseFile(string fileName, Window owner)
        {
            this.InitializeComponent();
            this.Message.SetVisibility();

            this.CurrentFileName.Text = fileName;
            this.Owner = owner;
            this.NewFileName = null;
            this.NewFileNameWithoutExtension.Text = Path.GetFileNameWithoutExtension(fileName);
            this.NewFileNameWithoutExtension.CaretIndex = this.NewFileNameWithoutExtension.Text.Length;
            this.NewFileNameWithoutExtension.TextChanged += this.NewFileNameWithoutExtension_TextChanged;
            this.OkButton.IsEnabled = false;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            CommonUserInterface.SetDefaultDialogPosition(this);
            CommonUserInterface.TryFitWindowInWorkingArea(this);

            this.NewFileNameWithoutExtension.Focus();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }

        private void NewFileNameWithoutExtension_TextChanged(object sender, TextChangedEventArgs e)
        {
            this.NewFileName = this.NewFileNameWithoutExtension.Text + ".ddb";
            this.OkButton.IsEnabled = !String.Equals(this.NewFileName, this.CurrentFileName.Text, StringComparison.OrdinalIgnoreCase); // Enable the button only if the two names differ
        }
    }
}
