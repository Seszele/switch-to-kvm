using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SwitchToKVM.Core;
using SwitchToKVM.Core.Updates;

namespace SwitchToKVM.App.ViewModels;

public sealed partial class Updater : ViewModelBase, IDisposable
{
    private static readonly TimeSpan CheckEvery = TimeSpan.FromDays(1);

    private readonly HttpClient _http = GitHubUpdates.CreateClient();
    private DispatcherTimer? _timer;
    private DateTime _lastCheck;
    private bool _enabled;
    private bool _checking;

    public static bool IsInstalled { get; } = File.Exists(Path.Combine(AppContext.BaseDirectory, "unins000.exe"));

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUpdate), nameof(Header), nameof(CanInstall), nameof(ButtonText))]
    [NotifyCanExecuteChangedFor(nameof(UpdateCommand))]
    public partial AvailableUpdate? Available { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Header))]
    [NotifyCanExecuteChangedFor(nameof(UpdateCommand))]
    public partial bool IsInstalling { get; set; }

    [ObservableProperty] public partial string? Error { get; set; }

    public bool HasUpdate => Available is not null;
    public bool CanInstall => IsInstalled && Available is { InstallerUrl: not null, InstallerSha256: not null };
    public string ButtonText => CanInstall ? "Update" : "Download";
    public string Header => IsInstalling ? "Downloading the update..." : $"Update to {Available?.Version}";

    public void Start(bool enabled)
    {
        _enabled = enabled;
        _timer = new DispatcherTimer(TimeSpan.FromHours(1), DispatcherPriority.Background, (_, _) => _ = CheckIfDueAsync());
        DispatcherTimer.RunOnce(() => _ = CheckIfDueAsync(), TimeSpan.FromSeconds(30));
    }

    public void SetEnabled(bool enabled)
    {
        if (enabled == _enabled) return;
        _enabled = enabled;
        if (enabled)
        {
            _lastCheck = default;
            _ = CheckIfDueAsync();
        }
        else
        {
            Available = null;
        }
    }

    public void Dispose()
    {
        _timer?.Stop();
        _http.Dispose();
    }

    private async Task CheckIfDueAsync()
    {
        if (!_enabled || _checking || DateTime.UtcNow - _lastCheck < CheckEvery) return;
        _checking = true;
        _lastCheck = DateTime.UtcNow;
        try
        {
            var update = await GitHubUpdates.CheckAsync(_http, GitHubUpdates.CurrentVersion);
            if (!_enabled) return;
            if (update is not null && update.Version != Available?.Version)
                Log.Write($"Update available: {update.Version}");
            Available = update;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException)
        {
            Log.Write($"Update check failed: {e.Message}");
        }
        finally
        {
            _checking = false;
        }
    }

    private bool CanUpdate() => Available is not null && !IsInstalling;

    [RelayCommand(CanExecute = nameof(CanUpdate))]
    private async Task UpdateAsync()
    {
        if (Available is not { } update) return;
        if (!CanInstall)
        {
            Process.Start(new ProcessStartInfo(update.PageUrl) { UseShellExecute = true });
            return;
        }

        IsInstalling = true;
        Error = null;
        try
        {
            var installer = await GitHubUpdates.DownloadInstallerAsync(_http, update);
            Log.Write($"Installing {update.Version}");
            using var process = Process.Start(new ProcessStartInfo(installer, "/SILENT /RESTARTAPP") { UseShellExecute = false });
            if (process is null) return;
            await process.WaitForExitAsync();
            Log.Write($"The installer closed without updating (exit code {process.ExitCode})");
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or IOException
                                       or UnauthorizedAccessException or InvalidDataException or Win32Exception)
        {
            Log.Write($"Update failed: {e.Message}");
            Error = "The update didn't install. Try again later, or download it from GitHub.";
        }
        finally
        {
            IsInstalling = false;
        }
    }
}
