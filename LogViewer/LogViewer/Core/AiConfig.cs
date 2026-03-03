using System;
using System.IO;
using System.Text.Json;

namespace Menelaus.Tian.Venus.LogViewer
{
    /// <summary>
    /// Persists AI connection settings across all three configuration modes to
    /// %APPDATA%\LogViewer\openai.json.
    /// </summary>
    public sealed class AiSettings
    {
        public int    ActiveTab { get; set; }   // 0 = LLM Service, 1 = Azure OpenAI Key, 2 = Script plugin
        // Tab 0 — LLM Service
        public string LlmUrl    { get; set; } = "";
        // Tab 1 — Azure OpenAI Key
        public string Endpoint  { get; set; } = "";
        public string ApiKey    { get; set; } = "";
        public string Model     { get; set; } = "";
        // Tab 2 — Script-based plugin
        public string Command   { get; set; } = "";

        /// <summary>Label shown in the main-window toolbar button.</summary>
        public string ButtonLabel => ActiveTab switch
        {
            0 when !string.IsNullOrWhiteSpace(LlmUrl)  => "LLM Service",
            1 when !string.IsNullOrWhiteSpace(Model)   => Model,
            2 when !string.IsNullOrWhiteSpace(Command)  => "Plugin",
            _                                           => "Configure AI"
        };
    }

    public static class AiConfig
    {
        private static readonly string StorageDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LogViewer");

        private static readonly string ConfigFile = Path.Combine(StorageDir, "openai.json");

        private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

        /// <summary>Returns saved settings, or null if the file is absent or invalid.</summary>
        public static AiSettings? Load()
        {
            if (!File.Exists(ConfigFile)) return null;
            try   { return JsonSerializer.Deserialize<AiSettings>(File.ReadAllText(ConfigFile)); }
            catch { return null; }
        }

        public static void Save(AiSettings settings)
        {
            Directory.CreateDirectory(StorageDir);
            File.WriteAllText(ConfigFile, JsonSerializer.Serialize(settings, JsonOpts));
        }
    }
}
