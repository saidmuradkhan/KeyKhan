using System.Windows;
using System.Windows.Threading;
using PassIn.App.Services;

namespace PassIn.App;

public partial class App : Application
{
    private SingleInstance? _instance;
    private AppHost? _host;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _instance = SingleInstance.TryAcquire(AppPaths.ProfileKey);
        if (_instance is null)
        {
            Shutdown();
            return;
        }

        if (!LegacyMigration.Run())
        {
            MessageBox.Show("PassIn could not move your vault from the previous version's folder. Close the old version if it is still running and start PassIn again.",
                "PassIn", MessageBoxButton.OK, MessageBoxImage.Warning);
            Shutdown(1);
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
        MessageBox.Show(e.Exception.ToString(), "PassIn encountered an error", MessageBoxButton.OK, MessageBoxImage.Error);
        Shutdown(1);
    }
}
