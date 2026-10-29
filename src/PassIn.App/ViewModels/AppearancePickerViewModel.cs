using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PassIn.App.Appearance;
using PassIn.Vault;

namespace PassIn.App.ViewModels;

/// <summary>Empty keys mean the automatic look.</summary>
public sealed record AppearanceChoice(string Icon, string Color);

public sealed partial class AppearancePickerViewModel : ObservableObject
{
    private readonly string _name;
    private readonly bool _isFolder;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Preview))]
    private IconOption? _selectedIcon;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Preview))]
    private ColorOption? _selectedColor;

    [ObservableProperty]
    private string _search = "";

    public AppearancePickerViewModel(string name, string icon, string color, bool isFolder)
    {
        _name = name;
        _isFolder = isFolder;
        _selectedIcon = Catalog.Icon(icon);
        _selectedColor = Catalog.Color(color);
        Icons = new ObservableCollection<IconOption>(Catalog.Icons);
    }

    public event EventHandler? CloseRequested;

    public string Title => $"Icon for {_name}";
    public ObservableCollection<IconOption> Icons { get; }
    public IReadOnlyList<ColorOption> Colors => Catalog.Colors;

    /// <summary>Rendered with the regular entry tile, so the preview matches the list exactly.</summary>
    public VaultEntry Preview => new()
    {
        Title = _name,
        Icon = SelectedIcon?.Key ?? (_isFolder ? "folder" : ""),
        Color = SelectedColor?.Key ?? "",
    };

    public AppearanceChoice? Result { get; private set; }

    partial void OnSearchChanged(string value)
    {
        Icons.Clear();
        foreach (var icon in Catalog.Icons.Where(i =>
                     i.Label.Contains(value.Trim(), StringComparison.CurrentCultureIgnoreCase) ||
                     i.Key.Contains(value.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            Icons.Add(icon);
        }
    }

    [RelayCommand]
    private void AutomaticIcon() => SelectedIcon = null;

    [RelayCommand]
    private void AutomaticColor() => SelectedColor = null;

    [RelayCommand]
    private void Save()
    {
        Result = new AppearanceChoice(SelectedIcon?.Key ?? "", SelectedColor?.Key ?? "");
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }
}
