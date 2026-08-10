using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Mate.Services.Interfaces;
using Microsoft.Win32;

namespace Mate.Services.Implementations;

public sealed class PrivateBrowserService : IPrivateBrowserService
{
    private const string DefaultBrowserId = "default";

    private static readonly Regex ExecutablePattern = new(
        "^\\s*(?:\"(?<quoted>[^\"]+\\.exe)\"|(?<plain>.+?\\.exe))",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly IReadOnlyList<DetectedBrowser> _detectedBrowsers;
    private readonly string _settingsPath;

    public PrivateBrowserService()
    {
        _detectedBrowsers = DetectBrowsers();
        AvailableBrowsers = new[]
            {
                new BrowserOption(DefaultBrowserId, "Браузер по умолчанию")
            }
            .Concat(_detectedBrowsers.Select(browser =>
                new BrowserOption(browser.Id, browser.DisplayName)))
            .ToArray();
        AvailableSearchEngines = new[]
        {
            new SearchEngineOption(BrowserSearchEngine.Google, "Google"),
            new SearchEngineOption(BrowserSearchEngine.Yandex, "Яндекс"),
            new SearchEngineOption(BrowserSearchEngine.Bing, "Bing"),
            new SearchEngineOption(BrowserSearchEngine.DuckDuckGo, "DuckDuckGo")
        };

        var dataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LD.Desktop.Mate");
        Directory.CreateDirectory(dataFolder);
        _settingsPath = Path.Combine(dataFolder, "browser-settings.json");
        Settings = Normalize(Load());
    }

    public IReadOnlyList<BrowserOption> AvailableBrowsers { get; }

    public IReadOnlyList<SearchEngineOption> AvailableSearchEngines { get; }

    public BrowserLaunchSettings Settings { get; private set; }

    public event EventHandler? SettingsChanged;

    public void SetBrowser(string browserId)
    {
        if (!AvailableBrowsers.Any(browser => browser.Id == browserId)) return;
        UpdateSettings(Settings with { BrowserId = browserId });
    }

    public void SetSearchEngine(BrowserSearchEngine searchEngine)
    {
        if (!Enum.IsDefined(searchEngine)) return;
        UpdateSettings(Settings with { SearchEngine = searchEngine });
    }

    public void SetPrivateMode(bool usePrivateMode) =>
        UpdateSettings(Settings with { UsePrivateMode = usePrivateMode });

    public PrivateBrowserOpenResult OpenSearch(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return PrivateBrowserOpenResult.BrowserNotFound;

        var target = BuildTarget(query.Trim(), Settings.SearchEngine);
        if (Settings.BrowserId == DefaultBrowserId && !Settings.UsePrivateMode)
        {
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
            return PrivateBrowserOpenResult.Opened;
        }

        DetectedBrowser? browser;
        BrowserResolutionStatus defaultBrowserStatus = BrowserResolutionStatus.NotFound;
        if (Settings.BrowserId == DefaultBrowserId)
        {
            var defaultBrowser = GetDefaultBrowser();
            defaultBrowserStatus = defaultBrowser.Status;
            browser = defaultBrowser.Launch ?? _detectedBrowsers.FirstOrDefault();
        }
        else
        {
            browser = _detectedBrowsers.FirstOrDefault(candidate =>
                candidate.Id == Settings.BrowserId);
        }

        if (browser is null)
        {
            return defaultBrowserStatus == BrowserResolutionStatus.Unsupported
                ? PrivateBrowserOpenResult.UnsupportedBrowser
                : PrivateBrowserOpenResult.BrowserNotFound;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = browser.ExecutablePath,
            UseShellExecute = true
        };
        if (Settings.UsePrivateMode) startInfo.ArgumentList.Add(browser.PrivateArgument);
        startInfo.ArgumentList.Add(target);

        Process.Start(startInfo);
        return PrivateBrowserOpenResult.Opened;
    }

    private void UpdateSettings(BrowserLaunchSettings settings)
    {
        settings = Normalize(settings);
        if (settings == Settings) return;

        Settings = settings;
        Save(settings);
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    private BrowserLaunchSettings Normalize(BrowserLaunchSettings settings)
    {
        var browserId = AvailableBrowsers.Any(browser => browser.Id == settings.BrowserId)
            ? settings.BrowserId
            : DefaultBrowserId;
        var searchEngine = Enum.IsDefined(settings.SearchEngine)
            ? settings.SearchEngine
            : BrowserSearchEngine.Google;
        return settings with
        {
            BrowserId = browserId,
            SearchEngine = searchEngine
        };
    }

    private BrowserLaunchSettings Load()
    {
        try
        {
            if (!File.Exists(_settingsPath)) return BrowserLaunchSettings.Default;
            return JsonSerializer.Deserialize<BrowserLaunchSettings>(
                       File.ReadAllText(_settingsPath),
                       JsonOptions)
                   ?? BrowserLaunchSettings.Default;
        }
        catch
        {
            return BrowserLaunchSettings.Default;
        }
    }

    private void Save(BrowserLaunchSettings settings)
    {
        try
        {
            File.WriteAllText(_settingsPath, JsonSerializer.Serialize(settings, JsonOptions));
        }
        catch
        {
            // Keep the selected settings for the current session.
        }
    }

    private static BrowserResolution GetDefaultBrowser()
    {
        try
        {
            using var userChoice = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\Shell\Associations\UrlAssociations\https\UserChoice");
            var programId = userChoice?.GetValue("ProgId") as string;
            if (string.IsNullOrWhiteSpace(programId)) return BrowserResolution.NotFound;

            using var commandKey = Registry.ClassesRoot.OpenSubKey($@"{programId}\shell\open\command");
            var command = commandKey?.GetValue(null) as string;
            var executablePath = ExtractExecutablePath(command);
            if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
            {
                return BrowserResolution.NotFound;
            }

            var launch = CreateBrowser(
                "default",
                Path.GetFileNameWithoutExtension(executablePath),
                executablePath,
                programId);
            return launch is null
                ? BrowserResolution.Unsupported
                : new BrowserResolution(BrowserResolutionStatus.Supported, launch);
        }
        catch
        {
            return BrowserResolution.NotFound;
        }
    }

    private static IReadOnlyList<DetectedBrowser> DetectBrowsers()
    {
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var candidates = new[]
        {
            new BrowserCandidate("edge", "Microsoft Edge", "-inprivate", Path.Combine(programFilesX86, "Microsoft", "Edge", "Application", "msedge.exe")),
            new BrowserCandidate("edge", "Microsoft Edge", "-inprivate", Path.Combine(programFiles, "Microsoft", "Edge", "Application", "msedge.exe")),
            new BrowserCandidate("chrome", "Google Chrome", "--incognito", Path.Combine(programFiles, "Google", "Chrome", "Application", "chrome.exe")),
            new BrowserCandidate("chrome", "Google Chrome", "--incognito", Path.Combine(programFilesX86, "Google", "Chrome", "Application", "chrome.exe")),
            new BrowserCandidate("chrome", "Google Chrome", "--incognito", Path.Combine(localAppData, "Google", "Chrome", "Application", "chrome.exe")),
            new BrowserCandidate("yandex", "Яндекс Браузер", "--incognito", Path.Combine(localAppData, "Yandex", "YandexBrowser", "Application", "browser.exe")),
            new BrowserCandidate("firefox", "Mozilla Firefox", "-private-window", Path.Combine(programFiles, "Mozilla Firefox", "firefox.exe")),
            new BrowserCandidate("firefox", "Mozilla Firefox", "-private-window", Path.Combine(programFilesX86, "Mozilla Firefox", "firefox.exe")),
            new BrowserCandidate("brave", "Brave", "--incognito", Path.Combine(programFiles, "BraveSoftware", "Brave-Browser", "Application", "brave.exe")),
            new BrowserCandidate("brave", "Brave", "--incognito", Path.Combine(localAppData, "BraveSoftware", "Brave-Browser", "Application", "brave.exe")),
            new BrowserCandidate("vivaldi", "Vivaldi", "--incognito", Path.Combine(localAppData, "Vivaldi", "Application", "vivaldi.exe")),
            new BrowserCandidate("vivaldi", "Vivaldi", "--incognito", Path.Combine(programFiles, "Vivaldi", "Application", "vivaldi.exe")),
            new BrowserCandidate("opera", "Opera", "--private", Path.Combine(localAppData, "Programs", "Opera", "opera.exe")),
            new BrowserCandidate("opera-gx", "Opera GX", "--private", Path.Combine(localAppData, "Programs", "Opera GX", "opera.exe"))
        };

        var browsersFromKnownPaths = candidates
            .Where(candidate => File.Exists(candidate.ExecutablePath))
            .GroupBy(candidate => candidate.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .Select(candidate => new DetectedBrowser(
                candidate.Id,
                candidate.DisplayName,
                candidate.ExecutablePath,
                candidate.PrivateArgument))
            .ToArray();

        return DetectRegisteredBrowsers()
            .Concat(browsersFromKnownPaths)
            .GroupBy(browser => browser.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();
    }

    private static IEnumerable<DetectedBrowser> DetectRegisteredBrowsers()
    {
        var registrations = new (RegistryKey Root, string Path)[]
        {
            (Registry.CurrentUser, @"Software\Clients\StartMenuInternet"),
            (Registry.LocalMachine, @"Software\Clients\StartMenuInternet"),
            (Registry.LocalMachine, @"Software\WOW6432Node\Clients\StartMenuInternet")
        };

        foreach (var registration in registrations)
        {
            RegistryKey? clientsKey = null;
            try
            {
                clientsKey = registration.Root.OpenSubKey(registration.Path);
                if (clientsKey is null) continue;

                foreach (var clientName in clientsKey.GetSubKeyNames())
                {
                    using var clientKey = clientsKey.OpenSubKey(clientName);
                    using var commandKey = clientKey?.OpenSubKey(@"shell\open\command");
                    var executablePath = ExtractExecutablePath(commandKey?.GetValue(null) as string);
                    if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath)) continue;

                    var registeredName = clientKey?.GetValue(null) as string;
                    var browser = CreateRegisteredBrowser(
                        clientName,
                        registeredName,
                        executablePath);
                    if (browser is not null) yield return browser;
                }
            }
            finally
            {
                clientsKey?.Dispose();
            }
        }
    }

    private static DetectedBrowser? CreateRegisteredBrowser(
        string clientName,
        string? registeredName,
        string executablePath)
    {
        var identity = $"{clientName} {registeredName} {executablePath}".ToLowerInvariant();
        string id;
        string displayName;
        if (identity.Contains("yandex", StringComparison.Ordinal))
        {
            id = "yandex";
            displayName = "Яндекс Браузер";
        }
        else if (identity.Contains("edge", StringComparison.Ordinal))
        {
            id = "edge";
            displayName = "Microsoft Edge";
        }
        else if (identity.Contains("firefox", StringComparison.Ordinal))
        {
            var isDeveloperEdition = identity.Contains("developer", StringComparison.Ordinal);
            id = isDeveloperEdition ? "firefox-developer" : "firefox";
            displayName = isDeveloperEdition ? "Firefox Developer Edition" : "Mozilla Firefox";
        }
        else if (identity.Contains("brave", StringComparison.Ordinal))
        {
            id = "brave";
            displayName = "Brave";
        }
        else if (identity.Contains("vivaldi", StringComparison.Ordinal))
        {
            id = "vivaldi";
            displayName = "Vivaldi";
        }
        else if (identity.Contains("opera gx", StringComparison.Ordinal)
                 || identity.Contains("operagx", StringComparison.Ordinal))
        {
            id = "opera-gx";
            displayName = "Opera GX";
        }
        else if (identity.Contains("opera", StringComparison.Ordinal))
        {
            id = "opera";
            displayName = "Opera";
        }
        else if (identity.Contains("chrome", StringComparison.Ordinal))
        {
            id = "chrome";
            displayName = "Google Chrome";
        }
        else
        {
            return null;
        }

        return CreateBrowser(id, displayName, executablePath, identity);
    }

    private static DetectedBrowser? CreateBrowser(
        string id,
        string displayName,
        string executablePath,
        string identity)
    {
        var browserIdentity = $"{Path.GetFileNameWithoutExtension(executablePath)} {identity}"
            .ToLowerInvariant();
        var privateArgument = browserIdentity switch
        {
            var value when value.Contains("edge", StringComparison.Ordinal) => "-inprivate",
            var value when value.Contains("firefox", StringComparison.Ordinal) => "-private-window",
            var value when value.Contains("opera", StringComparison.Ordinal) => "--private",
            var value when value.Contains("chrome", StringComparison.Ordinal)
                           || value.Contains("chromium", StringComparison.Ordinal)
                           || value.Contains("brave", StringComparison.Ordinal)
                           || value.Contains("vivaldi", StringComparison.Ordinal)
                           || value.Contains("yandex", StringComparison.Ordinal) => "--incognito",
            _ => null
        };

        return privateArgument is null
            ? null
            : new DetectedBrowser(id, displayName, executablePath, privateArgument);
    }

    private static string? ExtractExecutablePath(string? command)
    {
        if (string.IsNullOrWhiteSpace(command)) return null;
        var match = ExecutablePattern.Match(command);
        if (!match.Success) return null;

        var path = match.Groups["quoted"].Success
            ? match.Groups["quoted"].Value
            : match.Groups["plain"].Value;
        return Environment.ExpandEnvironmentVariables(path.Trim());
    }

    private static string BuildTarget(string query, BrowserSearchEngine searchEngine)
    {
        if (Uri.TryCreate(query, UriKind.Absolute, out var uri)
            && uri.Scheme is "http" or "https")
        {
            return uri.AbsoluteUri;
        }

        var encodedQuery = Uri.EscapeDataString(query);
        return searchEngine switch
        {
            BrowserSearchEngine.Yandex => $"https://yandex.ru/search/?text={encodedQuery}",
            BrowserSearchEngine.Bing => $"https://www.bing.com/search?q={encodedQuery}",
            BrowserSearchEngine.DuckDuckGo => $"https://duckduckgo.com/?q={encodedQuery}",
            _ => $"https://www.google.com/search?q={encodedQuery}"
        };
    }

    private sealed record BrowserCandidate(
        string Id,
        string DisplayName,
        string PrivateArgument,
        string ExecutablePath);

    private sealed record DetectedBrowser(
        string Id,
        string DisplayName,
        string ExecutablePath,
        string PrivateArgument);

    private sealed record BrowserResolution(BrowserResolutionStatus Status, DetectedBrowser? Launch)
    {
        public static BrowserResolution NotFound { get; } = new(BrowserResolutionStatus.NotFound, null);

        public static BrowserResolution Unsupported { get; } = new(BrowserResolutionStatus.Unsupported, null);
    }

    private enum BrowserResolutionStatus
    {
        NotFound,
        Supported,
        Unsupported
    }
}
