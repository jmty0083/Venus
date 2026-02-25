using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using Microsoft.Win32;

namespace Menelaus.Tian.Venus.LogViewer
{
    public partial class MainWindow : Window
    {
        private string? currentPatternId = PatternStore.GetCurrentPatternId();
        private string? loadedText;
        private string loadedLabel = "";
        private int searchCurrentIndex = -1;
        private string searchLastQuery = "";

        public MainWindow(string? initialContent = null, string? sourceLabel = null)
        {
            InitializeComponent();
            SourceInitialized += (_, _) => ThemeManager.ApplyTitleBar(this);

            if (initialContent != null)
            {
                LoadContent(initialContent, sourceLabel ?? "Unknown source");
            }
        }

        // ── Auto-detection ────────────────────────────────────────────────────

        /// <summary>
        /// Selects the best matching pattern for <paramref name="text"/> and parses it.
        /// Detection order:
        ///   1. The current (last-used) pattern — avoids re-testing when reloading the same log.
        ///   2. All saved patterns ordered by creation date (oldest first, i.e. most established).
        ///   3. Raw fallback — one "line" column, no structured parsing.
        /// A pattern must match at least 60 % of sampled lines to be considered a hit.
        /// </summary>
        private (PatternEntry? entry, DataTable table) AutoDetectAndParse(string text)
        {
            // At least 60 % of sampled lines must match for a pattern to be accepted
            const double Threshold = 0.6;
            var patterns = PatternStore.Load();

            if (patterns.Count == 0)
            {
                return (null, LogParser.ParseRaw(text));
            }

            // 1. Try the current pattern first — fast path for repeated loads of the same log type
            if (currentPatternId != null)
            {
                var current = patterns.FirstOrDefault(entry => entry.Id == currentPatternId);
                if (current != null && LogParser.TestPattern(text, current.Pattern) >= Threshold)
                {
                    return (current, LogParser.Parse(text, current.Pattern, current.TextColumn));
                }
            }

            // 2. Try all patterns ordered by creation time (older = more battle-tested)
            foreach (var entry in patterns.OrderBy(entry => entry.CreatedAt))
            {
                if (LogParser.TestPattern(text, entry.Pattern) >= Threshold)
                {
                    // Persist the newly chosen pattern so it becomes the fast-path next time
                    currentPatternId = entry.Id;
                    PatternStore.SetCurrentPatternId(entry.Id);
                    return (entry, LogParser.Parse(text, entry.Pattern, entry.TextColumn));
                }
            }

            // 3. Nothing matched — raw fallback (single "line" column)
            return (null, LogParser.ParseRaw(text));
        }

        // ── Content loading ───────────────────────────────────────────────────

        private void LoadContent(string text, string sourceLabel)
        {
            loadedText = text;
            loadedLabel = sourceLabel;
            searchCurrentIndex = -1;
            searchLastQuery = "";
            SearchStatus.Text = "";

            var (entry, table) = AutoDetectAndParse(text);

            if (entry != null)
            {
                currentPatternId = entry.Id;
                PatternStore.SetCurrentPatternId(entry.Id);
            }

            BindTable(table);
            Title = $"Log Viewer — {sourceLabel}";

            int total = table.Rows.Count;
            string patternInfo = entry != null
                ? $"Pattern: {entry.Name}"
                : "No pattern matched (raw lines)";

            // Count user-visible columns (skip internal _ columns)
            int userColumnCount = 0;
            string firstColumnName = "";
            foreach (DataColumn dataColumn in table.Columns)
            {
                if (!dataColumn.ColumnName.StartsWith("_"))
                {
                    if (firstColumnName == "")
                    {
                        firstColumnName = dataColumn.ColumnName;
                    }
                    userColumnCount++;
                }
            }

            if (entry != null && userColumnCount > 1)
            {
                int matched = 0;
                foreach (DataRow row in table.Rows)
                {
                    if (!string.IsNullOrEmpty(row[firstColumnName]?.ToString()))
                    {
                        matched++;
                    }
                }
                StatusText.Text =
                    $"{sourceLabel}  |  {patternInfo}  |  {total:N0} rows  ({matched:N0} parsed, {total - matched:N0} continuation)";
            }
            else
            {
                StatusText.Text = $"{sourceLabel}  |  {patternInfo}  |  {total:N0} rows";
            }
        }

        private void BindTable(DataTable table)
        {
            LogGrid.Columns.Clear();
            LogGrid.ItemsSource = null;

            // Collect user-visible column names, skipping internal "_" columns (e.g. _raw)
            var columnNames = new List<string>();
            foreach (DataColumn dataColumn in table.Columns)
            {
                if (!dataColumn.ColumnName.StartsWith("_"))
                {
                    columnNames.Add(dataColumn.ColumnName);
                }
            }

            foreach (var name in columnNames)
            {
                // DataView row indexer requires bracket syntax: [ColumnName]
                var column = new DataGridTextColumn
                {
                    Header = name,
                    Binding = new Binding($"[{name}]"),
                    SortMemberPath = name,
                    Width = DataGridLength.Auto,
                    ElementStyle = new Style(typeof(TextBlock))
                    {
                        Setters = { new Setter(TextBlock.PaddingProperty, new Thickness(4, 2, 4, 2)) }
                    }
                };
                LogGrid.Columns.Add(column);
            }

            LogGrid.ItemsSource = table.DefaultView;
            // Rebuild the column-visibility context menu to match the new column set
            AttachColumnContextMenu();
            // Ensure the last visible column always fills remaining horizontal space
            UpdateColumnWidths();
        }

        // ── Column visibility context menu ────────────────────────────────────

        private void AttachColumnContextMenu()
        {
            // One shared ContextMenu for all column headers; rebuilt on every BindTable call
            // so it resets automatically when the pattern changes.
            var menu = new ContextMenu();
            menu.SetResourceReference(ContextMenu.BackgroundProperty, "SurfaceBg");
            menu.SetResourceReference(ContextMenu.BorderBrushProperty, "BorderColor");

            foreach (var column in LogGrid.Columns)
            {
                var item = new MenuItem
                {
                    Header = column.Header?.ToString() ?? "",
                    IsCheckable = true,
                    IsChecked = true
                };
                item.SetResourceReference(MenuItem.BackgroundProperty, "SurfaceBg");
                item.SetResourceReference(MenuItem.ForegroundProperty, "PrimaryFg");

                var capturedColumn = column;
                item.Checked += (eventSender, eventArgs) =>
                {
                    capturedColumn.Visibility = Visibility.Visible;
                    UpdateColumnWidths();
                };
                item.Unchecked += (eventSender, eventArgs) =>
                {
                    capturedColumn.Visibility = Visibility.Collapsed;
                    UpdateColumnWidths();
                };

                menu.Items.Add(item);
            }

            // Apply to every column header via per-column HeaderStyle
            foreach (var column in LogGrid.Columns)
            {
                var baseStyle = Application.Current.TryFindResource(typeof(DataGridColumnHeader)) as Style;
                var style = new Style(typeof(DataGridColumnHeader), baseStyle);
                style.Setters.Add(new Setter(FrameworkElement.ContextMenuProperty, menu));
                column.HeaderStyle = style;
            }
        }

        // ── Column widths ─────────────────────────────────────────────────────

        /// <summary>
        /// Ensures the last visible column stretches to fill any remaining horizontal space
        /// so the grid never has an empty gap on the right side.
        ///
        /// Only two columns are touched per call: the column that previously had Width="*"
        /// (reset to Auto) and the new last visible column (promoted to Width="*").
        /// User-resized pixel-width columns in the middle are intentionally left alone.
        /// </summary>
        private void UpdateColumnWidths()
        {
            DataGridColumn? lastVisible = null;
            DataGridColumn? currentStar = null;

            foreach (DataGridColumn column in LogGrid.Columns)
            {
                if (column.Width.IsStar)
                {
                    currentStar = column;
                }
                if (column.Visibility == Visibility.Visible)
                {
                    lastVisible = column;
                }
            }

            if (lastVisible == null)
            {
                return; // All columns hidden — nothing to do
            }

            if (currentStar == lastVisible)
            {
                return; // Already correct — avoid unnecessary layout invalidation
            }

            if (currentStar != null)
            {
                currentStar.Width = DataGridLength.Auto;
            }

            lastVisible.Width = new DataGridLength(1, DataGridLengthUnitType.Star);
        }

        // ── Row detail panel ──────────────────────────────────────────────────

        private void LogGrid_SelectionChanged(object sender, SelectionChangedEventArgs eventArgs)
        {
            if (LogGrid.SelectedItem is DataRowView rowView)
            {
                DetailPanel.Text = rowView["_raw"]?.ToString() ?? "";
            }
            else
            {
                DetailPanel.Text = "";
            }
        }

        // ── Search ────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns the grid row indices (in display order) that contain <paramref name="query"/>
        /// in any currently visible column. The search is case-insensitive plain-text only.
        /// </summary>
        private List<int> GetSearchMatches(string query)
        {
            var result = new List<int>();
            string lowerCaseQuery = query.ToLowerInvariant();

            // Collect visible column names upfront to avoid re-checking on every row
            var visibleNames = new List<string>();
            foreach (DataGridColumn column in LogGrid.Columns)
            {
                if (column.Visibility == Visibility.Visible)
                {
                    string name = column.Header?.ToString() ?? "";
                    if (!string.IsNullOrEmpty(name))
                    {
                        visibleNames.Add(name);
                    }
                }
            }

            int rowIndex = 0;
            foreach (object gridItem in LogGrid.Items)
            {
                if (gridItem is DataRowView rowView)
                {
                    // A row matches if any visible cell contains the query; stop at first hit
                    foreach (string name in visibleNames)
                    {
                        string cellValue = rowView[name]?.ToString() ?? "";
                        if (cellValue.ToLowerInvariant().Contains(lowerCaseQuery))
                        {
                            result.Add(rowIndex);
                            break;
                        }
                    }
                }
                rowIndex++;
            }

            return result;
        }

        /// <summary>
        /// Navigates to the next or previous search match relative to the current position.
        /// Wraps around at either end of the match list.
        /// </summary>
        private void PerformSearch(bool forward)
        {
            string query = SearchBox.Text.Trim();
            if (string.IsNullOrEmpty(query))
            {
                return;
            }

            // Reset position when the query changes so navigation starts fresh
            if (query != searchLastQuery)
            {
                searchCurrentIndex = -1;
                searchLastQuery = query;
            }

            List<int> matches = GetSearchMatches(query);

            if (matches.Count == 0)
            {
                SearchStatus.Text = "No results";
                return;
            }

            int targetRowIndex;
            if (forward)
            {
                // Find first match after the current position; wrap to start if none found
                targetRowIndex = -1;
                foreach (int matchIndex in matches)
                {
                    if (matchIndex > searchCurrentIndex)
                    {
                        targetRowIndex = matchIndex;
                        break;
                    }
                }
                if (targetRowIndex == -1)
                {
                    targetRowIndex = matches[0]; // Wrap around to the first match
                }
            }
            else
            {
                // Find last match before the current position; wrap to end if none found
                targetRowIndex = -1;
                for (int reverseIndex = matches.Count - 1; reverseIndex >= 0; reverseIndex--)
                {
                    if (matches[reverseIndex] < searchCurrentIndex)
                    {
                        targetRowIndex = matches[reverseIndex];
                        break;
                    }
                }
                if (targetRowIndex == -1)
                {
                    targetRowIndex = matches[matches.Count - 1]; // Wrap around to the last match
                }
            }

            searchCurrentIndex = targetRowIndex;
            int matchPosition = matches.IndexOf(targetRowIndex) + 1;
            LogGrid.SelectedIndex = targetRowIndex;
            LogGrid.ScrollIntoView(LogGrid.Items[targetRowIndex]);
            SearchStatus.Text = $"{matchPosition} / {matches.Count}";
        }

        private void SearchNext_Click(object sender, RoutedEventArgs eventArgs)
        {
            PerformSearch(true);
        }

        private void SearchPrev_Click(object sender, RoutedEventArgs eventArgs)
        {
            PerformSearch(false);
        }

        private void SearchBox_KeyDown(object sender, KeyEventArgs eventArgs)
        {
            if (eventArgs.Key == Key.Enter)
            {
                PerformSearch(true);
                eventArgs.Handled = true;
            }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs eventArgs)
        {
            SearchStatus.Text = "";
        }

        // ── Menu handlers ─────────────────────────────────────────────────────

        private void OpenFile_Click(object sender, RoutedEventArgs eventArgs)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Open Log File",
                Filter = "Log files (*.log;*.txt)|*.log;*.txt|All files (*.*)|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                LoadContent(File.ReadAllText(dialog.FileName), dialog.FileName);
            }
        }

        private void PasteClipboard_Click(object sender, RoutedEventArgs eventArgs)
        {
            string text = Clipboard.GetText();
            if (string.IsNullOrEmpty(text))
            {
                MessageBox.Show("Clipboard is empty or does not contain text.", "Log Viewer",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            LoadContent(text, "Clipboard");
        }

        private void ManagePatterns_Click(object sender, RoutedEventArgs eventArgs)
        {
            new PatternManagerWindow { Owner = this }.ShowDialog();

            // Pick up any current-pattern change made in the manager
            currentPatternId = PatternStore.GetCurrentPatternId();

            // Re-parse the currently loaded content with the (possibly new) pattern set
            if (loadedText != null)
            {
                LoadContent(loadedText, loadedLabel);
            }
        }

        private void ThemeDark_Click(object sender, RoutedEventArgs eventArgs)
        {
            ThemeManager.Apply(AppTheme.Dark);
            PatternStore.SetTheme(AppTheme.Dark);
        }

        private void ThemeLight_Click(object sender, RoutedEventArgs eventArgs)
        {
            ThemeManager.Apply(AppTheme.Light);
            PatternStore.SetTheme(AppTheme.Light);
        }

        private void ThemeSystem_Click(object sender, RoutedEventArgs eventArgs)
        {
            ThemeManager.Apply(AppTheme.System);
            PatternStore.SetTheme(AppTheme.System);
        }

        private void Exit_Click(object sender, RoutedEventArgs eventArgs)
        {
            Close();
        }
    }
}
