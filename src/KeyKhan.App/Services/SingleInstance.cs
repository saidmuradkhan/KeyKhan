namespace KeyKhan.App.Services;

/// <summary>Keeps one running instance per user session; later launches bring the first one forward.</summary>
public sealed class SingleInstance : IDisposable
{
    private const string MutexName = @"Local\KeyKhan.Instance.7c1e2f0a";
    private const string ActivateEventName = @"Local\KeyKhan.Activate.7c1e2f0a";

    private readonly Mutex _mutex;
    private readonly EventWaitHandle _activate;
    private RegisteredWaitHandle? _registration;

    private SingleInstance(Mutex mutex, EventWaitHandle activate)
    {
        _mutex = mutex;
        _activate = activate;
    }

    /// <returns><c>null</c> if another instance owns the session; that instance is asked to show itself.</returns>
    public static SingleInstance? TryAcquire()
    {
        var mutex = new Mutex(initiallyOwned: false, MutexName, out bool createdNew);
        if (createdNew)
        {
            return new SingleInstance(mutex, new EventWaitHandle(false, EventResetMode.AutoReset, ActivateEventName));
        }

        mutex.Dispose();
        if (EventWaitHandle.TryOpenExisting(ActivateEventName, out var activate))
        {
            using (activate)
            {
                activate.Set();
            }
        }
        return null;
    }

    public void OnActivationRequested(Action callback) =>
        _registration = ThreadPool.RegisterWaitForSingleObject(_activate, (_, _) => callback(), null, Timeout.Infinite, executeOnlyOnce: false);

    public void Dispose()
    {
        _registration?.Unregister(null);
        _activate.Dispose();
        _mutex.Dispose();
    }
}
