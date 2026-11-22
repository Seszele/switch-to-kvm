using SwitchToKVM.Core.Monitors;

namespace SwitchToKVM.Core.Tests;

public class CapabilitiesTests
{
    private const string Iiyama =
        "(prot(monitor)type(LCD)model(PL2770QS)cmds(01 02 03 07 0C E3 F3)vcp(02 04 05 08 10 12 14(05 08 0B) 16 18 1A 52 60(0F 11) 62 87 AC AE B2 B6 C6 C8 CA CC(01 02 03 04 05 06 07 08 09 0A 0C 0D 0E 12 14 16 1E) D6(01 04 05) DF)mswhql(1)asset_eep(40)mccs_ver(2.2))";

    [Fact]
    public void ReadsModel() => Assert.Equal("PL2770QS", Capabilities.Model(Iiyama));

    [Fact]
    public void ReadsInputs() => Assert.Equal([0x0F, 0x11], Capabilities.Inputs(Iiyama));

    [Fact]
    public void ReadsInputsWithoutSpaces() => Assert.Equal([0x0F, 0x11], Capabilities.Inputs("vcp(10 60(0F11) 62)"));

    [Fact]
    public void IgnoresCodesEndingIn60() => Assert.Equal([0x03], Capabilities.Inputs("vcp(E60(01 02) 60(03))"));

    [Theory]
    [InlineData("")]
    [InlineData("garbage")]
    [InlineData("vcp(10 12 62)")]
    public void MissingDataGivesNothing(string capabilities)
    {
        Assert.Null(Capabilities.Model(capabilities));
        Assert.Empty(Capabilities.Inputs(capabilities));
    }
}
