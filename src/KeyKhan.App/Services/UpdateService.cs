using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Threading;
using KeyKhan.App.Settings;

namespace KeyKhan.App.Services;

/// <param name="SetupSha256">GitHub's digest of the installer; installs are refused without it.</param>
public sealed record AppRelease(Version Version, Uri Page, Uri? SetupUrl, string? SetupSha256)
{
    private const string SetupAssetName = "KeyKhan-Setup.exe";

    /// <summary>Reads GitHub's "latest release" response; <c>null</c> if it is not a usable release.</summary>
    public static AppRelease? Parse(string json)
    {
        var release = JsonSerializer.Deserialize<GitHubRelease>(json);
        if (release?.Tag is null || release.Page is null || !Version.TryParse(release.Tag.TrimStart('v', 'V'), out var version))
        {
            return null;
        }

        var setup = release.Assets?.FirstOrDefault(a => a.Name == SetupAssetName);
        string? sha256 = setup?.Digest is { } digest && digest.StartsWith("sha256:", StringComparison.Ordinal) ? digest[7..] : null;
        return new AppRelease(version, new Uri(release.Page), setup?.Url is null ? null : new Uri(setup.Url), sha256);
    }

    private sealed record GitHubRelease(
        [property: JsonPropertyName("tag_name")] string? Tag,
        [property: JsonPropertyName("html_url")] string? Page,
        [property: JsonPropertyName("assets")] List<GitHubAsset>? Assets);

    private sealed record GitHubAsset(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("browser_download_url")] string? Url,
        [property: JsonPropertyName("digest")] string? Digest);
}

/// <summary>
/// Looks for a newer release on GitHub and installs it. This is the app's only network access,
/// and it can be turned off in Settings.
/// </summary>
public sealed class UpdateService : IDisposable
{
    private const string LatestReleaseUrl = "https://api.github.com/repos/saidmuradkhan/KeyKhan/releases/latest";
    private static readonly TimeSpan FirstCheckDelay = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(3);

    // Opening the window checks again, but not more often than this (GitHub allows 60 anonymous requests an hour).
    private static readonly TimeSpan MinimumGap = TimeSpan.FromMinutes(30);

    private static readonly string InstallDirectory =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "KeyKhan");

    private readonly SettingsStore _settings;
    private readonly HttpClient _http;
    private readonly DispatcherTimer _timer = new() { Interval = FirstCheckDelay };
    private DateTime _lastCheck = DateTime.MinValue;

    public UpdateService(SettingsStore settings)
    {
        _settings = settings;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("KeyKhan", CurrentVersion.ToString(3)));
        _timer.Tick += OnTimerTick;
    }

    public static Version CurrentVersion { get; } = typeof(UpdateService).Assembly.GetName().Version ?? new Version(0, 0, 0);

    /// <summary>Installed with KeyKhan-Setup.exe, as opposed to running the portable executable.</summary>
    public static bool IsInstalled =>
        Environment.ProcessPath?.StartsWith(InstallDirectory, StringComparison.OrdinalIgnoreCase) == true;

    public AppRelease? Available { get; private set; }

    public event EventHandler? UpdateAvailable;

    public void StartAutomaticChecks() => _timer.Start();

    /// <summary>Checks quietly when automatic checks are on and the last check is not recent; errors are ignored.</summary>
    public async void CheckIfDue()
    {
        if (!_settings.Current.CheckForUpdates || DateTime.UtcNow - _lastCheck < MinimumGap)
        {
            return;
        }

        try
        {
            await CheckAsync();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            // Offline or rate-limited: the next interval or window activation tries again.
        }
    }

    /// <returns>The newer release, or <c>null</c> when this is the latest version.</returns>
    /// <exception cref="HttpRequestException">GitHub could not be reached.</exception>
    public async Task<AppRelease?> CheckAsync()
    {
        _lastCheck = DateTime.UtcNow;
        var release = AppRelease.Parse(await _http.GetStringAsync(LatestReleaseUrl));
        if (release is null || release.Version <= CurrentVersion)
        {
            return null;
        }

        if (Available?.Version != release.Version)
        {
            Available = release;
            UpdateAvailable?.Invoke(this, EventArgs.Empty);
        }
        return release;
    }

    /// <summary>
    /// Downloads the installer, checks it against GitHub's SHA-256 digest and starts it. The installer closes
    /// this process and relaunches KeyKhan when it is done. Portable copies open the release page instead.
    /// </summary>
    /// <returns><c>true</c> when the installer was started and the app should exit.</returns>
    public async Task<bool> InstallAsync(AppRelease release)
    {
        if (!IsInstalled || release.SetupUrl is null || release.SetupSha256 is null)
        {
            Process.Start(new ProcessStartInfo(release.Page.AbsoluteUri) { UseShellExecute = true });
            return false;
        }

        byte[] installer = await _http.GetByteArrayAsync(release.SetupUrl);
        string actual = Convert.ToHexString(SHA256.HashData(installer));
        if (!actual.Equals(release.SetupSha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new CryptographicException("The downloaded installer does not match the published checksum.");
        }

        string path = Path.Combine(Path.GetTempPath(), $"KeyKhan-Setup-{release.Version}.exe");
        await File.WriteAllBytesAsync(path, installer);
        Process.Start(new ProcessStartInfo(path, "/SILENT /SUPPRESSMSGBOXES /NORESTART /RELAUNCH=1") { UseShellExecute = true });
        return true;
    }

    public void Dispose()
    {
        _timer.Stop();
        _http.Dispose();
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        _timer.Interval = CheckInterval;
        CheckIfDue();
    }
}
