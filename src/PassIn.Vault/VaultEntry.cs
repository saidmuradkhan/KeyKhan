namespace PassIn.Vault;

public sealed record VaultEntry
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Title { get; init; }
    public string Username { get; init; } = "";
    public string Password { get; init; } = "";
    public string Url { get; init; } = "";
    public string Folder { get; init; } = "";
    public string Notes { get; init; } = "";

    /// <summary>Base32 secret or a full <c>otpauth://totp/</c> URI.</summary>
    public string Totp { get; init; } = "";

    public bool IsFavorite { get; init; }

    /// <summary>Icon and colour keys chosen by the user; empty means automatic (initial letter, colour from the title).</summary>
    public string Icon { get; init; } = "";
    public string Color { get; init; } = "";
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ModifiedAt { get; init; } = DateTimeOffset.UtcNow;
}
