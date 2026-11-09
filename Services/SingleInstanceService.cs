using System.IO.Pipes;
using System.IO;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;

namespace ShadeFlow.Services;

/// <summary>Per-user mutex plus a tiny local pipe used only to activate the first instance.</summary>
public sealed class SingleInstanceService : IDisposable
{
    public const string ShowQuickPanelCommand = "SHOW_QUICK_PANEL";
    public const string ShowSettingsCommand = "SHOW_SETTINGS";
    private readonly string _mutexName;
    private readonly string _pipeName;
    private Mutex? _mutex;
    private CancellationTokenSource? _cancellation;
    private Task? _listener;
    private bool _ownsMutex;
    private bool _disposed;

    public SingleInstanceService() : this(CreateUserScopedName()) { }

    internal SingleInstanceService(string scope)
    {
        _mutexName = OperatingSystem.IsWindows() ? $"Local\\ShadeFlow.{scope}" : $"ShadeFlow.{scope}";
        _pipeName = $"ShadeFlow.{scope}";
    }

    public event Action<string>? CommandRequested;

    public bool TryAcquire()
    {
        ThrowIfDisposed();
        _mutex = new Mutex(initiallyOwned: true, _mutexName, out var createdNew);
        _ownsMutex = createdNew;
        if (!createdNew) return false;

        _cancellation = new CancellationTokenSource();
        _listener = Task.Run(() => ListenAsync(_cancellation.Token));
        return true;
    }

    public Task<bool> TryActivateExistingAsync(TimeSpan timeout) =>
        TrySendCommandAsync(ShowQuickPanelCommand, timeout);

    public async Task<bool> TrySendCommandAsync(string command, TimeSpan timeout)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", _pipeName, PipeDirection.Out, PipeOptions.Asynchronous);
            using var timeoutSource = new CancellationTokenSource(timeout);
            await client.ConnectAsync(timeoutSource.Token).ConfigureAwait(false);
            var bytes = Encoding.UTF8.GetBytes(command);
            await client.WriteAsync(bytes, timeoutSource.Token).ConfigureAwait(false);
            await client.FlushAsync(timeoutSource.Token).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex) when (ex is IOException or TimeoutException or OperationCanceledException or UnauthorizedAccessException
            or System.Net.Sockets.SocketException)
        {
            return false;
        }
    }

    private async Task ListenAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using var server = new NamedPipeServerStream(_pipeName, PipeDirection.In, 1,
                    PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                await server.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
                var buffer = new byte[32];
                var read = await server.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                var command = Encoding.UTF8.GetString(buffer, 0, read);
                if ((command == ShowQuickPanelCommand || command == ShowSettingsCommand) &&
                    !cancellationToken.IsCancellationRequested)
                    CommandRequested?.Invoke(command);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { break; }
            catch (IOException) when (!cancellationToken.IsCancellationRequested)
            {
                // A short-lived client can disconnect mid-request; the next loop recreates the server.
            }
        }
    }

    private static string CreateUserScopedName()
    {
        var identity = Environment.UserName;
        if (OperatingSystem.IsWindows())
        {
            try { identity = WindowsIdentity.GetCurrent().User?.Value ?? identity; }
            catch { /* Environment.UserName remains an isolated fallback. */ }
        }
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(identity));
        return Convert.ToHexString(hash)[..24];
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { _cancellation?.Cancel(); } catch { }
        try { _listener?.Wait(TimeSpan.FromMilliseconds(250)); } catch { }
        _cancellation?.Dispose();
        if (_ownsMutex)
        {
            try { _mutex?.ReleaseMutex(); } catch (ApplicationException) { }
        }
        _mutex?.Dispose();
        _mutex = null;
    }
}
