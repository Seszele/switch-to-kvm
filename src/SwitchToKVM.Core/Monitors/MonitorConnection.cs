namespace SwitchToKVM.Core.Monitors;

public sealed class MonitorConnection(Func<IMonitor?> findMonitor, TimeSpan retryDelay, IMonitor? monitor = null) : IDisposable
{
    public const int Attempts = 4;

    private IMonitor? _monitor = monitor;

    public bool HasMonitor => _monitor is not null;

    public void Connect() => _monitor ??= findMonitor();

    public bool SetInput(byte input)
    {
        for (var attempt = 0; attempt < Attempts; attempt++)
        {
            if (attempt > 0) Thread.Sleep(retryDelay);
            if (_monitor?.SetInput(input) == true) return true;
            _monitor?.Dispose();
            _monitor = findMonitor();
            if (_monitor?.SetInput(input) == true) return true;
        }
        return false;
    }

    public void Dispose()
    {
        _monitor?.Dispose();
        _monitor = null;
    }
}
