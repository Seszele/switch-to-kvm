using SwitchToKVM.Core.Configuration;
using SwitchToKVM.Core.Monitors;

namespace SwitchToKVM.App.ViewModels;

public interface ISettingsHost
{
    bool StartsWithWindows { get; set; }
    ScreenArea? CurrentScreen();
    void PauseSwitching();
    void Save(Config config);
    void Close();
    void Quit();
}
