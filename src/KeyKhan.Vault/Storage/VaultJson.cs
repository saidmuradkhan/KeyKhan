using System.Text.Json.Serialization;

namespace KeyKhan.Vault.Storage;

internal sealed record VaultPayload(List<VaultEntry> Entries);

[JsonSerializable(typeof(VaultPayload))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal sealed partial class VaultJsonContext : JsonSerializerContext;
