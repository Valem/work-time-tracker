using System.Text.RegularExpressions;
using Wtt.App.Web;

namespace Wtt.App.Tests;

public sealed class LocalContentTests
{
    [Theory]
    [InlineData("https://app.wtt.example/index.html")]
    [InlineData("https://APP.WTT.EXAMPLE/index.html")]
    [InlineData("https://app.wtt.example/week?monday=2026-03-02#today")]
    public void The_apps_own_pages_may_be_opened(string uri)
    {
        Assert.True(LocalContent.IsAppContent(uri));
    }

    [Theory]
    [InlineData("https://example.com/")]
    [InlineData("https://app.wtt.example.evil.test/index.html")]
    [InlineData("https://evil.test/app.wtt.example/index.html")]
    [InlineData("http://app.wtt.example/index.html")]
    [InlineData("https://app.wtt.example:8443/index.html")]
    [InlineData("https://user@app.wtt.example/index.html")]
    [InlineData("file:///C:/Windows/win.ini")]
    [InlineData("data:text/html,<p>hi</p>")]
    [InlineData("javascript:alert(1)")]
    [InlineData("about:blank")]
    [InlineData("edge://settings")]
    [InlineData("/index.html")]
    [InlineData("")]
    [InlineData(null)]
    public void Everything_else_is_refused(string? uri)
    {
        Assert.False(LocalContent.IsAppContent(uri));
    }

    [Fact]
    public void The_start_page_is_app_content_on_a_reserved_host_name()
    {
        Assert.True(LocalContent.IsAppContent(LocalContent.StartPage.AbsoluteUri));
        Assert.EndsWith(".example", LocalContent.HostName);
    }
}

public sealed class WebViewPolicyTests
{
    [Fact]
    public void A_release_build_has_no_developer_tools_shortcuts_or_context_menu()
    {
        Assert.Equal(new WebViewPolicy(false, false, false), WebViewPolicy.For(developmentBuild: false));
    }

    [Fact]
    public void A_development_build_keeps_the_developer_tools()
    {
        Assert.True(WebViewPolicy.For(developmentBuild: true).DevToolsEnabled);
    }

#if !DEBUG
    [Fact]
    public void This_release_build_uses_the_release_policy()
    {
        Assert.Equal(WebViewPolicy.For(developmentBuild: false), WebViewPolicy.ForThisBuild);
    }
#endif

    [Fact]
    public void The_environment_keeps_crash_reports_local_and_sign_in_off()
    {
        var options = WebViewLockdown.EnvironmentOptions();

        Assert.True(options.IsCustomCrashReportingEnabled);
        Assert.False(options.AllowSingleSignOnUsingOSPrimaryAccount);
        Assert.False(options.AreBrowserExtensionsEnabled);
        Assert.True(string.IsNullOrEmpty(options.AdditionalBrowserArguments));
    }
}

public sealed class PlaceholderPageTests
{
    private static readonly string Content = Path.Combine(AppContext.BaseDirectory, "wwwroot");

    [Fact]
    public void The_placeholder_page_ships_with_the_app()
    {
        Assert.True(File.Exists(Path.Combine(Content, "index.html")));
    }

    [Fact]
    public void The_page_allows_only_its_own_content()
    {
        var page = File.ReadAllText(Path.Combine(Content, "index.html"));

        Assert.Contains("Content-Security-Policy", page);
        Assert.Contains("default-src 'self'", page);
    }

    [Fact]
    public void No_shipped_page_or_style_refers_to_anything_outside()
    {
        var remote = new Regex(@"(https?:)?//[a-z0-9.-]+", RegexOptions.IgnoreCase);
        foreach (var file in Directory.EnumerateFiles(Content, "*", SearchOption.AllDirectories))
        {
            Assert.DoesNotMatch(remote, File.ReadAllText(file));
        }
    }
}
