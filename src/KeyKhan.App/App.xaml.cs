using System.Windows;
using System.Windows.Threading;
using KeyKhan.App.Services;

namespace KeyKhan.App;

public partial class App : Application
{
    private SingleInstance? _instance;
    private AppHost? _host;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _instance = SingleInstance.TryAcquire();
        if (_instance is null)
        {
            Shutdown();
            return;
        }

        DispatcherUnhandledException += OnUnhandledException;
        _host = new AppHost(this);
        _instance.OnActivationRequested(() => Dispatcher.BeginInvoke(_host.ShowMainWindow));
        _host.Start(startHidden: e.Args.Contains(StartupRegistration.MinimizedArgument));
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _host?.Dispose();
        _instance?.Dispose();
        base.OnExit(e);
    }

    // State after an unexpected failure cannot be trusted; report once and exit. The vault on disk is always consistent.
    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        DispatcherUnhandledException -= OnUnhandledException;
        MessageBox.Show(e.Exception.ToString(), "KeyKhan encountered an error", MessageBoxButton.OK, MessageBoxImage.Error);
        Shutdown(1);
    }
}
