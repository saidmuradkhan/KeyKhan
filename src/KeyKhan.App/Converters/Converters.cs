using System.Collections;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using KeyKhan.Vault.Passwords;

namespace KeyKhan.App.Converters;

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

/// <summary>Stable colour per title so the same entry always gets the same tile.</summary>
public sealed class AvatarBrushConverter : OneWayConverter
{
    private static readonly Brush[] Palette =
    [
        Frozen(0x2563EB), Frozen(0x7C3AED), Frozen(0xDB2777), Frozen(0xDC2626), Frozen(0xEA580C),
        Frozen(0x16A34A), Frozen(0x0D9488), Frozen(0x0891B2), Frozen(0x4F46E5), Frozen(0x9333EA),
    ];

    public override object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        string text = (value as string ?? "").ToUpperInvariant();
        uint hash = 2166136261;
        foreach (char c in text)
        {
            hash = (hash ^ c) * 16777619;
        }
        return Palette[hash % Palette.Length];
    }

    private static Brush Frozen(int rgb)
    {
        var brush = new SolidColorBrush(Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb));
        brush.Freeze();
        return brush;
    }
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
