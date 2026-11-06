namespace Wtt.Application.Recording;

/// <summary>
/// Brings in what Windows kept since the history cursor. The Recorder's host may run it apart from the live
/// recording, so a long import never holds up a heartbeat or a signal.
/// </summary>
public interface IHistoryCatchUp
{
    Task CatchUpAsync();
}
