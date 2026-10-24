namespace Wtt.Cli.Tests;

internal sealed class FakeClock(DateTimeOffset now) : TimeProvider
{
    private static readonly TimeZoneInfo Zone =
        TimeZoneInfo.CreateCustomTimeZone("Test+02", TimeSpan.FromHours(2), "Test+02", "Test+02");

    public override TimeZoneInfo LocalTimeZone => Zone;

    public override DateTimeOffset GetUtcNow() => now.ToUniversalTime();
}
