using System.ComponentModel;

namespace PdfToMarkdown.Models;

public class PdfPageItem : INotifyPropertyChanged
{
    private bool _isSelected = true;

    public int    PageNumber  { get; init; }
    public int    WordCount   { get; init; }
    public int    ImageCount  { get; init; }

    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected))); }
    }

    public string DisplayName =>
        $"Page {PageNumber}" +
        (ImageCount > 0 ? $"  [{ImageCount} image{(ImageCount > 1 ? "s" : "")}]" : "");

    public event PropertyChangedEventHandler? PropertyChanged;
}
