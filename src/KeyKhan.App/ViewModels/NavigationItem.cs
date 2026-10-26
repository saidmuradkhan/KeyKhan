namespace KeyKhan.App.ViewModels;

public enum NavigationKind
{
    All,
    Favorites,
    Folder,
}

public sealed record NavigationItem(NavigationKind Kind, string Title, string Glyph, int Count, string? Folder = null)
{
    public bool SameTarget(NavigationItem? other) => other is not null && other.Kind == Kind && other.Folder == Folder;
}
