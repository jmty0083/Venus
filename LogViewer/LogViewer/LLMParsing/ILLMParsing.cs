using System.Collections.Generic;
using System.Threading.Tasks;

namespace Menelaus.Tian.Venus.LogViewer
{
    /// <summary>
    /// Contract for LLM-based log pattern inference.
    /// Implementations receive a sample of log lines and return a .NET named-group regex,
    /// or null if the model could not produce a usable pattern.
    /// The capture group containing the main message must be named "text".
    /// </summary>
    public interface ILLMParsing
    {
        /// <summary>
        /// Asks the LLM to infer a .NET named-capture-group regex from the sample lines.
        /// Returns the regex string on success, or null on failure / refusal.
        /// </summary>
        Task<string?> TryParsingAsync(IList<string> sampleLines);
    }
}
