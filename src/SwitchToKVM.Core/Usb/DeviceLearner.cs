namespace SwitchToKVM.Core.Usb;

public sealed class DeviceLearner(Func<IReadOnlySet<UsbId>> getPresent, TimeSpan pollInterval)
{
    public async Task<UsbId> LearnAsync(Action onGone, CancellationToken cancellationToken)
    {
        var before = getPresent();
        List<UsbId> gone;
        do
        {
            await Task.Delay(pollInterval, cancellationToken);
            var present = getPresent();
            gone = before.Where(id => !present.Contains(id)).ToList();
        } while (gone.Count == 0);

        onGone();

        while (true)
        {
            await Task.Delay(pollInterval, cancellationToken);
            var present = getPresent();
            var back = gone.Where(present.Contains).ToList();
            if (back.Count > 0) return back[0];
        }
    }
}
