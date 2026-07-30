namespace QueryToCsv;

internal sealed record CliRunArgs(
    string? ConnectionName,
    string? InlineQuery,
    string? SqlFile,
    string EncodingName,
    bool IncludeHeader)
{
    internal const string DefaultEncodingName = "utf-8";

    internal static (CliRunArgs? Args, string? Error) Parse(string[] args)
    {
        string? connectionName = null;
        string? inlineQuery = null;
        string? sqlFile = null;
        string encodingName = DefaultEncodingName;
        var includeHeader = true;

        for (var i = 0; i < args.Length; i++)
        {
            var argument = args[i];
            if (argument == "--")
            {
                if (i + 1 < args.Length)
                    return (null, $"unexpected argument '{args[i + 1]}'");

                break;
            }

            var equalsIndex = argument.StartsWith("--", StringComparison.Ordinal)
                ? argument.IndexOf('=')
                : -1;
            var option = equalsIndex >= 0 ? argument[..equalsIndex] : argument;
            var attachedValue = equalsIndex >= 0 ? argument[(equalsIndex + 1)..] : null;

            switch (option)
            {
                case "-c" or "--connection":
                    if (!TryReadOptionValue(
                            args,
                            ref i,
                            option,
                            attachedValue,
                            out connectionName,
                            out var connectionError))
                        return (null, connectionError);
                    break;
                case "--query":
                    if (!TryReadOptionValue(
                            args,
                            ref i,
                            option,
                            attachedValue,
                            out inlineQuery,
                            out var queryError))
                        return (null, queryError);
                    break;
                case "-f" or "--file":
                    if (!TryReadOptionValue(
                            args,
                            ref i,
                            option,
                            attachedValue,
                            out sqlFile,
                            out var fileError))
                        return (null, fileError);
                    break;
                case "-e" or "--encoding":
                    if (!TryReadOptionValue(
                            args,
                            ref i,
                            option,
                            attachedValue,
                            out encodingName,
                            out var encodingError))
                        return (null, encodingError);
                    break;
                case "--header":
                    if (attachedValue is not null)
                        return (null, "option '--header' does not accept a value.");
                    includeHeader = true;
                    break;
                case "--no-header":
                    if (attachedValue is not null)
                        return (null, "option '--no-header' does not accept a value.");
                    includeHeader = false;
                    break;
                default:
                    return (null, $"unknown option '{argument}'");
            }
        }

        if (inlineQuery is not null && sqlFile is not null)
            return (null, "options '--query' and '--file' cannot be used together.");

        if (inlineQuery is null && sqlFile is null)
            return (null, "either '--query' or '--file' is required when using one-liner options.");

        return (new CliRunArgs(connectionName, inlineQuery, sqlFile, encodingName, includeHeader), null);
    }

    private static bool TryReadOptionValue(
        string[] args,
        ref int index,
        string option,
        string? attachedValue,
        out string value,
        out string? error)
    {
        if (attachedValue is not null)
        {
            value = attachedValue;
            error = null;
            return true;
        }

        if (index + 1 >= args.Length)
        {
            value = "";
            error = $"option '{option}' requires a value.";
            return false;
        }

        value = args[++index];
        error = null;
        return true;
    }
}
