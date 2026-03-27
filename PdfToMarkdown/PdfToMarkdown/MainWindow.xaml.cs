using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PdfToMarkdown.Models;
using PdfToMarkdown.Services;
using UglyToad.PdfPig;
using WpfBrushes     = System.Windows.Media.Brushes;
using WpfMsgBox      = System.Windows.MessageBox;
using WpfMsgBoxBtn   = System.Windows.MessageBoxButton;
using WpfMsgBoxImg   = System.Windows.MessageBoxImage;
using WpfMsgBoxResult= System.Windows.MessageBoxResult;
using WinOpenFileDlg = Microsoft.Win32.OpenFileDialog;

namespace PdfToMarkdown;

public partial class MainWindow : Window
{
    private string              _pdfPath      = "";
    private string              _outputFolder = "";
    private List<PdfPageItem>   _pages        = new();

    public MainWindow() => InitializeComponent();

    // ---------------------------------------------------------------
    // Drag & drop
    // ---------------------------------------------------------------

    private void Window_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop);
            if (files.Any(IsPdf))
            {
                e.Effects = System.Windows.DragDropEffects.Copy;
                e.Handled = true;
                return;
            }
        }
        e.Effects = System.Windows.DragDropEffects.None;
        e.Handled = true;
    }

    private void Window_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop)) return;
        var files = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop);
        var pdf = files.FirstOrDefault(IsPdf);
        if (pdf != null) LoadPdf(pdf);
    }

    // ---------------------------------------------------------------
    // Open
    // ---------------------------------------------------------------

    private void DropZone_Click(object sender, MouseButtonEventArgs e)
    {
        var dlg = new WinOpenFileDlg
        {
            Title  = "Open PDF File",
            Filter = "PDF Files (*.pdf)|*.pdf|All Files|*.*"
        };
        if (dlg.ShowDialog() == true) LoadPdf(dlg.FileName);
    }

    // ---------------------------------------------------------------
    // Load PDF — enumerate pages
    // ---------------------------------------------------------------

    private async void LoadPdf(string path)
    {
        _pdfPath = path;
        TxtFilePath.Text       = path;
        TxtFilePath.Foreground = WpfBrushes.DimGray;
        SetStatus("Loading...");

        CollapseWork();

        try
        {
            _pages = await Task.Run(() =>
            {
                var items = new List<PdfPageItem>();
                using var doc = PdfDocument.Open(path);
                for (int pn = 1; pn <= doc.NumberOfPages; pn++)
                {
                    var page = doc.GetPage(pn);
                    items.Add(new PdfPageItem
                    {
                        PageNumber = pn,
                        WordCount  = page.GetWords().Count(),
                        ImageCount = page.GetImages().Count()
                    });
                }
                return items;
            });

            LstPages.ItemsSource = _pages;
            UpdatePageSummary();
            CardPages.Visibility   = Visibility.Visible;
            CardOutput.Visibility  = Visibility.Visible;
            CardConvert.Visibility = Visibility.Visible;
            SetStatus($"Loaded: {Path.GetFileName(path)}  —  {_pages.Count} page(s)");
        }
        catch (Exception ex)
        {
            SetStatus("Error loading PDF.");
            WpfMsgBox.Show($"Could not open the PDF:\n\n{ex.Message}",
                           "Error", WpfMsgBoxBtn.OK, WpfMsgBoxImg.Error);
        }
    }

    // ---------------------------------------------------------------
    // Page selection
    // ---------------------------------------------------------------

    private void BtnSelectAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var p in _pages) p.IsSelected = true;
        UpdatePageSummary();
        RefreshConvert();
    }

    private void BtnSelectNone_Click(object sender, RoutedEventArgs e)
    {
        foreach (var p in _pages) p.IsSelected = false;
        UpdatePageSummary();
        RefreshConvert();
    }

    private void PageCheck_Changed(object sender, RoutedEventArgs e)
    {
        UpdatePageSummary();
        RefreshConvert();
    }

    private void UpdatePageSummary()
    {
        int sel = _pages.Count(p => p.IsSelected);
        TxtPageSummary.Text = $"{sel} of {_pages.Count} page(s) selected";
    }

    // ---------------------------------------------------------------
    // Output folder
    // ---------------------------------------------------------------

    private void BtnBrowse_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new System.Windows.Forms.FolderBrowserDialog
        {
            Description         = "Select output folder for Markdown file and images",
            ShowNewFolderButton = true,
            SelectedPath        = _outputFolder
        };
        if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            _outputFolder              = dlg.SelectedPath;
            TxtOutputFolder.Text       = _outputFolder;
            TxtOutputFolder.Foreground = WpfBrushes.Black;
            RefreshConvert();
        }
    }

    // ---------------------------------------------------------------
    // Convert
    // ---------------------------------------------------------------

    private async void BtnConvert_Click(object sender, RoutedEventArgs e)
    {
        var selectedPages = _pages.Where(p => p.IsSelected).Select(p => p.PageNumber).ToList();
        if (selectedPages.Count == 0 || string.IsNullOrEmpty(_outputFolder)) return;

        BtnConvert.IsEnabled = false;
        PBar.Value           = 0;
        PBar.Visibility      = Visibility.Visible;
        TxtStatus.Text       = "";
        SetStatus("Converting...");

        try
        {
            var pdfPath      = _pdfPath;
            var outputFolder = _outputFolder;
            var outputName   = MakeSafeFileName(Path.GetFileNameWithoutExtension(pdfPath));

            bool perPage  = ChkPerPage.IsChecked == true;

            var converter = new PdfConverter();
            converter.OnPageProgress = (cur, total) =>
                Dispatcher.InvokeAsync(() => PBar.Value = (double)cur / total * 100);

            await Task.Run(() => converter.Convert(pdfPath, selectedPages, outputFolder, outputName, perPage));

            var mdFile = perPage
                ? Path.Combine(outputFolder, $"{outputName}_page_{selectedPages.Min()}.md")
                : Path.Combine(outputFolder, outputName + ".md");
            TxtStatus.Text = perPage
                ? $"Created {selectedPages.Count} file(s) in: {Path.GetFileName(outputFolder)}"
                : $"Created: {Path.GetFileName(mdFile)}";
            TxtStatus.Foreground = WpfBrushes.SeaGreen;
            SetStatus("Done.");

            var ans = WpfMsgBox.Show(
                $"Markdown file created!\n\n{mdFile}\n\nOpen output folder?",
                "Done", WpfMsgBoxBtn.YesNo, WpfMsgBoxImg.Information);

            if (ans == WpfMsgBoxResult.Yes)
                System.Diagnostics.Process.Start("explorer.exe", outputFolder);
        }
        catch (Exception ex)
        {
            TxtStatus.Text       = $"Error: {ex.Message}";
            TxtStatus.Foreground = WpfBrushes.Crimson;
            SetStatus("Conversion failed.");
            WpfMsgBox.Show($"Conversion failed:\n\n{ex.Message}",
                           "Error", WpfMsgBoxBtn.OK, WpfMsgBoxImg.Error);
        }
        finally
        {
            PBar.Visibility      = Visibility.Collapsed;
            BtnConvert.IsEnabled = CanConvert();
        }
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private void RefreshConvert() =>
        BtnConvert.IsEnabled = CanConvert();

    private bool CanConvert() =>
        _pages.Any(p => p.IsSelected) &&
        !string.IsNullOrEmpty(_outputFolder);

    private void CollapseWork()
    {
        CardPages.Visibility   = Visibility.Collapsed;
        CardOutput.Visibility  = Visibility.Collapsed;
        CardConvert.Visibility = Visibility.Collapsed;
        BtnConvert.IsEnabled   = false;
        TxtStatus.Text         = "";
        _pages.Clear();
    }

    private void SetStatus(string msg) => TxtStatusBar.Text = msg;

    private static bool IsPdf(string path) =>
        path.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);

    private static string MakeSafeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars().ToHashSet();
        return new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim('.');
    }
}
