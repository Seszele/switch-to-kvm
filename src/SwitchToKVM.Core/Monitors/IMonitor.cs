namespace SwitchToKVM.Core.Monitors;

public interface IMonitor : IDisposable
{
    string? Model { get; }
    bool SetInput(byte value);
}
