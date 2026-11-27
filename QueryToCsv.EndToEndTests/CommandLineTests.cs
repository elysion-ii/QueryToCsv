using QueryToCsv.TestSupport;
using Xunit;

namespace QueryToCsv.EndToEndTests;

// Runs that end before a database is needed.
public sealed class CommandLineTests : IDisposable
{
    private const string HelpHint = "Try 'QueryToCsv --help' for more information.";

    private readonly AppInstallation _app = new();

    public void Dispose() => _app.Dispose();

    [Fact]
    [Trait("Requirement", "QTC-1")]
    [Trait("Requirement", "QTC-3")]
    internal async Task Version_PrintsTheVersionLineAndExitsWithoutConfiguration()
    {
        AppResult result = await _app.RunAsync(["--version", "ignored"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Matches(@"^QueryToCsv \d+\.\d+\.\d+\r?\n$", result.Output);
        Assert.Equal("", result.Error);
        Assert.False(Directory.Exists(_app.LogFolder));
    }

    [Fact]
    [Trait("Requirement", "QTC-2")]
    internal async Task Help_PrintsEverySectionToStandardOutput()
    {
        AppResult result = await _app.RunAsync(["-h"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("", result.Error);
        string[] sections = ["QueryToCsv ", "Export a SQL Server result set to CSV.", "Usage:", "Options:", "--connection", "--query",
            "--file", "--encoding", "--no-header", "--guide", "Open targets:", "Examples:", "Cancelling:", "Ctrl+C"];
        int position = 0;
        foreach (string section in sections)
        {
            int found = result.Output.IndexOf(section, position, StringComparison.Ordinal);
            Assert.True(found >= 0, $"Help is missing \"{section}\" after position {position}.");
            position = found;
        }
    }

    [Fact]
    [Trait("Requirement", "QTC-1")]
    [Trait("Requirement", "QTC-29")]
    internal async Task Guide_PrintsTheRepositoryGuideAndExitsWithoutConfiguration()
    {
        // Without front matter, with CRLF line endings: the guide as the specification
        // states it, derived here independently of the application's own rendering.
        string document = File.ReadAllText(Path.Combine(Repository.Root, "docs", "guides", "usage.md")).ReplaceLineEndings("\n");
        string[] parts = document.Split("---\n", 3);
        string expected = parts[2].TrimStart('\n').Replace("\n", "\r\n", StringComparison.Ordinal);

        AppResult result = await _app.RunAsync(["--guide", "ignored"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(expected, result.Output);
        Assert.StartsWith("# QueryToCsv Usage Guide\r\n", result.Output, StringComparison.Ordinal);
        Assert.Equal("", result.Error);
        Assert.False(Directory.Exists(_app.LogFolder));
    }

    [Fact]
    [Trait("Requirement", "QTC-1")]
    [Trait("Requirement", "QTC-5")]
    [Trait("Requirement", "QTC-7")]
    [Trait("Requirement", "QTC-12")]
    internal async Task NoArguments_WithoutConfiguration_ShowsTheBannerAndTheConnectionsError()
    {
        AppResult result = await _app.RunAsync([], standardInput: "");

        Assert.Equal(1, result.ExitCode);
        Assert.Equal("", result.Output);
        Assert.Equal(
            "=== QueryToCsv ===" + Environment.NewLine + Environment.NewLine
            + "QueryToCsv: appsettings.json not found; continuing with built-in defaults." + Environment.NewLine
            + "QueryToCsv: Connections must contain at least one entry." + Environment.NewLine,
            result.Error);
        Assert.Contains("[ERROR] Connections must contain at least one entry.", _app.LogText(), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "QTC-5")]
    internal async Task UnparsableConfiguration_IsReportedAndDefaultsApply()
    {
        File.WriteAllText(Path.Combine(_app.Folder, "appsettings.json"), "{ not json");

        AppResult result = await _app.RunAsync(["--query", "SELECT 1"]);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("QueryToCsv: failed to load appsettings.json; continuing with built-in defaults.", result.Error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Server=x;TrustServerCertificate=True;Encrypt=False;", "must not turn encryption off")]
    [InlineData("Server=x;TrustServerCertificate=True;Password=secret;", "must not contain a password")]
    [Trait("Requirement", "QTC-7")]
    internal async Task UnsafeConnectionString_EndsTheRunBeforeConnecting(string connectionString, string expected)
    {
        _app.WriteSettings(new { Connections = new[] { new { Name = "Dev", ConnectionString = connectionString } } });

        AppResult result = await _app.RunAsync(["--query", "SELECT 1"]);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains($"QueryToCsv: Connections[0].ConnectionString {expected}", result.Error, StringComparison.Ordinal);
        // A run that ends on the configuration connects nowhere, so nothing warns about it.
        Assert.DoesNotContain("insecure setting", result.Error, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", _app.LogText(), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "QTC-6")]
    [Trait("Requirement", "QTC-24")]
    internal async Task RejectedPreference_IsReportedOnStandardErrorAndInTheLog()
    {
        _app.WriteSettings(new { QueryTimeout = 0, Connections = Array.Empty<object>() });

        AppResult result = await _app.RunAsync(["--query", "SELECT 1"]);

        const string Notice = "QueryTimeout \"0\" is not a whole number from 1 to 2147483647; using 30.";
        Assert.Contains("QueryToCsv: " + Notice, result.Error, StringComparison.Ordinal);
        string log = _app.LogText();
        Assert.Contains("[WARN ] " + Notice, log, StringComparison.Ordinal);
        // Every entry starts with an ISO 8601 time carrying the UTC offset.
        Assert.All(
            log.Split('\n', StringSplitOptions.RemoveEmptyEntries),
            line => Assert.Matches(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3}[+-]\d{2}:\d{2} \[", line));
        Assert.Matches(@"Application started \(v\d+\.\d+\.\d+\)", log);
        Assert.Contains("Application finished (exit code: 1)", log, StringComparison.Ordinal);
    }

    public static TheoryData<string[]> UsageErrors() => new()
    {
        { ["--open", ""] },
        { ["--open="] },
        { ["--query"] },
        { ["--query", "SELECT 1", "--", "positional"] },
        { ["-q", "SELECT 1"] },
    };

    [Theory]
    [MemberData(nameof(UsageErrors))]
    [Trait("Requirement", "QTC-4")]
    [Trait("Requirement", "QTC-9")]
    [Trait("Requirement", "QTC-13")]
    internal async Task UsageError_ExitsTwoWithTheHelpHint(string[] args)
    {
        AppResult result = await _app.RunAsync(args);

        Assert.Equal(2, result.ExitCode);
        Assert.Equal("", result.Output);
        string[] lines = result.Error.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length);
        Assert.StartsWith("QueryToCsv: ", lines[0], StringComparison.Ordinal);
        Assert.Equal(HelpHint, lines[1]);
    }

    [Fact]
    [Trait("Requirement", "QTC-10")]
    [Trait("Requirement", "QTC-13")]
    internal async Task UnknownEncoding_IsReportedBeforeConfiguration()
    {
        AppResult result = await _app.RunAsync(["--query", "SELECT 1", "-e", "utf-32"]);

        Assert.Equal(2, result.ExitCode);
        Assert.Equal(
            "QueryToCsv: unknown encoding \"utf-32\". Use: utf-8, utf-8-bom, utf-16, shift-jis" + Environment.NewLine
            + HelpHint + Environment.NewLine,
            result.Error);
    }

    [Theory]
    [InlineData("output", "QueryToCsv: output folder does not exist yet. Run a query first to create it.")]
    [InlineData("OUTPUT", "QueryToCsv: output folder does not exist yet. Run a query first to create it.")]
    [InlineData("Config", "QueryToCsv: file not found: {app}\\appsettings.json")]
    [InlineData("missing.csv", "QueryToCsv: file not found: {app}\\missing.csv")]
    [Trait("Requirement", "QTC-4")]
    internal async Task Open_MissingTarget_ExitsOneNamingIt(string target, string expected)
    {
        AppResult result = await _app.RunAsync(["--open", target]);

        Assert.Equal(1, result.ExitCode);
        Assert.EndsWith(expected.Replace("{app}", _app.Folder, StringComparison.Ordinal) + Environment.NewLine, result.Error, StringComparison.Ordinal);
        Assert.False(Directory.Exists(_app.LogFolder));
    }

    [Fact]
    [Trait("Requirement", "QTC-9")]
    [Trait("Requirement", "QTC-13")]
    internal async Task SqlFileOutsideQueryFolder_IsAUsageError()
    {
        File.WriteAllText(Path.Combine(_app.Folder, "outside.sql"), "SELECT 1");
        _app.WriteSettings(new { Connections = new[] { new { Name = "Dev", ConnectionString = "Server=x;Integrated Security=True;" } } });

        AppResult result = await _app.RunAsync(["-f", "..\\outside.sql"]);

        Assert.Equal(2, result.ExitCode);
        Assert.StartsWith(
            "QueryToCsv: SQL file \"..\\outside.sql\" is outside QueryFolder; give an absolute path instead." + Environment.NewLine + HelpHint,
            result.Error,
            StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "QTC-14")]
    internal async Task SqlFileWithInvalidBytes_IsReportedWithItsOffset()
    {
        File.WriteAllBytes(Path.Combine(_app.QueryFolder, "bad.sql"), [0x53, 0x45, 0xFF]);
        _app.WriteSettings(new { Connections = new[] { new { Name = "Dev", ConnectionString = "Server=x;Integrated Security=True;" } } });

        AppResult result = await _app.RunAsync(["-f", "bad.sql"]);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains(
            $"QueryToCsv: SQL file \"{Path.Combine(_app.QueryFolder, "bad.sql")}\" is not valid utf-8 at byte 2.",
            result.Error,
            StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "QTC-15")]
    internal async Task NonSelectStatement_IsRejectedBeforeConnecting()
    {
        // The server named here does not exist: a statement that reached it would fail differently.
        _app.WriteSettings(new { Connections = new[] { new { Name = "Dev", ConnectionString = "Server=nowhere.invalid;Integrated Security=True;" } } });

        AppResult result = await _app.RunAsync(["--query", "SELECT 1; DELETE FROM T"]);

        Assert.Equal(1, result.ExitCode);
        Assert.Equal("QueryToCsv: only SELECT statements are allowed." + Environment.NewLine, result.Error);
    }
}
