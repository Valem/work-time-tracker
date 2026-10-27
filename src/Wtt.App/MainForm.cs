using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using Wtt.App.Bridge;
using Wtt.App.Web;
using Wtt.Infrastructure.Diagnostics;

namespace Wtt.App;

/// <summary>
/// The app's one window: a web view on the app's own pages, answering their bridge messages off the window's thread.
/// When the pages cannot be shown — no runtime, a crashed page, a data file it cannot use — the window shows the host's
/// own message instead and stays that way until the app is restarted.
/// </summary>
internal sealed class MainForm : Form
{
    private readonly AppFolders folders;
    private readonly ErrorLog log;
    private readonly BridgeHost bridge;
    private readonly Task data;
    private WebView2? view = new() { Dock = DockStyle.Fill };

    public MainForm(AppFolders folders, ErrorLog log, BridgeHost bridge, Task data)
    {
        this.folders = folders;
        this.log = log;
        this.bridge = bridge;
        this.data = data;
        Text = "Work Time Tracker";
        ClientSize = new Size(1100, 720);
        MinimumSize = new Size(640, 480);
        StartPosition = FormStartPosition.CenterScreen;
        Controls.Add(view);
    }

    protected override async void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        _ = ShowProblemIfDataFailsAsync();
        try
        {
            await ShowAppContentAsync();
        }
        catch (WebView2RuntimeNotFoundException failure)
        {
            log.Write("The WebView2 runtime is not installed", failure);
            ShowProblem(HostMessages.WebViewMissing);
        }
        catch (Exception failure)
        {
            log.Write("The window could not show its pages", failure);
            ShowProblem(HostMessages.PagesFailed(log.Path));
        }
    }

    /// <summary>A minimized window hides the page, so it notices when it is shown again and refreshes.</summary>
    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (view is not null)
        {
            view.Visible = WindowState != FormWindowState.Minimized;
        }
    }

    private async Task ShowAppContentAsync()
    {
        var environment = await CoreWebView2Environment.CreateAsync(
            browserExecutableFolder: null,
            userDataFolder: folders.WebViewDataFolder,
            options: WebViewLockdown.EnvironmentOptions(AppLocale.Name));
        if (view is null)
        {
            return;
        }

        await view.EnsureCoreWebView2Async(environment);
        view.AllowExternalDrop = false;

        var core = view.CoreWebView2;
        WebViewLockdown.Apply(core, WebViewPolicy.ForThisBuild, log);
        core.ProcessFailed += OnProcessFailed;
        core.WebMessageReceived += OnWebMessageReceived;
        core.SetVirtualHostNameToFolderMapping(LocalContent.HostName, folders.ContentFolder, CoreWebView2HostResourceAccessKind.Deny);
        core.Navigate(LocalContent.StartPage(AppLocale.Name).AbsoluteUri);
    }

    private async Task ShowProblemIfDataFailsAsync()
    {
        try
        {
            await data;
        }
        catch (Exception failure)
        {
            log.Write("The data file could not be opened", failure);
            ShowProblem(HostMessages.DataFileUnavailable(log.Path));
        }
    }

    /// <summary>The message is read here, on the window's thread; reading the data and answering runs on the thread pool.</summary>
    private async void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            var source = e.Source;
            var json = e.WebMessageAsJson;
            var reply = await Task.Run(() => bridge.AnswerAsync(source, json));
            if (reply is null)
            {
                return;
            }

            view?.CoreWebView2?.PostWebMessageAsJson(reply.Json);
            if (reply.DataFileFailed)
            {
                ShowProblem(HostMessages.DataFileUnavailable(log.Path));
            }
        }
        catch (Exception failure)
        {
            log.Write("Answering the page failed", failure);
        }
    }

    /// <summary>A crashed page is not reloaded: the window asks to restart the app.</summary>
    private void OnProcessFailed(object? sender, CoreWebView2ProcessFailedEventArgs e)
    {
        if (e.ProcessFailedKind is CoreWebView2ProcessFailedKind.RenderProcessExited or CoreWebView2ProcessFailedKind.BrowserProcessExited)
        {
            BeginInvoke(() => ShowProblem(HostMessages.PageCrashed));
        }
    }

    /// <summary>The first problem stays: the web view is gone, and a later one would only replace its explanation.</summary>
    private void ShowProblem(string message)
    {
        if (view is null)
        {
            return;
        }

        Controls.Remove(view);
        view.Dispose();
        view = null;
        Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Padding = new Padding(32),
            Font = new Font(Font.FontFamily, 11f),
            Text = message,
        });
    }
}
