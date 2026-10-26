using KeyKhan.App.ViewModels;
using KeyKhan.Vault;

namespace KeyKhan.App.Services;

public interface IDialogService
{
    /// <returns>The saved entry, or <c>null</c> if the user cancelled.</returns>
    VaultEntry? EditEntry(VaultEntry? entry, string? defaultFolder = null);

    /// <returns>The chosen password, or <c>null</c> if the user cancelled.</returns>
    string? PickPassword();

    void ShowGenerator();

    void ShowSettings(SettingsFocus focus = SettingsFocus.None);

    void ShowChangeMasterPassword();

    /// <summary>Shows a newly created recovery key until the user confirms it is saved.</summary>
    void ShowRecoveryKey(string recoveryKey);

    bool Confirm(string title, string message);

    /// <returns>The trimmed, non-empty text, or <c>null</c> if the user cancelled.</returns>
    string? Prompt(string title, string label, string initialValue = "");

    void ShowMessage(string title, string message);

    string? PickFileToOpen(string filter);

    string? PickFileToSave(string filter, string fileName);
}
