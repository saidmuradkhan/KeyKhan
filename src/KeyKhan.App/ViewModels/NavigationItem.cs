namespace KeyKhan.App.ViewModels;

public enum NavigationKind
{
    All,
    Favorites,
    Folder,
}

/// <param name="Color">Colour key for the icon; empty for the default text colour.</param>
public sealed record NavigationItem(NavigationKind Kind, string Title, string Glyph, int Count, string? Folder = null, string Color = "")
{
    public bool SameTarget(NavigationItem? other) => other is not null && other.Kind == Kind && other.Folder == Folder;
}
