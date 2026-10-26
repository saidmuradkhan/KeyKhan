using KeyKhan.Vault;

namespace KeyKhan.App.Services;

public interface IDialogService
{
    /// <returns>The saved entry, or <c>null</c> if the user cancelled.</returns>
    VaultEntry? EditEntry(VaultEntry? entry, string? defaultFolder = null);

    /// <returns>The chosen password, or <c>null</c> if the user cancelled.</returns>
    string? PickPassword();

    void ShowGenerator();

    void ShowSettings();

    void ShowChangeMasterPassword();

    bool Confirm(string title, string message);

    void ShowMessage(string title, string message);

    string? PickFileToOpen(string filter);

    string? PickFileToSave(string filter, string fileName);
}
