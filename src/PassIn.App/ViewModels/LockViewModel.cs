using System.Security;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PassIn.App.Services;
using PassIn.Vault;
using PassIn.Vault.Passwords;
using PassIn.Vault.Security;

namespace PassIn.App.ViewModels;

public enum LockMode
{
    Create,
    Unlock,
    Recover,
}

/// <summary>Creates the vault on first run, unlocks it, or resets a forgotten master password with the recovery key.</summary>
public sealed partial class LockViewModel(VaultSession session) : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Subtitle), nameof(SubmitText), nameof(PasswordLabel),
        nameof(IsUnlocking), nameof(IsRecovering), nameof(IsChoosingPassword))]
    private LockMode _mode = session.Exists ? LockMode.Unlock : LockMode.Create;

    [ObservableProperty]
    private string _recoveryKey = "";

    [ObservableProperty]
    private string? _error;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private StrengthLevel _strength;

    public bool IsUnlocking => Mode == LockMode.Unlock;
    public bool IsRecovering => Mode == LockMode.Recover;
    public bool IsChoosingPassword => Mode != LockMode.Unlock;

    public string Subtitle => Mode switch
    {
        LockMode.Create => "Create a master password. You will also get a recovery key in case you ever forget it.",
        LockMode.Recover => "Enter your recovery key and choose a new master password.",
        _ => "Enter your master password to unlock your vault.",
    };

    public string SubmitText => Mode switch
    {
        LockMode.Create => "Create vault",
        LockMode.Recover => "Reset master password",
        _ => "Unlock",
    };

    public string PasswordLabel => IsRecovering ? "New master password" : "Master password";

    partial void OnModeChanged(LockMode value)
    {
        Error = null;
        RecoveryKey = "";
        Strength = StrengthLevel.None;
    }

    /// <summary>Skips the password when this Windows account is remembered.</summary>
    public Task<bool> TryUnlockWithDeviceAsync() =>
        IsUnlocking ? session.TryUnlockWithDeviceAsync() : Task.FromResult(false);

    public void OnPasswordChanged(SecureString password)
    {
        Error = null;
        if (IsChoosingPassword)
        {
            Strength = password.Use(PasswordStrength.Evaluate);
        }
    }

    [RelayCommand]
    private void ForgotPassword() => Mode = LockMode.Recover;

    [RelayCommand]
    private void BackToUnlock() => Mode = LockMode.Unlock;

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
            switch (Mode)
            {
                case LockMode.Create:
                    await session.CreateAsync(password);
                    break;
                case LockMode.Recover:
                    await session.RecoverAsync(RecoveryKey, password);
                    break;
                default:
                    await session.UnlockAsync(password);
                    break;
            }
        }
        catch (VaultAuthenticationException)
        {
            Error = IsRecovering ? "That recovery key does not open this vault." : "Incorrect master password.";
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
        if (IsRecovering && Vault.Security.RecoveryKey.Normalize(RecoveryKey) is null)
        {
            return "A recovery key has 32 letters and digits, like ABCD-EFGH-….";
        }
        if (password.Length == 0)
        {
            return IsRecovering ? "Choose a new master password." : "Enter your master password.";
        }
        if (!IsChoosingPassword)
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
