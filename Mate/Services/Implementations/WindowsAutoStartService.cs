using System;
using System.IO;
using Mate.Services.Interfaces;
using Microsoft.Win32;

namespace Mate.Services.Implementations;

public sealed class WindowsAutoStartService : IAutoStartService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Mate";

    public event EventHandler? EnabledChanged;

    public bool IsEnabled
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
                var configuredCommand = key?.GetValue(ValueName) as string;
                return string.Equals(
                    configuredCommand?.Trim(),
                    GetLaunchCommand(),
                    StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }
    }

    public bool SetEnabled(bool enabled)
    {
        var wasEnabled = IsEnabled;
        try
        {
            if (enabled)
            {
                using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
                key.SetValue(ValueName, GetLaunchCommand(), RegistryValueKind.String);
            }
            else
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
                key?.DeleteValue(ValueName, throwOnMissingValue: false);
            }

            var isEnabled = IsEnabled;
            var succeeded = isEnabled == enabled;
            if (succeeded && wasEnabled != isEnabled)
            {
                EnabledChanged?.Invoke(this, EventArgs.Empty);
            }

            return succeeded;
        }
        catch
        {
            return false;
        }
    }

    private static string GetLaunchCommand() => $"\"{GetExecutablePath()}\"";

    private static string GetExecutablePath()
    {
        var processPath = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(processPath)
            && !string.Equals(
                Path.GetFileNameWithoutExtension(processPath),
                "dotnet",
                StringComparison.OrdinalIgnoreCase))
        {
            return Path.GetFullPath(processPath);
        }

        var executablePath = Path.Combine(AppContext.BaseDirectory, "Mate.exe");
        return Path.GetFullPath(executablePath);
    }
}
