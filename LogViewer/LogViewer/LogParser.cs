using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.RegularExpressions;

namespace LogViewer
{
    public static class LogParser
    {
        /// <summary>
        /// Extracts named capture group names from a regex pattern, in order.
        /// </summary>
        public static List<string> GetColumnNames(string pattern)
        {
            var regex = new Regex(pattern);
            return regex.GetGroupNames()
                        .Where(groupName => !int.TryParse(groupName, out _))
                        .ToList();
        }

        /// <summary>
        /// Tests what fraction of non-empty sample lines match the pattern (0.0–1.0).
        /// Returns 0 if the pattern is an invalid regex.
        /// </summary>
        public static double TestPattern(string text, string pattern, int sampleSize = 50)
        {
            Regex regex;
            try
            {
                regex = new Regex(pattern, RegexOptions.Compiled);
            }
            catch
            {
                return 0.0;
            }

            var lines = text.Split('\n')
                            .Select(line => line.TrimEnd('\r'))
                            .Where(line => !string.IsNullOrWhiteSpace(line))
                            .Take(sampleSize)
                            .ToList();

            if (lines.Count == 0)
            {
                return 0.0;
            }

            return (double)lines.Count(line => regex.IsMatch(line)) / lines.Count;
        }

        /// <summary>
        /// Parses every line against <paramref name="pattern"/>.
        /// Matching lines fill named-group columns; non-matching lines put the raw text
        /// in the "text" column (or the last named column if none is named "text").
        /// Empty/whitespace lines are skipped.
        /// </summary>
        public static DataTable Parse(string text, string pattern)
        {
            var regex = new Regex(pattern, RegexOptions.Compiled);
            var groupNames = GetColumnNames(pattern);

            // Prefer a group named "text" as the fallback column for non-matching lines;
            // if none exists, use the last named group so continuation lines remain visible.
            string textColumn = groupNames.Contains("text") ? "text"
                              : groupNames.Count > 0 ? groupNames[^1]
                              : "text";

            var table = new DataTable();
            // _raw stores the verbatim original line so the detail panel can show it
            // without reconstructing it from individual group values.
            // The underscore prefix marks it as an internal column — BindTable skips it.
            table.Columns.Add("_raw", typeof(string));

            if (groupNames.Count == 0)
            {
                // Pattern has no named groups — treat the whole line as "text"
                table.Columns.Add("text", typeof(string));
                groupNames = ["text"];
                textColumn = "text";
            }
            else
            {
                foreach (var name in groupNames)
                {
                    table.Columns.Add(name, typeof(string));
                }
            }

            foreach (var rawLine in text.Split('\n'))
            {
                var line = rawLine.TrimEnd('\r');
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                var row = table.NewRow();
                row["_raw"] = line;
                var match = regex.Match(line);

                if (match.Success)
                {
                    // Matched line: populate each named column from its capture group
                    foreach (var name in groupNames)
                    {
                        row[name] = match.Groups[name].Value;
                    }
                }
                else
                {
                    // Non-matching line (e.g. a stack-trace continuation): store the raw
                    // text in the fallback column so it still appears in the grid.
                    row[textColumn] = line;
                }

                table.Rows.Add(row);
            }

            return table;
        }

        /// <summary>
        /// Fallback parse when no pattern matches: single column "line" with raw text.
        /// </summary>
        public static DataTable ParseRaw(string text)
        {
            var table = new DataTable();
            table.Columns.Add("_raw", typeof(string));
            table.Columns.Add("line", typeof(string));

            foreach (var rawLine in text.Split('\n'))
            {
                var line = rawLine.TrimEnd('\r');
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                var row = table.NewRow();
                row["_raw"] = line;
                row["line"] = line;
                table.Rows.Add(row);
            }

            return table;
        }
    }
}
