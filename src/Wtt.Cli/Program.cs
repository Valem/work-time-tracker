using System.Text;
using Wtt.Cli;

try
{
    Console.OutputEncoding = Encoding.UTF8;
}
catch (IOException)
{
    // No console to set it on: the output goes where it was sent, in its own encoding.
}

try
{
    return await CliApp.RunAsync(args, Console.Out, CliEnvironment.ThisWindowsUser());
}
catch (Exception failure)
{
    Console.Error.WriteLine($"wtt-cli failed: {failure.Message}");
    return 1;
}
