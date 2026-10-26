using System.Security;
using CommunityToolkit.Mvvm.ComponentModel;
using KeyKhan.App.Services;
using KeyKhan.Vault;
using KeyKhan.Vault.Passwords;
using KeyKhan.Vault.Security;

namespace KeyKhan.App.ViewModels;

/// <summary>Unlocks an existing vault, or creates one on first run.</summary>
public sealed partial class LockViewModel(VaultSession session) : ObservableObject
{
    [ObservableProperty]
    private string? _error;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private StrengthLevel _strength;

    public bool IsNewVault { get; } = !session.Exists;

    public string Subtitle => IsNewVault
        ? "Create a master password. It encrypts your vault and cannot be recovered if forgotten."
        : "Enter your master password to unlock your vault.";

    public string SubmitText => IsNewVault ? "Create vault" : "Unlock";

    public void OnPasswordChanged(SecureString password)
    {
        Error = null;
        if (IsNewVault)
        {
            Strength = password.Use(PasswordStrength.Evaluate);
        }
    }

    public async Task SubmitAsync(SecureString password, SecureString confirmation)
    {
        if (IsBusy)
        {
            return;
        }

        Error = Validate(password, confirmation);
        if (Error is not null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            if (IsNewVault)
            {
                await session.CreateAsync(password);
            }
            else
            {
                await session.UnlockAsync(password);
            }
        }
        catch (VaultAuthenticationException)
        {
            Error = "Incorrect master password.";
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            Error = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private string? Validate(SecureString password, SecureString confirmation)
    {
        if (password.Length == 0)
        {
            return "Enter your master password.";
        }
        if (!IsNewVault)
        {
            return null;
        }
        if (password.Length < VaultSession.MinimumMasterPasswordLength)
        {
            return $"Use at least {VaultSession.MinimumMasterPasswordLength} characters.";
        }
        return password.ContentEquals(confirmation) ? null : "The passwords do not match.";
    }
}
