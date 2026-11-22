using SwitchToKVM.Core.Monitors;

namespace SwitchToKVM.Core.Tests;

public class InputNamesTests
{
    [Theory]
    [InlineData(0x0F, "DisplayPort 1")]
    [InlineData(0x11, "HDMI 1")]
    [InlineData(0x03, "DVI 1")]
    [InlineData(0x1B, "Input 0x1B")]
    public void NamesInputs(byte value, string name) => Assert.Equal(name, InputNames.Get(value));
}
