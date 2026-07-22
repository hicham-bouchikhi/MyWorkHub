using System.IO.Pipes;

namespace MyWorkHub.App;

// Uses a named pipe instead of a Win32 Mutex so the mechanism works cross-platform
// (NamedPipeServerStream maps to Unix domain sockets on macOS / Linux).
internal sealed class SingleInstanceGuard : IDisposable
{
    private const string PIPE_NAME = "MyWorkHub-single-instance";
    private const int CONNECT_TIMEOUT_MS = 500;

    private readonly NamedPipeServerStream? _server;
    private readonly CancellationTokenSource _cts = new();

    public bool IsAlreadyRunning { get; }

    public event EventHandler? ShowRequested;

    private SingleInstanceGuard(bool isAlreadyRunning, NamedPipeServerStream? server)
    {
        IsAlreadyRunning = isAlreadyRunning;
        _server = server;
        if (server is not null)
            _ = ListenAsync(_cts.Token);
    }

    public static SingleInstanceGuard Acquire()
    {
        try
        {
            using var probe = new NamedPipeClientStream(".", PIPE_NAME, PipeDirection.Out);
            probe.Connect(CONNECT_TIMEOUT_MS);
            probe.WriteByte(1);
            return new SingleInstanceGuard(isAlreadyRunning: true, server: null);
        }
        catch (TimeoutException) { }
        catch (IOException) { }

        var server = new NamedPipeServerStream(
            PIPE_NAME,
            PipeDirection.In,
            maxNumberOfServerInstances: 1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous);

        return new SingleInstanceGuard(isAlreadyRunning: false, server);
    }

    private async Task ListenAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _server is not null)
        {
            try
            {
                await _server.WaitForConnectionAsync(ct).ConfigureAwait(false);
                _server.ReadByte();
                _server.Disconnect();
                ShowRequested?.Invoke(this, EventArgs.Empty);
            }
            catch (OperationCanceledException) { break; }
            catch { /* keep listening on transient pipe errors */ }
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
        _server?.Dispose();
    }
}
