using System;
using System.ClientModel;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using OpenAI.Chat;

namespace Menelaus.Tian.Venus.LogViewer
{
    /// <summary>
    /// ILLMParsing implementation that uses the official OpenAI .NET SDK
    /// to call a chat completions endpoint.
    /// </summary>
    public sealed class OpenAIParsing : ILLMParsing
    {
        private readonly string _endpoint;
        private readonly string _apiKey;
        private readonly string _model;

        private const string SystemPrompt =
            "You are a log parser expert. Analyze the provided sample log lines and generate " +
            "a single .NET named-capture-group regular expression that parses them.\n" +
            "Requirements:\n" +
            "- Use .NET named group syntax: (?<name>...)\n" +
            "- The group containing the main log message or content MUST be named exactly 'text'\n" +
            "- The pattern should match as many of the sample lines as possible\n" +
            "- Keep the regex simple and practical\n" +
            "Respond ONLY with valid JSON in this exact format, no explanation, no markdown:\n" +
            "{\"pattern\": \"<regex_here>\"}";

        public OpenAIParsing(string endpoint, string apiKey, string model)
        {
            _endpoint = endpoint;
            _apiKey   = apiKey;
            _model    = model;
        }

        public async Task<string?> TryParsingAsync(IList<string> sampleLines)
        {
            string sample = string.Join('\n', sampleLines);

            var clientOptions = new OpenAI.OpenAIClientOptions
            {
                Endpoint = new Uri(_endpoint.TrimEnd('/'))
            };

            var client = new ChatClient(
                model:      _model,
                credential: new ApiKeyCredential(_apiKey),
                options:    clientOptions);

            var chatOptions = new ChatCompletionOptions
            {
                Temperature        = 0.1f,
                MaxOutputTokenCount = 400
            };

            ChatCompletion completion;
            try
            {
                completion = await client.CompleteChatAsync(
                    [
                        new SystemChatMessage(SystemPrompt),
                        new UserChatMessage($"Sample log lines:\n{sample}")
                    ],
                    chatOptions);
            }
            catch
            {
                return null;
            }

            string? content = completion.Content[0].Text;
            return ExtractPattern(content);
        }

        private static string? ExtractPattern(string? content)
        {
            if (string.IsNullOrWhiteSpace(content)) return null;

            // Strip markdown code fences the model might have added
            content = content.Trim();
            if (content.StartsWith("```"))
            {
                int start = content.IndexOf('\n') + 1;
                int end   = content.LastIndexOf("```");
                if (end > start) content = content[start..end].Trim();
            }

            try
            {
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
