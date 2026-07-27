using Xunit;

namespace QueryToCsv.Tests;

public class CliRunArgsTests
{
    // args -> the exact message printed to stderr before exiting 1
    public static TheoryData<string[], string> RejectedArgs() => new()
    {
        { ["-c"], "Error: -c requires a value." },
        { ["--connection"], "Error: --connection requires a value." },
        { ["-q"], "Error: -q requires a value." },
        { ["--query"], "Error: --query requires a value." },
        { ["-f"], "Error: -f requires a value." },
        { ["--file"], "Error: --file requires a value." },
        { ["-e"], "Error: -e requires a value." },
        { ["-q", "SELECT 1", "-e"], "Error: -e requires a value." },
        { ["--no-header"], "Error: -q or -f is required when using CLI options." },
        { ["-c", "Dev Server"], "Error: -q or -f is required when using CLI options." },
        { ["-q", "SELECT 1", "-f", "report.sql"], "Error: -q and -f cannot be used together." },
        { ["--verbose"], "Error: Unknown option: --verbose" },
        { ["-q", "SELECT 1", "--csv"], "Error: Unknown option: --csv" },
        { ["--open"], "Error: Unknown option: --open" },
    };

    // args -> connection, inline query, sql file, encoding, include header
    public static TheoryData<string[], string?, string?, string?, string, bool> AcceptedArgs() => new()
    {
        { ["-q", "SELECT * FROM Users"], null, "SELECT * FROM Users", null, "utf-8", true },
        { ["-f", "sales_report.sql"], null, null, "sales_report.sql", "utf-8", true },
        { ["--query", "SELECT 1"], null, "SELECT 1", null, "utf-8", true },
        { ["--file", "a.sql"], null, null, "a.sql", "utf-8", true },
        { ["-q", "SELECT 1", "--header"], null, "SELECT 1", null, "utf-8", true },
        { ["-q", "SELECT 1", "--no-header"], null, "SELECT 1", null, "utf-8", false },
        { ["-q", "SELECT 1", "--no-header", "--header"], null, "SELECT 1", null, "utf-8", true },
        { ["-q", "SELECT 1", "--header", "--no-header"], null, "SELECT 1", null, "utf-8", false },
        { ["-c", "Dev Server", "-q", "SELECT 1"], "Dev Server", "SELECT 1", null, "utf-8", true },
        { ["--connection", "Prod", "-f", "a.sql", "--encoding", "utf-16"], "Prod", null, "a.sql", "utf-16", true },
        {
            ["-c", "Dev Server", "-q", "SELECT * FROM Users WHERE Active = 1", "--no-header", "-e", "utf-8-bom"],
            "Dev Server", "SELECT * FROM Users WHERE Active = 1", null, "utf-8-bom", false
        },
        // A value is consumed verbatim even when it looks like an option
        { ["-q", "--no-header"], null, "--no-header", null, "utf-8", true },
    };

    [Fact]
    public void Parse_NoArgs_SelectsInteractiveMode()
    {
        var (runArgs, error) = CliRunArgs.Parse([]);

        Assert.Null(runArgs);
        Assert.Null(error);
    }

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
