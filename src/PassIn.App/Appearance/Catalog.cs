using System.Windows.Media;

namespace PassIn.App.Appearance;

/// <param name="Glyph">A Segoe Fluent Icons character.</param>
public sealed record IconOption(string Key, string Label, string Glyph);

public sealed record ColorOption(string Key, string Label, Brush Brush);

/// <summary>
/// The icons and colours users can give entries and folders. Vaults store the keys, so glyphs and shades
/// can change without touching saved data; unknown keys fall back to the automatic look.
/// </summary>
public static class Catalog
{
    public const string FolderGlyph = "";

    public static IReadOnlyList<IconOption> Icons { get; } =
    [
        new("globe", "Website", ""), new("mail", "Mail", ""), new("chat", "Chat", ""),
        new("social", "Social", ""), new("people", "Team", ""), new("person", "Account", ""),
        new("phone", "Phone", ""), new("home", "Home", ""), new("work", "Work", ""),
        new("bank", "Bank", ""), new("card", "Card", ""), new("shop", "Shop", ""),
        new("cart", "Cart", ""), new("tag", "Tag", ""), new("code", "Code", ""),
        new("terminal", "Terminal", ""), new("bug", "Bug", ""), new("cloud", "Cloud", ""),
        new("download", "Download", ""), new("server", "Server", ""), new("network", "Network", ""),
        new("connections", "Connections", ""), new("wifi", "Wi-Fi", ""), new("antenna", "Antenna", ""),
        new("vpn", "VPN", ""), new("key", "Key", ""), new("lock", "Lock", ""),
        new("shield", "Shield", ""), new("game", "Game", ""), new("music", "Music", ""),
        new("video", "Video", ""), new("tv", "TV", ""), new("camera", "Camera", ""),
        new("photo", "Photo", ""), new("headphones", "Headphones", ""), new("mic", "Microphone", ""),
        new("book", "Book", ""), new("education", "Education", ""), new("document", "Document", ""),
        new("clipboard", "Checklist", ""), new("folder", "Folder", ""), new("archive", "Archive", ""),
        new("package", "Package", ""), new("library", "Library", ""), new("calendar", "Calendar", ""),
        new("clock", "Clock", ""), new("airplane", "Travel", ""), new("car", "Car", ""),
        new("bus", "Transport", ""), new("world", "World", ""), new("health", "Health", ""),
        new("heart", "Heart", ""), new("star", "Star", ""), new("flag", "Flag", ""),
        new("lightning", "Lightning", ""), new("idea", "Idea", ""), new("megaphone", "Marketing", ""),
        new("smile", "Fun", ""), new("like", "Like", ""), new("chart", "Chart", ""),
        new("trend", "Trend", ""), new("gauge", "Dashboard", ""), new("tools", "Tools", ""),
        new("settings", "Settings", ""), new("admin", "Admin", ""), new("laptop", "Laptop", ""),
        new("devices", "Devices", ""), new("printer", "Printer", ""), new("link", "Link", ""),
        new("cafe", "Cafe", ""), new("food", "Food", ""), new("puzzle", "Puzzle", ""),
        new("sparkle", "Sparkle", ""), new("warning", "Important", ""), new("power", "Power", ""),
    ];

    public static IReadOnlyList<ColorOption> Colors { get; } =
    [
        new("blue", "Blue", Frozen(0x2563EB)), new("indigo", "Indigo", Frozen(0x4F46E5)),
        new("violet", "Violet", Frozen(0x7C3AED)), new("purple", "Purple", Frozen(0x9333EA)),
        new("pink", "Pink", Frozen(0xDB2777)), new("red", "Red", Frozen(0xDC2626)),
        new("orange", "Orange", Frozen(0xEA580C)), new("amber", "Amber", Frozen(0xD97706)),
        new("green", "Green", Frozen(0x16A34A)), new("teal", "Teal", Frozen(0x0D9488)),
        new("cyan", "Cyan", Frozen(0x0891B2)), new("slate", "Slate", Frozen(0x475569)),
    ];

    public static IconOption? Icon(string? key) => Icons.FirstOrDefault(i => i.Key == key);

    public static ColorOption? Color(string? key) => Colors.FirstOrDefault(c => c.Key == key);

    /// <summary>The automatic colour: stable per title, so an entry keeps its colour between sessions.</summary>
    public static Brush ColorFor(string title)
    {
        uint hash = 2166136261;
        foreach (char c in title.ToUpperInvariant())
        {
            hash = (hash ^ c) * 16777619;
        }
        return Colors[(int)(hash % (uint)Colors.Count)].Brush;
    }

    private static Brush Frozen(int rgb)
    {
        var brush = new SolidColorBrush(System.Windows.Media.Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb));
        brush.Freeze();
        return brush;
    }
}
