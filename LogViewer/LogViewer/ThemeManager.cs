using System;
using System.Windows;
using Microsoft.Win32;

namespace LogViewer
{
    public enum AppTheme
    {
        Dark,
        Light,
        System
    }

    public static class ThemeManager
    {
        /// <summary>
        /// Switches the application to the given theme by swapping the active
        /// ResourceDictionary under Application.Resources.MergedDictionaries.
        /// If <paramref name="theme"/> is <see cref="AppTheme.System"/>, the current
        /// Windows light/dark preference is detected first.
        /// </summary>
        public static void Apply(AppTheme theme)
        {
            // Resolve "System" to a concrete Dark or Light value before loading resources
            AppTheme resolved = theme == AppTheme.System ? DetectSystemTheme() : theme;
            string path = resolved == AppTheme.Light ? "Themes/Light.xaml" : "Themes/Dark.xaml";

            var themeUri         = new Uri($"pack://application:,,,/{path}", UriKind.Absolute);
            var themeDictionary  = new ResourceDictionary { Source = themeUri };

            var mergedDictionaries = Application.Current.Resources.MergedDictionaries;

            // Remove any existing theme dictionary before adding the new one.
            // Iterate backwards to safely remove while iterating.
            for (int index = mergedDictionaries.Count - 1; index >= 0; index--)
            {
                if (mergedDictionaries[index].Source?.OriginalString.Contains("/Themes/") == true)
                {
                    mergedDictionaries.RemoveAt(index);
                }
            }

            // Adding the new dictionary triggers DynamicResource re-evaluation
            // across all live windows, so the UI updates immediately.
            mergedDictionaries.Add(themeDictionary);
        }

        /// <summary>
        /// Reads the Windows registry to determine whether the user prefers light or dark mode.
        /// Falls back to Dark if the registry key is unavailable.
        /// </summary>
        private static AppTheme DetectSystemTheme()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                var registryValue = key?.GetValue("AppsUseLightTheme");
                // AppsUseLightTheme = 1 means light mode; 0 (or missing) means dark
                return registryValue is int lightThemeValue && lightThemeValue == 1 ? AppTheme.Light : AppTheme.Dark;
            }
            catch
            {
                return AppTheme.Dark;
            }
        }
    }
}
