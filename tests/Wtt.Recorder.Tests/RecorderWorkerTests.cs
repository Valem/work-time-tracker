using Wtt.Application.Ports;

namespace Wtt.Recorder.Tests;

public sealed class RecorderWorkerTests
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(10);

    [Fact]
    public void Work_runs_one_item_at_a_time_in_the_order_it_was_posted()
    {
        var errors = new ListErrorLog();
        var worker = new RecorderWorker(errors);
        var order = new List<int>();
        var running = 0;
        var overlapped = false;

        for (var i = 0; i < 20; i++)
        {
            var item = i;
            worker.Post($"item {item}", async () =>
            {
                overlapped |= Interlocked.Increment(ref running) > 1;
                await Task.Delay(1);
                order.Add(item);
                Interlocked.Decrement(ref running);
            });
        }

        Assert.True(worker.Stop(Wait));
        Assert.Equal(Enumerable.Range(0, 20), order);
        Assert.False(overlapped);
        Assert.Empty(errors.Entries);
    }

    [Fact]
    public void A_failing_item_is_logged_with_its_context_and_the_next_one_still_runs()
    {
        var errors = new ListErrorLog();
        var worker = new RecorderWorker(errors);
        var ranAfter = false;

        worker.Post("Heartbeat", () => throw new InvalidOperationException("data file locked"));
        Assert.True(worker.PostAndWait("next", () =>
        {
            ranAfter = true;
            return Task.CompletedTask;
        }, Wait));

        Assert.True(ranAfter);
        var (context, error) = Assert.Single(errors.Entries);
        Assert.Equal("Heartbeat", context);
        Assert.Equal("data file locked", error.Message);
        Assert.True(worker.Stop(Wait));
    }

    [Fact]
    public void Waiting_for_posted_work_gives_up_after_the_timeout()
    {
        var worker = new RecorderWorker(new ListErrorLog());
        var release = new TaskCompletionSource();

        var finished = worker.PostAndWait("slow", () => release.Task, TimeSpan.FromMilliseconds(50));

        Assert.False(finished);
        release.SetResult();
        Assert.True(worker.Stop(Wait));
    }

    [Fact]
    public void After_stopping_nothing_more_runs()
    {
        var worker = new RecorderWorker(new ListErrorLog());
        Assert.True(worker.Stop(Wait));

        var ran = false;
        worker.Post("late", () =>
        {
            ran = true;
            return Task.CompletedTask;
        });

        Assert.False(worker.PostAndWait("late too", () => Task.CompletedTask, TimeSpan.FromMilliseconds(50)));
        Assert.False(ran);
    }

    private sealed class ListErrorLog : IErrorLog
    {
        public List<(string Context, Exception Error)> Entries { get; } = [];

        public void Write(string context, Exception error) => Entries.Add((context, error));
    }
}
