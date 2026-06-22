using System.IO;
using System.IO.Pipes;
using System.Security.Principal;
using System.Text;

namespace AMID.Services;

public sealed class SingleInstanceService : IDisposable
{
    private static readonly string InstanceKey = CreateInstanceKey();
    private static readonly string MutexName = $@"Local\AMID.SingleInstance.{InstanceKey}";
    private static readonly string PipeName = $"AMID.SingleInstance.{InstanceKey}";

    private readonly Mutex _mutex;
    private readonly bool _ownsInstance;
    private CancellationTokenSource? _listenerCancellationTokenSource;
    private Task? _listenerTask;
    private bool _disposed;

    public event Action? ActivationRequested;

    public SingleInstanceService()
    {
        _mutex = new Mutex(initiallyOwned: true, MutexName, out _ownsInstance);
    }

    public bool TryAcquire()
    {
        return _ownsInstance;
    }

    public void StartActivationListener()
    {
        if (!_ownsInstance || _listenerTask is not null)
        {
            return;
        }

        _listenerCancellationTokenSource = new CancellationTokenSource();
        _listenerTask = Task.Run(() => ListenForActivationRequestsAsync(_listenerCancellationTokenSource.Token));
    }

    public static void SignalExistingInstance()
    {
        try
        {
            using var client = new NamedPipeClientStream(
                ".",
                PipeName,
                PipeDirection.Out,
                PipeOptions.Asynchronous);
            client.Connect(timeout: 1500);

            using var writer = new StreamWriter(client, Encoding.UTF8)
            {
                AutoFlush = true
            };
            writer.WriteLine("activate");
        }
        catch
        {
            // If the first process is still starting, the mutex is enough to prevent a second UI.
        }
    }

    private async Task ListenForActivationRequestsAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using var server = new NamedPipeServerStream(
                    PipeName,
                    PipeDirection.In,
                    maxNumberOfServerInstances: 1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                await server.WaitForConnectionAsync(cancellationToken);

                using var reader = new StreamReader(server, Encoding.UTF8);
                string? message = await reader.ReadLineAsync(cancellationToken);
                if (string.Equals(message, "activate", StringComparison.OrdinalIgnoreCase))
                {
                    ActivationRequested?.Invoke();
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                AppLog.Add($"Single-instance listener error: {ex.Message}");
                try
                {
                    await Task.Delay(500, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _listenerCancellationTokenSource?.Cancel();
        _listenerCancellationTokenSource?.Dispose();

        if (_ownsInstance)
        {
            _mutex.ReleaseMutex();
        }

        _mutex.Dispose();
    }

    private static string CreateInstanceKey()
    {
        string userKey;
        try
        {
            userKey = WindowsIdentity.GetCurrent().User?.Value ?? Environment.UserName;
        }
        catch
        {
            userKey = Environment.UserName;
        }

        var builder = new StringBuilder(userKey.Length);
        foreach (char character in userKey)
        {
            builder.Append(char.IsLetterOrDigit(character) ? character : '_');
        }

        return builder.Length > 0 ? builder.ToString() : "DefaultUser";
    }
}
