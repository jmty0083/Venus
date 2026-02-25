using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;

namespace Menelaus.Tian.Venus.LogViewer
{
    public partial class PatternManagerWindow : Window
    {
        private List<PatternEntry> patterns  = [];
        private string?            currentId;  // ID of the pattern currently starred (active in main window)
        private string?            editingId;  // ID of the entry loaded in the edit form; null = new entry mode
        private DispatcherTimer?   _saveStatusTimer;

        private const string DefaultTextColumnLabel = "(last column — default)";

        public PatternManagerWindow()
        {
            InitializeComponent();
            SourceInitialized += (_, _) => ThemeManager.ApplyTitleBar(this);
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

        // ── Text column helpers ───────────────────────────────────────────────

        /// <summary>
        /// Rebuilds the Text Column ComboBox items from the named groups in the current
        /// pattern text. Tries to preserve the previously selected column name.
        /// </summary>
        private void UpdateTextColumnOptions()
        {
            string? previous = GetSelectedTextColumn();

            TextColumnBox.Items.Clear();
            TextColumnBox.Items.Add(DefaultTextColumnLabel);

            string pattern = PatternBox.Text.Trim();
            if (!string.IsNullOrEmpty(pattern))
            {
                try
                {
                    foreach (var name in LogParser.GetColumnNames(pattern))
                        TextColumnBox.Items.Add(name);
                }
                catch { /* invalid regex while typing — ignore */ }
            }

            // Restore the previous column if it still exists in the new pattern
            if (previous != null && TextColumnBox.Items.Contains(previous))
                TextColumnBox.SelectedItem = previous;
            else
                TextColumnBox.SelectedIndex = 0;
        }

        /// <summary>
        /// Returns the selected text column name, or null when the default (last column) is chosen.
        /// </summary>
        private string? GetSelectedTextColumn()
        {
            var selected = TextColumnBox.SelectedItem as string;
            return string.IsNullOrEmpty(selected) || selected == DefaultTextColumnLabel ? null : selected;
        }

        private void PatternBox_TextChanged(object sender, TextChangedEventArgs eventArgs)
        {
            UpdateTextColumnOptions();
        }

        // ── Selection ─────────────────────────────────────────────────────────

        private void PatternGrid_SelectionChanged(object sender, SelectionChangedEventArgs eventArgs)
        {
            var entry = GetSelectedEntry();
            if (entry != null)
            {
                editingId       = entry.Id;
                NameBox.Text    = entry.Name;
                PatternBox.Text = entry.Pattern;  // triggers UpdateTextColumnOptions via TextChanged
                EditLabel.Text  = $"Edit: {entry.Name}";

                // Restore the saved text column selection (UpdateTextColumnOptions already ran)
                if (!string.IsNullOrEmpty(entry.TextColumn) && TextColumnBox.Items.Contains(entry.TextColumn))
                    TextColumnBox.SelectedItem = entry.TextColumn;
                else
                    TextColumnBox.SelectedIndex = 0;
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
            PatternBox.Text          = "";  // triggers UpdateTextColumnOptions via TextChanged
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
                    existing.Name       = name;
                    existing.Pattern    = pattern;
                    existing.TextColumn = GetSelectedTextColumn();
                }
            }
            else
            {
                // Create a new entry and switch to edit mode for it
                var entry  = new PatternEntry { Name = name, Pattern = pattern, TextColumn = GetSelectedTextColumn() };
                patterns.Add(entry);
                editingId = entry.Id;
            }

            PatternStore.Save(patterns);
            Refresh();
            ShowSaveConfirmation();
        }

        private void ShowSaveConfirmation()
        {
            SaveStatus.Text = "Saved ✓";
            _saveStatusTimer?.Stop();
            _saveStatusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
            _saveStatusTimer.Tick += (_, _) =>
            {
                SaveStatus.Text = "";
                _saveStatusTimer.Stop();
            };
            _saveStatusTimer.Start();
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
