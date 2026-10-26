using System.Collections.ObjectModel;
using System.Security;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KeyKhan.App.Services;
using KeyKhan.Vault;
using KeyKhan.Vault.Otp;

namespace KeyKhan.App.ViewModels;

/// <summary>Spotlight-style search that copies or auto-types a login without opening the main window.</summary>
public sealed partial class QuickAccessViewModel : ObservableObject
{
    private const int MaxResults = 8;

    private readonly VaultSession _session;
    private readonly ClipboardService _clipboard;
    private readonly AutoTypeService _autoType;

    [ObservableProperty]
    private string _query = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(
        nameof(CopyPasswordCommand), nameof(CopyUsernameCommand), nameof(CopyTotpCommand),
        nameof(AutoTypeCommand), nameof(OpenUrlCommand))]
    private VaultEntry? _selected;

    [ObservableProperty]
    private string? _error;

    [ObservableProperty]
    private bool _isBusy;

    public QuickAccessViewModel(VaultSession session, ClipboardService clipboard, AutoTypeService autoType)
    {
        _session = session;
        _clipboard = clipboard;
        _autoType = autoType;
        _session.StateChanged += (_, _) => OnPropertyChanged(nameof(IsLocked));
    }

    /// <summary>Raised when the window should hide and give focus back to the previous app.</summary>
    public event EventHandler? DismissRequested;

    public ObservableCollection<VaultEntry> Results { get; } = [];
    public bool IsLocked => !_session.IsUnlocked;

    public void Reset()
    {
        Error = null;
        Query = "";
        Refresh();
    }

    /// <summary>Clears the search and, when this Windows account is remembered, unlocks without a password.</summary>
    public async Task ActivateAsync()
    {
        Reset();
        if (IsLocked && await _session.TryUnlockWithDeviceAsync())
        {
            Reset();
        }
    }

    public async Task UnlockAsync(SecureString password)
    {
        if (IsBusy || password.Length == 0)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await _session.UnlockAsync(password);
            Reset();
        }
        catch (VaultAuthenticationException)
        {
            Error = "Incorrect master password.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    partial void OnQueryChanged(string value) => Refresh();

    [RelayCommand]
    private void SelectNext() => MoveSelection(1);

    [RelayCommand]
    private void SelectPrevious() => MoveSelection(-1);

    private void MoveSelection(int delta)
    {
        if (Results.Count == 0)
        {
            return;
        }
        int index = Selected is null ? 0 : Results.IndexOf(Selected) + delta;
        Selected = Results[Math.Clamp(index, 0, Results.Count - 1)];
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void CopyPassword() => CopyAndDismiss(Selected!.Password);

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void CopyUsername() => CopyAndDismiss(Selected!.Username);

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void CopyTotp()
    {
        if (TotpSettings.TryParse(Selected!.Totp, out var settings))
        {
            CopyAndDismiss(TotpGenerator.Generate(settings, DateTimeOffset.UtcNow).Code);
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task AutoType()
    {
        var entry = Selected!;
        DismissRequested?.Invoke(this, EventArgs.Empty);
        await _autoType.TypeLoginAsync(entry.Username, entry.Password);
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void OpenUrl()
    {
        if (UrlLauncher.TryOpen(Selected!.Url))
        {
            DismissRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    [RelayCommand]
    private void Dismiss() => DismissRequested?.Invoke(this, EventArgs.Empty);

    private bool HasSelection() => Selected is not null;

    private void CopyAndDismiss(string value)
    {
        _clipboard.CopySecret(value);
        DismissRequested?.Invoke(this, EventArgs.Empty);
    }

    private void Refresh()
    {
        Results.Clear();
        foreach (var entry in EntrySearch.Search(_session.Entries, Query).Take(MaxResults))
        {
            Results.Add(entry);
        }
        Selected = Results.FirstOrDefault();
    }
}
