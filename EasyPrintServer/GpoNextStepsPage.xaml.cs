using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace EasyPrintServer
{
    public partial class GpoNextStepsPage : UserControl
    {
        public event EventHandler Finished;

        public event EventHandler AddAnotherPrinter;

        private readonly string _scriptFolderPath;

        // Designer constructor
        public GpoNextStepsPage()
        {
            InitializeComponent();
            _scriptFolderPath = "";
        }

        // Runtime constructor (MainWindow can pass the SYSVOL folder path)
        public GpoNextStepsPage(string scriptFolderPath)
        {
            InitializeComponent();
            _scriptFolderPath = scriptFolderPath ?? "";
        }

        private void CopySteps_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText(StepsText.Text ?? "");
                MessageBox.Show("Steps copied to clipboard.", "EasyPrintServer",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to copy.\n\n" + ex.Message, "EasyPrintServer",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void AddAnotherPrinter_Click(object sender, RoutedEventArgs e)
        {
            AddAnotherPrinter?.Invoke(this, EventArgs.Empty);
        }



        private void OpenGpmc_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "gpmc.msc",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not open Group Policy Management (gpmc.msc).\n\n" + ex.Message,
                    "EasyPrintServer", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void OpenScriptFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string pathToOpen = _scriptFolderPath;

                // If a full file path was passed, open its directory
                if (!string.IsNullOrWhiteSpace(pathToOpen) && File.Exists(pathToOpen))
                    pathToOpen = Path.GetDirectoryName(pathToOpen);

                if (string.IsNullOrWhiteSpace(pathToOpen) || !Directory.Exists(pathToOpen))
                {
                    MessageBox.Show("Script folder path is empty or not found:\n\n" + (pathToOpen ?? ""),
                        "EasyPrintServer", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = pathToOpen,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not open script folder.\n\n" + ex.Message,
                    "EasyPrintServer", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void Finish_Click(object sender, RoutedEventArgs e)
        {
            Finished?.Invoke(this, EventArgs.Empty);
        }
    }
}
