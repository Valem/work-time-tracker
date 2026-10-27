namespace Wtt.App.Web;

/// <summary>
/// The app's pages load from the install folder through a virtual host; a reserved <c>.example</c> name is
/// never a real site, and WebView2 resolves it without DNS.
/// </summary>
internal static class LocalContent
{
    public const string HostName = "app.wtt.example";

    /// <summary>The start page, told which locale to show.</summary>
    public static Uri StartPage(string locale) => new($"https://{HostName}/index.html?locale={Uri.EscapeDataString(locale)}");

    /// <summary>Only the app's own pages may be navigated to or send messages; anything else is refused.</summary>
    public static bool IsAppContent(string? uri) =>
        Uri.TryCreate(uri, UriKind.Absolute, out var parsed)
        && parsed.Scheme == Uri.UriSchemeHttps
        && parsed.IsDefaultPort
        && string.IsNullOrEmpty(parsed.UserInfo)
        && string.Equals(parsed.Host, HostName, StringComparison.OrdinalIgnoreCase);
}
