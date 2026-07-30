namespace QueryToCsv;

internal enum CliMode
{
    Interactive,
    Help,
    Version,
    Open,
    OneLiner,
}

internal sealed record CliInvocation(
    CliMode Mode,
    string? OpenTarget,
    CliRunArgs? RunArgs)
{
    internal static (CliInvocation? Invocation, string? Error) Parse(string[] args)
    {
        if (args.Length == 0)
            return (new CliInvocation(CliMode.Interactive, null, null), null);

        if (args[0] is "-h" or "--help")
            return (new CliInvocation(CliMode.Help, null, null), null);

        if (args[0] is "-V" or "--version")
            return (new CliInvocation(CliMode.Version, null, null), null);

        if (args[0] == "--open")
        {
            if (args.Length == 1)
                return (null, "option '--open' requires a value.");

            if (args.Length > 2)
                return (null, $"unexpected argument '{args[2]}'");

            return (new CliInvocation(CliMode.Open, args[1], null), null);
        }

        const string openPrefix = "--open=";
        if (args[0].StartsWith(openPrefix, StringComparison.Ordinal))
        {
            var target = args[0][openPrefix.Length..];
            if (target.Length == 0)
                return (null, "option '--open' requires a value.");

            if (args.Length > 1)
                return (null, $"unexpected argument '{args[1]}'");

            return (new CliInvocation(CliMode.Open, target, null), null);
        }

        var (runArgs, error) = CliRunArgs.Parse(args);
        return error is null
            ? (new CliInvocation(CliMode.OneLiner, null, runArgs), null)
            : (null, error);
    }
}
