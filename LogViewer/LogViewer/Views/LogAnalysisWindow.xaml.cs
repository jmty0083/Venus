using System.Windows;

namespace Menelaus.Tian.Venus.LogViewer
{
    public partial class LogAnalysisWindow : Window
    {
        public LogAnalysisWindow()
        {
            InitializeComponent();
            SourceInitialized += (_, _) => ThemeManager.ApplyTitleBar(this);
        }

        public void SetLoading() => ResultBox.Text = "Analyzing log content…";

        public void SetResult(string text) => ResultBox.Text = text;
    }
}
