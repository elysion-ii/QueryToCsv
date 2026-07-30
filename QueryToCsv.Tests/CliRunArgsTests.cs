using Xunit;

namespace QueryToCsv.Tests;

public class CliRunArgsTests
{
    // args -> the exact usage error passed to the CLI error writer
    public static TheoryData<string[], string> RejectedArgs() => new()
    {
        { ["-c"], "option '-c' requires a value." },
        { ["--connection"], "option '--connection' requires a value." },
        { ["--query"], "option '--query' requires a value." },
        { ["-f"], "option '-f' requires a value." },
        { ["--file"], "option '--file' requires a value." },
        { ["-e"], "option '-e' requires a value." },
        { ["--query", "SELECT 1", "-e"], "option '-e' requires a value." },
        { ["--no-header"], "either '--query' or '--file' is required when using one-liner options." },
        { ["-c", "Dev Server"], "either '--query' or '--file' is required when using one-liner options." },
        {
            ["--query", "SELECT 1", "-f", "report.sql"],
            "options '--query' and '--file' cannot be used together."
        },
        { ["-q"], "unknown option '-q'" },
        { ["--verbose"], "unknown option '--verbose'" },
        { ["--query", "SELECT 1", "--csv"], "unknown option '--csv'" },
        { ["--header=true"], "option '--header' does not accept a value." },
        { ["--query", "SELECT 1", "--", "--no-header"], "unexpected argument '--no-header'" },
    };

    // args -> connection, inline query, sql file, encoding, include header
    public static TheoryData<string[], string?, string?, string?, string, bool> AcceptedArgs() => new()
    {
        { ["--query", "SELECT * FROM Users"], null, "SELECT * FROM Users", null, "utf-8", true },
        { ["-f", "sales_report.sql"], null, null, "sales_report.sql", "utf-8", true },
        { ["--query", "SELECT 1"], null, "SELECT 1", null, "utf-8", true },
        { ["--query=SELECT 1"], null, "SELECT 1", null, "utf-8", true },
        { ["--file", "a.sql"], null, null, "a.sql", "utf-8", true },
        { ["--file=a.sql"], null, null, "a.sql", "utf-8", true },
        { ["--query", "SELECT 1", "--header"], null, "SELECT 1", null, "utf-8", true },
        { ["--query", "SELECT 1", "--no-header"], null, "SELECT 1", null, "utf-8", false },
        { ["--query", "SELECT 1", "--no-header", "--header"], null, "SELECT 1", null, "utf-8", true },
        { ["--query", "SELECT 1", "--header", "--no-header"], null, "SELECT 1", null, "utf-8", false },
        { ["-c", "Dev Server", "--query", "SELECT 1"], "Dev Server", "SELECT 1", null, "utf-8", true },
        { ["--connection=Prod", "--file=a.sql", "--encoding=utf-16"], "Prod", null, "a.sql", "utf-16", true },
        { ["--connection", "Prod", "-f", "a.sql", "--encoding", "utf-16"], "Prod", null, "a.sql", "utf-16", true },
        {
            ["-c", "Dev Server", "--query", "SELECT * FROM Users WHERE Active = 1", "--no-header", "-e", "utf-8-bom"],
            "Dev Server", "SELECT * FROM Users WHERE Active = 1", null, "utf-8-bom", false
        },
        // A value is consumed verbatim even when it looks like an option
        { ["--query", "--no-header"], null, "--no-header", null, "utf-8", true },
    };

    [Theory]
    [MemberData(nameof(RejectedArgs))]
    public void Parse_InvalidArgs_ReturnsExpectedError(string[] args, string expectedError)
    {
        var (runArgs, error) = CliRunArgs.Parse(args);

        Assert.Null(runArgs);
        Assert.Equal(expectedError, error);
    }

    [Theory]
    [MemberData(nameof(AcceptedArgs))]
    public void Parse_ValidArgs_ReturnsExpectedRunArgs(
        string[] args,
        string? expectedConnection,
        string? expectedInlineQuery,
        string? expectedSqlFile,
        string expectedEncoding,
        bool expectedIncludeHeader)
    {
        var (runArgs, error) = CliRunArgs.Parse(args);

        Assert.Null(error);
        Assert.NotNull(runArgs);
        Assert.Equal(expectedConnection, runArgs.ConnectionName);
        Assert.Equal(expectedInlineQuery, runArgs.InlineQuery);
        Assert.Equal(expectedSqlFile, runArgs.SqlFile);
        Assert.Equal(expectedEncoding, runArgs.EncodingName);
        Assert.Equal(expectedIncludeHeader, runArgs.IncludeHeader);
    }
}
