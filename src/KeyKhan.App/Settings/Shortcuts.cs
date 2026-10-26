using System.Windows.Input;

namespace KeyKhan.App.Settings;

public enum ShortcutScope
{
    MainWindow,
    QuickAccess,
}

public enum ShortcutAction
{
    Search,
    NewEntry,
    EditEntry,
    DeleteEntry,
    CopyPassword,
    CopyUsername,
    CopyTotp,
    OpenWebsite,
    ToggleFavorite,
    PasswordGenerator,
    Lock,
    QuickCopyPassword,
    QuickCopyUsername,
    QuickAutoType,
    QuickCopyTotp,
    QuickOpenWebsite,
    QuickNext,
    QuickPrevious,
    QuickClose,
}

public sealed record ShortcutDefinition(ShortcutAction Action, ShortcutScope Scope, string Label, KeyCombo Default);

public static class Shortcuts
{
    public static IReadOnlyList<ShortcutDefinition> All { get; } =
    [
        new(ShortcutAction.Search, ShortcutScope.MainWindow, "Search", new(ModifierKeys.Control, Key.F)),
        new(ShortcutAction.NewEntry, ShortcutScope.MainWindow, "New entry", new(ModifierKeys.Control, Key.N)),
        new(ShortcutAction.EditEntry, ShortcutScope.MainWindow, "Edit entry", new(ModifierKeys.Control, Key.E)),
        new(ShortcutAction.DeleteEntry, ShortcutScope.MainWindow, "Delete entry", new(ModifierKeys.None, Key.Delete)),
        new(ShortcutAction.CopyPassword, ShortcutScope.MainWindow, "Copy password", new(ModifierKeys.Control, Key.C)),
        new(ShortcutAction.CopyUsername, ShortcutScope.MainWindow, "Copy username", new(ModifierKeys.Control, Key.B)),
        new(ShortcutAction.CopyTotp, ShortcutScope.MainWindow, "Copy 2FA code", new(ModifierKeys.Control, Key.T)),
        new(ShortcutAction.OpenWebsite, ShortcutScope.MainWindow, "Open website", new(ModifierKeys.Control, Key.U)),
        new(ShortcutAction.ToggleFavorite, ShortcutScope.MainWindow, "Toggle favorite", new(ModifierKeys.Control, Key.D)),
        new(ShortcutAction.PasswordGenerator, ShortcutScope.MainWindow, "Password generator", new(ModifierKeys.Control, Key.G)),
        new(ShortcutAction.Lock, ShortcutScope.MainWindow, "Lock", new(ModifierKeys.Control, Key.L)),
        new(ShortcutAction.QuickCopyPassword, ShortcutScope.QuickAccess, "Copy password", new(ModifierKeys.None, Key.Enter)),
        new(ShortcutAction.QuickCopyUsername, ShortcutScope.QuickAccess, "Copy username", new(ModifierKeys.Shift, Key.Enter)),
        new(ShortcutAction.QuickAutoType, ShortcutScope.QuickAccess, "Auto-type login", new(ModifierKeys.Control, Key.Enter)),
        new(ShortcutAction.QuickCopyTotp, ShortcutScope.QuickAccess, "Copy 2FA code", new(ModifierKeys.Control, Key.T)),
        new(ShortcutAction.QuickOpenWebsite, ShortcutScope.QuickAccess, "Open website", new(ModifierKeys.Control, Key.O)),
        new(ShortcutAction.QuickNext, ShortcutScope.QuickAccess, "Next result", new(ModifierKeys.None, Key.PageDown)),
        new(ShortcutAction.QuickPrevious, ShortcutScope.QuickAccess, "Previous result", new(ModifierKeys.None, Key.PageUp)),
        new(ShortcutAction.QuickClose, ShortcutScope.QuickAccess, "Close", new(ModifierKeys.None, Key.Escape)),
    ];

    public static ShortcutDefinition Get(ShortcutAction action) => All.First(d => d.Action == action);

    /// <summary>
    /// Checks a full set of shortcuts; returns a message for the first problem, or <c>null</c> when all are usable.
    /// </summary>
    public static string? Validate(IReadOnlyDictionary<ShortcutAction, KeyCombo> shortcuts)
    {
        foreach (var definition in All)
        {
            if (!shortcuts[definition.Action].IsValidLocal)
            {
                return $"\"{definition.Label}\" needs a different shortcut: letters and digits must be combined with Ctrl or Alt.";
            }
        }

        foreach (var scope in All.GroupBy(d => d.Scope))
        {
            var clash = scope.GroupBy(d => shortcuts[d.Action]).FirstOrDefault(g => g.Count() > 1);
            if (clash is not null)
            {
                return $"{clash.Key} is used for both \"{clash.First().Label}\" and \"{clash.Last().Label}\".";
            }
        }
        return null;
    }
}
