namespace SwitchToKVM.Core.Monitors;

public readonly record struct ScreenArea(int X, int Y, int Width, int Height)
{
    public int CenterX => X + Width / 2;
}
