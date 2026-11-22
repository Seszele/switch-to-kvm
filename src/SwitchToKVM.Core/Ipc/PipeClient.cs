using System.IO.Pipes;

namespace SwitchToKVM.Core.Ipc;

public static class PipeClient
{
    public static async Task<PipeResponse?> SendAsync(string name, string command, TimeSpan connectTimeout)
    {
        await using var pipe = new NamedPipeClientStream(
            ".", name, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        try
        {
            await pipe.ConnectAsync((int)connectTimeout.TotalMilliseconds);
        }
        catch (Exception e) when (e is TimeoutException or UnauthorizedAccessException or IOException)
        {
            return null;
        }

        await using (var writer = new StreamWriter(pipe, leaveOpen: true))
        {
            await writer.WriteLineAsync(command);
        }

        using var reader = new StreamReader(pipe, leaveOpen: true);
        return PipeResponse.Parse(await reader.ReadToEndAsync());
    }
}
