using SwitchToKVM.Core.Usb;

namespace SwitchToKVM.Core.Tests;

public class UsbIdTests
{
    [Theory]
    [InlineData(@"\?\USB#VID_3434&PID_D030#6&1e763613&0&3#{a5dcbf10-6530-11d2-901f-00c04fb951ed}")]
    [InlineData(@"\?\usb#vid_3434&pid_d030&mi_00#7&2354b1f3&0&0000#{a5dcbf10-6530-11d2-901f-00c04fb951ed}")]
    public void ReadsIdFromPath(string path) => Assert.Equal(new UsbId(0x3434, 0xD030), UsbId.FromPath(path));

    [Theory]
    [InlineData(@"\?\ROOT#HUB#0000")]
    [InlineData(@"\?\USB#VID_34&PID_D030#")]
    [InlineData("")]
    public void IgnoresOtherPaths(string path) => Assert.Null(UsbId.FromPath(path));

    [Fact]
    public void FormatsAsHex() => Assert.Equal("3434:D030", new UsbId(0x3434, 0xD030).ToString());
}
