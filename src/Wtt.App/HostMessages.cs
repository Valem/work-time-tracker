using System.Globalization;
using System.Resources;

namespace Wtt.App;

/// <summary>
/// The few messages the host shows itself, instead of the page, from its resources in the host's UI culture: a new
/// language adds a resource file and changes no code.
/// </summary>
internal static class HostMessages
{
    private static readonly ResourceManager Resources = new("Wtt.App.HostMessages", typeof(HostMessages).Assembly);

    public static string WebViewMissing => Text(nameof(WebViewMissing));

    public static string PageCrashed => Text(nameof(PageCrashed));

    public static string PagesFailed(string errorLog) => Format(nameof(PagesFailed), errorLog);

    public static string DataFileUnavailable(string errorLog) => Format(nameof(DataFileUnavailable), errorLog);

    private static string Format(string name, params object[] values) =>
        string.Format(CultureInfo.CurrentUICulture, Text(name), values);

    private static string Text(string name) =>
        Resources.GetString(name, CultureInfo.CurrentUICulture) ?? throw new MissingManifestResourceException(name);
}
