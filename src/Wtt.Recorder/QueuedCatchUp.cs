using Wtt.Application.Recording;

namespace Wtt.Recorder;

/// <summary>
/// Runs history imports on a worker of their own, so the first import after install — weeks of event logs — never
/// delays the heartbeat that tells the App the Recorder is recording. A request while one import still waits to
/// start adds nothing: that import reads everything up to its own start anyway.
/// </summary>
internal sealed class QueuedCatchUp(IHistoryCatchUp import, RecorderWorker worker) : IHistoryCatchUp
{
    private int waiting;

    public Task CatchUpAsync()
    {
        if (Interlocked.Exchange(ref waiting, 1) == 0)
        {
            worker.Post("Importing Windows history", () =>
            {
                Volatile.Write(ref waiting, 0);
                return import.CatchUpAsync();
            });
        }

        return Task.CompletedTask;
    }
}
