using CommunityToolkit.Mvvm.ComponentModel;
using KeyKhan.App.Services;

namespace KeyKhan.App.ViewModels;

/// <summary>Switches the main window between the lock screen and the vault.</summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly VaultSession _session;
    private readonly Func<LockViewModel> _createLock;
    private readonly Func<VaultViewModel> _createVault;

    [ObservableProperty]
    private ObservableObject _page;

    public MainViewModel(VaultSession session, Func<LockViewModel> createLock, Func<VaultViewModel> createVault)
    {
        _session = session;
        _createLock = createLock;
        _createVault = createVault;
        _page = CreatePage();
        _session.StateChanged += (_, _) => Page = CreatePage();
    }

    partial void OnPageChanging(ObservableObject value) => (Page as IDisposable)?.Dispose();

    private ObservableObject CreatePage() => _session.IsUnlocked ? _createVault() : _createLock();
}
