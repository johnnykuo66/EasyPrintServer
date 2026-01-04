using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace EasyPrintServer
{
    public static class InfModelExtractor
    {
        public static List<string> ExtractModelDisplayNames(string infPath)
        {
            var lines = File.ReadAllLines(infPath);

            // 1) Parse [Strings] into a dictionary: token -> display string
            var strings = ParseStringsSection(lines);

            // 2) Find manufacturer section names from [Manufacturer]
            var manufacturerSections = ParseManufacturerSections(lines);

            // 3) From each manufacturer section, collect model tokens and resolve to display names
            var models = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var sec in manufacturerSections)
            {
                foreach (var token in ExtractLeftSideTokensFromSection(lines, sec))
                {
                    // token will look like %SOMEKEY% or a quoted name. Prefer resolving %KEY%.
                    var display = ResolveToken(token, strings);
                    if (!string.IsNullOrWhiteSpace(display))
                        models.Add(display.Trim());
                }
            }

            return models.OrderBy(x => x).ToList();
        }

        private static Dictionary<string, string> ParseStringsSection(string[] lines)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var section = GetSectionLines(lines, "Strings");
            if (section.Count == 0) return dict;

            foreach (var raw in section)
            {
                var line = StripComments(raw).Trim();
                if (string.IsNullOrWhiteSpace(line)) continue;

                // Example: XeroxGPD = "Xerox Global Print Driver PCL6"
                var idx = line.IndexOf('=');
                if (idx < 1) continue;

                var key = line.Substring(0, idx).Trim();
                var val = line.Substring(idx + 1).Trim();

                // Remove surrounding quotes if present
                val = TrimQuotes(val);

                if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(val))
                    dict[key] = val;
            }

            return dict;
        }

        private static List<string> ParseManufacturerSections(string[] lines)
        {
            var section = GetSectionLines(lines, "Manufacturer");
            var result = new List<string>();

            foreach (var raw in section)
            {
                var line = StripComments(raw).Trim();
                if (string.IsNullOrWhiteSpace(line)) continue;

                // %Xerox% = Xerox,NTamd64
                var idx = line.IndexOf('=');
                if (idx < 0) continue;

                var right = line.Substring(idx + 1).Trim();
                if (string.IsNullOrWhiteSpace(right)) continue;

                var baseName = right.Split(',')[0].Trim();
                baseName = TrimQuotes(baseName);

                if (string.IsNullOrWhiteSpace(baseName))
                    continue;

                // Collect ALL matching sections:
                // Xerox
                // Xerox.NTamd64
                // Xerox.NTamd64.10.0
                for (int i = 0; i < lines.Length; i++)
                {
                    var t = lines[i].Trim();
                    if (t.StartsWith("[") && t.EndsWith("]"))
                    {
                        var sec = t.Substring(1, t.Length - 2);
                        if (sec.StartsWith(baseName + ".", StringComparison.OrdinalIgnoreCase) ||
                            sec.Equals(baseName, StringComparison.OrdinalIgnoreCase))
                        {
                            result.Add(sec);
                        }
                    }
                }
            }

            return result.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static IEnumerable<string> ExtractLeftSideTokensFromSection(string[] lines, string sectionName)
        {
            var secLines = GetSectionLines(lines, sectionName);
            foreach (var raw in secLines)
            {
                var line = StripComments(raw).Trim();
                if (string.IsNullOrWhiteSpace(line)) continue;

                var idx = line.IndexOf('=');
                if (idx < 1) continue;

                // Left side could be %TOKEN% or "Display Name"
                var left = line.Substring(0, idx).Trim();
                if (!string.IsNullOrWhiteSpace(left))
                    yield return left;
            }
        }

        private static string ResolveToken(string token, Dictionary<string, string> strings)
        {
            token = token.Trim();

            // If it's quoted already, use it
            if (token.StartsWith("\"") && token.EndsWith("\""))
                return TrimQuotes(token);

            // If it's %KEY%, resolve using [Strings]
            if (token.StartsWith("%") && token.EndsWith("%") && token.Length > 2)
            {
                var key = token.Substring(1, token.Length - 2).Trim();
                if (strings.TryGetValue(key, out var display))
                    return display;

                // Sometimes strings include the percent signs in key usage; try raw
                if (strings.TryGetValue(token, out var display2))
                    return display2;

                return null;
            }

            // Otherwise, it might already be a model name (rare)
            return token;
        }

        private static List<string> GetSectionLines(string[] lines, string sectionName)
        {
            var result = new List<string>();
            var header = $"[{sectionName}]";

            int start = -1;
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Trim().Equals(header, StringComparison.OrdinalIgnoreCase))
                {
                    start = i + 1;
                    break;
                }
            }
            if (start == -1) return result;

            for (int i = start; i < lines.Length; i++)
            {
                var t = lines[i].Trim();
                if (t.StartsWith("[") && t.EndsWith("]")) break; // next section
                result.Add(lines[i]);
            }

            return result;
        }

        private static string StripComments(string line)
        {
            // INF comments often start with ';'
            var idx = line.IndexOf(';');
            return idx >= 0 ? line.Substring(0, idx) : line;
        }

        private static string TrimQuotes(string s)
        {
            s = s.Trim();
            if (s.StartsWith("\"") && s.EndsWith("\"") && s.Length >= 2)
                return s.Substring(1, s.Length - 2);
            return s;
        }
    }
}
