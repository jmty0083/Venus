using System.Windows;

namespace Menelaus.Tian.Venus.LogViewer
{
    public partial class AiSetupDialog : Window
    {
        public AiSetupDialog()
        {
            InitializeComponent();
            SourceInitialized += (_, _) => ThemeManager.ApplyTitleBar(this);

            // Pre-populate all fields from saved config
            var existing = AiConfig.Load();
            if (existing != null)
            {
                TabCtrl.SelectedIndex  = existing.ActiveTab;
                EndpointBox.Text       = existing.Endpoint;
                ApiKeyBox.Password     = existing.ApiKey;
                ModelBox.Text          = existing.Model;
                LlmUrlBox.Text         = existing.LlmUrl;
                CommandBox.Text        = existing.Command;
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            int tab = TabCtrl.SelectedIndex;

            // Validate required fields for the active tab only
            bool valid = tab switch
            {
                0 => !string.IsNullOrWhiteSpace(LlmUrlBox.Text),
                1 => !string.IsNullOrWhiteSpace(EndpointBox.Text)
                  && !string.IsNullOrWhiteSpace(ApiKeyBox.Password)
                  && !string.IsNullOrWhiteSpace(ModelBox.Text),
                2 => !string.IsNullOrWhiteSpace(CommandBox.Text),
                _ => false
            };

            if (!valid)
            {
                MessageBox.Show("Please fill in all required fields for the selected tab.", "Configure AI",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            AiConfig.Save(new AiSettings
            {
                ActiveTab = tab,
                Endpoint  = EndpointBox.Text.Trim(),
                ApiKey    = ApiKeyBox.Password.Trim(),
                Model     = ModelBox.Text.Trim(),
                LlmUrl    = LlmUrlBox.Text.Trim(),
                Command   = CommandBox.Text.Trim()
            });

            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
