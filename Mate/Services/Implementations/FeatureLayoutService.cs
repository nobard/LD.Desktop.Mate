using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Mate.Services.Interfaces;

namespace Mate.Services.Implementations;

public sealed class FeatureLayoutService : IFeatureLayoutService
{
    private static readonly AppFeature[] DefaultOrder =
    {
        AppFeature.Player,
        AppFeature.Folder,
        AppFeature.Clipboard,
        AppFeature.Snippets,
        AppFeature.Browser,
        AppFeature.Translator,
        AppFeature.Notifications,
        AppFeature.Pomodoro
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _settingsPath;
    private List<FeatureLayoutItem> _items;

    public FeatureLayoutService()
    {
        var dataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LD.Desktop.Mate");
        Directory.CreateDirectory(dataFolder);
        _settingsPath = Path.Combine(dataFolder, "feature-layout.json");
        _items = Normalize(Load());
    }

    public IReadOnlyList<FeatureLayoutItem> Items => _items;

    public event EventHandler? LayoutChanged;

    public bool SetVisible(AppFeature feature, bool isVisible)
    {
        var index = _items.FindIndex(item => item.Feature == feature);
        if (index < 0 || _items[index].IsVisible == isVisible) return false;
        if (!isVisible && _items.Count(item => item.IsVisible) <= 1) return false;

        _items[index] = _items[index] with { IsVisible = isVisible };
        Save();
        LayoutChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public bool Move(AppFeature feature, int targetIndex)
    {
        var sourceIndex = _items.FindIndex(item => item.Feature == feature);
        if (sourceIndex < 0 || targetIndex < 0 || targetIndex >= _items.Count || sourceIndex == targetIndex)
        {
            return false;
        }

        var item = _items[sourceIndex];
        _items.RemoveAt(sourceIndex);
        _items.Insert(Math.Min(targetIndex, _items.Count), item);
        Save();
        LayoutChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    private IReadOnlyList<FeatureLayoutItem>? Load()
    {
        try
        {
            if (!File.Exists(_settingsPath)) return null;
            return JsonSerializer.Deserialize<List<FeatureLayoutItem>>(
                File.ReadAllText(_settingsPath),
                JsonOptions);
        }
        catch (Exception exception) when (exception is IOException
                                           or UnauthorizedAccessException
                                           or JsonException)
        {
            return null;
        }
    }

    private static List<FeatureLayoutItem> Normalize(IReadOnlyList<FeatureLayoutItem>? storedItems)
    {
        var result = new List<FeatureLayoutItem>(DefaultOrder.Length);
        var added = new HashSet<AppFeature>();

        if (storedItems is not null)
        {
            foreach (var item in storedItems)
            {
                if (!Enum.IsDefined(item.Feature) || !added.Add(item.Feature)) continue;
                result.Add(item);
            }
        }

        foreach (var feature in DefaultOrder)
        {
            if (added.Add(feature)) result.Add(new FeatureLayoutItem(feature, true));
        }

        if (result.All(item => !item.IsVisible))
        {
            result[0] = result[0] with { IsVisible = true };
        }

        return result;
    }

    private void Save()
    {
        try
        {
            File.WriteAllText(_settingsPath, JsonSerializer.Serialize(_items, JsonOptions));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Keep the selected layout for the current session.
        }
    }
}
