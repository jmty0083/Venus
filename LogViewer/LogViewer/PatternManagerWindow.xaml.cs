using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Menelaus.Tian.Venus.LogViewer
{
    public partial class PatternManagerWindow : Window
    {
        private List<PatternEntry> patterns  = [];
        private string?            currentId;  // ID of the pattern currently starred (active in main window)
        private string?            editingId;  // ID of the entry loaded in the edit form; null = new entry mode

        public PatternManagerWindow()
        {
            InitializeComponent();
            currentId = PatternStore.GetCurrentPatternId();
            Refresh();
            SetButtonStates();
        }

        // ── Grid population ───────────────────────────────────────────────────

        /// <summary>
        /// Reloads patterns from disk and rebuilds the grid.
        /// The hidden "id" column carries the entry's GUID so selection events can look up
        /// the full PatternEntry without relying on display-text matching.
        /// After a save or delete, the previously edited row is re-selected automatically.
        /// </summary>
        private void Refresh()
        {
            patterns = PatternStore.Load();

            var table = new DataTable();
            table.Columns.Add("id",      typeof(string)); // Hidden key column — not shown in grid
            table.Columns.Add("C",       typeof(string)); // Star marker for the current/active pattern
            table.Columns.Add("Name",    typeof(string));
            table.Columns.Add("Created", typeof(string));

            foreach (var entry in patterns.OrderBy(entry => entry.CreatedAt))
            {
                var row        = table.NewRow();
                row["id"]      = entry.Id;
                row["C"]       = entry.Id == currentId ? "★" : "";
                row["Name"]    = entry.Name;
                row["Created"] = entry.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
                table.Rows.Add(row);
            }

            PatternGrid.Columns.Clear();
            PatternGrid.ItemsSource = null;

            PatternGrid.Columns.Add(new DataGridTextColumn
            {
                Header         = "C",
                Binding        = new Binding("[C]"),
                SortMemberPath = "C",
                Width          = new DataGridLength(22),
                ElementStyle   = StarColumnStyle()
            });
            PatternGrid.Columns.Add(new DataGridTextColumn
            {
                Header         = "Name",
                Binding        = new Binding("[Name]"),
                SortMemberPath = "Name",
                Width          = new DataGridLength(1, DataGridLengthUnitType.Star)
            });
            PatternGrid.Columns.Add(new DataGridTextColumn
            {
                Header         = "Created",
                Binding        = new Binding("[Created]"),
                SortMemberPath = "Created",
                Width          = new DataGridLength(150)
            });

            PatternGrid.ItemsSource = table.DefaultView;

            // Re-select the row that was being edited so the user doesn't lose their place
            if (editingId != null)
            {
                foreach (DataRowView rowView in PatternGrid.Items)
                {
                    if (rowView["id"]?.ToString() == editingId)
                    {
                        PatternGrid.SelectedItem = rowView;
                        PatternGrid.ScrollIntoView(rowView);
                        break;
                    }
                }
            }
        }

        private static Style StarColumnStyle()
        {
            var style = new Style(typeof(TextBlock));
            style.Setters.Add(new Setter(TextBlock.ForegroundProperty,
                System.Windows.Media.Brushes.Gold));
            style.Setters.Add(new Setter(TextBlock.HorizontalAlignmentProperty,
                HorizontalAlignment.Center));
            return style;
        }

        // ── Selection ─────────────────────────────────────────────────────────

        private void PatternGrid_SelectionChanged(object sender, SelectionChangedEventArgs eventArgs)
        {
            var entry = GetSelectedEntry();
            if (entry != null)
            {
                editingId       = entry.Id;
                NameBox.Text    = entry.Name;
                PatternBox.Text = entry.Pattern;
                EditLabel.Text  = $"Edit: {entry.Name}";
            }
            SetButtonStates();
        }

        private PatternEntry? GetSelectedEntry()
        {
            if (PatternGrid.SelectedItem is not DataRowView selectedRowView)
            {
                return null;
            }

            string id = selectedRowView["id"]?.ToString() ?? "";
            return patterns.FirstOrDefault(entry => entry.Id == id);
        }

        private void SetButtonStates()
        {
            bool hasSelection       = GetSelectedEntry() != null;
            DeleteBtn.IsEnabled     = hasSelection;
            SetCurrentBtn.IsEnabled = hasSelection;
        }

        // ── Buttons ───────────────────────────────────────────────────────────

        private void New_Click(object sender, RoutedEventArgs eventArgs)
        {
            editingId               = null;
            NameBox.Text             = "";
            PatternBox.Text          = "";
            EditLabel.Text           = "New Pattern";
            PatternGrid.SelectedItem = null;
            NameBox.Focus();
        }

        private void Save_Click(object sender, RoutedEventArgs eventArgs)
        {
            string name    = NameBox.Text.Trim();
            string pattern = PatternBox.Text.Trim();

            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show("Name cannot be empty.", "Validation",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrEmpty(pattern))
            {
                MessageBox.Show("Pattern cannot be empty.", "Validation",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Validate the regex before saving so the user gets immediate feedback
            try
            {
                _ = new Regex(pattern);
            }
            catch (RegexParseException parseException)
            {
                MessageBox.Show($"Invalid regular expression:\n{parseException.Message}", "Validation",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (editingId != null)
            {
                // Update the existing entry in-place (preserves CreatedAt and Id)
                var existing = patterns.FirstOrDefault(patternEntry => patternEntry.Id == editingId);
                if (existing != null)
                {
                    existing.Name    = name;
                    existing.Pattern = pattern;
                }
            }
            else
            {
                // Create a new entry and switch to edit mode for it
                var entry  = new PatternEntry { Name = name, Pattern = pattern };
                patterns.Add(entry);
                editingId = entry.Id;
            }

            PatternStore.Save(patterns);
            Refresh();
        }

        private void Delete_Click(object sender, RoutedEventArgs eventArgs)
        {
            var entry = GetSelectedEntry();
            if (entry == null)
            {
                return;
            }

            if (MessageBox.Show($"Delete pattern '{entry.Name}'?", "Confirm Delete",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                return;
            }

            patterns.Remove(entry);

            if (currentId == entry.Id)
            {
                currentId = null;
                PatternStore.SetCurrentPatternId(null);
            }

            PatternStore.Save(patterns);
            editingId       = null;
            NameBox.Text    = "";
            PatternBox.Text = "";
            EditLabel.Text  = "New Pattern";
            Refresh();
            SetButtonStates();
        }

        private void SetCurrent_Click(object sender, RoutedEventArgs eventArgs)
        {
            var entry = GetSelectedEntry();
            if (entry == null)
            {
                return;
            }

            currentId = entry.Id;
            PatternStore.SetCurrentPatternId(entry.Id);
            Refresh();
        }

        private void Close_Click(object sender, RoutedEventArgs eventArgs)
        {
            Close();
        }
    }
}
