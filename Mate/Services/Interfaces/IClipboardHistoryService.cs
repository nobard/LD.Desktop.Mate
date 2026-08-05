using System;
using System.Threading.Tasks;
using Mate.Models;

namespace Mate.Services.Interfaces;

public interface IClipboardHistoryService : IDisposable
{
    event Action? HistoryChanged;

    Task<ClipboardHistorySnapshot> GetHistoryAsync();

    bool SetCurrent(ClipboardItemSnapshot item);

    bool TogglePinned(ClipboardItemSnapshot item);

    bool ClearHistory();
}
