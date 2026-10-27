using System.Text.Json.Serialization;

namespace KeyKhan.Vault.Storage;

/// <param name="Folders">Folders the user created, including empty ones. Absent in vaults written before folders existed.</param>
/// <param name="FolderStyles">Custom folder icons and colours by folder name.</param>
internal sealed record VaultPayload(
    List<VaultEntry> Entries, List<string>? Folders = null, Dictionary<string, FolderStyle>? FolderStyles = null);

[JsonSerializable(typeof(VaultPayload))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal sealed partial class VaultJsonContext : JsonSerializerContext;
