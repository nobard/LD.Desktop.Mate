using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using Mate.Services.Interfaces;

namespace Mate.Services.Implementations;

public sealed class PrivateBrowserService : IPrivateBrowserService
{
    private static readonly Regex ExecutablePattern = new(
        "^\\s*(?:\"(?<quoted>[^\"]+\\.exe)\"|(?<plain>.+?\\.exe))",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public PrivateBrowserOpenResult OpenSearch(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return PrivateBrowserOpenResult.BrowserNotFound;

        var defaultBrowser = GetDefaultBrowser();
        var browser = defaultBrowser.Launch ?? FindFallbackBrowser();
        if (browser is null)
        {
            return defaultBrowser.Status == BrowserResolutionStatus.Unsupported
                ? PrivateBrowserOpenResult.UnsupportedBrowser
                : PrivateBrowserOpenResult.BrowserNotFound;
        }

        var target = BuildTarget(query.Trim());
        var startInfo = new ProcessStartInfo
        {
            FileName = browser.ExecutablePath,
            UseShellExecute = true
        };
        startInfo.ArgumentList.Add(browser.PrivateArgument);
        startInfo.ArgumentList.Add(target);

        Process.Start(startInfo);
        return PrivateBrowserOpenResult.Opened;
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

            var launch = CreateLaunch(executablePath, programId);
            return launch is null
                ? BrowserResolution.Unsupported
                : new BrowserResolution(BrowserResolutionStatus.Supported, launch);
        }
        catch
        {
            return BrowserResolution.NotFound;
        }
    }

    private static BrowserLaunch? FindFallbackBrowser()
    {
        foreach (var candidate in GetFallbackCandidates())
        {
            if (File.Exists(candidate.ExecutablePath)) return candidate;
        }

        return null;
    }

    private static IEnumerable<BrowserLaunch> GetFallbackCandidates()
    {
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        yield return new BrowserLaunch(Path.Combine(programFilesX86, "Microsoft", "Edge", "Application", "msedge.exe"), "-inprivate");
        yield return new BrowserLaunch(Path.Combine(programFiles, "Microsoft", "Edge", "Application", "msedge.exe"), "-inprivate");
        yield return new BrowserLaunch(Path.Combine(programFiles, "Google", "Chrome", "Application", "chrome.exe"), "--incognito");
        yield return new BrowserLaunch(Path.Combine(programFilesX86, "Google", "Chrome", "Application", "chrome.exe"), "--incognito");
        yield return new BrowserLaunch(Path.Combine(localAppData, "Google", "Chrome", "Application", "chrome.exe"), "--incognito");
        yield return new BrowserLaunch(Path.Combine(programFiles, "Mozilla Firefox", "firefox.exe"), "-private-window");
        yield return new BrowserLaunch(Path.Combine(programFilesX86, "Mozilla Firefox", "firefox.exe"), "-private-window");
        yield return new BrowserLaunch(Path.Combine(programFiles, "BraveSoftware", "Brave-Browser", "Application", "brave.exe"), "--incognito");
        yield return new BrowserLaunch(Path.Combine(localAppData, "BraveSoftware", "Brave-Browser", "Application", "brave.exe"), "--incognito");
    }

    private static BrowserLaunch? CreateLaunch(string executablePath, string programId)
    {
        var executableName = Path.GetFileNameWithoutExtension(executablePath).ToLowerInvariant();
        var browserIdentity = $"{executableName} {programId}".ToLowerInvariant();
        var privateArgument = browserIdentity switch
        {
            var identity when identity.Contains("edge", StringComparison.Ordinal) => "-inprivate",
            var identity when identity.Contains("firefox", StringComparison.Ordinal) => "-private-window",
            var identity when identity.Contains("opera", StringComparison.Ordinal) => "--private",
            var identity when identity.Contains("chrome", StringComparison.Ordinal)
                              || identity.Contains("chromium", StringComparison.Ordinal)
                              || identity.Contains("brave", StringComparison.Ordinal)
                              || identity.Contains("vivaldi", StringComparison.Ordinal)
                              || identity.Contains("yandex", StringComparison.Ordinal) => "--incognito",
            _ => null
        };

        return privateArgument is null ? null : new BrowserLaunch(executablePath, privateArgument);
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

    private static string BuildTarget(string query)
    {
        if (Uri.TryCreate(query, UriKind.Absolute, out var uri)
            && uri.Scheme is "http" or "https")
        {
            return uri.AbsoluteUri;
        }

        return $"https://www.google.com/search?q={Uri.EscapeDataString(query)}";
    }

    private sealed record BrowserLaunch(string ExecutablePath, string PrivateArgument);

    private sealed record BrowserResolution(BrowserResolutionStatus Status, BrowserLaunch? Launch)
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
