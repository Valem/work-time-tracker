using Wtt.Domain.Coverage;
using Wtt.Domain.Time;
using Wtt.Infrastructure.Storage;

namespace Wtt.Infrastructure.Tests;

public sealed class CoverageLogTests : IDisposable
{
    private static readonly TimeSpan Local = TimeSpan.FromHours(2);
    private readonly TempDatabase file = new();
    private readonly FakeClock clock = new(At(8, 10, 40));

    public void Dispose() => file.Dispose();

    [Fact]
    public async Task TD_002_the_first_start_in_a_session_covers_it_from_its_sign_in()
    {
        var coverage = Open();

        await coverage.BeginAsync(At(8, 10));

        Assert.Equal([new CoverageSpan(At(8, 10), At(8, 10, 40))], await Spans(coverage));
    }

    [Fact]
    public async Task A_start_soon_after_the_last_heartbeat_of_the_same_session_continues_its_span()
    {
        var coverage = Open();
        await coverage.BeginAsync(At(8, 10));
        await BeatUntilAsync(coverage, At(9, 0));

        clock.Now = At(9, 1, 30);
        await coverage.BeginAsync(At(8, 10));

        Assert.Equal([new CoverageSpan(At(8, 10), At(9, 1, 30))], await Spans(coverage));
    }

    [Fact]
    public async Task A_start_after_a_gap_in_a_session_already_covered_leaves_the_gap_uncovered()
    {
        var coverage = Open();
        await coverage.BeginAsync(At(8, 10));
        await BeatUntilAsync(coverage, At(10, 0));

        clock.Now = At(10, 30);
        await coverage.BeginAsync(At(8, 10));

        Assert.Equal(
            [new CoverageSpan(At(8, 10), At(10, 0)), new CoverageSpan(At(10, 30), At(10, 30))],
            await Spans(coverage));
    }

    [Fact]
    public async Task A_new_sign_in_after_the_last_span_gets_a_span_from_that_sign_in()
    {
        var coverage = Open();
        await coverage.BeginAsync(At(8, 10));
        await BeatUntilAsync(coverage, At(17, 0));

        clock.Now = At(20, 0, 30);
        await coverage.BeginAsync(At(20, 0));

        Assert.Equal(
            [new CoverageSpan(At(8, 10), At(17, 0)), new CoverageSpan(At(20, 0), At(20, 0, 30))],
            await Spans(coverage));
    }

    [Fact]
    public async Task TD_002_a_standby_the_Recorder_saw_begin_and_end_is_bridged()
    {
        var coverage = Open();
        await coverage.BeginAsync(At(8, 10));
        await BeatUntilAsync(coverage, At(12, 0));

        await coverage.ResumeAsync(At(12, 0), At(12, 45));
        await coverage.ExtendAsync(At(12, 46));

        Assert.Equal([new CoverageSpan(At(8, 10), At(12, 46))], await Spans(coverage));
    }

    [Fact]
    public async Task A_resume_whose_suspend_coverage_never_reached_starts_a_new_span()
    {
        var coverage = Open();
        await coverage.BeginAsync(At(8, 10));
        await BeatUntilAsync(coverage, At(11, 0));

        await coverage.ResumeAsync(At(12, 0), At(12, 45));

        Assert.Equal(
            [new CoverageSpan(At(8, 10), At(11, 0)), new CoverageSpan(At(12, 45), At(12, 45))],
            await Spans(coverage));
    }

    private SqliteCoverageLog Open() => new(file.Open(clock), clock);

    private async Task BeatUntilAsync(SqliteCoverageLog coverage, DateTimeOffset until)
    {
        for (var beat = At(8, 11); beat <= until; beat += TimeSpan.FromMinutes(1))
        {
            clock.Now = beat;
            await coverage.ExtendAsync(beat);
        }
    }

    private static async Task<IReadOnlyList<CoverageSpan>> Spans(SqliteCoverageLog coverage) => await coverage.ReadAsync(Interval.All);

    private static DateTimeOffset At(int hour, int minute, int second = 0) => new(2026, 10, 20, hour, minute, second, Local);
}
