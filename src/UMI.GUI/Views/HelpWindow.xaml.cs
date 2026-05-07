// SPDX-FileCopyrightText: 2026 Dirk Schelhasse
// SPDX-License-Identifier: GPL-3.0-or-later
//
// This file is part of UMI - Universal Media Import.
//
//     UMI - Universal Media Import is free software: you can redistribute it and/or modify
//     it under the terms of the GNU General Public License as published by
//     the Free Software Foundation, either version 3 of the License, or
//     (at your option) any later version.
//
//     UMI - Universal Media Import is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY; without even the implied warranty of
//     MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//     GNU General Public License for more details.
//
//     You should have received a copy of the GNU General Public License
//     along with UMI - Universal Media Import.  If not, see <http://www.gnu.org/licenses/>.

using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using UMI.GUI.Resources;
using UMI.GUI.ViewModels;

namespace UMI.GUI.Views;

/// <summary>
/// View-model for a single sidebar entry. Supports INotifyPropertyChanged so the
/// active-chapter highlight can be updated without rebuilding the entire list.
/// Level 1 = chapter (bold, upper tier), Level 3 = sub-section (indented, lower tier).
/// </summary>
public sealed class SidebarEntry : INotifyPropertyChanged
{
    public string Text       { get; }
    public int    Level      { get; }
    public string ChapterKey { get; }

    private bool _isActive;

    /// <summary>True when this entry belongs to the currently displayed chapter.</summary>
    public bool IsActive
    {
        get => _isActive;
        set { if (_isActive == value) return; _isActive = value; OnPropertyChanged(); }
    }

    public SidebarEntry(string text, int level, string chapterKey)
    {
        Text       = text;
        Level      = level;
        ChapterKey = chapterKey;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>
/// Describes one chapter as parsed from chapters.json.
/// </summary>
internal sealed record ChapterInfo(string Key, string File, string TitleEn, string TitleDe);

/// <summary>
/// Markdown-based Help window. Loads chapter files from docs/help/{lang}/ via chapters.json,
/// provides two-tier sidebar navigation (chapters + sub-sections), and full-text search across
/// all chapter files.
/// </summary>
public partial class HelpWindow : Window
{
    private readonly List<ChapterInfo>  _chapters   = new();
    private readonly List<SidebarEntry> _allEntries = new();
    private string _langDir     = string.Empty;
    private string _activeKey   = string.Empty;
    private string _rawMarkdown = string.Empty;

    /// <summary>App version string shown in the about footer.</summary>
    public string AppVersion { get; } =
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? string.Empty;

    /// <param name="language">Language code ("de", "en", or null → "en").</param>
    public HelpWindow(string? language = null)
    {
        InitializeComponent();
        DataContext = this;

        var lang    = language == "de" ? "de" : "en";
        var docsDir = Path.Combine(AppContext.BaseDirectory, "docs", "help");
        _langDir    = Path.Combine(docsDir, lang);

        if (!Directory.Exists(_langDir))
        {
            var fallback = lang == "de" ? "en" : "de";
            _langDir = Path.Combine(docsDir, fallback);
        }

        LoadChapters(Path.Combine(docsDir, "chapters.json"), lang);

        SearchBox.GotFocus  += (_, _) => SearchPlaceholder.Visibility = Visibility.Collapsed;
        SearchBox.LostFocus += (_, _) => SearchPlaceholder.Visibility =
            string.IsNullOrEmpty(SearchBox.Text) ? Visibility.Visible : Visibility.Collapsed;

        MarkdownViewer.Engine.HyperlinkCommand = new RelayCommand<object>(OnLinkClicked);
    }

    private void OnLinkClicked(object? param)
    {
        var url = param?.ToString();
        if (string.IsNullOrEmpty(url)) return;

        if (url.StartsWith("glossary:", StringComparison.OrdinalIgnoreCase))
        {
            var term = url["glossary:".Length..];
            OpenChapter("glossary");
            Dispatcher.InvokeAsync(() => TryScrollInCurrentDocument(term),
                System.Windows.Threading.DispatcherPriority.Loaded);
            return;
        }

        if (url.StartsWith("chapter:", StringComparison.OrdinalIgnoreCase))
        {
            var key = url["chapter:".Length..];
            OpenChapter(key);
            return;
        }

        if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                System.Diagnostics.Process.Start(
                    new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch { /* ignore */ }
        }
    }

    /// <summary>
    /// Opens a chapter by key (e.g. "quickstart", "tools") and updates the sidebar.
    /// Does nothing when the key is unknown.
    /// </summary>
    public void OpenChapter(string chapterKey)
    {
        var chapter = _chapters.FirstOrDefault(c => c.Key == chapterKey);
        if (chapter is not null) LoadChapterContent(chapter);
    }

    /// <summary>
    /// Scrolls the Markdown viewer to the paragraph matching the given anchor text.
    /// If the anchor lives in a different chapter than the current one, that chapter is
    /// loaded first and then scrolled to.
    /// </summary>
    /// <param name="anchor">Heading text to scroll to (e.g. "Gyroflow Stabilization").</param>
    public void ScrollToSection(string anchor)
    {
        if (string.IsNullOrWhiteSpace(anchor)) return;

        if (TryScrollInCurrentDocument(anchor)) return;

        foreach (var chapter in _chapters)
        {
            if (chapter.Key == _activeKey) continue;

            var filePath = Path.Combine(_langDir, chapter.File);
            if (!File.Exists(filePath)) continue;

            if (ContainsHeading(File.ReadAllText(filePath), anchor))
            {
                LoadChapterContent(chapter);
                Dispatcher.InvokeAsync(() => TryScrollInCurrentDocument(anchor),
                    System.Windows.Threading.DispatcherPriority.Loaded);
                return;
            }
        }
    }

    private void LoadChapters(string chaptersFile, string lang)
    {
        if (!File.Exists(chaptersFile))
        {
            ShowError();
            return;
        }

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(chaptersFile));
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                var key     = el.GetProperty("key").GetString()      ?? string.Empty;
                var file    = el.GetProperty("file").GetString()     ?? string.Empty;
                var titleEn = el.GetProperty("title_en").GetString() ?? string.Empty;
                var titleDe = el.GetProperty("title_de").GetString() ?? string.Empty;
                _chapters.Add(new ChapterInfo(key, file, titleEn, titleDe));
            }
        }
        catch
        {
            ShowError();
            return;
        }

        if (_chapters.Count == 0) { ShowError(); return; }

        foreach (var c in _chapters)
        {
            var title = lang == "de" ? c.TitleDe : c.TitleEn;
            _allEntries.Add(new SidebarEntry(title, 1, c.Key));
        }

        LoadChapterContent(_chapters[0]);
    }

    private void LoadChapterContent(ChapterInfo chapter)
    {
        var filePath = Path.Combine(_langDir, chapter.File);
        if (!File.Exists(filePath)) return;

        _activeKey   = chapter.Key;
        _rawMarkdown = File.ReadAllText(filePath);
        MarkdownViewer.Markdown = _rawMarkdown;

        var sv = FindVisualChild<ScrollViewer>(MarkdownViewer);
        sv?.ScrollToVerticalOffset(0);

        RefreshSubSections(chapter.Key, _rawMarkdown);
        RefreshActiveHighlight(chapter.Key);
    }

    /// <summary>
    /// Replaces the sub-section entries in _allEntries with those parsed from the given markdown,
    /// then refreshes the sidebar ItemsSource.
    /// </summary>
    private void RefreshSubSections(string chapterKey, string markdown)
    {
        _allEntries.RemoveAll(e => e.Level == 3);

        var insertIdx = _allEntries.FindIndex(e => e.ChapterKey == chapterKey && e.Level == 1);
        insertIdx = insertIdx < 0 ? _allEntries.Count : insertIdx + 1;

        foreach (var line in markdown.Split('\n'))
        {
            var trimmed = line.TrimStart();
            if (!trimmed.StartsWith("### ")) continue;

            var heading = trimmed[4..].Trim();
            if (!string.IsNullOrWhiteSpace(heading))
            {
                _allEntries.Insert(insertIdx++, new SidebarEntry(heading, 3, chapterKey));
            }
        }

        SidebarItems.ItemsSource = _allEntries.ToList();
    }

    /// <summary>
    /// Sets IsActive=true on the active chapter entry and false on all others.
    /// Works via INotifyPropertyChanged — no ItemsSource reset needed.
    /// </summary>
    private void RefreshActiveHighlight(string chapterKey)
    {
        foreach (var entry in _allEntries)
            entry.IsActive = (entry.Level == 1 && entry.ChapterKey == chapterKey);
    }

    private bool TryScrollInCurrentDocument(string anchor)
    {
        var document = MarkdownViewer.Document;
        if (document is null) return false;

        var normalized = anchor.Trim().ToLowerInvariant();
        foreach (var block in document.Blocks)
        {
            if (TryScrollToBlock(block, normalized))
                return true;
        }
        return false;
    }

    private bool TryScrollToBlock(Block block, string normalizedAnchor)
    {
        var text = GetBlockText(block).Trim().ToLowerInvariant();

        if (text == normalizedAnchor || text.Contains(normalizedAnchor))
        {
            ScrollBlockToTop(block);
            return true;
        }

        if (block is Section section)
        {
            foreach (var child in section.Blocks)
            {
                if (TryScrollToBlock(child, normalizedAnchor))
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Scrolls the viewer so that the given block appears at the top of the viewport.
    /// </summary>
    private void ScrollBlockToTop(Block block)
    {
        var sv = FindVisualChild<ScrollViewer>(MarkdownViewer);
        if (sv is null) return;

        var pointer = block.ContentStart.Paragraph?.ContentStart ?? block.ContentStart;
        var rect = pointer.GetCharacterRect(System.Windows.Documents.LogicalDirection.Forward);
        sv.ScrollToVerticalOffset(sv.VerticalOffset + rect.Top);
    }

    private static string GetBlockText(Block block) => block switch
    {
        Paragraph p => new TextRange(p.ContentStart, p.ContentEnd).Text,
        Section   s => string.Concat(s.Blocks.Select(GetBlockText)),
        _           => string.Empty
    };

    private static bool ContainsHeading(string markdown, string anchor)
    {
        var normalized = anchor.Trim().ToLowerInvariant();
        foreach (var line in markdown.Split('\n'))
        {
            var trimmed = line.TrimStart();
            string? headingText =
                trimmed.StartsWith("### ") ? trimmed[4..].Trim() :
                trimmed.StartsWith("## ")  ? trimmed[3..].Trim() :
                trimmed.StartsWith("# ")   ? trimmed[2..].Trim() :
                null;

            if (headingText is not null && headingText.ToLowerInvariant().Contains(normalized))
                return true;
        }
        return false;
    }

    private void ShowError()
    {
        MarkdownViewer.Visibility = Visibility.Collapsed;
        ErrorText.Visibility      = Visibility.Visible;
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var query = SearchBox.Text.Trim().ToLowerInvariant();
        SearchPlaceholder.Visibility = string.IsNullOrEmpty(SearchBox.Text) && !SearchBox.IsFocused
            ? Visibility.Visible
            : Visibility.Collapsed;

        if (string.IsNullOrWhiteSpace(query))
        {
            SidebarItems.ItemsSource = _allEntries.ToList();
            return;
        }

        var visible = new List<SidebarEntry>();
        foreach (var entry in _allEntries)
        {
            if (entry.Level == 1)
            {
                bool titleMatches = entry.Text.ToLowerInvariant().Contains(query);
                bool childMatches = _allEntries.Any(s =>
                    s.Level == 3 && s.ChapterKey == entry.ChapterKey &&
                    s.Text.ToLowerInvariant().Contains(query));

                if (titleMatches || childMatches)
                    visible.Add(entry);
            }
            else if (entry.Level == 3 && entry.Text.ToLowerInvariant().Contains(query))
            {
                if (!visible.Any(v => v.Level == 1 && v.ChapterKey == entry.ChapterKey))
                {
                    var parent = _allEntries.FirstOrDefault(v =>
                        v.Level == 1 && v.ChapterKey == entry.ChapterKey);
                    if (parent is not null) visible.Add(parent);
                }
                visible.Add(entry);
            }
        }

        SidebarItems.ItemsSource = visible;

        var firstMatch = visible.FirstOrDefault();
        if (firstMatch is null) return;

        if (firstMatch.ChapterKey != _activeKey)
        {
            var chapter = _chapters.FirstOrDefault(c => c.Key == firstMatch.ChapterKey);
            if (chapter is not null) LoadChapterContent(chapter);
        }
        if (firstMatch.Level == 3)
            TryScrollInCurrentDocument(firstMatch.Text);
    }

    private void SidebarItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not SidebarEntry entry) return;

        if (entry.Level == 1)
        {
            var chapter = _chapters.FirstOrDefault(c => c.Key == entry.ChapterKey);
            if (chapter is not null) LoadChapterContent(chapter);
        }
        else
        {
            if (entry.ChapterKey != _activeKey)
            {
                var chapter = _chapters.FirstOrDefault(c => c.Key == entry.ChapterKey);
                if (chapter is not null)
                {
                    LoadChapterContent(chapter);
                    Dispatcher.InvokeAsync(() => TryScrollInCurrentDocument(entry.Text),
                        System.Windows.Threading.DispatcherPriority.Loaded);
                    return;
                }
            }
            TryScrollInCurrentDocument(entry.Text);
        }
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    /// <summary>
    /// Accelerates mouse wheel scrolling (3× default) for the help content viewer.
    /// </summary>
    private void MarkdownViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        const double multiplier = 3.0;
        var sv = FindVisualChild<ScrollViewer>(MarkdownViewer);
        if (sv != null)
        {
            sv.ScrollToVerticalOffset(sv.VerticalOffset - (e.Delta * multiplier));
            e.Handled = true;
        }
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is T found) return found;
            var result = FindVisualChild<T>(child);
            if (result != null) return result;
        }
        return null;
    }
}
