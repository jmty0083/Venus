using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Menelaus.Tian.Venus.LogViewer
{
    /// <summary>
    /// ILLMParsing implementation for a generic LLM service endpoint (tab 1 in Configure AI).
    /// POSTs a JSON body with a single "data" field containing the full prompt, and expects
    /// the service to return {"pattern": "<regex>"} directly.
    /// </summary>
    public sealed class LLMParsing : ILLMParsing
    {
        private static readonly HttpClient Http = new();

        private readonly string _url;

        // Full prompt sent in the "data" field.  {0} is replaced with the sample lines.
        private const string PromptTemplate =
            "You are a log parser expert. Analyze the provided sample log lines and generate " +
            "a single .NET named-capture-group regular expression.\n" +
            "Requirements:\n" +
            "- Use .NET named group syntax: (?<name>...)\n" +
            "- The group containing the main log message or content MUST be named exactly 'text'\n" +
            "- The pattern should match as many of the sample lines as possible\n" +
            "- Keep the regex simple and practical\n" +
            "You MUST respond with ONLY valid JSON in this exact format, no explanation, no markdown:\n" +
            "{{\"pattern\": \"<regex_here>\"}}\n\n" +
            "Sample log lines:\n{0}";

        public LLMParsing(string url)
        {
            _url = url;
        }

        public async Task<string?> TryParsingAsync(IList<string> sampleLines)
        {
            string sample = string.Join('\n', sampleLines);
            string prompt = string.Format(PromptTemplate, sample);

            var requestBody = new { data = prompt };

            using var request = new HttpRequestMessage(HttpMethod.Post, _url);
            request.Content = new StringContent(
                JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

            HttpResponseMessage response;
            try
            {
                response = await Http.SendAsync(request);
                response.EnsureSuccessStatusCode();
            }
            catch
            {
                return null;
            }

            string json = await response.Content.ReadAsStringAsync();
            return ExtractPattern(json);
        }

        private static string? ExtractPattern(string responseJson)
        {
            try
            {
                // Response is expected to be {"pattern": "..."} directly
                string content = responseJson.Trim();

                // Strip markdown code fences in case the service wraps it
                if (content.StartsWith("```"))
                {
                    int start = content.IndexOf('\n') + 1;
                    int end   = content.LastIndexOf("```");
                    if (end > start) content = content[start..end].Trim();
                }

                using var doc = JsonDocument.Parse(content);
                return doc.RootElement.GetProperty("pattern").GetString();
            }
            catch
            {
                return null;
            }
        }
    }
}
