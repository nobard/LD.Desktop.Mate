using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Mate.Models;
using Mate.Services.Interfaces;
using Windows.ApplicationModel.DataTransfer;

namespace Mate.Services.Implementations;

public sealed class WindowsClipboardHistoryService : IClipboardHistoryService
{
    private readonly object _pinsLock = new();
    private readonly string _pinsFilePath;
    private readonly List<string> _pinnedTexts;
    private readonly List<string> _restoredTexts = new();
    private bool _disposed;

    public WindowsClipboardHistoryService()
    {
        var dataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LD.Desktop.Mate");
        Directory.CreateDirectory(dataFolder);
        _pinsFilePath = Path.Combine(dataFolder, "clipboard-pins.json");
        _pinnedTexts = LoadPinnedTexts(_pinsFilePath);

        Clipboard.HistoryChanged += Clipboard_HistoryChanged;
        Clipboard.HistoryEnabledChanged += Clipboard_HistoryEnabledChanged;
    }

    public event Action? HistoryChanged;

    public async Task<ClipboardHistorySnapshot> GetHistoryAsync()
    {
        var pinnedTexts = GetPinnedTexts();
        var restoredTexts = GetRestoredTexts();
        try
        {
            var result = await Clipboard.GetHistoryItemsAsync();
            if (result.Status == ClipboardHistoryItemsResultStatus.ClipboardHistoryDisabled)
            {
                return new ClipboardHistorySnapshot(
                    ClipboardHistoryState.Disabled,
                    CreateStoredSnapshots(pinnedTexts, restoredTexts));
            }

            if (result.Status == ClipboardHistoryItemsResultStatus.AccessDenied)
            {
                return new ClipboardHistorySnapshot(
                    ClipboardHistoryState.AccessDenied,
                    CreateStoredSnapshots(pinnedTexts, restoredTexts));
            }

            var pinnedSet = new HashSet<string>(pinnedTexts, StringComparer.Ordinal);
            var foundTexts = new HashSet<string>(StringComparer.Ordinal);
            var pinnedItems = new List<ClipboardItemSnapshot>();
            var regularItems = new List<ClipboardItemSnapshot>();

            foreach (var historyItem in result.Items)
            {
                if (!historyItem.Content.Contains(StandardDataFormats.Text)) continue;

                try
                {
                    var text = await historyItem.Content.GetTextAsync();
                    if (string.IsNullOrWhiteSpace(text)) continue;

                    foundTexts.Add(text);
                    var isPinned = pinnedSet.Contains(text);
                    var snapshot = new ClipboardItemSnapshot(historyItem, text, isPinned);
                    if (isPinned)
                    {
                        pinnedItems.Add(snapshot);
                    }
                    else
                    {
                        regularItems.Add(snapshot);
                    }
                }
                catch
                {
                    // Skip history entries whose text is no longer available.
                }
            }

            foreach (var pinnedText in pinnedTexts)
            {
                if (!foundTexts.Contains(pinnedText))
                {
                    pinnedItems.Add(new ClipboardItemSnapshot(null, pinnedText, true));
                }
            }

            foreach (var restoredText in restoredTexts)
            {
                if (!foundTexts.Contains(restoredText))
                {
                    pinnedItems.Add(new ClipboardItemSnapshot(null, restoredText, false));
                }
            }

            RemoveRestoredTexts(foundTexts);
            pinnedItems.AddRange(regularItems);
            return new ClipboardHistorySnapshot(ClipboardHistoryState.Available, pinnedItems);
        }
        catch
        {
            return new ClipboardHistorySnapshot(
                ClipboardHistoryState.Unavailable,
                CreateStoredSnapshots(pinnedTexts, restoredTexts));
        }
    }

    public bool SetCurrent(ClipboardItemSnapshot item)
    {
        try
        {
            if (item.HistoryItem is not null)
            {
                return Clipboard.SetHistoryItemAsContent(item.HistoryItem) == SetHistoryItemAsContentStatus.Success;
            }

            return PutTextInClipboard(item.Preview);
        }
        catch
        {
            return false;
        }
    }

    public bool TogglePinned(ClipboardItemSnapshot item)
    {
        if (string.IsNullOrWhiteSpace(item.Preview)) return false;

        if (item.IsPinned && item.HistoryItem is null && !PutTextInClipboard(item.Preview))
        {
            return false;
        }

        lock (_pinsLock)
        {
            var existingIndex = _pinnedTexts.FindIndex(text => string.Equals(text, item.Preview, StringComparison.Ordinal));
            if (existingIndex >= 0)
            {
                _pinnedTexts.RemoveAt(existingIndex);
                if (item.HistoryItem is null)
                {
                    _restoredTexts.RemoveAll(text => string.Equals(text, item.Preview, StringComparison.Ordinal));
                    _restoredTexts.Insert(0, item.Preview);
                }
            }
            else
            {
                _pinnedTexts.Insert(0, item.Preview);
                _restoredTexts.RemoveAll(text => string.Equals(text, item.Preview, StringComparison.Ordinal));
            }

            SavePinnedTexts();
        }

        HistoryChanged?.Invoke();
        return true;
    }

    public bool ClearHistory()
    {
        try
        {
            if (!Clipboard.ClearHistory()) return false;

            lock (_pinsLock)
            {
                _restoredTexts.Clear();
            }

            HistoryChanged?.Invoke();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool PutTextInClipboard(string text)
    {
        try
        {
            var data = new DataPackage();
            data.SetText(text);
            Clipboard.SetContent(data);
            Clipboard.Flush();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private string[] GetPinnedTexts()
    {
        lock (_pinsLock)
        {
            return _pinnedTexts.ToArray();
        }
    }

    private string[] GetRestoredTexts()
    {
        lock (_pinsLock)
        {
            return _restoredTexts.ToArray();
        }
    }

    private void RemoveRestoredTexts(IReadOnlySet<string> textsInWindowsHistory)
    {
        lock (_pinsLock)
        {
            _restoredTexts.RemoveAll(textsInWindowsHistory.Contains);
        }
    }

    private static ClipboardItemSnapshot[] CreateStoredSnapshots(
        IEnumerable<string> pinnedTexts,
        IEnumerable<string> restoredTexts) => pinnedTexts
        .Select(text => new ClipboardItemSnapshot(null, text, true))
        .Concat(restoredTexts.Select(text => new ClipboardItemSnapshot(null, text, false)))
        .ToArray();

    private static List<string> LoadPinnedTexts(string path)
    {
        try
        {
            if (!File.Exists(path)) return new List<string>();

            var values = JsonSerializer.Deserialize<string[]>(File.ReadAllText(path)) ?? Array.Empty<string>();
            return values
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }
        catch
        {
            return new List<string>();
        }
    }

    private void SavePinnedTexts()
    {
        try
        {
            File.WriteAllText(_pinsFilePath, JsonSerializer.Serialize(_pinnedTexts));
        }
        catch (IOException)
        {
            // Keep pins for the current session if persistence is temporarily unavailable.
        }
        catch (UnauthorizedAccessException)
        {
            // Keep pins for the current session if persistence is temporarily unavailable.
        }
    }

    private void Clipboard_HistoryChanged(object? sender, ClipboardHistoryChangedEventArgs e) => HistoryChanged?.Invoke();

    private void Clipboard_HistoryEnabledChanged(object? sender, object e) => HistoryChanged?.Invoke();

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Clipboard.HistoryChanged -= Clipboard_HistoryChanged;
        Clipboard.HistoryEnabledChanged -= Clipboard_HistoryEnabledChanged;
    }
}
