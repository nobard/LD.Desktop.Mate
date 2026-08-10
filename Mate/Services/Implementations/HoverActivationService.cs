using System;
using System.IO;
using Mate.Services.Interfaces;

namespace Mate.Services.Implementations;

public sealed class HoverActivationService : IHoverActivationService
{
    private readonly string _settingsPath;

    public HoverActivationService()
    {
        var dataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LD.Desktop.Mate");
        Directory.CreateDirectory(dataFolder);
        _settingsPath = Path.Combine(dataFolder, "hover-activation.txt");
        IsEnabled = Load();
    }

    public bool IsEnabled { get; private set; }

    public event EventHandler? EnabledChanged;

    public void SetEnabled(bool isEnabled)
    {
        if (IsEnabled == isEnabled) return;

        IsEnabled = isEnabled;
        Save(isEnabled);
        EnabledChanged?.Invoke(this, EventArgs.Empty);
    }

    private bool Load()
    {
        try
        {
            if (!File.Exists(_settingsPath)) return true;
            return bool.TryParse(File.ReadAllText(_settingsPath), out var isEnabled)
                ? isEnabled
                : true;
        }
        catch
        {
            return true;
        }
    }

    private void Save(bool isEnabled)
    {
        try
        {
            File.WriteAllText(_settingsPath, isEnabled.ToString());
        }
        catch
        {
            // Keep the setting for the current session if persistence is unavailable.
        }
    }
}
