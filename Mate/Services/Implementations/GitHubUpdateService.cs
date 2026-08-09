using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Mate.Models;
using Mate.Services.Interfaces;

namespace Mate.Services.Implementations;

public sealed class GitHubUpdateService : IUpdateService, IDisposable
{
    private const string LatestReleaseEndpoint =
        "https://api.github.com/repos/nobard/LD.Desktop.Mate/releases/latest";

    private const string ReleasesPage =
        "https://github.com/nobard/LD.Desktop.Mate/releases";

    private readonly HttpClient _httpClient;

    public GitHubUpdateService()
    {
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(10)
        };
        _httpClient.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("LD.Desktop.Mate", GetVersionText(GetCurrentVersion())));
        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        _httpClient.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
    }

    public async Task<UpdateCheckResult> CheckForUpdateAsync(
        CancellationToken cancellationToken = default)
    {
        var currentVersion = GetCurrentVersion();
        using var checkCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken);
        checkCancellation.CancelAfter(TimeSpan.FromSeconds(12));
        using var response = await _httpClient.GetAsync(
            LatestReleaseEndpoint,
            checkCancellation.Token);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return new UpdateCheckResult(currentVersion, null, null, null);
        }

        response.EnsureSuccessStatusCode();
        await using var content = await response.Content.ReadAsStreamAsync(cancellationToken);
        var release = await JsonSerializer.DeserializeAsync<GitHubReleaseResponse>(
            content,
            cancellationToken: checkCancellation.Token);

        if (release is null || !TryParseVersion(release.TagName, out var latestVersion))
        {
            throw new InvalidDataException("GitHub returned a release with an invalid version tag.");
        }

        var releasePage = TryCreateHttpsUri(release.HtmlUrl)
                          ?? new Uri(ReleasesPage, UriKind.Absolute);
        var expectedInstallerName = $"Mate-Setup-{GetVersionText(latestVersion)}.exe";
        var installerAsset = release.Assets?.FirstOrDefault(
                                 asset => string.Equals(
                                     asset.Name,
                                     expectedInstallerName,
                                     StringComparison.OrdinalIgnoreCase))
                             ?? release.Assets?.FirstOrDefault(
                                 asset => asset.Name?.StartsWith(
                                              "Mate-Setup-",
                                              StringComparison.OrdinalIgnoreCase) == true
                                          && asset.Name.EndsWith(
                                              ".exe",
                                              StringComparison.OrdinalIgnoreCase));
        var installerDownloadUri = TryCreateHttpsUri(installerAsset?.BrowserDownloadUrl);

        return new UpdateCheckResult(
            currentVersion,
            latestVersion,
            releasePage,
            installerDownloadUri);
    }

    public async Task<string> DownloadInstallerAsync(
        Uri installerDownloadUri,
        Version version,
        CancellationToken cancellationToken = default)
    {
        if (!installerDownloadUri.IsAbsoluteUri
            || installerDownloadUri.Scheme != Uri.UriSchemeHttps)
        {
            throw new ArgumentException(
                "The update installer must use HTTPS.",
                nameof(installerDownloadUri));
        }

        var updateDirectory = Path.Combine(
            Path.GetTempPath(),
            "LD.Desktop.Mate",
            "Updates");
        Directory.CreateDirectory(updateDirectory);

        var installerPath = Path.Combine(
            updateDirectory,
            $"Mate-Setup-{GetVersionText(version)}.exe");
        var temporaryPath = installerPath + ".download";

        try
        {
            using var response = await _httpClient.GetAsync(
                installerDownloadUri,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using (var destination = new FileStream(
                             temporaryPath,
                             FileMode.Create,
                             FileAccess.Write,
                             FileShare.None,
                             bufferSize: 81920,
                             useAsync: true))
            {
                await source.CopyToAsync(destination, cancellationToken);
            }

            if (new FileInfo(temporaryPath).Length == 0)
            {
                throw new InvalidDataException("The downloaded installer is empty.");
            }

            File.Move(temporaryPath, installerPath, overwrite: true);
            return installerPath;
        }
        catch
        {
            try
            {
                File.Delete(temporaryPath);
            }
            catch
            {
                // Cleanup failure must not hide the original download error.
            }

            throw;
        }
    }

    private static Version GetCurrentVersion()
    {
        var version = Assembly.GetEntryAssembly()?.GetName().Version;
        return version ?? new Version(0, 0, 0);
    }

    private static bool TryParseVersion(string? tagName, out Version version)
    {
        version = new Version(0, 0, 0);
        if (string.IsNullOrWhiteSpace(tagName)) return false;

        var value = tagName.Trim();
        if (value.StartsWith('v') || value.StartsWith('V')) value = value[1..];

        var suffixIndex = value.IndexOfAny(['-', '+']);
        if (suffixIndex >= 0) value = value[..suffixIndex];

        return Version.TryParse(value, out version!);
    }

    private static Uri? TryCreateHttpsUri(string? value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)) return null;
        return uri.Scheme == Uri.UriSchemeHttps ? uri : null;
    }

    private static string GetVersionText(Version version)
    {
        var build = version.Build >= 0 ? version.Build : 0;
        return $"{version.Major}.{version.Minor}.{build}";
    }

    public void Dispose() => _httpClient.Dispose();

    private sealed class GitHubReleaseResponse
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; init; }

        [JsonPropertyName("html_url")]
        public string? HtmlUrl { get; init; }

        [JsonPropertyName("assets")]
        public IReadOnlyList<GitHubReleaseAsset>? Assets { get; init; }
    }

    private sealed class GitHubReleaseAsset
    {
        [JsonPropertyName("name")]
        public string? Name { get; init; }

        [JsonPropertyName("browser_download_url")]
        public string? BrowserDownloadUrl { get; init; }
    }
}
