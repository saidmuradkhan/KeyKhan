using System.Windows;
using System.Windows.Input;
using PassIn.App.Settings;

namespace PassIn.App.Services;

/// <summary>Applies the user's shortcuts to a view and keeps them current when settings change.</summary>
public sealed class ShortcutBinder : IDisposable
{
    private readonly UIElement _target;
    private readonly SettingsStore _settings;
    private readonly IReadOnlyDictionary<ShortcutAction, ICommand> _commands;
    private readonly KeyBinding[] _fixedBindings;
    private readonly bool _beforeTextInput;
    private List<KeyBinding> _bindings = [];

    /// <param name="beforeTextInput">
    /// Handle keys before a focused text box does. Text boxes consume keys such as PgUp, PgDn and Del themselves,
    /// so a view whose shortcuts must win over typing (quick access) sets this; the main window does not,
    /// so Del in the search box still deletes text rather than an entry.
    /// </param>
    /// <param name="fixedBindings">Bindings that are not configurable, checked after the user's shortcuts.</param>
    public ShortcutBinder(UIElement target, SettingsStore settings, IReadOnlyDictionary<ShortcutAction, ICommand> commands,
        bool beforeTextInput = false, params KeyBinding[] fixedBindings)
    {
        _target = target;
        _settings = settings;
        _commands = commands;
        _beforeTextInput = beforeTextInput;
        _fixedBindings = fixedBindings;

        Apply();
        _settings.Changed += OnSettingsChanged;
        if (_beforeTextInput)
        {
            _target.PreviewKeyDown += OnPreviewKeyDown;
        }
    }

    public void Dispose()
    {
        _settings.Changed -= OnSettingsChanged;
        _target.PreviewKeyDown -= OnPreviewKeyDown;
    }

    private void OnSettingsChanged(object? sender, EventArgs e) => Apply();

    private void Apply()
    {
        _bindings = [];
        foreach (var (action, command) in _commands)
        {
            if (_settings.Current.Shortcut(action).TryCreateGesture(out var gesture))
            {
                _bindings.Add(new KeyBinding(command, gesture));
            }
        }
        _bindings.AddRange(_fixedBindings);

        if (!_beforeTextInput)
        {
            _target.InputBindings.Clear();
            _target.InputBindings.AddRange(_bindings);
        }
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        foreach (var binding in _bindings)
        {
            if (binding.Gesture.Matches(_target, e) && binding.Command.CanExecute(null))
            {
                binding.Command.Execute(null);
                e.Handled = true;
                return;
            }
        }
    }
}
