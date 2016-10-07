﻿using Carnassial.Util;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Documents;

namespace Carnassial.Dialog
{
    public partial class TemplateSynchronization : Window
    {
        public TemplateSynchronization(List<string> errors, Window owner)
        {
            this.InitializeComponent();
            Utilities.TryFitWindowInWorkingArea(this);

            this.Owner = owner;
            foreach (string error in errors)
            {
                this.TextBlockDetails.Inlines.Add(new Run { Text = "     " + error });
            }
        }

        private void ExitProgram_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }

        private void UseOldTemplate_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }
    }
}