using System;
using System.IO;
using System.Reflection;
using Mate.Services.Interfaces;
using Microsoft.Win32;

namespace Mate.Services.Implementations;

public sealed class WindowsAutoStartService : IAutoStartService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Mate";

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

            return IsEnabled == enabled;
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

        var entryAssemblyPath = Assembly.GetEntryAssembly()?.Location;
        if (!string.IsNullOrWhiteSpace(entryAssemblyPath))
        {
            var executablePath = Path.ChangeExtension(entryAssemblyPath, ".exe");
            if (File.Exists(executablePath)) return Path.GetFullPath(executablePath);
        }

        return Path.GetFullPath(processPath ?? entryAssemblyPath ?? "Mate.exe");
    }
}
