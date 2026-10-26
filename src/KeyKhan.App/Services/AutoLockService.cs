using System.Windows.Threading;
using Microsoft.Win32;
using KeyKhan.App.Interop;
using KeyKhan.App.Settings;

namespace KeyKhan.App.Services;

/// <summary>Locks the vault after system-wide inactivity, on Windows lock and before sleep.</summary>
public sealed class AutoLockService : IDisposable
{
    private readonly VaultSession _session;
    private readonly SettingsStore _settings;
    private readonly Dispatcher _dispatcher;
    private readonly DispatcherTimer _idleTimer = new() { Interval = TimeSpan.FromSeconds(10) };

    public AutoLockService(VaultSession session, SettingsStore settings)
    {
        _session = session;
        _settings = settings;
        _dispatcher = Dispatcher.CurrentDispatcher;

        _idleTimer.Tick += (_, _) => LockIfIdle();
        _idleTimer.Start();
        SystemEvents.SessionSwitch += OnSessionSwitch;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
    }

    public void Dispose()
    {
        _idleTimer.Stop();
        SystemEvents.SessionSwitch -= OnSessionSwitch;
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
    }

    private void LockIfIdle()
    {
        int minutes = _settings.Current.AutoLockMinutes;
        if (minutes > 0 && _session.IsUnlocked && User32.GetIdleTime() >= TimeSpan.FromMinutes(minutes))
        {
            _session.Lock();
        }
    }

    private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
    {
        if (e.Reason == SessionSwitchReason.SessionLock && _settings.Current.LockOnSessionLock)
        {
            _dispatcher.BeginInvoke(_session.Lock);
        }
    }

    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Suspend)
        {
            _dispatcher.BeginInvoke(_session.Lock);
        }
    }
}
