using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using KeyKhan.App.Settings;

namespace KeyKhan.App.Services;

/// <summary>
/// Copies secrets so that Windows clipboard history and cloud sync skip them,
/// and clears them after the configured delay unless the user copied something else meanwhile.
/// </summary>
public sealed class ClipboardService : IDisposable
{
    private readonly SettingsStore _settings;
    private readonly DispatcherTimer _clearTimer = new();
    private string? _copied;

    public ClipboardService(SettingsStore settings)
    {
        _settings = settings;
        _clearTimer.Tick += (_, _) => ClearIfOwned();
    }

    public int ClearAfterSeconds => _settings.Current.ClipboardClearSeconds;

    public void CopySecret(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var data = new DataObject();
        data.SetText(text, TextDataFormat.UnicodeText);
        data.SetData("ExcludeClipboardContentFromMonitorProcessing", new MemoryStream(new byte[4]));
        data.SetData("CanIncludeInClipboardHistory", new MemoryStream(BitConverter.GetBytes(0)));
        data.SetData("CanUploadToCloudClipboard", new MemoryStream(BitConverter.GetBytes(0)));

        SetWithRetry(data);
        _copied = text;

        _clearTimer.Stop();
        if (ClearAfterSeconds > 0)
        {
            _clearTimer.Interval = TimeSpan.FromSeconds(ClearAfterSeconds);
            _clearTimer.Start();
        }
    }

    public void Dispose()
    {
        ClearIfOwned();
        _clearTimer.Stop();
    }

    private void ClearIfOwned()
    {
        _clearTimer.Stop();
        if (_copied is not null && Clipboard.ContainsText() && Clipboard.GetText() == _copied)
        {
            Clipboard.Clear();
        }
        _copied = null;
    }

    // Another process may hold the clipboard open for a moment.
    private static void SetWithRetry(DataObject data)
    {
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                Clipboard.SetDataObject(data, copy: true);
                return;
            }
            catch (COMException) when (attempt < 5)
            {
                Thread.Sleep(30);
            }
        }
    }
}
