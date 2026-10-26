using System.Diagnostics;

namespace KeyKhan.App.Services;

public static class UrlLauncher
{
    /// <summary>Opens http(s) addresses in the default browser; bare hosts are treated as https.</summary>
    public static bool TryOpen(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        string candidate = url.Contains("://", StringComparison.Ordinal) ? url.Trim() : "https://" + url.Trim();
        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            return false;
        }

        Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
        return true;
    }
}
