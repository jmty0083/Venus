using System;

namespace Menelaus.Tian.Venus.LogViewer
{
    /// <summary>
    /// A saved parsing rule: a friendly name paired with a .NET regex that contains
    /// named capture groups. Each group becomes a column in the log grid.
    /// </summary>
    public class PatternEntry
    {
        /// <summary>Stable unique identifier used to reference this entry in settings.</summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>Human-readable label shown in the pattern manager and status bar.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>The .NET regular expression string with named capture groups.</summary>
        public string Pattern { get; set; } = string.Empty;

        /// <summary>
        /// The column that receives non-matching lines (e.g. stack-trace continuations).
        /// When null or empty, the last named capture group is used as the fallback.
        /// </summary>
        public string? TextColumn { get; set; }

        /// <summary>
        /// UTC creation timestamp. Used to determine evaluation order during auto-detection:
        /// older (more established) patterns are tried before newer ones.
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
