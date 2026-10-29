using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PassIn.App.Settings;

namespace PassIn.App.ViewModels;

public sealed partial class ShortcutItemViewModel(ShortcutDefinition definition, KeyCombo combo) : ObservableObject
{
    [ObservableProperty]
    private KeyCombo _combo = combo;

    public ShortcutDefinition Definition { get; } = definition;
    public string Label => Definition.Label;

    [RelayCommand]
    private void Reset() => Combo = Definition.Default;
}
