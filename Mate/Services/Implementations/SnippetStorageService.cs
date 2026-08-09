using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Mate.Models;
using Mate.Services.Interfaces;

namespace Mate.Services.Implementations;

public sealed class SnippetStorageService : ISnippetStorageService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly object _sync = new();
    private readonly string _storagePath;
    private readonly List<SnippetItem> _items;

    public SnippetStorageService()
    {
        var dataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LD.Desktop.Mate");
        Directory.CreateDirectory(dataFolder);
        _storagePath = Path.Combine(dataFolder, "snippets.json");
        _items = LoadItems(_storagePath);
    }

    public IReadOnlyList<SnippetItem> GetItems()
    {
        lock (_sync)
        {
            return _items
                .OrderByDescending(item => item.CreatedAt)
                .ToArray();
        }
    }

    public SnippetItem Add(SnippetType type, string comment, string value)
    {
        var item = new SnippetItem(
            Guid.NewGuid(),
            type,
            comment.Trim(),
            value.Trim(),
            DateTimeOffset.UtcNow);

        lock (_sync)
        {
            _items.Add(item);
            try
            {
                SaveItems();
            }
            catch
            {
                _items.Remove(item);
                throw;
            }
        }

        return item;
    }

    public SnippetItem Update(Guid id, SnippetType type, string comment, string value)
    {
        lock (_sync)
        {
            var index = _items.FindIndex(candidate => candidate.Id == id);
            if (index < 0) throw new InvalidOperationException("Snippet was not found.");

            var previousItem = _items[index];
            var updatedItem = previousItem with
            {
                Type = type,
                Comment = comment.Trim(),
                Value = value.Trim()
            };

            _items[index] = updatedItem;
            try
            {
                SaveItems();
            }
            catch
            {
                _items[index] = previousItem;
                throw;
            }

            return updatedItem;
        }
    }

    public void Delete(Guid id)
    {
        lock (_sync)
        {
            var item = _items.FirstOrDefault(candidate => candidate.Id == id);
            if (item is null) return;

            _items.Remove(item);
            try
            {
                SaveItems();
            }
            catch
            {
                _items.Add(item);
                throw;
            }
        }
    }

    private static List<SnippetItem> LoadItems(string path)
    {
        try
        {
            if (!File.Exists(path)) return new List<SnippetItem>();
            return JsonSerializer.Deserialize<List<SnippetItem>>(File.ReadAllText(path), JsonOptions)
                   ?? new List<SnippetItem>();
        }
        catch
        {
            return new List<SnippetItem>();
        }
    }

    private void SaveItems() => File.WriteAllText(
        _storagePath,
        JsonSerializer.Serialize(_items, JsonOptions));
}
