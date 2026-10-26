namespace KeyKhan.Vault;

public sealed class VaultAuthenticationException()
    : Exception("The master password is incorrect or the vault file is damaged.");
