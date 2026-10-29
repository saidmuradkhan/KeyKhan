using System.Collections;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using PassIn.App.Appearance;
using PassIn.Vault;
using PassIn.Vault.Passwords;

namespace PassIn.App.Converters;

public abstract class OneWayConverter : IValueConverter
{
    public abstract object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class BoolToVisibilityConverter : OneWayConverter
{
    public bool Invert { get; set; }

    public override object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ^ Invert ? Visibility.Visible : Visibility.Collapsed;
}

/// <summary>Collapses for null, empty strings, empty collections and zero.</summary>
public sealed class EmptyToCollapsedConverter : OneWayConverter
{
    public bool Invert { get; set; }

    public override object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool isEmpty = value switch
        {
            null => true,
            string s => s.Length == 0,
            ICollection c => c.Count == 0,
            int i => i == 0,
            _ => false,
        };
        return isEmpty ^ Invert ? Visibility.Collapsed : Visibility.Visible;
    }
}

public sealed class InitialConverter : OneWayConverter
{
    public override object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string { Length: > 0 } text ? char.ToUpper(text.TrimStart()[0], culture).ToString() : "?";
}

/// <summary>The tile colour of an entry: the one the user picked, otherwise a stable colour from its title.</summary>
public sealed class AvatarBrushConverter : OneWayConverter
{
    public override object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        VaultEntry entry => Catalog.Color(entry.Color)?.Brush ?? Catalog.ColorFor(entry.Title),
        string title => Catalog.ColorFor(title),
        _ => Catalog.ColorFor(""),
    };
}

/// <summary>Icon key to its glyph; <c>null</c> for no or unknown icon, so the view can show the initial instead.</summary>
public sealed class IconGlyphConverter : OneWayConverter
{
    public override object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Catalog.Icon(value as string)?.Glyph;
}

/// <summary>Colour key to its brush; falls back to the primary text colour.</summary>
public sealed class ColorKeyBrushConverter : OneWayConverter
{
    public override object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Catalog.Color(value as string)?.Brush ?? Application.Current.TryFindResource("TextFillColorPrimaryBrush");
}
/// <summary>True when the value equals the converter parameter, e.g. to tick the current choice in a menu.</summary>
public sealed class EqualsParameterConverter : OneWayConverter
{
    public override object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Equals(value, parameter);
}

public sealed class StrengthBrushConverter : OneWayConverter
{
    public override object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Application.Current.TryFindResource(value switch
        {
            StrengthLevel.VeryWeak or StrengthLevel.Weak => "SystemFillColorCriticalBrush",
            StrengthLevel.Fair => "SystemFillColorCautionBrush",
            _ => "SystemFillColorSuccessBrush",
        });
}

public sealed class StrengthValueConverter : OneWayConverter
{
    public override object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is StrengthLevel level ? (double)level : 0d;
}

public sealed class StrengthTextConverter : OneWayConverter
{
    public override object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        StrengthLevel.VeryWeak => "Very weak",
        StrengthLevel.Weak => "Weak",
        StrengthLevel.Fair => "Fair",
        StrengthLevel.Strong => "Strong",
        StrengthLevel.VeryStrong => "Very strong",
        _ => "",
    };
}
