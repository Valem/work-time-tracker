using System.Threading.Channels;
using Wtt.Application.Ports;

namespace Wtt.Recorder;

/// <summary>
/// Runs the Recorder's work one item at a time, in the order it was posted, off the window's thread. A failing
/// item is written to the error log and the next one runs: one bad moment never stops the recording.
/// </summary>
internal sealed class RecorderWorker
{
    private readonly Channel<WorkItem> queue = Channel.CreateUnbounded<WorkItem>(new UnboundedChannelOptions { SingleReader = true });
    private readonly IErrorLog errors;
    private readonly Task loop;

    public RecorderWorker(IErrorLog errors)
    {
        this.errors = errors;
        loop = Task.Run(RunAsync);
    }

    public void Post(string context, Func<Task> work) => queue.Writer.TryWrite(new WorkItem(context, work, Done: null));

    /// <summary>Posts the work and waits for it and everything before it, at most <paramref name="timeout"/>.</summary>
    /// <returns>Whether the work finished in time.</returns>
    public bool PostAndWait(string context, Func<Task> work, TimeSpan timeout)
    {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        return queue.Writer.TryWrite(new WorkItem(context, work, done)) && done.Task.Wait(timeout);
    }

    /// <summary>Lets the posted work finish, at most <paramref name="timeout"/>, and takes no more.</summary>
    public bool Stop(TimeSpan timeout)
    {
        queue.Writer.TryComplete();
        return loop.Wait(timeout);
    }

    private async Task RunAsync()
    {
        await foreach (var item in queue.Reader.ReadAllAsync())
        {
            try
            {
                await item.Work();
            }
            catch (Exception failure)
            {
                errors.Write(item.Context, failure);
            }
            finally
            {
                item.Done?.TrySetResult();
            }
        }
    }

    private sealed record WorkItem(string Context, Func<Task> Work, TaskCompletionSource? Done);
}
