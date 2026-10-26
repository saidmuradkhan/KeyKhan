using System.Text.Json.Serialization;

namespace KeyKhan.Vault.Storage;

/// <param name="Folders">Folders the user created, including empty ones. Absent in vaults written before folders existed.</param>
internal sealed record VaultPayload(List<VaultEntry> Entries, List<string>? Folders = null);

[JsonSerializable(typeof(VaultPayload))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal sealed partial class VaultJsonContext : JsonSerializerContext;
