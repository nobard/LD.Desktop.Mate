using System;
using System.Collections.Generic;

namespace Mate.Services.Interfaces;

public sealed record BrowserOption(string Id, string DisplayName);

public enum BrowserSearchEngine
{
    Google,
    Yandex,
    Bing,
    DuckDuckGo
}

public sealed record SearchEngineOption(
    BrowserSearchEngine Engine,
    string DisplayName);

public sealed record BrowserLaunchSettings(
    string BrowserId,
    BrowserSearchEngine SearchEngine,
    bool UsePrivateMode)
{
    public static BrowserLaunchSettings Default { get; } = new(
        "default",
        BrowserSearchEngine.Google,
        true);
}

public interface IPrivateBrowserService
{
    IReadOnlyList<BrowserOption> AvailableBrowsers { get; }

    IReadOnlyList<SearchEngineOption> AvailableSearchEngines { get; }

    BrowserLaunchSettings Settings { get; }

    event EventHandler? SettingsChanged;

    void SetBrowser(string browserId);

    void SetSearchEngine(BrowserSearchEngine searchEngine);

    void SetPrivateMode(bool usePrivateMode);

    PrivateBrowserOpenResult OpenSearch(string query);
}

public enum PrivateBrowserOpenResult
{
    Opened,
    UnsupportedBrowser,
    BrowserNotFound
}
