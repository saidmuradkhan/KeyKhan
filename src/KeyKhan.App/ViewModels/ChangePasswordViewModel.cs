using System.Security;
using CommunityToolkit.Mvvm.ComponentModel;
using KeyKhan.App.Services;
using KeyKhan.Vault;
using KeyKhan.Vault.Passwords;
using KeyKhan.Vault.Security;

namespace KeyKhan.App.ViewModels;

public sealed partial class ChangePasswordViewModel(VaultSession session) : ObservableObject
{
    [ObservableProperty]
    private string? _error;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private StrengthLevel _strength;

    public event EventHandler? Completed;

    public void OnNewPasswordChanged(SecureString password)
    {
        Error = null;
        Strength = password.Use(PasswordStrength.Evaluate);
    }

    public async Task SubmitAsync(SecureString current, SecureString next, SecureString confirmation)
    {
        if (IsBusy)
        {
            return;
        }

        if (next.Length < VaultSession.MinimumMasterPasswordLength)
        {
            Error = $"Use at least {VaultSession.MinimumMasterPasswordLength} characters.";
            return;
        }
        if (!next.ContentEquals(confirmation))
        {
            Error = "The new passwords do not match.";
            return;
        }

        IsBusy = true;
        try
        {
            await session.ChangeMasterPasswordAsync(current, next);
            Completed?.Invoke(this, EventArgs.Empty);
        }
        catch (VaultAuthenticationException)
        {
            Error = "The current master password is incorrect.";
        }
        catch (IOException ex)
        {
            Error = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
