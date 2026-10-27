namespace KeyKhan.Vault;

/// <summary>How a folder looks in the sidebar. Empty keys mean the default folder icon and colour.</summary>
public sealed record FolderStyle(string Icon = "", string Color = "")
{
    public static FolderStyle Default { get; } = new();
}