namespace Wtt.Recorder;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args) => RecorderApp.Run(args);
}
