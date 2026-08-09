using System;
using System.IO;
using System.Text.Json;
using Mate.Models;
using Mate.Services.Interfaces;

namespace Mate.Services.Implementations;

public sealed class PomodoroSettingsService : IPomodoroSettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _storagePath;

    public PomodoroSettingsService()
    {
        _storagePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LD.Desktop.Mate",
            "pomodoro-settings.json");
    }

    public PomodoroSettings Load()
    {
        try
        {
            if (!File.Exists(_storagePath)) return PomodoroSettings.Default;

            var json = File.ReadAllText(_storagePath);
            return JsonSerializer.Deserialize<PomodoroSettings>(json, JsonOptions)
                   ?? PomodoroSettings.Default;
        }
        catch (IOException)
        {
            return PomodoroSettings.Default;
        }
        catch (UnauthorizedAccessException)
        {
            return PomodoroSettings.Default;
        }
        catch (JsonException)
        {
            return PomodoroSettings.Default;
        }
    }

    public void Save(PomodoroSettings settings)
    {
        var directory = Path.GetDirectoryName(_storagePath);
        if (directory is not null) Directory.CreateDirectory(directory);

        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(_storagePath, json);
    }
}
