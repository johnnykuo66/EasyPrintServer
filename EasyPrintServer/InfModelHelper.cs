using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace EasyPrintServer
{
    public static class InfModelHelper
    {
        // This is a "best-effort" parser:
        // Many printer INFs include model names under [Manufacturer] and related sections.
        // We'll extract the quoted display names from lines like:
        // "HP Universal Printing PCL 6" = ...
        public static List<string> GetModelNames(string infPath)
        {
            try
            {
                var lines = File.ReadAllLines(infPath);

                // Pull anything that looks like:  "Model Name" = something
                var regex = new Regex("^\\s*\"(?<name>[^\"]+)\"\\s*=", RegexOptions.Compiled);

                var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var line in lines)
                {
                    var m = regex.Match(line);
                    if (m.Success)
                    {
                        var name = m.Groups["name"].Value.Trim();
                        if (!string.IsNullOrWhiteSpace(name))
                            names.Add(name);
                    }
                }

                // Return sorted list
                return names.OrderBy(x => x).ToList();
            }
            catch
            {
                return new List<string>();
            }
        }
    }
}
