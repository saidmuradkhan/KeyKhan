using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KeyKhan.App.Services;
using KeyKhan.App.Settings;
using KeyKhan.Vault.Passwords;

namespace KeyKhan.App.ViewModels;

public sealed partial class GeneratorViewModel : ObservableObject
{
    private readonly SettingsStore _settings;
    private readonly ClipboardService _clipboard;

    [ObservableProperty]
    private int _length;

    [ObservableProperty]
    private bool _uppercase;

    [ObservableProperty]
    private bool _lowercase;

    [ObservableProperty]
    private bool _digits;

    [ObservableProperty]
    private bool _symbols;

    [ObservableProperty]
    private bool _excludeAmbiguous;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Strength))]
    [NotifyCanExecuteChangedFor(nameof(CopyCommand), nameof(UseCommand))]
    private string _password = "";

    [ObservableProperty]
    private string? _status;

    public GeneratorViewModel(SettingsStore settings, ClipboardService clipboard, bool isPickMode)
    {
        _settings = settings;
        _clipboard = clipboard;
        IsPickMode = isPickMode;

        var options = settings.Current.Generator;
        _length = options.Length;
        _uppercase = options.Uppercase;
        _lowercase = options.Lowercase;
        _digits = options.Digits;
        _symbols = options.Symbols;
        _excludeAmbiguous = options.ExcludeAmbiguous;
        Regenerate();
    }

    public event EventHandler? CloseRequested;

    public bool IsPickMode { get; }
    public StrengthLevel Strength => PasswordStrength.Evaluate(Password);

    public PasswordGeneratorOptions Options => new()
    {
        Length = Length,
        Uppercase = Uppercase,
        Lowercase = Lowercase,
        Digits = Digits,
        Symbols = Symbols,
        ExcludeAmbiguous = ExcludeAmbiguous,
    };

    /// <summary>The password the user chose in pick mode.</summary>
    public string? Result { get; private set; }

    public void SaveOptions() => _settings.Update(s => s with { Generator = Options });

    protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName is nameof(Length) or nameof(Uppercase) or nameof(Lowercase) or nameof(Digits)
            or nameof(Symbols) or nameof(ExcludeAmbiguous))
        {
            Regenerate();
        }
    }

    [RelayCommand]
    private void Regenerate()
    {
        bool anySet = Uppercase || Lowercase || Digits || Symbols;
        Password = anySet ? PasswordGenerator.Generate(Options) : "";
        Status = anySet ? null : "Select at least one character type.";
    }

    [RelayCommand(CanExecute = nameof(HasPassword))]
    private void Copy()
    {
        _clipboard.CopySecret(Password);
        Status = "Copied to clipboard";
    }

    [RelayCommand(CanExecute = nameof(HasPassword))]
    private void Use()
    {
        Result = Password;
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private bool HasPassword() => Password.Length > 0;
}
