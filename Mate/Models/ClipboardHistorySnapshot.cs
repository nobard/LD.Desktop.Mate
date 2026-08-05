using System.Collections.Generic;
using Windows.ApplicationModel.DataTransfer;

namespace Mate.Models;

public enum ClipboardHistoryState
{
    Available,
    Disabled,
    AccessDenied,
    Unavailable
}

public sealed class ClipboardItemSnapshot
{
    public ClipboardItemSnapshot(ClipboardHistoryItem? historyItem, string preview, bool isPinned)
    {
        HistoryItem = historyItem;
        Preview = preview;
        IsPinned = isPinned;
    }

    internal ClipboardHistoryItem? HistoryItem { get; }

    public string Preview { get; }

    public bool IsPinned { get; }
}

public sealed record ClipboardHistorySnapshot(
    ClipboardHistoryState State,
    IReadOnlyList<ClipboardItemSnapshot> Items);
