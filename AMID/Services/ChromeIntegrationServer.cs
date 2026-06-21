using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace AMID.Services;

public sealed class ChromeIntegrationServer
{
    public const int Port = 51234;
    private const int MaxRequestBytes = 1024 * 1024;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly Func<ChromeDownloadBridgeRequest, CancellationToken, Task<ChromeDownloadBridgeResult>> _downloadHandler;
    private CancellationTokenSource? _cancellationTokenSource;
    private TcpListener? _listener;

    public ChromeIntegrationServer(
        Func<ChromeDownloadBridgeRequest, CancellationToken, Task<ChromeDownloadBridgeResult>> downloadHandler)
    {
        _downloadHandler = downloadHandler;
    }

    public bool IsRunning { get; private set; }

    public void Start()
    {
        if (IsRunning)
        {
            return;
        }

        _cancellationTokenSource = new CancellationTokenSource();
        _listener = new TcpListener(IPAddress.Loopback, Port);
        _listener.Start();
        IsRunning = true;
        _ = AcceptLoopAsync(_cancellationTokenSource.Token);
    }

    public void Stop()
    {
        if (!IsRunning)
        {
            return;
        }

        IsRunning = false;
        _cancellationTokenSource?.Cancel();
        _listener?.Stop();
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;
        _listener = null;
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _listener is not null)
        {
            try
            {
                TcpClient client = await _listener.AcceptTcpClientAsync(cancellationToken);
                _ = HandleClientAsync(client, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        await using NetworkStream stream = client.GetStream();
        using (client)
        {
            try
            {
                HttpRequestData request = await ReadRequestAsync(stream, cancellationToken);

                if (request.Method == "OPTIONS")
                {
                    await WriteJsonResponseAsync(stream, HttpStatusCode.NoContent, null, cancellationToken);
                    return;
                }

                if (request.Method == "GET" && request.Path == "/health")
                {
                    await WriteJsonResponseAsync(
                        stream,
                        HttpStatusCode.OK,
                        new { app = "AMID", running = true },
                        cancellationToken);
                    return;
                }

                if (request.Method == "POST" && request.Path == "/api/downloads")
                {
                    await HandleDownloadRequestAsync(stream, request, cancellationToken);
                    return;
                }

                await WriteJsonResponseAsync(
                    stream,
                    HttpStatusCode.NotFound,
                    new { accepted = false, message = "Unknown endpoint." },
                    cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                await WriteJsonResponseAsync(
                    stream,
                    HttpStatusCode.BadRequest,
                    new { accepted = false, message = ex.Message },
                    CancellationToken.None);
            }
        }
    }

    private async Task HandleDownloadRequestAsync(
        Stream stream,
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        ChromeDownloadBridgeRequest? bridgeRequest =
            JsonSerializer.Deserialize<ChromeDownloadBridgeRequest>(request.Body, JsonOptions);

        if (bridgeRequest is null || string.IsNullOrWhiteSpace(bridgeRequest.Url))
        {
            await WriteJsonResponseAsync(
                stream,
                HttpStatusCode.BadRequest,
                ChromeDownloadBridgeResult.CreateRejected("Missing URL."),
                cancellationToken);
            return;
        }

        ChromeDownloadBridgeResult result = await _downloadHandler(bridgeRequest, cancellationToken);
        await WriteJsonResponseAsync(stream, HttpStatusCode.OK, result, cancellationToken);
    }

    private static async Task<HttpRequestData> ReadRequestAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[8192];
        using var memoryStream = new MemoryStream();
        int headerEndIndex = -1;

        while (headerEndIndex < 0)
        {
            int bytesRead = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
            if (bytesRead == 0)
            {
                throw new InvalidOperationException("Empty HTTP request.");
            }

            memoryStream.Write(buffer, 0, bytesRead);
            if (memoryStream.Length > MaxRequestBytes)
            {
                throw new InvalidOperationException("HTTP request is too large.");
            }

            headerEndIndex = FindHeaderEnd(memoryStream.GetBuffer(), (int)memoryStream.Length);
        }

        byte[] requestBytes = memoryStream.ToArray();
        string headerText = Encoding.ASCII.GetString(requestBytes, 0, headerEndIndex);
        string[] headerLines = headerText.Split("\r\n", StringSplitOptions.None);
        string[] requestLineParts = headerLines[0].Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
        if (requestLineParts.Length < 2)
        {
            throw new InvalidOperationException("Invalid HTTP request line.");
        }

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int index = 1; index < headerLines.Length; index++)
        {
            string line = headerLines[index];
            int separatorIndex = line.IndexOf(':');
            if (separatorIndex <= 0)
            {
                continue;
            }

            string name = line[..separatorIndex].Trim();
            string value = line[(separatorIndex + 1)..].Trim();
            headers[name] = value;
        }

        int bodyStart = headerEndIndex + 4;
        int contentLength = headers.TryGetValue("Content-Length", out string? contentLengthText)
                            && int.TryParse(contentLengthText, out int parsedLength)
            ? parsedLength
            : 0;
        if (contentLength > MaxRequestBytes)
        {
            throw new InvalidOperationException("HTTP request body is too large.");
        }

        using var bodyStream = new MemoryStream();
        int alreadyReadBodyBytes = Math.Max(0, requestBytes.Length - bodyStart);
        if (alreadyReadBodyBytes > 0)
        {
            bodyStream.Write(requestBytes, bodyStart, Math.Min(alreadyReadBodyBytes, contentLength));
        }

        while (bodyStream.Length < contentLength)
        {
            int bytesRead = await stream.ReadAsync(buffer.AsMemory(0, Math.Min(buffer.Length, contentLength - (int)bodyStream.Length)), cancellationToken);
            if (bytesRead == 0)
            {
                break;
            }

            bodyStream.Write(buffer, 0, bytesRead);
        }

        string path = requestLineParts[1].Split('?', 2)[0];
        return new HttpRequestData(
            requestLineParts[0].ToUpperInvariant(),
            path,
            Encoding.UTF8.GetString(bodyStream.ToArray()));
    }

    private static int FindHeaderEnd(byte[] buffer, int count)
    {
        for (int index = 0; index <= count - 4; index++)
        {
            if (buffer[index] == '\r'
                && buffer[index + 1] == '\n'
                && buffer[index + 2] == '\r'
                && buffer[index + 3] == '\n')
            {
                return index;
            }
        }

        return -1;
    }

    private static async Task WriteJsonResponseAsync(
        Stream stream,
        HttpStatusCode statusCode,
        object? body,
        CancellationToken cancellationToken)
    {
        byte[] bodyBytes = body is null
            ? []
            : JsonSerializer.SerializeToUtf8Bytes(body, JsonOptions);

        string header =
            $"HTTP/1.1 {(int)statusCode} {statusCode}\r\n" +
            "Access-Control-Allow-Origin: *\r\n" +
            "Access-Control-Allow-Methods: GET, POST, OPTIONS\r\n" +
            "Access-Control-Allow-Headers: content-type\r\n" +
            "Connection: close\r\n" +
            "Content-Type: application/json; charset=utf-8\r\n" +
            $"Content-Length: {bodyBytes.Length}\r\n\r\n";

        byte[] headerBytes = Encoding.ASCII.GetBytes(header);
        await stream.WriteAsync(headerBytes, cancellationToken);
        if (bodyBytes.Length > 0)
        {
            await stream.WriteAsync(bodyBytes, cancellationToken);
        }
    }

    private sealed record HttpRequestData(string Method, string Path, string Body);
}

public sealed record ChromeDownloadBridgeRequest(
    string? Url,
    int? ChromeDownloadId,
    string? Filename,
    string? Mime,
    string? Referrer);

public sealed record ChromeDownloadBridgeResult(
    bool Accepted,
    string Message,
    string? FileName)
{
    public static ChromeDownloadBridgeResult CreateAccepted(string fileName)
    {
        return new ChromeDownloadBridgeResult(true, "Download accepted by AMID.", fileName);
    }

    public static ChromeDownloadBridgeResult CreateRejected(string message)
    {
        return new ChromeDownloadBridgeResult(false, message, null);
    }
}
