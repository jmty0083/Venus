using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;

namespace PdfToMarkdown.Services;

/// <summary>
/// Converts selected pages of a PDF to a Markdown file.
/// Images are saved to a sub-folder alongside the .md file.
/// Structure (headings, paragraphs, lists) is inferred from font
/// size, position and text patterns — no OCR is performed.
/// </summary>
public sealed class PdfConverter
{
    // ── per-conversion state ──────────────────────────────────────
    private string _assetsDir     = "";
    private string _assetsDirName = "";
    private int    _imgCounter    = 0;

    // ── progress callback ─────────────────────────────────────────
    public Action<int, int>? OnPageProgress;   // (current, total)

    // =============================================================
    // Entry point
    // =============================================================

    /// <param name="perPage">
    /// When <c>true</c>, each page is written to its own file:
    /// <c>{outputFileName}_page_{N}.md</c> with a matching assets folder.
    /// When <c>false</c>, all pages are combined into one file.
    /// </param>
    public void Convert(
        string      pdfPath,
        IList<int>  pageNumbers,   // 1-based
        string      outputDir,
        string      outputFileName,
        bool        perPage = false)
    {
        _imgCounter = 0;
        Directory.CreateDirectory(outputDir);

        using var doc = PdfDocument.Open(pdfPath);

        // ── First pass: compute body font size across all selected pages ──
        var allSizes = new List<double>();
        foreach (var pn in pageNumbers)
        {
            var pg = doc.GetPage(pn);
            foreach (var w in pg.GetWords())
                foreach (var l in w.Letters)
                    if (l.PointSize > 1) allSizes.Add(l.PointSize);
        }
        double bodySize = allSizes.Count > 0 ? Mode(allSizes) : 10.0;

        var sorted   = pageNumbers.OrderBy(p => p).ToList();
        int progress = 0;

        if (perPage)
        {
            // ── One file per page ────────────────────────────────────
            foreach (var pn in sorted)
            {
                OnPageProgress?.Invoke(++progress, sorted.Count);

                var pageFileName  = $"{outputFileName}_page_{pn}";
                _assetsDirName    = pageFileName + "_assets";
                _assetsDir        = Path.Combine(outputDir, _assetsDirName);

                var sb   = new StringBuilder();
                var page = doc.GetPage(pn);
                ConvertPage(page, sb, bodySize);

                var mdPath = Path.Combine(outputDir, pageFileName + ".md");
                File.WriteAllText(mdPath, sb.ToString().TrimEnd() + "\n",
                                  new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            }
        }
        else
        {
            // ── All pages into one file ──────────────────────────────
            _assetsDirName = outputFileName + "_assets";
            _assetsDir     = Path.Combine(outputDir, _assetsDirName);

            var sb = new StringBuilder();

            foreach (var pn in sorted)
            {
                OnPageProgress?.Invoke(++progress, sorted.Count);

                if (pn != sorted[0]) sb.AppendLine("\n\n---\n");

                var page = doc.GetPage(pn);
                ConvertPage(page, sb, bodySize);
            }

            var mdPath = Path.Combine(outputDir, outputFileName + ".md");
            File.WriteAllText(mdPath, sb.ToString().TrimEnd() + "\n",
                              new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }
    }

    // =============================================================
    // Page conversion
    // =============================================================

    private void ConvertPage(Page page, StringBuilder sb, double bodySize)
    {
        var words  = page.GetWords().ToList();
        var images = page.GetImages().ToList();

        // Build sorted content blocks (text lines + image placeholders)
        var blocks = new List<ContentBlock>();

        // ── text lines ──
        foreach (var line in GroupIntoLines(words))
            blocks.Add(new ContentBlock { Y = line.BaselineY, Line = line });

        // ── images ──
        foreach (var img in images)
        {
            double y = img.Bounds.Bottom + img.Bounds.Height / 2.0;
            blocks.Add(new ContentBlock { Y = y, Image = img });
        }

        // Top-to-bottom (PDF Y=0 is bottom, so descending = top first)
        blocks = blocks.OrderByDescending(b => b.Y).ToList();

        // Track previous line for gap-based paragraph detection
        double? prevBottom = null;
        double  lineHeight = EstimateLineHeight(words, bodySize);

        foreach (var block in blocks)
        {
            if (block.Image != null)
            {
                sb.AppendLine();
                sb.AppendLine(SaveImage(block.Image));
                sb.AppendLine();
                prevBottom = null;
                continue;
            }

            var line = block.Line!;
            if (string.IsNullOrWhiteSpace(line.Text)) continue;

            // Insert blank line for paragraph breaks
            if (prevBottom.HasValue)
            {
                double gap = prevBottom.Value - line.TopY;
                if (gap > lineHeight * 0.8)
                    sb.AppendLine();
            }

            sb.AppendLine(RenderLine(line, bodySize));
            prevBottom = line.BaselineY;
        }
    }

    // =============================================================
    // Line → Markdown
    // =============================================================

    private static string RenderLine(TextLine line, double bodySize)
    {
        double size  = line.AvgFontSize;
        string text  = line.Text.Trim();
        bool   bold  = line.IsBold;
        bool   italic = line.IsItalic;

        // ── heading detection ────────────────────────────────────
        if (size >= bodySize * 1.9) return $"# {text}";
        if (size >= bodySize * 1.5) return $"## {text}";
        if (size >= bodySize * 1.25) return $"### {text}";
        if (size >= bodySize * 1.12 || bold && size >= bodySize * 1.05)
            return $"#### {text}";

        // ── bullet list ──────────────────────────────────────────
        var bulletMatch = Regex.Match(text, @"^([•·‣▸▶➤\-–—*])\s+(.+)$");
        if (bulletMatch.Success)
            return $"- {bulletMatch.Groups[2].Value.Trim()}";

        // ── numbered list ────────────────────────────────────────
        var numMatch = Regex.Match(text, @"^(\d+)[.)]\s+(.+)$");
        if (numMatch.Success)
            return $"{numMatch.Groups[1].Value}. {numMatch.Groups[2].Value.Trim()}";

        // ── letter list  a) A. etc. ──────────────────────────────
        var alphaMatch = Regex.Match(text, @"^([a-zA-Z])[.)]\s+(.+)$");
        if (alphaMatch.Success)
            return $"- {alphaMatch.Groups[2].Value.Trim()}";

        // ── inline bold / italic for entire line ─────────────────
        if (bold && italic) return $"***{text}***";
        if (bold)           return $"**{text}**";
        if (italic)         return $"*{text}*";

        return text;
    }

    // =============================================================
    // Image saving
    // =============================================================

    private string SaveImage(IPdfImage img)
    {
        Directory.CreateDirectory(_assetsDir);
        _imgCounter++;

        // Try PNG first (PdfPig decodes most image types to PNG)
        if (img.TryGetPng(out var pngBytes) && pngBytes?.Length > 0)
        {
            var fileName = $"image_{_imgCounter}.png";
            File.WriteAllBytes(Path.Combine(_assetsDir, fileName), pngBytes);
            var uriPath = $"{Uri.EscapeDataString(_assetsDirName)}/{Uri.EscapeDataString(fileName)}";
            return $"![image]({uriPath})";
        }

        // Fallback: save raw bytes; detect JPEG by magic bytes
        var raw = img.RawBytes.ToArray();
        if (raw.Length > 2)
        {
            bool isJpeg = raw[0] == 0xFF && raw[1] == 0xD8;
            var ext      = isJpeg ? "jpg" : "bin";
            var fileName = $"image_{_imgCounter}.{ext}";
            File.WriteAllBytes(Path.Combine(_assetsDir, fileName), raw);
            var uriPath  = $"{Uri.EscapeDataString(_assetsDirName)}/{Uri.EscapeDataString(fileName)}";
            return $"![image]({uriPath})";
        }

        _imgCounter--;   // nothing saved
        return "";
    }

    // =============================================================
    // Word → Line grouping
    // =============================================================

    private static List<TextLine> GroupIntoLines(IList<Word> words)
    {
        const double tolerance = 3.5;   // pt — words within this Y range are the same line
        var lines = new List<TextLine>();

        foreach (var word in words)
        {
            double y = word.BoundingBox.Bottom;
            var line = lines.FirstOrDefault(l => Math.Abs(l.BaselineY - y) <= tolerance);
            if (line != null)
                line.Add(word);
            else
                lines.Add(new TextLine(y, word));
        }

        return lines.OrderByDescending(l => l.BaselineY).ToList();
    }

    private static double EstimateLineHeight(IList<Word> words, double bodySize)
    {
        if (words.Count == 0) return bodySize * 1.4;
        var heights = words.Select(w => w.BoundingBox.Height).Where(h => h > 1).ToList();
        return heights.Count > 0 ? heights.Average() * 1.5 : bodySize * 1.4;
    }

    // =============================================================
    // Statistics
    // =============================================================

    private static double Mode(IEnumerable<double> values)
    {
        // Round to 0.5 pt buckets, return the most frequent size
        return values
            .Select(v => Math.Round(v * 2) / 2)
            .GroupBy(v => v)
            .OrderByDescending(g => g.Count())
            .First().Key;
    }

    // =============================================================
    // Helper types
    // =============================================================

    private sealed class TextLine
    {
        public double     BaselineY  { get; }
        public List<Word> Words      { get; } = new();

        public TextLine(double y, Word first) { BaselineY = y; Words.Add(first); }
        public void Add(Word w) => Words.Add(w);

        public string Text =>
            string.Join(" ", Words.OrderBy(w => w.BoundingBox.Left).Select(w => w.Text));

        public double TopY =>
            Words.Max(w => w.BoundingBox.Top);

        public double AvgFontSize =>
            Words.SelectMany(w => w.Letters)
                 .Where(l => l.PointSize > 0)
                 .Select(l => l.PointSize)
                 .DefaultIfEmpty(10)
                 .Average();

        public bool IsBold =>
            Words.Any(w => w.Letters.Any(l =>
                l.FontName?.Contains("Bold", StringComparison.OrdinalIgnoreCase) == true));

        public bool IsItalic =>
            Words.Any(w => w.Letters.Any(l =>
                l.FontName?.Contains("Italic",  StringComparison.OrdinalIgnoreCase) == true ||
                l.FontName?.Contains("Oblique", StringComparison.OrdinalIgnoreCase) == true));
    }

    private sealed class ContentBlock
    {
        public double     Y      { get; init; }
        public TextLine?  Line   { get; init; }
        public IPdfImage? Image  { get; init; }
    }
}
