namespace Wtt.App.Recording;

internal static class NamedMutex
{
    /// <summary>True when a process holds a mutex of this name; one this user may not open still exists.</summary>
    public static bool Exists(string name)
    {
        try
        {
            if (!Mutex.TryOpenExisting(name, out var mutex))
            {
                return false;
            }

            mutex.Dispose();
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return true;
        }
    }
}
