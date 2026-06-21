using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;

namespace AMID.Services;

public sealed class DownloadService
{
    private const string PartialExtension = ".amidpart";
    private static readonly HttpClient HttpClient = CreateHttpClient();

    public async Task<DownloadResult> DownloadAsync(
        DownloadRequest request,
        IProgress<DownloadProgress> progress,
        CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(request.Url, UriKind.Absolute, out Uri? uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("Only HTTP and HTTPS URLs are supported.", nameof(request));
        }

        Directory.CreateDirectory(request.DownloadFolder);

        long resumeFromBytes = GetResumeOffset(request);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, uri);
        if (resumeFromBytes > 0)
        {
            httpRequest.Headers.Range = new RangeHeaderValue(resumeFromBytes, null);
        }

        using HttpResponseMessage response = await HttpClient.SendAsync(
            httpRequest,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (resumeFromBytes > 0 && response.StatusCode != HttpStatusCode.PartialContent)
        {
            throw new ResumeNotSupportedException("The server did not honor the HTTP range request needed to resume this download.");
        }

        response.EnsureSuccessStatusCode();

        string fileName = GetDownloadFileName(request, response, uri);
        string destinationPath = GetDestinationPath(request, fileName);
        string partialPath = GetPartialPath(request, destinationPath);
        long? totalBytes = GetTotalBytes(response, resumeFromBytes);
        bool supportsResume = SupportsRangeRequests(response);
        string status = supportsResume ? "Downloading" : "Downloading";

        progress.Report(new DownloadProgress(
            fileName,
            destinationPath,
            partialPath,
            resumeFromBytes,
            totalBytes,
            0,
            status,
            supportsResume));

        byte[] buffer = ArrayPool<byte>.Shared.Rent(81920);
        var stopwatch = Stopwatch.StartNew();
        long downloadedBytes = resumeFromBytes;
        long bytesAtLastReport = resumeFromBytes;
        TimeSpan lastReportTime = TimeSpan.Zero;

        try
        {
            await using Stream contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using (var fileStream = new FileStream(
                partialPath,
                resumeFromBytes > 0 ? FileMode.OpenOrCreate : FileMode.Create,
                FileAccess.Write,
                FileShare.Read,
                bufferSize: 81920,
                useAsync: true))
            {
                if (resumeFromBytes > 0)
                {
                    fileStream.Seek(resumeFromBytes, SeekOrigin.Begin);
                }

                while (true)
                {
                    int bytesRead = await contentStream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
                    if (bytesRead == 0)
                    {
                        break;
                    }

                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                    downloadedBytes += bytesRead;

                    TimeSpan elapsed = stopwatch.Elapsed;
                    if (elapsed - lastReportTime >= TimeSpan.FromMilliseconds(250))
                    {
                        double seconds = Math.Max((elapsed - lastReportTime).TotalSeconds, 0.001);
                        double speed = (downloadedBytes - bytesAtLastReport) / seconds;
                        progress.Report(new DownloadProgress(
                            fileName,
                            destinationPath,
                            partialPath,
                            downloadedBytes,
                            totalBytes,
                            speed,
                            "Downloading",
                            supportsResume));

                        bytesAtLastReport = downloadedBytes;
                        lastReportTime = elapsed;
                    }
                }

                await fileStream.FlushAsync(cancellationToken);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        string finalDestinationPath = MoveCompletedDownload(partialPath, destinationPath);

        progress.Report(new DownloadProgress(
            fileName,
            finalDestinationPath,
            string.Empty,
            downloadedBytes,
            totalBytes,
            0,
            "Completed",
            supportsResume));

        return new DownloadResult(fileName, finalDestinationPath, downloadedBytes, totalBytes, supportsResume);
    }

    public static string GetSuggestedFileName(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out Uri? uri)
            ? GetUrlFileName(uri)
            : "download";
    }

    public static void DeletePartialFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return;
        }

        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static HttpClient CreateHttpClient()
    {
        var httpClient = new HttpClient
        {
            Timeout = Timeout.InfiniteTimeSpan
        };

        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("AMID/0.1");
        return httpClient;
    }

    private static long GetResumeOffset(DownloadRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.PartialPath) || !File.Exists(request.PartialPath))
        {
            return 0;
        }

        return Math.Max(0, new FileInfo(request.PartialPath).Length);
    }

    private static string GetDownloadFileName(DownloadRequest request, HttpResponseMessage response, Uri uri)
    {
        if (!string.IsNullOrWhiteSpace(request.FileName))
        {
            return SanitizeFileName(request.FileName);
        }

        string? headerFileName = response.Content.Headers.ContentDisposition?.FileNameStar
                                 ?? response.Content.Headers.ContentDisposition?.FileName;

        if (!string.IsNullOrWhiteSpace(headerFileName))
        {
            return SanitizeFileName(headerFileName.Trim('"'));
        }

        return GetUrlFileName(uri);
    }

    private static string GetDestinationPath(DownloadRequest request, string fileName)
    {
        if (!string.IsNullOrWhiteSpace(request.DestinationPath))
        {
            return request.DestinationPath;
        }

        return GetAvailableFilePath(request.DownloadFolder, fileName);
    }

    private static string GetPartialPath(DownloadRequest request, string destinationPath)
    {
        if (!string.IsNullOrWhiteSpace(request.PartialPath))
        {
            return request.PartialPath;
        }

        return destinationPath + PartialExtension;
    }

    private static long? GetTotalBytes(HttpResponseMessage response, long resumeFromBytes)
    {
        if (response.Content.Headers.ContentRange?.Length is long contentRangeLength)
        {
            return contentRangeLength;
        }

        if (response.Content.Headers.ContentLength is long contentLength)
        {
            return resumeFromBytes + contentLength;
        }

        return null;
    }

    private static bool SupportsRangeRequests(HttpResponseMessage response)
    {
        return response.StatusCode == HttpStatusCode.PartialContent
               || response.Content.Headers.ContentRange is not null
               || response.Headers.AcceptRanges.Any(value =>
                   string.Equals(value, "bytes", StringComparison.OrdinalIgnoreCase));
    }

    private static string MoveCompletedDownload(string partialPath, string destinationPath)
    {
        if (File.Exists(destinationPath))
        {
            string folder = Path.GetDirectoryName(destinationPath) ?? string.Empty;
            string fileName = Path.GetFileName(destinationPath);
            destinationPath = GetAvailableFilePath(folder, fileName);
        }

        File.Move(partialPath, destinationPath);
        return destinationPath;
    }

    private static string GetUrlFileName(Uri uri)
    {
        string fileName = Path.GetFileName(uri.LocalPath);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            fileName = $"download-{DateTime.Now:yyyyMMdd-HHmmss}";
        }

        return SanitizeFileName(Uri.UnescapeDataString(fileName));
    }

    private static string SanitizeFileName(string fileName)
    {
        char[] invalidCharacters = Path.GetInvalidFileNameChars();
        string sanitized = new(fileName.Select(character =>
            invalidCharacters.Contains(character) ? '_' : character).ToArray());

        sanitized = sanitized.Trim().TrimEnd('.');
        if (string.IsNullOrWhiteSpace(sanitized))
        {
            return "download";
        }

        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(sanitized);
        if (IsReservedWindowsFileName(fileNameWithoutExtension))
        {
            sanitized = $"download-{sanitized}";
        }

        return sanitized.Length <= 180 ? sanitized : sanitized[..180];
    }

    private static bool IsReservedWindowsFileName(string fileName)
    {
        string[] reservedNames =
        [
            "CON",
            "PRN",
            "AUX",
            "NUL",
            "COM1",
            "COM2",
            "COM3",
            "COM4",
            "COM5",
            "COM6",
            "COM7",
            "COM8",
            "COM9",
            "LPT1",
            "LPT2",
            "LPT3",
            "LPT4",
            "LPT5",
            "LPT6",
            "LPT7",
            "LPT8",
            "LPT9"
        ];

        return reservedNames.Any(name =>
            string.Equals(name, fileName, StringComparison.OrdinalIgnoreCase));
    }

    private static string GetAvailableFilePath(string folder, string fileName)
    {
        string candidate = Path.Combine(folder, fileName);
        if (!File.Exists(candidate) && !File.Exists(candidate + PartialExtension))
        {
            return candidate;
        }

        string baseName = Path.GetFileNameWithoutExtension(fileName);
        string extension = Path.GetExtension(fileName);

        for (int index = 1; ; index++)
        {
            candidate = Path.Combine(folder, $"{baseName} ({index}){extension}");
            if (!File.Exists(candidate) && !File.Exists(candidate + PartialExtension))
            {
                return candidate;
            }
        }
    }
}

public sealed record DownloadRequest(
    string Url,
    string DownloadFolder,
    string FileName,
    string DestinationPath,
    string PartialPath);

public sealed record DownloadProgress(
    string FileName,
    string DestinationPath,
    string PartialPath,
    long DownloadedBytes,
    long? TotalBytes,
    double SpeedBytesPerSecond,
    string Status,
    bool SupportsResume);

public sealed record DownloadResult(
    string FileName,
    string DestinationPath,
    long DownloadedBytes,
    long? TotalBytes,
    bool SupportsResume);

public sealed class ResumeNotSupportedException : Exception
{
    public ResumeNotSupportedException(string message)
        : base(message)
    {
    }
}
