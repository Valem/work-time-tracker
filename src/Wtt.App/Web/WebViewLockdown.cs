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
}
