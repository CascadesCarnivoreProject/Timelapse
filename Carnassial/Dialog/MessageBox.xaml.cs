using Carnassial.Util;
using System;
using System.Globalization;
using System.Windows;

namespace Carnassial.Dialog
{
    public partial class MessageBox : WindowWithSystemMenu
    {
        private MessageBoxButton buttonType;

        private MessageBox(Message message, Window owner, params object?[] args)
        {
            this.InitializeComponent();
            this.ButtonType = message.Buttons;
            this.DisplayDontShowAgain = message.DisplayDontShowAgain;
            this.Message.Initialize(message, args);
            // this.Message.SetVisibility() not needed as it's called from Initialize()
            this.Owner = owner;
            this.Title = message.WindowTitle;
        }

        public MessageBoxButton ButtonType
        {
            get
            {
                return this.buttonType;
            }
            set
            {
                switch (value)
                {
                    case MessageBoxButton.OK:
                        this.CancelButton.IsCancel = false;
                        this.CancelButton.Visibility = Visibility.Collapsed;
                        this.OKButton.Content = App.FindResource<string>(Constant.ResourceKey.DialogOK);
                        this.OKButton.IsCancel = true;
                        this.OKButton.Visibility = Visibility.Visible;
                        break;
                    case MessageBoxButton.OKCancel:
                        this.CancelButton.Content = App.FindResource<string>(Constant.ResourceKey.DialogCancel);
                        this.CancelButton.IsCancel = true;
                        this.CancelButton.Visibility = Visibility.Visible;
                        this.OKButton.Content = App.FindResource<string>(Constant.ResourceKey.DialogOK);
                        this.OKButton.IsCancel = false;
                        this.OKButton.Visibility = Visibility.Visible;
                        break;
                    case MessageBoxButton.YesNo:
                        this.CancelButton.Content = App.FindResource<string>(Constant.ResourceKey.DialogNo);
                        this.CancelButton.IsCancel = true;
                        this.CancelButton.Visibility = Visibility.Visible;
                        this.OKButton.Content = App.FindResource<string>(Constant.ResourceKey.DialogYes);
                        this.OKButton.IsCancel = false;
                        this.OKButton.Visibility = Visibility.Visible;
                        break;
                    case MessageBoxButton.YesNoCancel:
                    default:
                        throw new ArgumentOutOfRangeException(nameof(value), String.Format(CultureInfo.CurrentCulture, "Unhandled button type {0}.", value));
                }
                this.buttonType = value;
            }
        }

        public bool DisplayDontShowAgain
        {
            get
            {
                return this.DontShowAgain.Visibility == Visibility.Visible;
            }
            set
            {
                this.DontShowAgain.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }

        public static MessageBox FromResource(string messageResourceKey, Window owner, params object?[] args)
        {
            return new MessageBox(App.FindResource<Message>(messageResourceKey), owner, args);
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            CommonUserInterface.SetDefaultDialogPosition(this);
            CommonUserInterface.TryFitWindowInWorkingArea(this);
        }
    }
}
