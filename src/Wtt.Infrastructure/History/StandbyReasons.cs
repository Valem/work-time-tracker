using System.Globalization;
using System.Text.RegularExpressions;
using Wtt.Domain.Facts;

namespace Wtt.Infrastructure.History;

/// <summary>
/// Names for the reasons Windows reports for standby and wakes. The Kernel-Power 506/507 codes are
/// undocumented; the table holds what Windows renders for them. High bits carry flags
/// (16777220 = 0x01000004 is input, 4), so only the low 16 bits name the reason.
/// </summary>
internal static partial class StandbyReasons
{
    private const int ReasonMask = 0xFFFF;
    private const char WakeSourceSeparator = '|';

    private static readonly Dictionary<int, string> KernelPowerNames = new()
    {
        [1] = WakeReason.PowerButton,
        [3] = "MonitorPowerOff",
        [4] = WakeReason.UserInput,
        [5] = "DisplayBurst",
        [12] = "IdleTimeout",
        [15] = WakeReason.Lid,
        [20] = "SleepHibernateOrShutdown",
        [25] = "ResumeFromHibernate",
        [28] = "DisplayBurstSuppressed",
        [31] = WakeReason.InputKeyboard,
        [32] = WakeReason.InputMouse,
        [33] = WakeReason.InputTouchpad,
        [34] = WakeReason.InputPen,
        [36] = WakeReason.InputHid,
        [44] = WakeReason.Fingerprint,
        [54] = WakeReason.InputTouch,
        [55] = "BatteryDrainBudgetExceeded",
    };

    private static readonly (Regex Pattern, string Name)[] WakeSourceNames =
    [
        (PowerButtonText(), WakeReason.PowerButton),
        (LidText(), WakeReason.Lid),
        (KeyboardText(), WakeReason.InputKeyboard),
        (MouseText(), WakeReason.InputMouse),
        (TouchpadText(), WakeReason.InputTouchpad),
        (PenText(), WakeReason.InputPen),
        (TouchText(), WakeReason.InputTouch),
        (HidText(), WakeReason.InputHid),
    ];

    /// <summary>A Kernel-Power 506/507 reason code.</summary>
    public static string KernelPower(string? code) =>
        int.TryParse(code, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)
        && KernelPowerNames.TryGetValue(number & ReasonMask, out var name)
            ? name
            : WakeReason.Unmapped($"KernelPower {code}");

    /// <summary>A Power-Troubleshooter 1 wake source, as the S3/hibernate resume reports it.</summary>
    public static string WakeSource(string? type, string? text) =>
        WakeSourceNames.FirstOrDefault(entry => text is not null && entry.Pattern.IsMatch(text)).Name
        ?? WakeReason.Unmapped($"WakeSource {type} {text}".TrimEnd());

    /// <summary>How a wake source is kept as a raw reason: its type and text. A Kernel-Power reason is kept as its code.</summary>
    public static string WakeSourceRaw(string? type, string? text) => $"{type}{WakeSourceSeparator}{text}";

    /// <summary>
    /// A System-log fact's reason, named from the raw reason Windows gave with this version's tables, so
    /// a reason named after the fact was stored reaches it; the stored name is ignored.
    /// </summary>
    public static FactDetail NamedOnRead(FactSource source, FactDetail detail) =>
        source == FactSource.SystemHistory && detail.RawReason is { } raw
            ? detail with { Reason = FromRaw(raw) }
            : detail;

    private static string FromRaw(string raw) =>
        raw.Split(WakeSourceSeparator, 2) is [var type, var text]
            ? WakeSource(type, text)
            : KernelPower(raw);

    [GeneratedRegex(@"\bpower\s*button\b", RegexOptions.IgnoreCase)]
    private static partial Regex PowerButtonText();

    [GeneratedRegex(@"\blid\b", RegexOptions.IgnoreCase)]
    private static partial Regex LidText();

    [GeneratedRegex(@"\bkeyboard\b", RegexOptions.IgnoreCase)]
    private static partial Regex KeyboardText();

    [GeneratedRegex(@"\bmouse\b", RegexOptions.IgnoreCase)]
    private static partial Regex MouseText();

    [GeneratedRegex(@"\btouch\s*pad\b", RegexOptions.IgnoreCase)]
    private static partial Regex TouchpadText();

    [GeneratedRegex(@"\bpen\b", RegexOptions.IgnoreCase)]
    private static partial Regex PenText();

    [GeneratedRegex(@"\btouch\s*screen\b|\btouch\b", RegexOptions.IgnoreCase)]
    private static partial Regex TouchText();

    [GeneratedRegex(@"\bhid\b", RegexOptions.IgnoreCase)]
    private static partial Regex HidText();
}
