using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;

namespace AMID.Services;

public sealed class GitHubUpdateService
{
    private const string Owner = "dylanchromedome";
    private const string Repository = "amid";
    private const string LatestReleaseUrl = $"https://api.github.com/repos/{Owner}/{Repository}/releases/latest";
    private static readonly HttpClient HttpClient = CreateHttpClient();

    public Version CurrentVersion { get; } = NormalizeVersion(
        Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0));

    public string CurrentVersionText => CurrentVersion.ToString(3);

    public async Task<GitHubUpdateInfo?> CheckForUpdateAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseUrl);
        request.Headers.Accept.ParseAdd("application/vnd.github+json");

        using HttpResponseMessage response = await HttpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using JsonDocument document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        JsonElement release = document.RootElement;

        if (release.TryGetProperty("draft", out JsonElement draftElement) && draftElement.GetBoolean())
        {
            return null;
        }

        if (release.TryGetProperty("prerelease", out JsonElement prereleaseElement) && prereleaseElement.GetBoolean())
        {
            return null;
        }

        string tagName = GetStringProperty(release, "tag_name");
        if (!TryParseReleaseVersion(tagName, out Version? releaseVersion)
            || releaseVersion is null
            || releaseVersion.CompareTo(CurrentVersion) <= 0)
        {
            return null;
        }

        GitHubReleaseAsset? asset = FindPortableZipAsset(release);
        if (asset is null)
        {
            throw new InvalidOperationException(
                $"GitHub release {tagName} does not include a portable .zip asset.");
        }

        return new GitHubUpdateInfo(
            tagName,
            releaseVersion,
            GetStringProperty(release, "name"),
            GetStringProperty(release, "html_url"),
            GetStringProperty(release, "body"),
            asset.Name,
            asset.DownloadUrl,
            asset.Size);
    }

    public async Task<string> DownloadUpdateAsync(
        GitHubUpdateInfo updateInfo,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        string updateFolder = Path.Combine(Path.GetTempPath(), "AMID", "Updates");
        Directory.CreateDirectory(updateFolder);

        string zipPath = Path.Combine(updateFolder, SanitizeFileName(updateInfo.AssetName));

        using var request = new HttpRequestMessage(HttpMethod.Get, updateInfo.AssetDownloadUrl);
        using HttpResponseMessage response = await HttpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        long? totalBytes = response.Content.Headers.ContentLength;
        long downloadedBytes = 0;
        byte[] buffer = new byte[81920];

        await using Stream input = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var output = new FileStream(
            zipPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: buffer.Length,
            useAsync: true);

        while (true)
        {
            int bytesRead = await input.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
            if (bytesRead == 0)
            {
                break;
            }

            await output.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            downloadedBytes += bytesRead;

            if (totalBytes is > 0)
            {
                progress?.Report(Math.Min(100, downloadedBytes * 100d / totalBytes.Value));
            }
        }

        progress?.Report(100);
        return zipPath;
    }

    public void LaunchUpdater(string zipPath, int processId)
    {
        string installDir = AppContext.BaseDirectory.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);
        string updaterScriptPath = Path.Combine(installDir, "apply-update.ps1");
        if (!File.Exists(updaterScriptPath))
        {
            throw new FileNotFoundException(
                "The updater script was not found next to AMID.exe.",
                updaterScriptPath);
        }

        string exePath = Process.GetCurrentProcess().MainModule?.FileName
                         ?? Path.Combine(installDir, "AMID.exe");
        string arguments = string.Join(
            " ",
            "-NoProfile",
            "-ExecutionPolicy Bypass",
            "-File",
            QuoteArgument(updaterScriptPath),
            "-ZipPath",
            QuoteArgument(zipPath),
            "-InstallDir",
            QuoteArgument(installDir),
            "-ProcessId",
            processId.ToString(),
            "-ExePath",
            QuoteArgument(exePath));

        Process.Start(new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        });
    }

    private static HttpClient CreateHttpClient()
    {
        var httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(20)
        };

        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("AMID-Updater/1.0");
        return httpClient;
    }

    private static GitHubReleaseAsset? FindPortableZipAsset(JsonElement release)
    {
        if (!release.TryGetProperty("assets", out JsonElement assets)
            || assets.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        List<GitHubReleaseAsset> zipAssets = assets.EnumerateArray()
            .Select(CreateAsset)
            .Where(asset => asset is not null)
            .Cast<GitHubReleaseAsset>()
            .Where(asset => asset.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            .ToList();

        return zipAssets
                   .OrderByDescending(asset => asset.Name.Contains("portable", StringComparison.OrdinalIgnoreCase))
                   .ThenByDescending(asset => asset.Name.Contains("win-x64", StringComparison.OrdinalIgnoreCase))
                   .FirstOrDefault();
    }

    private static GitHubReleaseAsset? CreateAsset(JsonElement asset)
    {
        string name = GetStringProperty(asset, "name");
        string downloadUrl = GetStringProperty(asset, "browser_download_url");
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(downloadUrl))
        {
            return null;
        }

        long? size = null;
        if (asset.TryGetProperty("size", out JsonElement sizeElement)
            && sizeElement.TryGetInt64(out long parsedSize))
        {
            size = parsedSize;
        }

        return new GitHubReleaseAsset(name, downloadUrl, size);
    }

    private static bool TryParseReleaseVersion(string tagName, out Version? version)
    {
        version = null;
        string normalized = tagName.Trim();
        if (normalized.StartsWith('v') || normalized.StartsWith('V'))
        {
            normalized = normalized[1..];
        }

        int prereleaseIndex = normalized.IndexOfAny(['-', '+']);
        if (prereleaseIndex >= 0)
        {
            normalized = normalized[..prereleaseIndex];
        }

        if (!Version.TryParse(normalized, out Version? parsedVersion))
        {
            return false;
        }

        version = NormalizeVersion(parsedVersion);
        return true;
    }

    private static Version NormalizeVersion(Version version)
    {
        return new Version(
            Math.Max(0, version.Major),
            Math.Max(0, version.Minor),
            Math.Max(0, version.Build));
    }

    private static string GetStringProperty(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out JsonElement property)
               && property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? string.Empty
            : string.Empty;
    }

    private static string SanitizeFileName(string fileName)
    {
        char[] invalidCharacters = Path.GetInvalidFileNameChars();
        string sanitized = new(fileName.Select(character =>
            invalidCharacters.Contains(character) ? '_' : character).ToArray());

        return string.IsNullOrWhiteSpace(sanitized)
            ? "AMID-update.zip"
            : sanitized;
    }

    private static string QuoteArgument(string value)
    {
        return $"\"{value.Replace("\"", "\\\"", StringComparison.Ordinal)}\"";
    }

    private sealed record GitHubReleaseAsset(string Name, string DownloadUrl, long? Size);
}

public sealed record GitHubUpdateInfo(
    string TagName,
    Version Version,
    string Name,
    string ReleaseUrl,
    string Body,
    string AssetName,
    string AssetDownloadUrl,
    long? AssetSize)
{
    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? TagName : Name;

    public string AssetSizeText => AssetSize is > 0
        ? FormatBytes(AssetSize.Value)
        : "Unknown size";

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = bytes;
        int unitIndex = 0;

        while (value >= 1024 && unitIndex < units.Length - 1)
        {
            value /= 1024;
            unitIndex++;
        }

        return unitIndex == 0
            ? $"{bytes} {units[unitIndex]}"
            : $"{value:0.##} {units[unitIndex]}";
    }
}
