using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using Wtt.App.Diagnostics;
using Wtt.App.Web;

namespace Wtt.App;

/// <summary>The app's one window: a web view on the app's own pages.</summary>
internal sealed class MainForm : Form
{
    private readonly AppFolders folders;
    private readonly ErrorLog log;
    private readonly WebView2 view = new() { Dock = DockStyle.Fill };

    public MainForm(AppFolders folders, ErrorLog log)
    {
        this.folders = folders;
        this.log = log;
        Text = "Work Time Tracker";
        ClientSize = new Size(1100, 720);
        MinimumSize = new Size(640, 480);
        StartPosition = FormStartPosition.CenterScreen;
        Controls.Add(view);
    }

    protected override async void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        try
        {
            await ShowAppContentAsync();
        }
        catch (WebView2RuntimeNotFoundException failure)
        {
            log.Write("The WebView2 runtime is not installed", failure);
            ShowProblem("This app needs the Microsoft Edge WebView2 Runtime, which is part of Windows 11.");
        }
        catch (Exception failure)
        {
            log.Write("The window could not show its pages", failure);
            ShowProblem($"The window could not show its pages. Details are in {log.Path}.");
        }
    }

    private async Task ShowAppContentAsync()
    {
        var environment = await CoreWebView2Environment.CreateAsync(
            browserExecutableFolder: null,
            userDataFolder: folders.WebViewDataFolder,
            options: WebViewLockdown.EnvironmentOptions());
        await view.EnsureCoreWebView2Async(environment);

        var core = view.CoreWebView2;
        WebViewLockdown.Apply(core, WebViewPolicy.ForThisBuild, log);
        core.SetVirtualHostNameToFolderMapping(LocalContent.HostName, folders.ContentFolder, CoreWebView2HostResourceAccessKind.Deny);
        core.Navigate(LocalContent.StartPage.AbsoluteUri);
    }

    private void ShowProblem(string message)
    {
        Controls.Remove(view);
        view.Dispose();
        Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Text = message,
        });
    }
}
