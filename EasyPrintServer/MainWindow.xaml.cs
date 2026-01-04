using System;
using System.Windows;

namespace EasyPrintServer
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            LoadWelcomePage();
        }

        private void LoadWelcomePage()
        {
            var welcomePage = new WelcomePage();
            welcomePage.StartClicked += (s, e) =>
            {
                LoadPrinterSetupPage();
            };

            WizardContent.Content = welcomePage;
        }

        private void LoadPrinterSetupPage()
        {
            var printerPage = new PrinterSetupPage();
            printerPage.PrinterValidated += (s, e) =>
            {
                var pp = (PrinterSetupPage)s;
                LoadDriverAndSharePage(pp.PrinterIP, pp.PrinterName);
            };

            WizardContent.Content = printerPage;
        }

        private void LoadCompletionPage(
            string printerName,
            string printerIp,
            bool simulated,
            bool shared,
            string shareName)
        {
            var page = new CompletionPage(printerName, printerIp, simulated, shared, shareName);

            page.Finished += (s, e) =>
            {
                Close();
            };

            WizardContent.Content = page;
        }

        private void LoadDriverAndSharePage(string printerIp, string printerName)
        {
            var page = new DriverAndSharePage(printerIp, printerName);

            page.BackRequested += (s, e) => LoadPrinterSetupPage();

            page.InstallCompleted += (s, e) =>
            {
                // If GPO deployment was selected and succeeded, show Next Steps screen
                if (page.LastDeployGpo && !page.IsSimulated && !string.IsNullOrWhiteSpace(page.LastGpoName))
                {
                    // NOTE:
                    // page.LastScriptPath should ideally be the SYSVOL\...\User\Scripts\Logon folder
                    // OR the full path to EasyPrintServer_AddPrinters.ps1.
                    // Either is fine for guiding the user.
                    LoadGpoNextStepsPage(
                        page.LastGpoName,
                        page.LastOuDn,
                        page.LastScriptPath,
                        page.LastDeployUsers
                    );
                    return;
                }

                // Otherwise go to normal completion page
                LoadCompletionPage(
                    printerName,
                    printerIp,
                    simulated: page.IsSimulated,
                    shared: page.IsShared,
                    shareName: page.ShareName
                );
            };

            WizardContent.Content = page;
        }

        private void LoadGpoNextStepsPage(string gpoName, string ouDn, string scriptPath, bool deployUsers)
        {
            // Make the instructions clean + consistent
            string modeText = deployUsers ? "Users (Logon)" : "Computers (Startup)";

            string stepsText =
                "Almost done!\n\n" +
                "In Group Policy Management:\n" +
                $"1) Right-click the GPO and click Edit: {gpoName}\n" +
                $"2) Confirm it's linked to the OU: {ouDn}\n\n" +
                "Now add the PowerShell script:\n" +
                "User Configuration → Policies → Windows Settings → Scripts (Logon/Logoff)\n" +
                "Double-click Logon → PowerShell Scripts tab\n" +
                "Click Add → Browse\n" +
                "Select EasyPrintServer_AddPrinters.ps1\n" +
                "Leave Parameters blank → OK → Apply\n\n" +
                "If Browse opens the wrong folder, use this path:\n" +
                scriptPath + "\n\n" +
                "Tip: After this, run gpupdate /force on the workstation and sign out/in.";

            // ✅ Pass deployUsers so your Next Steps page can show Mode correctly
            var nextPage = new GpoNextStepsPage(scriptPath);

            nextPage.AddAnotherPrinter += (s, e) =>
            {
                LoadPrinterSetupPage(); // goes back to IP input screen
            };

            nextPage.Finished += (s, e) =>
            {
                LoadCompletionPage(
                    printerName: "(GPO Deployment)",
                    printerIp: "",
                    simulated: false,
                    shared: true,
                    shareName: ""
                );
            };

            WizardContent.Content = nextPage;
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                "EasyPrintServer setup has started.\n\nNext step: printer configuration.",
                "EasyPrintServer",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
        }
    }
}
