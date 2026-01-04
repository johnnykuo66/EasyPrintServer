using System;
using System.Net;
using System.Net.NetworkInformation;
using System.Windows;
using System.Windows.Controls;

namespace EasyPrintServer
{
    public partial class PrinterSetupPage : UserControl
    {
        public event EventHandler PrinterValidated;

        public string PrinterIP { get; private set; }
        public string PrinterName { get; private set; }

        public PrinterSetupPage()
        {
            InitializeComponent();
        }

        private void TestConnection_Click(object sender, RoutedEventArgs e)
        {
            StatusText.Text = "";
            NextButton.IsEnabled = false;

#if DEBUG
    // Dev-only bypass for testing without a printer
    if (IpAddressTextBox.Text.Trim().Equals("TEST", StringComparison.OrdinalIgnoreCase))
    {
        Logger.Info("Demo bypass used (TEST). Skipped ping check.");

        PrinterIP = "[SIMULATED]";
        PrinterName = string.IsNullOrWhiteSpace(PrinterNameTextBox.Text)
            ? "Demo Printer"
            : PrinterNameTextBox.Text.Trim();

        StatusText.Text = "Demo mode ✔ Skipping ping check (Debug build).";
        StatusText.Foreground = System.Windows.Media.Brushes.Green;
        NextButton.IsEnabled = true;
        return;
    }
#endif

            var ipText = (IpAddressTextBox.Text ?? "").Trim();
            var nameText = (PrinterNameTextBox.Text ?? "").Trim();

            if (!IPAddress.TryParse(ipText, out _))
            {
                StatusText.Text = "Invalid IP address format.";
                StatusText.Foreground = System.Windows.Media.Brushes.Red;
                return;
            }

            if (string.IsNullOrWhiteSpace(nameText))
            {
                nameText = "New Printer";
            }

            while (true)
            {
                try
                {
                    PingReply reply;
                    using (var ping = new Ping())
                    {
                        reply = ping.Send(ipText, 2000);
                    }

                    if (reply.Status == IPStatus.Success)
                    {
                        PrinterIP = ipText;
                        PrinterName = nameText;

                        Logger.Info($"Ping success. IP={PrinterIP}, Name={PrinterName}");

                        StatusText.Text = "Device is reachable ✔";
                        StatusText.Foreground = System.Windows.Media.Brushes.Green;
                        NextButton.IsEnabled = true;
                        return;
                    }

                    // Ping failed: ask to retry
                    var retry = MessageBox.Show(
                        "The device did not respond to ping.\n\nWould you like to try again?",
                        "Ping Failed",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (retry == MessageBoxResult.Yes)
                    {
                        continue; // loop again
                    }

                    // User chose not to retry: allow “Continue anyway”
                    var cont = MessageBox.Show(
                        "Ping failed.\n\nThis can happen if:\n" +
                        "• The printer is offline or not powered on\n" +
                        "• ICMP (ping) is blocked by the network\n" +
                        "• You are setting up a print server / GPO before the printer is online\n\n" +
                        "Do you want to continue anyway?",
                        "Continue Without Ping?",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (cont == MessageBoxResult.Yes)
                    {
                        PrinterIP = ipText;
                        PrinterName = nameText;

                        Logger.Warn($"Continuing without ping. EnteredIP={ipText}, Name={nameText}");

                        StatusText.Text = "Continuing without ping ✔ (printer may be offline or ICMP blocked).";
                        StatusText.Foreground = System.Windows.Media.Brushes.DarkOrange;
                        NextButton.IsEnabled = true;
                        return;
                    }

                    // User does not want to continue
                    StatusText.Text = "Ping failed. Please verify the IP address and network connection.";
                    StatusText.Foreground = System.Windows.Media.Brushes.Red;
                    NextButton.IsEnabled = false;
                    return;
                }
                catch (Exception ex)
                {
                    var retry = MessageBox.Show(
                        "Error testing connection:\n\n" + ex.Message + "\n\nTry again?",
                        "Ping Error",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Error);

                    if (retry == MessageBoxResult.Yes)
                    {
                        continue;
                    }

                    var cont = MessageBox.Show(
                        "Would you like to continue anyway?\n\n" +
                        "You can still proceed if you're doing an offline setup or if ping is blocked.",
                        "Continue Without Ping?",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (cont == MessageBoxResult.Yes)
                    {
                        PrinterIP = ipText;
                        PrinterName = nameText;

                        Logger.Warn($"Continuing without ping after error. EnteredIP={ipText}, Name={nameText}");

                        StatusText.Text = "Continuing without ping ✔ (connection test error).";
                        StatusText.Foreground = System.Windows.Media.Brushes.DarkOrange;
                        NextButton.IsEnabled = true;
                        return;
                    }

                    StatusText.Text = "Error testing printer connection.";
                    StatusText.Foreground = System.Windows.Media.Brushes.Red;
                    NextButton.IsEnabled = false;
                    return;
                }
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Are you sure you want to close EasyPrintServer?",
                "Confirm Exit",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question
            );

            if (result == MessageBoxResult.Yes)
            {
                // Close the main window (wizard)
                Window.GetWindow(this)?.Close();
            }
        }



        private void Next_Click(object sender, RoutedEventArgs e)
        {
            PrinterValidated?.Invoke(this, EventArgs.Empty);
        }
    }
}
