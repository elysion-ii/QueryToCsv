using System.ComponentModel;
using System.Text;
using Microsoft.Data.SqlClient;
using NLog;
using NLog.Config;
using NLog.Targets;
using QueryToCsv.TestSupport;
using Xunit;

namespace QueryToCsv.Tests;

// Each failure that ends a run is reported on standard error and recorded in the log
// (docs/specs/QueryToCsv.md, QTC-13 and QTC-24). None of these runs reaches a database.
[Collection(ConsoleStreamsDefinition.Name)]
public sealed class QueryRunTests : IDisposable
{
    private readonly TextReader _originalIn = Console.In;
    private readonly TextWriter _originalError = Console.Error;
    private readonly StringWriter _error = new();
    private readonly MemoryTarget _log = new("memory") { Layout = "${level:uppercase=true}|${message}" };
    private readonly LogFactory _logFactory = new();
    private readonly Logger _logger;
    private readonly TempDirectory _dir = new();

    public QueryRunTests()
    {
        LoggingConfiguration config = new(_logFactory);
        config.AddRuleForAllLevels(_log);
        _logFactory.Configuration = config;
        _logger = _logFactory.GetLogger(nameof(QueryRunTests));
        Console.SetError(_error);
    }

    public void Dispose()
    {
        Console.SetIn(_originalIn);
        Console.SetError(_originalError);
        _logFactory.Dispose();
        _error.Dispose();
        _dir.Dispose();
    }

    [Fact]
    [Trait("Requirement", "QTC-11")]
    internal async Task RunOneLinerAsync_UnconfiguredConnectionName_ReportsAndLogsAUsageError()
    {
        CliRunArgs runArgs = new("Prod", "SELECT 1", null, "utf-8", true);

        int exitCode = await QueryRun.RunOneLinerAsync(Settings("Dev"), runArgs, Encoding.UTF8, _logger);

        Assert.Equal(2, exitCode);
        Assert.Contains("QueryToCsv: connection \"Prod\" not found.", _error.ToString(), StringComparison.Ordinal);
        Assert.Contains("Try 'QueryToCsv --help' for more information.", _error.ToString(), StringComparison.Ordinal);
        Assert.Contains("ERROR|connection \"Prod\" not found.", _log.Logs);
    }

    [Fact]
    [Trait("Requirement", "QTC-11")]
    internal async Task RunOneLinerAsync_SeveralConnectionsWithoutName_ReportsAndLogsAUsageError()
    {
        CliRunArgs runArgs = new(null, "SELECT 1", null, "utf-8", true);

        int exitCode = await QueryRun.RunOneLinerAsync(Settings("Dev", "Prod"), runArgs, Encoding.UTF8, _logger);

        Assert.Equal(2, exitCode);
        Assert.Contains(
            "ERROR|option '--connection' is required when multiple connections are configured.",
            _log.Logs);
    }

    [Fact]
    [Trait("Requirement", "QTC-11")]
    internal async Task RunOneLinerAsync_ConnectionNameInOtherCase_SelectsThatEntry()
    {
        CliRunArgs runArgs = new("PROD", "DELETE FROM T", null, "utf-8", true);

        int exitCode = await QueryRun.RunOneLinerAsync(Settings("Dev", "Prod"), runArgs, Encoding.UTF8, _logger);

        // The statement is rejected after the connection was chosen, before anything is sent.
        Assert.Equal(1, exitCode);
        Assert.Contains("INFO|Connection selected: Prod", _log.Logs);
        Assert.Contains("INFO|Query selected: [Inline]", _log.Logs);
        Assert.Contains("ERROR|Rejected: not a single SELECT statement in [Inline]", _log.Logs);
    }

    [Fact]
    [Trait("Requirement", "QTC-9")]
    internal async Task RunOneLinerAsync_SqlFileMissing_ReportsAndLogsARuntimeError()
    {
        CliRunArgs runArgs = new(null, null, "missing.sql", "utf-8", true);
        string expectedPath = Path.Combine(_dir.Path, "missing.sql");

        int exitCode = await QueryRun.RunOneLinerAsync(Settings("Dev"), runArgs, Encoding.UTF8, _logger);

        Assert.Equal(1, exitCode);
        Assert.Contains($"QueryToCsv: SQL file not found: {expectedPath}", _error.ToString(), StringComparison.Ordinal);
        Assert.Contains($"ERROR|SQL file not found: {expectedPath}", _log.Logs);
    }

    [Fact]
    [Trait("Requirement", "QTC-9")]
    internal async Task RunOneLinerAsync_SqlFileOutsideQueryFolder_ReportsAUsageError()
    {
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(_dir.Path)!, $"{Path.GetFileName(_dir.Path)}.sql"), "SELECT 1");
        CliRunArgs runArgs = new(null, null, $"..\\{Path.GetFileName(_dir.Path)}.sql", "utf-8", true);

        try
        {
            int exitCode = await QueryRun.RunOneLinerAsync(Settings("Dev"), runArgs, Encoding.UTF8, _logger);

            Assert.Equal(2, exitCode);
            Assert.Contains(
                $"QueryToCsv: SQL file \"{runArgs.SqlFile}\" is outside QueryFolder; give an absolute path instead.",
                _error.ToString(),
                StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(Path.Combine(Path.GetDirectoryName(_dir.Path)!, $"{Path.GetFileName(_dir.Path)}.sql"));
        }
    }

    [Theory]
    // value -> resolves inside the folder (relative) or as given (absolute); null when outside
    [InlineData("a.sql", "{q}\\a.sql")]
    [InlineData("sub\\a.sql", "{q}\\sub\\a.sql")]
    [InlineData(".\\a.sql", "{q}\\a.sql")]
    [InlineData("sub\\..\\a.sql", "{q}\\a.sql")]
    [InlineData("..\\a.sql", null)]
    [InlineData("sub\\..\\..\\a.sql", null)]
    [InlineData("\\a.sql", null)]
    [InlineData("C:\\elsewhere\\a.sql", "C:\\elsewhere\\a.sql")]
    [InlineData("\\\\server\\share\\a.sql", "\\\\server\\share\\a.sql")]
    [Trait("Requirement", "QTC-9")]
    internal void ResolveSqlFile_Value_StaysInsideQueryFolderUnlessAbsolute(string value, string? expected)
    {
        string queryFolder = Path.Combine(_dir.Path, "q");

        Assert.Equal(expected?.Replace("{q}", queryFolder, StringComparison.Ordinal), QueryRun.ResolveSqlFile(value, queryFolder));
    }

    [Fact]
    [Trait("Requirement", "QTC-8")]
    internal void ResolveConnectionString_NoCredential_UsesTheConnectionStringAsWritten()
    {
        ConnectionEntry entry = new() { Name = "Dev", ConnectionString = "Server=x;Integrated Security=True;" };

        string resolved = QueryRun.ResolveConnectionString(entry, _ => throw new InvalidOperationException("not read"));

        Assert.Equal(entry.ConnectionString, resolved);
    }

    [Fact]
    [Trait("Requirement", "QTC-8")]
    internal void ResolveConnectionString_Credential_AddsItsUserNameAndPassword()
    {
        ConnectionEntry entry = new() { Name = "Dev", ConnectionString = "Server=x;Database=y;", Credential = "QueryToCsv/Dev" };

        string resolved = QueryRun.ResolveConnectionString(
            entry, name => name == "QueryToCsv/Dev" ? new CredentialStore.Secret("reader", "p@ss;word") : null);

        SqlConnectionStringBuilder builder = new(resolved);
        Assert.Equal("reader", builder.UserID);
        Assert.Equal("p@ss;word", builder.Password);
        Assert.Equal("x", builder.DataSource);
    }

    [Fact]
    [Trait("Requirement", "QTC-8")]
    internal void ResolveConnectionString_CredentialMissingOrUnreadable_FailsNamingIt()
    {
        ConnectionEntry entry = new() { Name = "Dev", ConnectionString = "Server=x;", Credential = "QueryToCsv/Dev" };
        const string Expected = "credential \"QueryToCsv/Dev\" was not found in Windows Credential Manager.";

        Assert.Equal(Expected, Assert.Throws<RunFailureException>(() => QueryRun.ResolveConnectionString(entry, _ => null)).Message);
        Assert.Equal(
            Expected,
            Assert.Throws<RunFailureException>(() => QueryRun.ResolveConnectionString(entry, _ => throw new Win32Exception(5))).Message);
    }

    [Fact]
    [Trait("Requirement", "QTC-8")]
    internal void CredentialStore_UnknownTarget_ReturnsNothing()
    {
        Assert.Null(CredentialStore.Read($"QueryToCsvTests/{Guid.NewGuid():N}"));
    }

    [Fact]
    [Trait("Requirement", "QTC-13")]
    internal async Task RunAsync_UnknownEncoding_IsReportedBeforeConfigurationIsRead()
    {
        CliRunArgs runArgs = new(null, "SELECT 1", null, "utf-32", true);

        int exitCode = await QueryRun.RunAsync(runArgs, _logger);

        Assert.Equal(2, exitCode);
        string error = _error.ToString();
        Assert.StartsWith(
            "QueryToCsv: unknown encoding \"utf-32\". Use: utf-8, utf-8-bom, utf-16, shift-jis" + Environment.NewLine
            + "Try 'QueryToCsv --help' for more information.",
            error,
            StringComparison.Ordinal);
        Assert.DoesNotContain("appsettings.json", error, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "QTC-13")]
    internal async Task RunAsync_NoConfiguration_ReportsTheMissingFileThenTheConnectionsError()
    {
        CliRunArgs runArgs = new("Nowhere", "SELECT 1", null, "utf-8", true);

        int exitCode = await QueryRun.RunAsync(runArgs, _logger);

        // The test assembly's directory holds no appsettings.json, so no connection exists;
        // that runtime error comes before the -c usage error.
        Assert.Equal(1, exitCode);
        Assert.Equal(
            "QueryToCsv: appsettings.json not found; continuing with built-in defaults." + Environment.NewLine
            + "QueryToCsv: Connections must contain at least one entry." + Environment.NewLine,
            _error.ToString());
    }

    [Fact]
    [Trait("Requirement", "QTC-12")]
    internal async Task RunInteractiveAsync_InputEndsAtTheConnectionPrompt_EndsTheRunAndLogsIt()
    {
        Console.SetIn(new StringReader(""));

        int exitCode = await QueryRun.RunInteractiveAsync(Settings("Dev", "Prod"), _logger);

        Assert.Equal(1, exitCode);
        Assert.Contains("INFO|Input ended at a prompt", _log.Logs);
        Assert.Contains("1. Dev (unused - unused)", _error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "QTC-12")]
    internal async Task RunInteractiveAsync_ConnectionWithoutServer_ShowsQuestionMarks()
    {
        Console.SetIn(new StringReader(""));
        AppSettings settings = new()
        {
            Connections =
            [
                new ConnectionEntry { Name = "A", ConnectionString = "Encrypt=Mandatory;" },
                new ConnectionEntry { Name = "B", ConnectionString = "Server=s;" },
            ],
            QueryFolder = _dir.Path,
        };

        await QueryRun.RunInteractiveAsync(settings, _logger);

        Assert.Contains("1. A (? - ?)", _error.ToString(), StringComparison.Ordinal);
        Assert.Contains("2. B (s - ?)", _error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "QTC-12")]
    internal async Task RunInteractiveAsync_InvalidAnswers_AreAskedAgain()
    {
        File.WriteAllText(Path.Combine(_dir.Path, "b.sql"), "SELECT 1");
        File.WriteAllText(Path.Combine(_dir.Path, "A.sql"), "SELECT 1");
        Console.SetIn(new StringReader("9\n+2\n 2 \nmaybe\n Y \n5\n"));

        int exitCode = await QueryRun.RunInteractiveAsync(Settings("Dev"), _logger);

        Assert.Equal(1, exitCode);
        string error = _error.ToString();
        Assert.Contains("1. A.sql" + Environment.NewLine + "2. b.sql", error, StringComparison.Ordinal);
        Assert.Equal(2, CountOf(error, "Please enter a number between 0 and 2."));
        Assert.Equal(1, CountOf(error, "Please enter y or n."));
        Assert.Equal(1, CountOf(error, "Please enter a number between 1 and 4."));
        Assert.Contains("INFO|Query selected: b.sql", _log.Logs);
    }

    [Fact]
    [Trait("Requirement", "QTC-12")]
    internal async Task RunInteractiveAsync_InputEndsAtTheQueryPrompt_EndsTheRunAndLogsIt()
    {
        Console.SetIn(new StringReader(""));

        int exitCode = await QueryRun.RunInteractiveAsync(Settings("Dev"), _logger);

        Assert.Equal(1, exitCode);
        Assert.Contains("INFO|Input ended at a prompt", _log.Logs);
    }

    [Fact]
    [Trait("Requirement", "QTC-12")]
    internal async Task RunInteractiveAsync_DirectEntryLeftEmpty_ReportsAndLogsTheError()
    {
        Console.SetIn(new StringReader("0\n   \n"));

        int exitCode = await QueryRun.RunInteractiveAsync(Settings("Dev"), _logger);

        Assert.Equal(1, exitCode);
        Assert.Contains("QueryToCsv: no query entered.", _error.ToString(), StringComparison.Ordinal);
        Assert.Contains("ERROR|no query entered.", _log.Logs);
    }

    [Fact]
    [Trait("Requirement", "QTC-14")]
    internal void InputQuery_DirectEntryOverTheLimit_KeepsOnlyEnoughToBeRejected()
    {
        string line = new('x', 600_000);
        Console.SetIn(new StringReader($"{line}\n{line}\n{line}\n{line}\n"));

        string? typed = ConsoleUi.InputQuery();

        // Two lines pass the limit; the rest are read and dropped, so memory stays bounded
        // and the length check still rejects the text.
        Assert.NotNull(typed);
        Assert.Equal((line.Length * 2) + 2, typed.Length);
        Assert.NotNull(SqlText.LengthError(typed));
    }

    [Fact]
    [Trait("Requirement", "QTC-14")]
    internal async Task RunOneLinerAsync_InlineQueryOverTheLimit_IsRejected()
    {
        CliRunArgs runArgs = new(null, new string('x', SqlText.MaxCharacters + 1), null, "utf-8", true);

        int exitCode = await QueryRun.RunOneLinerAsync(Settings("Dev"), runArgs, Encoding.UTF8, _logger);

        Assert.Equal(1, exitCode);
        Assert.Contains("QueryToCsv: SQL text exceeds 1,048,576 characters.", _error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "QTC-12")]
    internal async Task RunInteractiveAsync_InputEndsAtTheHeaderPrompt_EndsTheRunAndLogsIt()
    {
        File.WriteAllText(Path.Combine(_dir.Path, "report.sql"), "SELECT 1");
        Console.SetIn(new StringReader("1\n"));

        int exitCode = await QueryRun.RunInteractiveAsync(Settings("Dev"), _logger);

        Assert.Equal(1, exitCode);
        Assert.Contains("INFO|Query selected: report.sql", _log.Logs);
        Assert.Contains("INFO|Input ended at a prompt", _log.Logs);
    }

    [Fact]
    [Trait("Requirement", "QTC-12")]
    internal async Task RunInteractiveAsync_InputEndsAtTheEncodingPrompt_EndsTheRunAndLogsIt()
    {
        File.WriteAllText(Path.Combine(_dir.Path, "report.sql"), "SELECT 1");
        Console.SetIn(new StringReader("1\ny\n"));

        int exitCode = await QueryRun.RunInteractiveAsync(Settings("Dev"), _logger);

        Assert.Equal(1, exitCode);
        Assert.DoesNotContain(_log.Logs, entry => entry.StartsWith("INFO|Header:", StringComparison.Ordinal));
        Assert.Contains("INFO|Input ended at a prompt", _log.Logs);
    }

    // Ctrl+C ends standard input at the prompt; the run reports cancellation rather than
    // the end of input (QTC-25).
    [Fact]
    [Trait("Requirement", "QTC-25")]
    internal async Task RunInteractiveAsync_CancelledWhileAtAPrompt_ThrowsCancellation()
    {
        Console.SetIn(new StringReader(""));
        using CancellationTokenSource cancellation = new();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => QueryRun.RunInteractiveAsync(Settings("Dev"), _logger, cancellation.Token));
        Assert.DoesNotContain("INFO|Input ended at a prompt", _log.Logs);
    }

    private static int CountOf(string text, string part)
        => (text.Length - text.Replace(part, "", StringComparison.Ordinal).Length) / part.Length;

    private AppSettings Settings(params string[] connectionNames) => new()
    {
        Connections =
        [
            .. connectionNames.Select(name => new ConnectionEntry
            {
                Name = name,
                ConnectionString = "Server=unused;Database=unused;",
            }),
        ],
        QueryFolder = _dir.Path,
        OutputFolder = Path.Combine(_dir.Path, "output"),
    };
}
