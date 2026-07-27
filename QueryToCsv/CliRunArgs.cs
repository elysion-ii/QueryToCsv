namespace QueryToCsv;

// One-liner mode arguments, parsed from the command line.
internal sealed record CliRunArgs(
    string? ConnectionName,
    string? InlineQuery,
    string? SqlFile,
    string EncodingName,
    bool IncludeHeader)
{
    internal const string DefaultEncodingName = "utf-8";

    // Three outcomes, distinguished by the returned pair:
    //   (null, null)  no arguments — run interactively
    //   (args, null)  one-liner mode
    //   (null, error) unusable arguments; the caller prints the message and exits 1
    internal static (CliRunArgs? Args, string? Error) Parse(string[] args)
    {
        if (args.Length == 0)
            return (null, null);

        string? connectionName = null;
        string? inlineQuery = null;
        string? sqlFile = null;
        string encodingName = DefaultEncodingName;
        var includeHeader = true;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-c" or "--connection":
                    if (i + 1 >= args.Length)
                        return (null, $"Error: {args[i]} requires a value.");
                    connectionName = args[++i];
                    break;
                case "-q" or "--query":
                    if (i + 1 >= args.Length)
                        return (null, $"Error: {args[i]} requires a value.");
                    inlineQuery = args[++i];
                    break;
                case "-f" or "--file":
                    if (i + 1 >= args.Length)
                        return (null, $"Error: {args[i]} requires a value.");
                    sqlFile = args[++i];
                    break;
                case "-e" or "--encoding":
                    if (i + 1 >= args.Length)
                        return (null, $"Error: {args[i]} requires a value.");
                    encodingName = args[++i];
                    break;
                case "--header":
                    includeHeader = true;
                    break;
                case "--no-header":
                    includeHeader = false;
                    break;
                default:
                    return (null, $"Error: Unknown option: {args[i]}");
            }
        }

        if (inlineQuery is not null && sqlFile is not null)
            return (null, "Error: -q and -f cannot be used together.");

        if (inlineQuery is null && sqlFile is null)
            return (null, "Error: -q or -f is required when using CLI options.");

        return (new CliRunArgs(connectionName, inlineQuery, sqlFile, encodingName, includeHeader), null);
    }
}
