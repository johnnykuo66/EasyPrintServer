using System;
using System.Windows;
using System.Windows.Controls;

namespace EasyPrintServer
{
    public partial class WelcomePage : UserControl
    {
        public event EventHandler StartClicked;

        public WelcomePage()
        {
            InitializeComponent();
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            StartClicked?.Invoke(this, EventArgs.Empty);
        }
    }
}
