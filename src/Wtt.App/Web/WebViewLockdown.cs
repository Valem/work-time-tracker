using Microsoft.Web.WebView2.Core;
using Wtt.Infrastructure.Diagnostics;

namespace Wtt.App.Web;

/// <summary>
/// The web view shows the app's own pages and nothing else: no outside navigation, new windows, downloads,
/// permissions, external protocols or script dialogs; no single sign-on, reputation checks, autofill or crash uploads.
/// Web messages are on for the bridge; the host answers only its own pages' messages.
/// </summary>
internal static class WebViewLockdown
{
    /// <summary>
    /// Variables the WebView2 loader reads before the app's own options: browser arguments (a remote debugging port),
    /// another profile folder, another runtime or channel, a script debugger.
    /// </summary>
    private static readonly string[] EnvironmentOverrides =
    [
        "WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS",
        "WEBVIEW2_USER_DATA_FOLDER",
        "WEBVIEW2_BROWSER_EXECUTABLE_FOLDER",
        "WEBVIEW2_RELEASE_CHANNEL_PREFERENCE",
        "WEBVIEW2_CHANNEL_SEARCH_KIND",
        "WEBVIEW2_RELEASE_CHANNELS",
        "WEBVIEW2_PIPE_FOR_SCRIPT_DEBUGGER",
        "WEBVIEW2_WAIT_FOR_SCRIPT_DEBUGGER",
    ];

    /// <summary>
    /// Unless the policy honours them, removes the overrides from this process's environment before the web view's
    /// environment is created, so neither the runtime nor its child processes see them.
    /// </summary>
    public static void ClearEnvironmentOverrides(WebViewPolicy policy)
    {
        if (policy.EnvironmentOverridesHonoured)
        {
            return;
        }

        foreach (var name in EnvironmentOverrides)
        {
            Environment.SetEnvironmentVariable(name, null);
        }
    }

    /// <exception cref="InvalidOperationException">The runtime keeps its profile somewhere else than the data folder.</exception>
    public static void EnsureProfileIn(CoreWebView2Environment environment, string userDataFolder, WebViewPolicy policy)
    {
        if (policy.EnvironmentOverridesHonoured || SameFolder(environment.UserDataFolder, userDataFolder))
        {
            return;
        }

        throw new InvalidOperationException($"The web view keeps its profile in '{environment.UserDataFolder}', not in '{userDataFolder}'.");
    }

    /// <param name="language">The language of anything the runtime itself shows, from the app's locale.</param>
    public static CoreWebView2EnvironmentOptions EnvironmentOptions(string language) => new()
    {
        AllowSingleSignOnUsingOSPrimaryAccount = false,
        IsCustomCrashReportingEnabled = true,
        EnableTrackingPrevention = false,
        AreBrowserExtensionsEnabled = false,
        Language = language,
    };

    public static void Apply(CoreWebView2 view, WebViewPolicy policy, ErrorLog log)
    {
        var settings = view.Settings;
        settings.AreDevToolsEnabled = policy.DevToolsEnabled;
        settings.AreBrowserAcceleratorKeysEnabled = policy.BrowserAcceleratorKeysEnabled;
        settings.AreDefaultContextMenusEnabled = policy.DefaultContextMenusEnabled;
        settings.AreHostObjectsAllowed = false;
        settings.IsWebMessageEnabled = true;
        settings.AreDefaultScriptDialogsEnabled = false;
        settings.IsStatusBarEnabled = false;
        settings.IsPasswordAutosaveEnabled = false;
        settings.IsGeneralAutofillEnabled = false;
        settings.IsReputationCheckingRequired = false;
        settings.IsSwipeNavigationEnabled = false;

        view.NavigationStarting += (_, e) => e.Cancel |= !LocalContent.IsAppContent(e.Uri);
        view.FrameNavigationStarting += (_, e) => e.Cancel |= !LocalContent.IsAppContent(e.Uri);
        view.NewWindowRequested += (_, e) => e.Handled = true;
        view.DownloadStarting += (_, e) => e.Cancel = true;
        view.LaunchingExternalUriScheme += (_, e) => e.Cancel = true;
        view.PermissionRequested += (_, e) => e.State = CoreWebView2PermissionState.Deny;
        view.ProcessFailed += (_, e) => log.Write($"Web view process failed: {e.ProcessFailedKind}, {e.Reason}, exit code {e.ExitCode}");
    }

    private static bool SameFolder(string a, string b) =>
        string.Equals(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(a)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(b)),
            StringComparison.OrdinalIgnoreCase);
}
