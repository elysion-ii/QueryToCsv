namespace QueryToCsv;

// Warnings and errors share one form on standard error: "QueryToCsv: <message>".
internal static class ConsoleMessages
{
    internal static void Write(string message)
    {
        Console.Error.WriteLine($"{ApplicationVersion.ApplicationName}: {message}");
    }

    internal static void WriteUsageError(string message)
    {
        Write(message);
        Console.Error.WriteLine(
            $"Try '{ApplicationVersion.ApplicationName} --help' for more information.");
    }
}
