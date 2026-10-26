using System.ComponentModel;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Power;
using Windows.Win32.UI.WindowsAndMessaging;
using Wtt.Application.Recording;
using static Wtt.Infrastructure.Live.WindowsSignals;

namespace Wtt.Infrastructure.Live;

/// <summary>
/// A window that is never shown, receiving session and power notifications for the Recorder. It is a
/// top-level window, not a message-only one, because Windows tells only top-level windows that the session
/// ends (sign-out, shutdown, an installer closing the program). It asks Windows for notifications only —
/// it never holds a power request, so it never keeps the laptop awake.
/// </summary>
public sealed class SignalWindow(Action<LiveSignal> onSignal, Action onCloseRequested, Action<Exception> onError, TimeProvider clock)
{
    private const string ClassName = "WorkTimeTracker.Recorder.Signals";
    private const uint WM_SETTINGCHANGE = 0x001A;
    private const uint NOTIFY_FOR_THIS_SESSION = 0;

    private WNDPROC? windowProc;
    private nint window;
    private DateTimeOffset? endSessionAskedAt;

    /// <summary>Creates the window on the calling thread and pumps its messages until <see cref="Close"/>.</summary>
    public unsafe void Run()
    {
        windowProc = WindowProc;
        var instance = PInvoke.GetModuleHandle((string?)null);
        fixed (char* className = ClassName)
        {
            var windowClass = new WNDCLASSEXW
            {
                cbSize = (uint)Marshal.SizeOf<WNDCLASSEXW>(),
                lpfnWndProc = windowProc,
                hInstance = (HINSTANCE)instance.DangerousGetHandle(),
                lpszClassName = className,
            };
            if (PInvoke.RegisterClassEx(windowClass) == 0)
            {
                throw new Win32Exception();
            }
        }

        var hwnd = PInvoke.CreateWindowEx(
            WINDOW_EX_STYLE.WS_EX_TOOLWINDOW, ClassName, "Work Time Tracker Recorder", 0, 0, 0, 0, 0, HWND.Null, null, instance, null);
        if (hwnd.IsNull)
        {
            throw new Win32Exception();
        }

        Interlocked.Exchange(ref window, (nint)hwnd.Value);
        var registrations = Register(hwnd);
        try
        {
            while (PInvoke.GetMessage(out var message, HWND.Null, 0, 0) > 0)
            {
                PInvoke.TranslateMessage(message);
                PInvoke.DispatchMessage(message);
            }
        }
        finally
        {
            Unregister(hwnd, registrations);
            PInvoke.UnregisterClass(ClassName, instance);
            GC.KeepAlive(windowProc);
        }
    }

    /// <summary>Ends <see cref="Run"/>; safe from any thread.</summary>
    public unsafe void Close()
    {
        var hwnd = Interlocked.CompareExchange(ref window, 0, 0);
        if (hwnd != 0)
        {
            PInvoke.PostMessage(new HWND((void*)hwnd), WM_CLOSE, default, default);
        }
    }

    private static unsafe (HPOWERNOTIFY Lid, HPOWERNOTIFY Suspend) Register(HWND hwnd)
    {
        if (!PInvoke.WTSRegisterSessionNotification(hwnd, NOTIFY_FOR_THIS_SESSION))
        {
            throw new Win32Exception();
        }

        var handle = new HANDLE(hwnd.Value);
        var lidSetting = LidSwitchStateChange;
        var lid = PInvoke.RegisterPowerSettingNotification(handle, &lidSetting, REGISTER_NOTIFICATION_FLAGS.DEVICE_NOTIFY_WINDOW_HANDLE);
        var suspend = OperatingSystem.IsWindowsVersionAtLeast(8)
            ? PInvoke.RegisterSuspendResumeNotification(handle, REGISTER_NOTIFICATION_FLAGS.DEVICE_NOTIFY_WINDOW_HANDLE)
            : default;
        return (lid, suspend);
    }

    private static void Unregister(HWND hwnd, (HPOWERNOTIFY Lid, HPOWERNOTIFY Suspend) registrations)
    {
        PInvoke.WTSUnRegisterSessionNotification(hwnd);
        if (!registrations.Lid.IsNull)
        {
            PInvoke.UnregisterPowerSettingNotification(registrations.Lid);
        }

        if (!registrations.Suspend.IsNull && OperatingSystem.IsWindowsVersionAtLeast(8))
        {
            PInvoke.UnregisterSuspendResumeNotification(registrations.Suspend);
        }
    }

    private LRESULT WindowProc(HWND hwnd, uint message, WPARAM wParam, LPARAM lParam)
    {
        try
        {
            switch (message)
            {
                case WM_WTSSESSION_CHANGE:
                    Raise(FromSessionChange(wParam.Value));
                    break;
                case WM_POWERBROADCAST:
                    Raise(wParam.Value == PBT_POWERSETTINGCHANGE ? PowerSetting(lParam) : FromPowerBroadcast(wParam.Value));
                    return new LRESULT(1);
                case WM_QUERYENDSESSION:
                    endSessionAskedAt ??= clock.GetUtcNow();
                    return new LRESULT(1);
                case WM_ENDSESSION:
                    OnEndSession(FromEndSession(wParam.Value != 0, lParam.Value));
                    return new LRESULT(0);
                case WM_TIMECHANGE:
                case WM_SETTINGCHANGE:
                    TimeZoneInfo.ClearCachedData();
                    break;
                case WM_CLOSE:
                    PInvoke.DestroyWindow(hwnd);
                    return new LRESULT(0);
                case WM_DESTROY:
                    PInvoke.PostQuitMessage(0);
                    return new LRESULT(0);
            }
        }
        catch (Exception failure)
        {
            onError(failure);
        }

        return PInvoke.DefWindowProc(hwnd, message, wParam, lParam);
    }

    /// <summary>Sign-out and shutdown are dated when Windows first asked, which is when the person chose them.</summary>
    private void OnEndSession(SessionEnding ending)
    {
        var at = endSessionAskedAt ?? clock.GetUtcNow();
        endSessionAskedAt = null;
        switch (ending)
        {
            case SessionEnding.SigningOut:
                onSignal(new LiveSignal(LiveSignalKind.SigningOut, at));
                break;
            case SessionEnding.ShuttingDown:
                onSignal(new LiveSignal(LiveSignalKind.ShuttingDown, at));
                break;
            case SessionEnding.CloseRequested:
                onCloseRequested();
                break;
        }
    }

    private void Raise(LiveSignalKind? kind)
    {
        if (kind is { } signal)
        {
            onSignal(new LiveSignal(signal, clock.GetUtcNow()));
        }
    }

    /// <summary>POWERBROADCAST_SETTING: the setting's GUID, the data length, then the data.</summary>
    private static unsafe LiveSignalKind? PowerSetting(LPARAM lParam)
    {
        var setting = (byte*)lParam.Value;
        if (setting is null)
        {
            return null;
        }

        var guid = *(Guid*)setting;
        var length = *(uint*)(setting + sizeof(Guid));
        return length >= sizeof(uint) ? FromPowerSetting(guid, *(uint*)(setting + sizeof(Guid) + sizeof(uint))) : null;
    }
}
