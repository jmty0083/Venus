using System;
using System.IO;
using System.Windows;

namespace Menelaus.Tian.Venus.LogViewer
{
    public partial class App : Application
    {
        private void OnStartup(object sender, StartupEventArgs eventArgs)
        {
            // Apply saved theme before any window is created
            ThemeManager.Apply(PatternStore.GetTheme());

            string? content = null;
            string? sourceLabel = null;

            // Input priority 1: stdin pipeline (e.g. "type file.log | LogViewer.exe")
            if (Console.IsInputRedirected)
            {
                content = Console.In.ReadToEnd();
                sourceLabel = "Piped Input";
            }
            // Input priority 2: file path passed as command-line argument
            else if (eventArgs.Args.Length > 0)
            {
                string path = eventArgs.Args[0];
                if (File.Exists(path))
                {
                    content = File.ReadAllText(path);
                    sourceLabel = path;
                }
                else
                {
                    MessageBox.Show($"File not found:\n{path}", "Log Viewer",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }

            var window = new MainWindow(content, sourceLabel);
            window.Show();
        }
    }
}
