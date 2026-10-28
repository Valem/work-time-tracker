namespace Wtt.App.Web;

/// <summary>
/// The developer conveniences of the web view: on in a Debug build, off in Release. Off includes the WebView2
/// environment variables that would otherwise open a debugging port, move the profile or pick another runtime.
/// </summary>
internal sealed record WebViewPolicy(
    bool DevToolsEnabled,
    bool BrowserAcceleratorKeysEnabled,
    bool DefaultContextMenusEnabled,
    bool EnvironmentOverridesHonoured)
{
    public static WebViewPolicy For(bool developmentBuild) => new(developmentBuild, developmentBuild, developmentBuild, developmentBuild);

#if DEBUG
    public static WebViewPolicy ForThisBuild { get; } = For(developmentBuild: true);
#else
    public static WebViewPolicy ForThisBuild { get; } = For(developmentBuild: false);
#endif
}
