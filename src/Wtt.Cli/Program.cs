using Wtt.Cli;

try
{
    return await CliApp.RunAsync(args, Console.Out, CliEnvironment.ThisWindowsUser());
}
catch (Exception failure)
{
    Console.Error.WriteLine($"wtt-cli failed: {failure.Message}");
    return 1;
}
