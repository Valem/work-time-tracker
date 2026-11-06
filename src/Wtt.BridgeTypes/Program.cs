using System.Text;
using Wtt.App.Bridge;

namespace Wtt.BridgeTypes;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length != 1)
        {
            Console.Error.WriteLine("Usage: Wtt.BridgeTypes <declarations file>");
            return 2;
        }

        var text = Declarations.Write(BridgeContract.Json, BridgeContract.Messages, BridgeContract.Envelopes, typeof(RefusalCode));
        var path = Path.GetFullPath(args[0]);
        if (File.Exists(path) && File.ReadAllText(path) == text)
        {
            return 0;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        Console.WriteLine($"Bridge declarations written: {path}");
        return 0;
    }
}
