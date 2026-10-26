namespace Wtt.App.Web;

/// <summary>The developer conveniences of the web view: on in a Debug build, off in Release.</summary>
internal sealed record WebViewPolicy(bool DevToolsEnabled, bool BrowserAcceleratorKeysEnabled, bool DefaultContextMenusEnabled)
{
    public static WebViewPolicy For(bool developmentBuild) => new(developmentBuild, developmentBuild, developmentBuild);

#if DEBUG
    public static WebViewPolicy ForThisBuild { get; } = For(developmentBuild: true);
#else
    public static WebViewPolicy ForThisBuild { get; } = For(developmentBuild: false);
#endif
}
