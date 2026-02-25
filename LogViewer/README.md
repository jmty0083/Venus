# Log Viewer

A lightweight WPF desktop application for viewing and navigating structured log files on Windows.

## Features

- **Three input modes** — open a file via the menu, paste text from the clipboard, or pipe content directly on the command line (`type app.log | view.exe`)
- **Regex-based parsing** — define named-capture-group patterns to split each log line into typed columns (timestamp, level, message, etc.)
- **Auto-detection** — on load, the app tests each saved pattern against the first 50 lines and picks the best match automatically
- **Pattern manager** — create, edit, delete, and star (set as current) patterns; patterns are stored in `%APPDATA%\LogViewer\patterns.json`
- **Column control** — right-click any column header to show/hide columns; columns are also resizable and reorderable
- **Detail panel** — selecting a row shows the original raw log line in a resizable panel below the grid
- **Search** — type in the search bar and press Enter or Next/Prev to navigate matches across all visible columns
- **Themes** — Dark, Light, and System (follows Windows setting); the title bar and scroll bars match the selected theme; preference is saved between sessions

## Requirements

- Windows 10 or later
- [.NET 10 Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) (Windows Desktop)

## Building

```
dotnet build LogViewer/LogViewer.csproj
```

Or open the solution in Visual Studio 2022+ and press F5.

The build produces two executables in the output directory:

| File | Description |
|---|---|
| `LogViewer.exe` | Primary executable |
| `view.exe` | Alias — identical behaviour, shorter to type |

## Installation (add to PATH)

Run the installer script from the repo root after building:

```powershell
.\install.ps1
```

This finds the build output automatically and adds it to your **User PATH** (no admin required). Open a new terminal and the `view` and `LogViewer` commands will be available globally.

To point to a specific build directory:

```powershell
.\install.ps1 -BinDir ".\LogViewer\bin\Release\net10.0-windows"
```

## Usage

**Open a file:**
```
view path\to\app.log
LogViewer.exe path\to\app.log
```

**Pipe from another command:**
```
type app.log | view
Get-Content app.log | view
```

**Open interactively:**
Use *File > Open File* or *Edit > Paste from Clipboard*.

## Defining Patterns

A pattern is a .NET regular expression with **named capture groups** — each group becomes a column.

Example pattern for a common log format:
```
^(?<timestamp>\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}) \[(?<level>\w+)\] (?<source>[\w\.]+): (?<text>.+)$
```

This produces columns: `timestamp`, `level`, `source`, `text`.

Lines that do not match (e.g. continuation lines or stack traces) are stored as raw text in the last column, keeping them visible and searchable.

Open *Patterns > Manage Patterns* to add and test patterns. A brief **Saved ✓** confirmation appears in the window after each successful save.
