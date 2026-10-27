using Wtt.Application.Ports;
using Wtt.Application.Recording;

namespace Wtt.Application.History;

/// <summary>Imports this user's history since the cursor; a failed import is logged and tried again at the next catch-up.</summary>
public sealed class HistoryCatchUp(HistoryImport history, UserSid user, IErrorLog errors) : IHistoryCatchUp
{
    public async Task CatchUpAsync()
    {
        try
        {
            await history.ImportAsync(user);
        }
        catch (Exception failure)
        {
            errors.Write("Importing Windows history", failure);
        }
    }
}
