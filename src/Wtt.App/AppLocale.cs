using System.Globalization;

namespace Wtt.App;

/// <summary>
/// The one place that decides the app's language and formats. The host's own messages follow its UI culture; the UI
/// receives its name in the start page's address and looks it up in its registry of locales. German with Swiss formats
/// is the only locale so far.
/// </summary>
internal static class AppLocale
{
    public const string Name = "de-CH";

    public static CultureInfo Culture { get; } = CultureInfo.GetCultureInfo(Name);

    public static void ApplyToHost()
    {
        CultureInfo.DefaultThreadCurrentUICulture = Culture;
        CultureInfo.CurrentUICulture = Culture;
    }
}
