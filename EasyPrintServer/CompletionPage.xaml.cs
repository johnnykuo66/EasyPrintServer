using System;
using System.Windows;
using System.Windows.Controls;

namespace EasyPrintServer
{
    public partial class CompletionPage : UserControl
    {
        public event EventHandler Finished;

        public CompletionPage(string printerName,
                              string printerIp,
                              bool simulated,
                              bool shared,
                              string shareName)
        {
            InitializeComponent();

            SummaryText.Text =
                $"Printer Name: {printerName}\n" +
                $"Printer IP: {printerIp}\n\n" +
                $"{(simulated ? "Mode: SIMULATED (no system changes made)\n" : "Mode: INSTALLED\n")}" +
                $"{(shared ? $"Shared As: \\\\{Environment.MachineName}\\{shareName}\n" : "Shared: No\n")}";
        }

        private void Finish_Click(object sender, RoutedEventArgs e)
        {
            Finished?.Invoke(this, EventArgs.Empty);
        }
    }
}




















//  <<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<   INVALID ARGUMENT ERROR WHEN SEARCHING DRIVER FOLDER ALSO CHECK THIS OUT TMRW >>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>