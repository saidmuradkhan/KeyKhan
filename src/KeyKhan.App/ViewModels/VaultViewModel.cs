using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows.Input;
using KeyKhan.App.Services;
using KeyKhan.App.Settings;
using KeyKhan.Vault;
using KeyKhan.Vault.Otp;

namespace KeyKhan.App.ViewModels;

public sealed record MenuTarget(string Label, ICommand Command);

public sealed partial class VaultViewModel : ObservableObject, IDisposable
{
    private static readonly TimeSpan ToastDuration = TimeSpan.FromSeconds(2.5);
    private static readonly StringComparer FolderComparer = StringComparer.OrdinalIgnoreCase;

    private readonly VaultSession _session;
    private readonly ClipboardService _clipboard;
    private readonly IDialogService _dialogs;
    private readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromSeconds(1) };
    private DateTime _toastExpiresAt;

    [ObservableProperty]
    private NavigationItem? _selectedNavigation;

    [ObservableProperty]
    private string _searchText = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection), nameof(LastModified))]
    [NotifyCanExecuteChangedFor(
        nameof(EditEntryCommand), nameof(DeleteEntryCommand), nameof(ToggleFavoriteCommand),
        nameof(CopyUsernameCommand), nameof(CopyPasswordCommand), nameof(CopyTotpCommand), nameof(OpenUrlCommand))]
    private VaultEntry? _selectedEntry;

    [ObservableProperty]
    private bool _isPasswordRevealed;

    [ObservableProperty]
    private string? _totpCode;

    [ObservableProperty]
    private int _totpSecondsRemaining;

    [ObservableProperty]
    private int _totpPeriod = 30;

    [ObservableProperty]
    private string? _toast;

    public VaultViewModel(VaultSession session, ClipboardService clipboard, IDialogService dialogs, SettingsStore settings)
    {
        Settings = settings;
        _session = session;
        _clipboard = clipboard;
        _dialogs = dialogs;

        _session.EntriesChanged += OnEntriesChanged;
        _clock.Tick += OnClockTick;
        _clock.Start();

        RebuildNavigation();
    }

    public ObservableCollection<NavigationItem> Navigation { get; } = [];
    public ObservableCollection<VaultEntry> Entries { get; } = [];

    /// <summary>Choices for the entry's "Move to folder" menu.</summary>
    public ObservableCollection<MenuTarget> MoveTargets { get; } = [];

    public SettingsStore Settings { get; }

    public bool HasSelection => SelectedEntry is not null;
    public string? LastModified => SelectedEntry?.ModifiedAt.LocalDateTime.ToString("g", CultureInfo.CurrentCulture);
    public bool IsVaultEmpty => _session.Entries.Count == 0;

    public void Dispose()
    {
        _clock.Stop();
        _session.EntriesChanged -= OnEntriesChanged;
    }

    /// <param name="search">Focusing the search box is a view concern, so the view supplies it.</param>
    public IReadOnlyDictionary<ShortcutAction, ICommand> ShortcutCommands(ICommand search) => new Dictionary<ShortcutAction, ICommand>
    {
        [ShortcutAction.Search] = search,
        [ShortcutAction.NewEntry] = NewEntryCommand,
        [ShortcutAction.EditEntry] = EditEntryCommand,
        [ShortcutAction.DeleteEntry] = DeleteEntryCommand,
        [ShortcutAction.CopyPassword] = CopyPasswordCommand,
        [ShortcutAction.CopyUsername] = CopyUsernameCommand,
        [ShortcutAction.CopyTotp] = CopyTotpCommand,
        [ShortcutAction.OpenWebsite] = OpenUrlCommand,
        [ShortcutAction.ToggleFavorite] = ToggleFavoriteCommand,
        [ShortcutAction.PasswordGenerator] = OpenGeneratorCommand,
        [ShortcutAction.Lock] = LockCommand,
    };

    partial void OnSelectedNavigationChanged(NavigationItem? value) => ApplyFilter();

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    partial void OnSelectedEntryChanged(VaultEntry? value)
    {
        IsPasswordRevealed = false;
        RefreshTotp();
    }

    [RelayCommand]
    private void NewEntry()
    {
        var created = _dialogs.EditEntry(null, SelectedNavigation?.Folder);
        if (created is not null)
        {
            _session.Save(created);
            Select(created.Id);
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void EditEntry()
    {
        var edited = _dialogs.EditEntry(SelectedEntry);
        if (edited is not null)
        {
            _session.Save(edited);
            Select(edited.Id);
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void DeleteEntry()
    {
        var entry = SelectedEntry!;
        if (_dialogs.Confirm("Delete entry", $"Delete \"{entry.Title}\"? This cannot be undone."))
        {
            _session.Delete(entry.Id);
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void ToggleFavorite()
    {
        var entry = SelectedEntry!;
        _session.Save(entry with { IsFavorite = !entry.IsFavorite, ModifiedAt = DateTimeOffset.UtcNow });
        Select(entry.Id);
    }

    [RelayCommand]
    private void NewFolder()
    {
        if (_dialogs.Prompt("New folder", "Folder name") is { } name)
        {
            _session.AddFolder(name);
            SelectFolder(name);
        }
    }

    [RelayCommand]
    private void RenameFolder(NavigationItem item)
    {
        if (item.Folder is { } folder && _dialogs.Prompt("Rename folder", "Folder name", folder) is { } name && name != folder)
        {
            _session.RenameFolder(folder, name);
            SelectFolder(name);
        }
    }

    [RelayCommand]
    private void DeleteFolder(NavigationItem item)
    {
        if (item.Folder is { } folder &&
            _dialogs.Confirm("Delete folder", $"Delete the folder \"{folder}\"? Its {item.Count} entries stay in All items."))
        {
            _session.DeleteFolder(folder);
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void CopyUsername() => Copy(SelectedEntry!.Username, "Username");

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void CopyPassword() => Copy(SelectedEntry!.Password, "Password");

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void CopyTotp()
    {
        RefreshTotp();
        Copy(TotpCode, "One-time code");
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void OpenUrl()
    {
        if (!UrlLauncher.TryOpen(SelectedEntry!.Url))
        {
            ShowToast("This entry has no valid web address");
        }
    }

    [RelayCommand]
    private void Lock() => _session.Lock();

    [RelayCommand]
    private void OpenGenerator() => _dialogs.ShowGenerator();

    [RelayCommand]
    private void OpenSettings() => _dialogs.ShowSettings();

    private void Copy(string? value, string what)
    {
        if (string.IsNullOrEmpty(value))
        {
            ShowToast($"{what} is empty");
            return;
        }

        _clipboard.CopySecret(value);
        int seconds = _clipboard.ClearAfterSeconds;
        ShowToast(seconds > 0 ? $"{what} copied · clears in {seconds}s" : $"{what} copied");
    }

    private void MoveSelectedEntry(string folder)
    {
        if (SelectedEntry is not { } entry || entry.Folder == folder)
        {
            return;
        }
        _session.Save(entry with { Folder = folder, ModifiedAt = DateTimeOffset.UtcNow });
        Select(entry.Id);
        ShowToast(folder.Length > 0 ? $"Moved to {folder}" : "Removed from its folder");
    }

    private void MoveSelectedEntryToNewFolder()
    {
        if (_dialogs.Prompt("New folder", "Folder name") is { } name)
        {
            MoveSelectedEntry(name);
        }
    }

    private void SelectFolder(string name) =>
        SelectedNavigation = Navigation.FirstOrDefault(n => FolderComparer.Equals(n.Folder, name)) ?? SelectedNavigation;

    private void ShowToast(string message)
    {
        Toast = message;
        _toastExpiresAt = DateTime.UtcNow + ToastDuration;
    }

    private void OnClockTick(object? sender, EventArgs e)
    {
        RefreshTotp();
        if (Toast is not null && DateTime.UtcNow >= _toastExpiresAt)
        {
            Toast = null;
        }
    }

    private void RefreshTotp()
    {
        if (SelectedEntry is null || !TotpSettings.TryParse(SelectedEntry.Totp, out var settings))
        {
            TotpCode = null;
            return;
        }

        var code = TotpGenerator.Generate(settings, DateTimeOffset.UtcNow);
        TotpCode = code.Code;
        TotpSecondsRemaining = code.SecondsRemaining;
        TotpPeriod = code.Period;
    }

    private void OnEntriesChanged(object? sender, EventArgs e)
    {
        RebuildNavigation();
        OnPropertyChanged(nameof(IsVaultEmpty));
    }

    private void RebuildNavigation()
    {
        var previous = SelectedNavigation;
        var entries = _session.Entries;

        Navigation.Clear();
        Navigation.Add(new NavigationItem(NavigationKind.All, "All items", "", entries.Count));
        Navigation.Add(new NavigationItem(NavigationKind.Favorites, "Favorites", "", entries.Count(e => e.IsFavorite)));
        MoveTargets.Clear();
        MoveTargets.Add(new MenuTarget("No folder", new RelayCommand(() => MoveSelectedEntry(""))));
        foreach (string folder in _session.Folders)
        {
            int count = entries.Count(e => FolderComparer.Equals(e.Folder, folder));
            Navigation.Add(new NavigationItem(NavigationKind.Folder, folder, "", count, folder));
            MoveTargets.Add(new MenuTarget(folder, new RelayCommand(() => MoveSelectedEntry(folder))));
        }
        MoveTargets.Add(new MenuTarget("New folder…", new RelayCommand(MoveSelectedEntryToNewFolder)));

        var next = Navigation.FirstOrDefault(n => n.SameTarget(previous)) ?? Navigation[0];
        if (ReferenceEquals(next, SelectedNavigation))
        {
            ApplyFilter();
        }
        else
        {
            SelectedNavigation = next;
        }
    }

    private void ApplyFilter()
    {
        Guid? selectedId = SelectedEntry?.Id;
        var scope = SelectedNavigation?.Kind switch
        {
            NavigationKind.Favorites => _session.Entries.Where(e => e.IsFavorite),
            NavigationKind.Folder => _session.Entries.Where(e => FolderComparer.Equals(e.Folder, SelectedNavigation.Folder)),
            _ => _session.Entries,
        };

        Entries.Clear();
        foreach (var entry in EntrySearch.Search(scope, SearchText))
        {
            Entries.Add(entry);
        }
        SelectedEntry = Entries.FirstOrDefault(e => e.Id == selectedId) ?? Entries.FirstOrDefault();
    }

    private void Select(Guid id)
    {
        var entry = Entries.FirstOrDefault(e => e.Id == id);
        if (entry is null && _session.Find(id) is not null)
        {
            SearchText = "";
            SelectedNavigation = Navigation[0];
            entry = Entries.FirstOrDefault(e => e.Id == id);
        }
        SelectedEntry = entry ?? SelectedEntry;
    }
}
