using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KeyKhan.App.Services;

namespace KeyKhan.App.ViewModels;

public sealed partial class RecoveryKeyViewModel(string recoveryKey, ClipboardService clipboard, IDialogService dialogs) : ObservableObject
{
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DoneCommand))]
    private bool _isSaved;

    [ObservableProperty]
    private string? _status;

    public string RecoveryKey { get; } = recoveryKey;

    public event EventHandler? CloseRequested;

    [RelayCommand]
    private void Copy()
    {
        clipboard.CopySecret(RecoveryKey);
        Status = clipboard.ClearAfterSeconds > 0
            ? $"Copied. The clipboard clears in {clipboard.ClearAfterSeconds}s — paste it somewhere safe now."
            : "Copied.";
    }

    [RelayCommand]
    private void SaveToFile()
    {
        if (dialogs.PickFileToSave("Text files (*.txt)|*.txt", "KeyKhan recovery key.txt") is not { } path)
        {
            return;
        }

        File.WriteAllText(path,
            $"""
            KeyKhan recovery key
            ====================

            {RecoveryKey}

            Use it on the unlock screen ("Forgot your master password?") to set a new master password.
            Keep this file offline, e.g. printed or on a USB stick — not next to your vault.
            Created {DateTime.Now:yyyy-MM-dd HH:mm}.
            """);
        Status = $"Saved to {path}";
    }

    [RelayCommand(CanExecute = nameof(IsSaved))]
    private void Done() => CloseRequested?.Invoke(this, EventArgs.Empty);
}
