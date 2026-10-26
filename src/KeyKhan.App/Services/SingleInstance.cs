namespace KeyKhan.App.Services;

/// <summary>Keeps one running instance per profile and user session; later launches bring the first one forward.</summary>
public sealed class SingleInstance : IDisposable
{
    private const string NamePrefix = @"Local\KeyKhan";
    private const string NameSuffix = "7c1e2f0a";

    private readonly Mutex _mutex;
    private readonly EventWaitHandle _activate;
    private RegisteredWaitHandle? _registration;

    private SingleInstance(Mutex mutex, EventWaitHandle activate)
    {
        _mutex = mutex;
        _activate = activate;
    }

    /// <param name="profile">Set for a non-default profile so it can run beside the default one.</param>
    /// <returns><c>null</c> if another instance owns the profile; that instance is asked to show itself.</returns>
    public static SingleInstance? TryAcquire(string? profile)
    {
        string suffix = profile is null ? NameSuffix : $"{NameSuffix}.{profile}";
        string mutexName = $"{NamePrefix}.Instance.{suffix}";
        string activateEventName = $"{NamePrefix}.Activate.{suffix}";

        var mutex = new Mutex(initiallyOwned: false, mutexName, out bool createdNew);
        if (createdNew)
        {
            return new SingleInstance(mutex, new EventWaitHandle(false, EventResetMode.AutoReset, activateEventName));
        }

        mutex.Dispose();
        if (EventWaitHandle.TryOpenExisting(activateEventName, out var activate))
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
