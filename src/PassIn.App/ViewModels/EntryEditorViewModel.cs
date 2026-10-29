using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PassIn.App.Services;
using PassIn.App.Settings;
using PassIn.Vault;
using PassIn.Vault.Otp;
using PassIn.Vault.Passwords;

namespace PassIn.App.ViewModels;

public sealed partial class EntryEditorViewModel : ObservableObject
{
    private readonly VaultEntry? _original;
    private readonly SettingsStore _settings;
    private readonly IDialogService _dialogs;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string _title;

    [ObservableProperty]
    private string _username;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Strength))]
    private string _password;

    [ObservableProperty]
    private string _url;

    [ObservableProperty]
    private string _folder;

    [ObservableProperty]
    private string _notes;

    [ObservableProperty]
    private string _totp;

    [ObservableProperty]
    private bool _isFavorite;

    [ObservableProperty]
    private string? _error;

    public EntryEditorViewModel(
        VaultEntry? original, string? defaultFolder, IReadOnlyList<string> folders, SettingsStore settings, IDialogService dialogs)
    {
        _original = original;
        _settings = settings;
        _dialogs = dialogs;
        Folders = folders;

        _title = original?.Title ?? "";
        _username = original?.Username ?? "";
        _password = original?.Password ?? PasswordGenerator.Generate(settings.Current.Generator);
        _url = original?.Url ?? "";
        _folder = original?.Folder ?? defaultFolder ?? "";
        _notes = original?.Notes ?? "";
        _totp = original?.Totp ?? "";
        _isFavorite = original?.IsFavorite ?? false;
    }

    public event EventHandler? CloseRequested;

    public string WindowTitle => _original is null ? "New entry" : "Edit entry";
    public IReadOnlyList<string> Folders { get; }
    public StrengthLevel Strength => PasswordStrength.Evaluate(Password);

    public VaultEntry? Result { get; private set; }

    [RelayCommand]
    private void GeneratePassword() => Password = PasswordGenerator.Generate(_settings.Current.Generator);

    [RelayCommand]
    private void CustomizePassword()
    {
        if (_dialogs.PickPassword() is { } password)
        {
            Password = password;
        }
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Save()
    {
        if (Totp.Trim().Length > 0 && !TotpSettings.TryParse(Totp, out _))
        {
            Error = "The 2FA secret must be a Base32 key or an otpauth:// link.";
            return;
        }

        var now = DateTimeOffset.UtcNow;
        Result = (_original ?? new VaultEntry { Title = Title, CreatedAt = now }) with
        {
            Title = Title.Trim(),
            Username = Username.Trim(),
            Password = Password,
            Url = Url.Trim(),
            Folder = Folder.Trim(),
            Notes = Notes,
            Totp = Totp.Trim(),
            IsFavorite = IsFavorite,
            ModifiedAt = now,
        };
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private bool CanSave() => !string.IsNullOrWhiteSpace(Title);
}
