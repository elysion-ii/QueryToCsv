namespace QueryToCsv;

internal static class ConsoleMessages
{
    internal static void WriteError(string message)
    {
        Console.Error.WriteLine($"{ApplicationVersion.ApplicationName}: {message}");
    }

    internal static void WriteUsageError(string message)
    {
        WriteError(message);
        Console.Error.WriteLine(
            $"Try '{ApplicationVersion.ApplicationName} --help' for more information.");
    }
}
