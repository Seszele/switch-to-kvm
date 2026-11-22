using System.IO.Pipes;

namespace SwitchToKVM.Core.Ipc;

public sealed class PipeServer : IDisposable
{
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(5);

    private readonly string _name;
    private readonly Func<string, Task<PipeResponse>> _handle;
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _loop;

    public PipeServer(string name, Func<string, Task<PipeResponse>> handle)
    {
        _name = name;
        _handle = handle;
        _loop = Task.Run(RunAsync);
    }

    public void Dispose()
    {
        _stop.Cancel();
        try
        {
            _loop.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
        }
        _stop.Dispose();
    }

    private async Task RunAsync()
    {
        while (!_stop.IsCancellationRequested)
        {
            try
            {
                await using var pipe = new NamedPipeServerStream(
                    _name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await pipe.WaitForConnectionAsync(_stop.Token);
                await ServeAsync(pipe);
            }
            catch (OperationCanceledException) when (_stop.IsCancellationRequested)
            {
                return;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or OperationCanceledException)
            {
                Log.Write($"Pipe error: {e.Message}");
                if (!await WaitBeforeRetryAsync()) return;
            }
        }
    }

    private async Task<bool> WaitBeforeRetryAsync()
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(1), _stop.Token);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private async Task ServeAsync(NamedPipeServerStream pipe)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);
        timeout.CancelAfter(RequestTimeout);

        using var reader = new StreamReader(pipe, leaveOpen: true);
        var request = await reader.ReadLineAsync(timeout.Token) ?? "";

        PipeResponse response;
        try
        {
            response = await _handle(request.Trim());
        }
        catch (Exception e)
        {
            Log.Write($"Command '{request}' failed: {e}");
            response = PipeResponse.Error($"The app failed to run '{request}', see the log.");
        }

        await using var writer = new StreamWriter(pipe, leaveOpen: true);
        await writer.WriteAsync(response.Serialize());
        await writer.FlushAsync(_stop.Token);
        if (OperatingSystem.IsWindows()) pipe.WaitForPipeDrain();
    }
}
