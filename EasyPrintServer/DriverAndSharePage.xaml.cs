using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;

using WinForms = System.Windows.Forms;

namespace EasyPrintServer
{
    public class OuItem
    {
        public string Name { get; set; }
        public string DistinguishedName { get; set; }

        public override string ToString()
        {
            return Name + "  (" + DistinguishedName + ")";
        }
    }

    public partial class DriverAndSharePage : UserControl
    {
        public event EventHandler BackRequested;
        public event EventHandler InstallCompleted;

        private readonly string _printerIp;
        private readonly string _printerName;

        // NOTE: these read UI controls, so DO NOT use them from background threads.
        public bool IsSimulated => SimulateCheckBox?.IsChecked == true;
        public bool IsShared => ShareCheckBox?.IsChecked == true;
        public string ShareName => (ShareNameTextBox?.Text ?? "").Trim();

        // ✅ Exposed so MainWindow can decide to show the Next Steps page
        public bool LastDeployGpo { get; private set; }
        public bool LastDeployUsers { get; private set; }
        public string LastOuDn { get; private set; }
        public string LastGpoName { get; private set; }
        public string LastScriptPath { get; private set; }

        public DriverAndSharePage(string printerIp, string printerName)
        {
            InitializeComponent();

            _printerIp = printerIp;
            _printerName = printerName;

            TargetDeviceText.Text = $"Target: {_printerName} ({_printerIp})";

            try
            {
                LoadInstalledDrivers();
            }
            catch (Exception ex)
            {
                Logger.Error("Error loading drivers: " + ex);
                StatusText.Text = "Failed to load installed printer drivers.";
                StatusText.Foreground = Brushes.Red;
            }

            // Nice default for share name
            if (string.IsNullOrWhiteSpace(ShareNameTextBox.Text))
                ShareNameTextBox.Text = MakeSafeShareName(_printerName);

            // Default GPO name autofill (updates when share name changes)
            if (GpoNameTextBox != null && string.IsNullOrWhiteSpace(GpoNameTextBox.Text))
                GpoNameTextBox.Text = "EasyPrintServer - Deploy - " + MakeSafeShareName(ShareNameTextBox.Text);

            if (ShareNameTextBox != null && GpoNameTextBox != null)
            {
                ShareNameTextBox.TextChanged += (s, e) =>
                {
                    if (string.IsNullOrWhiteSpace(GpoNameTextBox.Text) ||
                        GpoNameTextBox.Text.StartsWith("EasyPrintServer - Deploy - ", StringComparison.OrdinalIgnoreCase))
                    {
                        GpoNameTextBox.Text = "EasyPrintServer - Deploy - " + MakeSafeShareName(ShareNameTextBox.Text);
                    }
                };
            }

            // Load OUs for dropdown if possible
            try
            {
                LoadOUsIfPossible();
            }
            catch (Exception ex)
            {
                Logger.Warn("OU dropdown load failed (non-fatal): " + ex);
            }
        }

        private void LoadInstalledDrivers()
        {
            DriverComboBox.Items.Clear();

            // Use PowerShell to list installed drivers
            var ps = "Get-PrinterDriver | Select-Object -ExpandProperty Name";

            var result = PowerShellRunner.Run(ps);

            if (result.ExitCode != 0 || string.IsNullOrWhiteSpace(result.StdOut))
                throw new Exception("Failed to read installed drivers: " + (result.StdErr ?? result.StdOut));

            var drivers = result.StdOut
                .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct()
                .OrderBy(x => x)
                .ToList();

            foreach (var d in drivers)
                DriverComboBox.Items.Add(d);

            if (DriverComboBox.Items.Count > 0)
                DriverComboBox.SelectedIndex = 0;
        }

        private void InstallInf_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dlg = new OpenFileDialog
                {
                    Title = "Select .INF driver file",
                    Filter = "INF files (*.inf)|*.inf|All files (*.*)|*.*"
                };

                if (dlg.ShowDialog() != true) return;

                var infPath = dlg.FileName;

                InfStatusText.Text = "Installing driver from INF...";
                InfStatusText.Foreground = Brushes.DodgerBlue;

                var ps =
                    "$inf = '" + EscapePs(infPath) + "'\n" +
                    "pnputil /add-driver $inf /install | Out-Null\n" +
                    "Start-Sleep -Seconds 1\n" +
                    "'OK'\n";

                var result = PowerShellRunner.Run(ps);

                if (result.ExitCode == 0)
                {
                    InfStatusText.Text = "Driver install completed ✔ (refreshing list)";
                    InfStatusText.Foreground = Brushes.Green;
                    LoadInstalledDrivers();
                }
                else
                {
                    InfStatusText.Text = "Driver install failed: " + (result.StdErr ?? result.StdOut);
                    InfStatusText.Foreground = Brushes.Red;
                }
            }
            catch (Exception ex)
            {
                Logger.Error("InstallInf_Click failed: " + ex);
                InfStatusText.Text = "Driver install failed (see logs).";
                InfStatusText.Foreground = Brushes.Red;
            }
        }

        private void InstallFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using (var dlg = new WinForms.FolderBrowserDialog())
                {
                    dlg.Description = "Select driver folder (must contain .INF files)";
                    dlg.ShowNewFolderButton = false;

                    if (dlg.ShowDialog() != WinForms.DialogResult.OK) return;

                    var folder = dlg.SelectedPath;

                    InfStatusText.Text = "Installing driver(s) from folder...";
                    InfStatusText.Foreground = Brushes.DodgerBlue;

                    var ps =
                        "$folder = '" + EscapePs(folder) + "'\n" +
                        "$infs = Get-ChildItem -Path $folder -Recurse -Filter *.inf -ErrorAction SilentlyContinue\n" +
                        "foreach ($i in $infs) { try { pnputil /add-driver $i.FullName /install | Out-Null } catch { } }\n" +
                        "Start-Sleep -Seconds 1\n" +
                        "'OK'\n";

                    var result = PowerShellRunner.Run(ps);

                    if (result.ExitCode == 0)
                    {
                        InfStatusText.Text = "Folder driver install completed ✔ (refreshing list)";
                        InfStatusText.Foreground = Brushes.Green;
                        LoadInstalledDrivers();
                    }
                    else
                    {
                        InfStatusText.Text = "Folder driver install failed: " + (result.StdErr ?? result.StdOut);
                        InfStatusText.Foreground = Brushes.Red;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("InstallFolder_Click failed: " + ex);
                InfStatusText.Text = "Driver install failed (see logs).";
                InfStatusText.Foreground = Brushes.Red;
            }
        }

        // IMPORTANT: keep this handler name exactly as XAML expects
        private void InstallHaveDiskStyle_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Have Disk style install is not implemented in this build.", "EasyPrintServer",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void Install_Click(object sender, RoutedEventArgs e)
        {
            // Clear last deployment info
            LastDeployGpo = false;
            LastDeployUsers = false;
            LastOuDn = null;
            LastGpoName = null;
            LastScriptPath = null;

            StatusText.Text = "Installing printer...";
            StatusText.Foreground = Brushes.Black;

            string driver = (DriverComboBox?.SelectedItem as string ?? "").Trim();
            string shareNameRaw = (ShareNameTextBox?.Text ?? "").Trim();

            bool isSimulated = (SimulateCheckBox?.IsChecked == true);
            bool isShared = (ShareCheckBox?.IsChecked == true);

            bool deployGpo = (DeployGpoCheckBox?.IsChecked == true);
            bool deployUsers = (DeployTargetComboBox != null && DeployTargetComboBox.SelectedIndex == 0);

            string ouDn = (OuComboBox?.SelectedItem as OuItem)?.DistinguishedName
                          ?? (OuComboBox?.Text ?? "").Trim();

            string gpoNameRaw = (GpoNameTextBox?.Text ?? "").Trim();
            bool setDefaultUi = (DeployAsDefaultCheckBox?.IsChecked == true);

            if (string.IsNullOrWhiteSpace(driver))
            {
                StatusText.Text = "Please select a driver first.";
                StatusText.Foreground = Brushes.Red;
                return;
            }

            if (isShared && string.IsNullOrWhiteSpace(shareNameRaw))
            {
                StatusText.Text = "Share name is required if sharing is enabled.";
                StatusText.Foreground = Brushes.Red;
                return;
            }

            string safeShare = MakeSafeShareName(shareNameRaw);
            if (!string.Equals(shareNameRaw, safeShare, StringComparison.Ordinal))
                ShareNameTextBox.Text = safeShare;

            if (string.IsNullOrWhiteSpace(gpoNameRaw))
            {
                gpoNameRaw = "EasyPrintServer - Deploy - " + MakeSafeShareName(safeShare);
                if (GpoNameTextBox != null) GpoNameTextBox.Text = gpoNameRaw;
            }

            string printerName = safeShare;
            string portName = "IP_" + _printerIp;

            string serverName = Environment.MachineName;
            string sharePath = "\\\\" + serverName + "\\" + safeShare;
            bool setDefault = deployUsers && setDefaultUi;

            SetUiEnabled(false);

            try
            {
                // Install printer/share
                await Task.Run(() =>
                {
                    if (isSimulated)
                    {
                        Logger.Info($"SIMULATED install: Printer={printerName}, Port={portName}, Driver={driver}, Share={isShared}");
                        return;
                    }

                    var ps =
                        "$ErrorActionPreference = 'Stop'\n" +
                        "$printerName = '" + EscapePs(printerName) + "'\n" +
                        "$portName = '" + EscapePs(portName) + "'\n" +
                        "$ip = '" + EscapePs(_printerIp) + "'\n" +
                        "$driver = '" + EscapePs(driver) + "'\n" +
                        "$doShare = " + (isShared ? "$true" : "$false") + "\n" +
                        "$shareName = '" + EscapePs(safeShare) + "'\n" +
                        "\n" +
                        "if (-not (Get-PrinterPort -Name $portName -ErrorAction SilentlyContinue)) {\n" +
                        "  Add-PrinterPort -Name $portName -PrinterHostAddress $ip\n" +
                        "}\n" +
                        "\n" +
                        "if (-not (Get-Printer -Name $printerName -ErrorAction SilentlyContinue)) {\n" +
                        "  Add-Printer -Name $printerName -DriverName $driver -PortName $portName\n" +
                        "}\n" +
                        "\n" +
                        "if ($doShare) {\n" +
                        "  Set-Printer -Name $printerName -Shared $true -ShareName $shareName\n" +
                        "}\n" +
                        "\n" +
                        "'OK'\n";

                    var result = PowerShellRunner.Run(ps);

                    if (result.ExitCode != 0 || (result.StdOut ?? "").IndexOf("OK", StringComparison.OrdinalIgnoreCase) < 0)
                        throw new Exception(string.IsNullOrWhiteSpace(result.StdErr) ? result.StdOut : result.StdErr);
                }).ConfigureAwait(true);

                await Dispatcher.InvokeAsync(() =>
                {
                    StatusText.Text = isSimulated ? "Simulated install completed ✔" : "Printer installed successfully ✔";
                    StatusText.Foreground = Brushes.Green;
                });

                // GPO deploy (create/link + drop script only)
                if (!isSimulated && deployGpo)
                {
                    if (string.IsNullOrWhiteSpace(ouDn))
                        throw new InvalidOperationException("Link to OU DN is required for GPO deployment.");

                    string gpoDeployError = null;
                    GpoDeployInfo info = null;

                    await Task.Run(() =>
                    {
                        try
                        {
                            info = DeployPrinterViaGpo(
                                sharePath: sharePath,
                                deployUsers: deployUsers,
                                ouDn: ouDn,
                                gpoName: gpoNameRaw,
                                serverName: serverName,
                                setDefault: setDefault
                            );
                        }
                        catch (Exception ex)
                        {
                            gpoDeployError = ex.Message;
                        }
                    }).ConfigureAwait(true);

                    if (!string.IsNullOrWhiteSpace(gpoDeployError))
                        throw new Exception("Printer install succeeded, but GPO deployment failed:\n\n" + gpoDeployError);

                    // ✅ Store for MainWindow to show a full Next Steps page (no popups)
                    LastDeployGpo = true;
                    LastDeployUsers = deployUsers;
                    LastOuDn = ouDn;
                    LastGpoName = gpoNameRaw;
                    LastScriptPath = info?.ScriptPath;

                    await Dispatcher.InvokeAsync(() =>
                    {
                        StatusText.Text += "\nGPO created + linked ✔ (" + (deployUsers ? "Users/Logon" : "Computers/Startup") + ")";
                        StatusText.Text += "\nNext: follow the Next Steps screen to add the script manually.";
                        StatusText.Foreground = Brushes.Green;
                    });
                }

                await Dispatcher.InvokeAsync(() =>
                {
                    InstallCompleted?.Invoke(this, EventArgs.Empty);
                });
            }
            catch (Exception ex)
            {
                Logger.Error("Install_Click failed: " + ex);

                await Dispatcher.InvokeAsync(() =>
                {
                    StatusText.Text = "Install failed. This usually means the driver isn't installed, the IP/port is wrong, or Windows blocked the install.";
                    StatusText.Foreground = Brushes.Red;

                    MessageBox.Show(
                        ex.Message,
                        "EasyPrintServer",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                });
            }
            finally
            {
                await Dispatcher.InvokeAsync(() => SetUiEnabled(true));
            }
        }

        private void SetUiEnabled(bool enabled)
        {
            try
            {
                if (SimulateCheckBox != null) SimulateCheckBox.IsEnabled = enabled;
                if (ShareCheckBox != null) ShareCheckBox.IsEnabled = enabled;
                if (DriverComboBox != null) DriverComboBox.IsEnabled = enabled;
                if (ShareNameTextBox != null) ShareNameTextBox.IsEnabled = enabled;

                if (DeployGpoCheckBox != null) DeployGpoCheckBox.IsEnabled = enabled;
                if (DeployTargetComboBox != null) DeployTargetComboBox.IsEnabled = enabled;
                if (OuComboBox != null) OuComboBox.IsEnabled = enabled;
                if (GpoNameTextBox != null) GpoNameTextBox.IsEnabled = enabled;
                if (DeployAsDefaultCheckBox != null) DeployAsDefaultCheckBox.IsEnabled = enabled;
            }
            catch { }
        }

        private void LoadOUsIfPossible()
        {
            try
            {
                if (OuComboBox == null) return;

                var ps =
                    "try {\n" +
                    "  Import-Module ActiveDirectory -ErrorAction Stop\n" +
                    "  Get-ADOrganizationalUnit -Filter * |\n" +
                    "    Select-Object Name, DistinguishedName |\n" +
                    "    ForEach-Object { \"$( $_.Name )|$( $_.DistinguishedName )\" }\n" +
                    "} catch { }\n";

                var result = PowerShellRunner.Run(ps);
                if (result.ExitCode != 0 || string.IsNullOrWhiteSpace(result.StdOut))
                    return;

                var lines = result.StdOut.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                var items = new List<OuItem>();

                foreach (var line in lines)
                {
                    var parts = line.Split('|');
                    if (parts.Length != 2) continue;

                    var name = parts[0].Trim();
                    var dn = parts[1].Trim();
                    if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(dn)) continue;

                    items.Add(new OuItem { Name = name, DistinguishedName = dn });
                }

                if (items.Count == 0) return;

                OuComboBox.ItemsSource = items;
                OuComboBox.SelectedIndex = 0;
            }
            catch
            {
                // ignore
            }
        }

        private static string EscapePs(string s) => (s ?? "").Replace("'", "''");

        private class GpoDeployInfo
        {
            public string GpoGuid { get; set; }
            public string PolicyRoot { get; set; }
            public string ScriptPath { get; set; }
        }

        private GpoDeployInfo DeployPrinterViaGpo(string sharePath, bool deployUsers, string ouDn, string gpoName, string serverName, bool setDefault)
        {
            if (string.IsNullOrWhiteSpace(ouDn))
                throw new InvalidOperationException("Link to OU DN is required for GPO deployment.");
            if (string.IsNullOrWhiteSpace(gpoName))
                throw new InvalidOperationException("GPO name is required for GPO deployment.");
            if (string.IsNullOrWhiteSpace(serverName))
                throw new InvalidOperationException("Server name is required for GPO deployment.");

            string ps =
                "$ErrorActionPreference = 'Stop'\n" +
                "Import-Module GroupPolicy -ErrorAction Stop\n" +
                "\n" +
                "$domain = $env:USERDNSDOMAIN\n" +
                "if ([string]::IsNullOrWhiteSpace($domain)) {\n" +
                "  try { $domain = ([System.DirectoryServices.ActiveDirectory.Domain]::GetCurrentDomain()).Name } catch { }\n" +
                "}\n" +
                "if ([string]::IsNullOrWhiteSpace($domain)) { throw 'Could not determine domain name.' }\n" +
                "\n" +
                "$gpoName   = '" + EscapePs(gpoName) + "'\n" +
                "$ouDn      = '" + EscapePs(ouDn) + "'\n" +
                "$server    = '" + EscapePs(serverName) + "'\n" +
                "$sharePath = '" + EscapePs(sharePath) + "'\n" +
                "$deployUsers = " + (deployUsers ? "$true" : "$false") + "\n" +
                "$setDefault  = " + (setDefault ? "$true" : "$false") + "\n" +
                "\n" +
                "$gpo = Get-GPO -Name $gpoName -ErrorAction SilentlyContinue\n" +
                "if (-not $gpo) { $gpo = New-GPO -Name $gpoName }\n" +
                "try { New-GPLink -Name $gpoName -Target $ouDn -LinkEnabled Yes -ErrorAction Stop | Out-Null } catch { }\n" +
                "try { Set-GPPermission -Name $gpoName -TargetName 'Authenticated Users' -TargetType Group -PermissionLevel GpoApply | Out-Null } catch { }\n" +
                "\n" +
                "$serverFqdn = \"$server.$domain\"\n" +
                "$servers = \"$serverFqdn;$server\"\n" +
                "$ppKey  = 'HKLM\\Software\\Policies\\Microsoft\\Windows NT\\Printers\\PointAndPrint'\n" +
                "$pppKey = 'HKLM\\Software\\Policies\\Microsoft\\Windows NT\\Printers\\PackagePointAndPrint'\n" +
                "Set-GPRegistryValue -Name $gpoName -Key $ppKey  -ValueName 'RestrictDriverInstallationToAdministrators' -Type DWord -Value 0\n" +
                "Set-GPRegistryValue -Name $gpoName -Key $ppKey  -ValueName 'TrustedServers' -Type DWord -Value 1\n" +
                "Set-GPRegistryValue -Name $gpoName -Key $ppKey  -ValueName 'ServerList' -Type String -Value $servers\n" +
                "Set-GPRegistryValue -Name $gpoName -Key $ppKey  -ValueName 'NoWarningNoElevationOnInstall' -Type DWord -Value 1\n" +
                "Set-GPRegistryValue -Name $gpoName -Key $ppKey  -ValueName 'NoWarningNoElevationOnUpdate'  -Type DWord -Value 1\n" +
                "Set-GPRegistryValue -Name $gpoName -Key $pppKey -ValueName 'TrustedServers' -Type DWord -Value 1\n" +
                "Set-GPRegistryValue -Name $gpoName -Key $pppKey -ValueName 'ServerList' -Type String -Value $servers\n" +
                "\n" +
                "if ($deployUsers) {\n" +
                "  Set-GPRegistryValue -Name $gpoName -Key 'HKCU\\Software\\EasyPrintServer' -ValueName 'UserConfigInitialized' -Type String -Value '1'\n" +
                "}\n" +
                "\n" +
                "$guid = (Get-GPO -Name $gpoName).Id.Guid\n" +
                "$policyRoot = \"\\\\$domain\\SYSVOL\\$domain\\Policies\\{$guid}\"\n" +
                "if (-not (Test-Path $policyRoot)) { throw \"Policy folder not found: $policyRoot\" }\n" +
                "\n" +
                "if ($deployUsers) {\n" +
                "  $scriptDir = Join-Path $policyRoot 'User\\Scripts\\Logon'\n" +
                "} else {\n" +
                "  $scriptDir = Join-Path $policyRoot 'Machine\\Scripts\\Startup'\n" +
                "}\n" +
                "New-Item -ItemType Directory -Force -Path $scriptDir | Out-Null\n" +
                "\n" +
                "$ps1Name = 'EasyPrintServer_AddPrinters.ps1'\n" +
                "$ps1Path = Join-Path $scriptDir $ps1Name\n" +
                "\n" +
                "$template = @'\n" +
                "\n" +
                "$server = \"__SERVER__\"\n" +
                "\n" +
                "# get all shared printer names from the print server\n" +
                "$shared = Get-Printer -ComputerName $server | Where-Object { $_.Shared -eq $true }\n" +
                "\n" +
                "foreach ($p in $shared) {\n" +
                "    $cn = \"\\\\$server\\\\$($p.ShareName)\"\n" +
                "    try { Add-Printer -ConnectionName $cn -ErrorAction Stop } catch { }\n" +
                "}\n" +
                "'@\n" +
                "\n" +
                "$scriptText = $template.Replace('__SERVER__', $server)\n" +
                "Set-Content -Path $ps1Path -Value $scriptText -Encoding UTF8\n" +
                "\"OK|$guid|$policyRoot|$ps1Path\"\n";

            var result = PowerShellRunner.Run(ps);

            var stdout = (result.StdOut ?? "").Trim();
            if (result.ExitCode == 0 && stdout.Contains("OK|"))
            {
                var line = stdout.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                                 .LastOrDefault(x => x.StartsWith("OK|", StringComparison.OrdinalIgnoreCase))
                                 ?? stdout;

                var parts = line.Split('|');
                var info = new GpoDeployInfo();
                if (parts.Length >= 4)
                {
                    info.GpoGuid = parts[1];
                    info.PolicyRoot = parts[2];
                    info.ScriptPath = parts[3];
                }
                Logger.Info("GPO created/linked and script dropped: " + gpoName);
                return info;
            }
            else
            {
                var detail = string.IsNullOrWhiteSpace(result.StdErr) ? result.StdOut : result.StdErr;
                throw new Exception((detail ?? "").Trim());
            }
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            BackRequested?.Invoke(this, EventArgs.Empty);
        }

        private static string MakeSafeShareName(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return "Printer";

            var safe = new string(input
                .Trim()
                .Select(c =>
                {
                    if (char.IsLetterOrDigit(c)) return c;
                    if (c == '_' || c == '-') return c;
                    return '_';
                }).ToArray());

            while (safe.Contains("__"))
                safe = safe.Replace("__", "_");

            safe = safe.Trim('_');

            if (safe.Length == 0)
                safe = "Printer";

            if (safe.Length > 60)
                safe = safe.Substring(0, 60);

            return safe;
        }
    }
}
