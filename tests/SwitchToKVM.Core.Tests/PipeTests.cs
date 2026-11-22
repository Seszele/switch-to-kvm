using SwitchToKVM.Core.Ipc;

namespace SwitchToKVM.Core.Tests;

public class PipeTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(2);
    private readonly string _name = $"SwitchToKVM-test-{Guid.NewGuid()}";

    [Fact]
    public async Task SendsCommandAndGetsAnswer()
    {
        using var server = new PipeServer(_name, command => Task.FromResult(PipeResponse.Success($"got {command}")));

        var response = await PipeClient.SendAsync(_name, "status", Timeout);

        Assert.Equal(PipeResponse.Success("got status"), response);
    }

    [Fact]
    public async Task PassesErrorsThrough()
    {
        using var server = new PipeServer(_name, _ => Task.FromResult(PipeResponse.Error("nope")));
        Assert.Equal(PipeResponse.Error("nope"), await PipeClient.SendAsync(_name, "here", Timeout));
    }

    [Fact]
    public async Task KeepsServingAfterEachCommand()
    {
        var count = 0;
        using var server = new PipeServer(_name, _ => Task.FromResult(PipeResponse.Success($"{++count}")));

        await PipeClient.SendAsync(_name, "status", Timeout);
        await PipeClient.SendAsync(_name, "status", Timeout);
        var third = await PipeClient.SendAsync(_name, "status", Timeout);

        Assert.Equal("3", third?.Message);
    }

    [Fact]
    public async Task HandlerFailureBecomesError()
    {
        using var server = new PipeServer(_name, _ => throw new InvalidOperationException("boom"));
        var response = await PipeClient.SendAsync(_name, "here", Timeout);
        Assert.False(response?.Ok);
    }

    [Fact]
    public async Task MultilineMessagesSurvive()
    {
        using var server = new PipeServer(_name, _ => Task.FromResult(PipeResponse.Success("line 1\nline 2")));
        Assert.Equal("line 1\nline 2", (await PipeClient.SendAsync(_name, "status", Timeout))?.Message);
    }

    [Fact]
    public async Task NoServerGivesNull() =>
        Assert.Null(await PipeClient.SendAsync(_name, "status", TimeSpan.FromMilliseconds(200)));

    [Theory]
    [InlineData("")]
    [InlineData("hello")]
    [InlineData("OK\nmessage")]
    public void RejectsUnknownResponses(string text) => Assert.Null(PipeResponse.Parse(text));
}
