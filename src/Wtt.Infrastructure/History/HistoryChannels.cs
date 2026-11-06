namespace Wtt.Infrastructure.History;

internal sealed record EventSelector(string Provider, IReadOnlyList<int> EventIds);

internal sealed record HistoryChannel(string Name, IReadOnlyList<EventSelector> Selectors);

/// <summary>The events the history import reads (ADR-003).</summary>
internal static class HistoryChannels
{
    public const string WinlogonOperational = "Microsoft-Windows-Winlogon/Operational";
    public const string System = "System";

    public const string Winlogon = "Microsoft-Windows-Winlogon";
    public const string KernelGeneral = "Microsoft-Windows-Kernel-General";
    public const string KernelPower = "Microsoft-Windows-Kernel-Power";
    public const string PowerTroubleshooter = "Microsoft-Windows-Power-Troubleshooter";
    public const string User32 = "User32";
    public const string EventLog = "EventLog";

    public static readonly IReadOnlyList<HistoryChannel> All =
    [
        new(WinlogonOperational, [new EventSelector(Winlogon, [811])]),
        new(System,
        [
            new EventSelector(KernelGeneral, [12, 13]),
            new EventSelector(KernelPower, [42, 107, 506, 507]),
            new EventSelector(PowerTroubleshooter, [1]),
            new EventSelector(Winlogon, [7001, 7002]),
            new EventSelector(User32, [1074]),
            new EventSelector(EventLog, [6008]),
        ]),
    ];
}
