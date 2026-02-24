using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace LogViewer
{
    /// <summary>
    /// Persists patterns and app settings to %APPDATA%\LogViewer\.
    /// Two separate JSON files are used so patterns and settings can be managed independently:
    ///   patterns.json — list of PatternEntry objects
    ///   settings.json — current pattern ID and theme preference
    /// </summary>
    public static class PatternStore
    {
        // Storage root: C:\Users\<user>\AppData\Roaming\LogViewer\
        private static readonly string StorageDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LogViewer");

        private static readonly string PatternsFile = Path.Combine(StorageDir, "patterns.json");
        private static readonly string SettingsFile = Path.Combine(StorageDir, "settings.json");

        private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

        // ── Settings ──────────────────────────────────────────────────────────

        private class StoredSettings
        {
            public string? CurrentPatternId { get; set; }
            public string  Theme            { get; set; } = nameof(AppTheme.System);
        }

        private static StoredSettings LoadSettings()
        {
            if (!File.Exists(SettingsFile))
            {
                return new StoredSettings();
            }

            try
            {
                return JsonSerializer.Deserialize<StoredSettings>(
                    File.ReadAllText(SettingsFile)) ?? new StoredSettings();
            }
            catch
            {
                // Corrupted or unreadable settings file — silently fall back to defaults
                return new StoredSettings();
            }
        }

        private static void SaveSettings(StoredSettings settings)
        {
            Directory.CreateDirectory(StorageDir);
            File.WriteAllText(SettingsFile, JsonSerializer.Serialize(settings, JsonOpts));
        }

        public static string? GetCurrentPatternId()
        {
            return LoadSettings().CurrentPatternId;
        }

        public static void SetCurrentPatternId(string? id)
        {
            var settings = LoadSettings();
            settings.CurrentPatternId = id;
            SaveSettings(settings);
        }

        public static AppTheme GetTheme()
        {
            var settings = LoadSettings();
            if (Enum.TryParse<AppTheme>(settings.Theme, out var theme))
            {
                return theme;
            }

            return AppTheme.System;
        }

        public static void SetTheme(AppTheme theme)
        {
            var settings = LoadSettings();
            settings.Theme = theme.ToString();
            SaveSettings(settings);
        }

        // ── Patterns ──────────────────────────────────────────────────────────

        public static List<PatternEntry> Load()
        {
            if (!File.Exists(PatternsFile))
            {
                return [];
            }

            try
            {
                return JsonSerializer.Deserialize<List<PatternEntry>>(
                    File.ReadAllText(PatternsFile)) ?? [];
            }
            catch
            {
                // Corrupted patterns file — return empty list so the app stays usable
                return [];
            }
        }

        public static void Save(List<PatternEntry> patterns)
        {
            Directory.CreateDirectory(StorageDir);
            File.WriteAllText(PatternsFile, JsonSerializer.Serialize(patterns, JsonOpts));
        }
    }
}
