using System.Diagnostics;
using System.Text;

namespace EasyPrintServer
{
    public static class PowerShellRunner
    {
        public static (int ExitCode, string StdOut, string StdErr) Run(string script)
        {
            // Use -EncodedCommand to avoid quoting nightmares
            var bytes = Encoding.Unicode.GetBytes(script);
            var encoded = System.Convert.ToBase64String(bytes);

            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -EncodedCommand {encoded}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (var p = Process.Start(psi))
            {
                var stdout = p.StandardOutput.ReadToEnd();
                var stderr = p.StandardError.ReadToEnd();
                p.WaitForExit();

                return (p.ExitCode, stdout, stderr);
            }

        }
    }
}