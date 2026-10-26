using System.Windows;
using Microsoft.Win32;
using KeyKhan.App.Settings;
using KeyKhan.App.ViewModels;
using KeyKhan.App.Views;
using KeyKhan.Vault;

namespace KeyKhan.App.Services;

internal sealed class DialogService(
    Window mainWindow, SettingsStore settings, VaultSession session, ClipboardService clipboard, HotkeyService hotkeys,
    UpdateService updates)
    : IDialogService
{
    private Window? Owner =>
        Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
        ?? (mainWindow.IsVisible ? mainWindow : null);

    public VaultEntry? EditEntry(VaultEntry? entry, string? defaultFolder = null)
    {
        var viewModel = new EntryEditorViewModel(entry, defaultFolder, session.Folders, settings, this);
        return ShowDialog(new EntryEditorWindow(viewModel)) ? viewModel.Result : null;
    }

    public string? PickPassword()
    {
        var viewModel = new GeneratorViewModel(settings, clipboard, isPickMode: true);
        return ShowDialog(new GeneratorWindow(viewModel)) ? viewModel.Result : null;
    }

    public void ShowGenerator() =>
        ShowDialog(new GeneratorWindow(new GeneratorViewModel(settings, clipboard, isPickMode: false)));

    public void ShowSettings() =>
        ShowDialog(new SettingsWindow(new SettingsViewModel(settings, hotkeys, session, updates, this)));

    public void ShowChangeMasterPassword()
    {
        if (ShowDialog(new ChangePasswordWindow(new ChangePasswordViewModel(session))))
        {
            ShowMessage("Master password changed", "Use your new master password the next time you unlock KeyKhan.");
        }
    }

    public void ShowRecoveryKey(string recoveryKey) =>
        ShowDialog(new RecoveryKeyWindow(new RecoveryKeyViewModel(recoveryKey, clipboard, this)));

    public string? Prompt(string title, string label, string initialValue = "")
    {
        var window = new PromptWindow(title, label, initialValue);
        return ShowDialog(window) ? window.Value : null;
    }

    public bool Confirm(string title, string message) =>
        Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;

    public void ShowMessage(string title, string message) =>
        Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);

    public string? PickFileToOpen(string filter)
    {
        var dialog = new OpenFileDialog { Filter = filter };
        return dialog.ShowDialog(Owner) == true ? dialog.FileName : null;
    }

    public string? PickFileToSave(string filter, string fileName)
    {
        var dialog = new SaveFileDialog { Filter = filter, FileName = fileName };
        return dialog.ShowDialog(Owner) == true ? dialog.FileName : null;
    }

    private bool ShowDialog(Window window)
    {
        window.Owner = Owner;
        window.WindowStartupLocation = window.Owner is null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner;
        return window.ShowDialog() == true;
    }

    private MessageBoxResult Show(string message, string title, MessageBoxButton buttons, MessageBoxImage image) =>
        Owner is { } owner
            ? MessageBox.Show(owner, message, title, buttons, image)
            : MessageBox.Show(message, title, buttons, image);
}
