using System.Net.Http;
using System.Security.Cryptography;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PassIn.App.Services;

namespace PassIn.App.ViewModels;

/// <summary>Switches the main window between the lock screen and the vault, and offers available updates.</summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly VaultSession _session;
    private readonly UpdateService _updates;
    private readonly IDialogService _dialogs;
    private readonly Action _exit;
    private readonly Func<LockViewModel> _createLock;
    private readonly Func<VaultViewModel> _createVault;

    [ObservableProperty]
    private ObservableObject _page;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UpdateMessage))]
    private AppRelease? _update;

    [ObservableProperty]
    private bool _isUpdating;

    public MainViewModel(
        VaultSession session, UpdateService updates, IDialogService dialogs, Action exit,
        Func<LockViewModel> createLock, Func<VaultViewModel> createVault)
    {
        _session = session;
        _updates = updates;
        _dialogs = dialogs;
        _exit = exit;
        _createLock = createLock;
        _createVault = createVault;
        _page = CreatePage();
        _session.StateChanged += (_, _) => Page = CreatePage();
        _updates.UpdateAvailable += (_, _) => Update = _updates.Available;
    }

    public string UpdateMessage => Update is null ? "" : $"PassIn {Update.Version.ToString(3)} is available.";

    public string UpdateActionText => UpdateService.IsInstalled ? "Install and restart" : "Download";

    partial void OnPageChanging(ObservableObject value) => (Page as IDisposable)?.Dispose();

    [RelayCommand]
    private async Task InstallUpdate()
    {
        IsUpdating = true;
        try
        {
            if (await _updates.InstallAsync(Update!))
            {
                _exit();
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or CryptographicException or IOException)
        {
            _dialogs.ShowMessage("Update failed", ex.Message);
        }
        finally
        {
            IsUpdating = false;
        }
    }

    [RelayCommand]
    private void DismissUpdate() => Update = null;

    private ObservableObject CreatePage() => _session.IsUnlocked ? _createVault() : _createLock();
}
