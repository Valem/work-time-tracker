namespace Wtt.Domain.Facts;

/// <summary>
/// Names of the reasons a laptop leaves standby. Only input, the power button and the lid point at
/// a person — and even those need the 2-minute test, because Windows reports night wakes the same way.
/// </summary>
public static class WakeReason
{
    public const string PowerButton = "PowerButton";
    public const string Lid = "Lid";
    public const string UserInput = "UserInput";
    public const string InputKeyboard = "InputKeyboard";
    public const string InputMouse = "InputMouse";
    public const string InputTouchpad = "InputTouchpad";
    public const string InputPen = "InputPen";
    public const string InputHid = "InputHid";
    public const string InputTouch = "InputTouch";

    /// <summary>Prefix of a reason Windows reported that has no name here; it never counts as a person.</summary>
    public const string UnmappedPrefix = "Unmapped:";

    private static readonly HashSet<string> PersonReasons =
    [
        PowerButton, Lid, UserInput, InputKeyboard, InputMouse, InputTouchpad, InputPen, InputHid, InputTouch,
    ];

    public static bool IsPerson(string? reason) => reason is not null && PersonReasons.Contains(reason);

    public static string Unmapped(string raw) => UnmappedPrefix + raw;

    public static bool IsUnmapped(string? reason) =>
        reason is not null && reason.StartsWith(UnmappedPrefix, StringComparison.Ordinal);
}
