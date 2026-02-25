using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Win32;

namespace Menelaus.Tian.Venus.LogViewer
{
    public enum AppTheme
    {
        Dark,
        Light,
        System
    }

    public static class ThemeManager
    {
        private static bool _isDark = true;

        /// <summary>
        /// Switches the application to the given theme by swapping the active
        /// ResourceDictionary under Application.Resources.MergedDictionaries.
        /// If <paramref name="theme"/> is <see cref="AppTheme.System"/>, the current
        /// Windows light/dark preference is detected first.
        /// Also updates the Win32 title bar colour for all open windows.
        /// </summary>
        public static void Apply(AppTheme theme)
        {
            // Resolve "System" to a concrete Dark or Light value before loading resources
            AppTheme resolved = theme == AppTheme.System ? DetectSystemTheme() : theme;
            _isDark = resolved != AppTheme.Light;

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

            // Repaint the Win32 title bar for every window that already has an HWND
            foreach (Window window in Application.Current.Windows)
                ApplyTitleBar(window);
        }

        /// <summary>
        /// Tells the Desktop Window Manager to draw this window's title bar in the
        /// current dark/light mode.  Must be called after the HWND is created
        /// (i.e. from the <c>SourceInitialized</c> event or later).
        /// </summary>
        public static void ApplyTitleBar(Window window)
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero) return;   // HWND not yet created — skip

            int value = _isDark ? 1 : 0;
            NativeMethods.DwmSetWindowAttribute(hwnd, 20 /* DWMWA_USE_IMMERSIVE_DARK_MODE */, ref value, sizeof(int));
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

        private static class NativeMethods
        {
            [DllImport("dwmapi.dll")]
            public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
        }
    }
}
