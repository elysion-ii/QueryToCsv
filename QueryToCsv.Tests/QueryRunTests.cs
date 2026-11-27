using System.Text;
using NLog;
using NLog.Config;
using NLog.Targets;
using Xunit;

namespace QueryToCsv.Tests;

// Each failure that ends a run is reported on standard error and recorded in the log
// (docs/specs/QueryToCsv.md, QueryToCsv-004 and QueryToCsv-011). None of these runs
// reaches a database.
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
    internal async Task RunOneLinerAsync_UnconfiguredConnectionName_ReportsAndLogsAUsageError()
    {
        CliRunArgs runArgs = new("Prod", "SELECT 1", null, "utf-8", true);

        int exitCode = await QueryRun.RunOneLinerAsync(Settings("Dev"), runArgs, Encoding.UTF8, _logger);

        Assert.Equal(2, exitCode);
        Assert.Contains("QueryToCsv: connection \"Prod\" not found.", _error.ToString(), StringComparison.Ordinal);
        Assert.Contains("ERROR|connection \"Prod\" not found.", _log.Logs);
    }

    [Fact]
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
    internal async Task RunInteractiveAsync_InputEndsAtTheConnectionPrompt_EndsTheRunAndLogsIt()
    {
        Console.SetIn(new StringReader(""));

        int exitCode = await QueryRun.RunInteractiveAsync(Settings("Dev", "Prod"), _logger);

        Assert.Equal(1, exitCode);
        Assert.Contains("INFO|Input ended at a prompt", _log.Logs);
    }

    [Fact]
    internal async Task RunInteractiveAsync_InputEndsAtTheQueryPrompt_EndsTheRunAndLogsIt()
    {
        Console.SetIn(new StringReader(""));

        int exitCode = await QueryRun.RunInteractiveAsync(Settings("Dev"), _logger);

        Assert.Equal(1, exitCode);
        Assert.Contains("INFO|Input ended at a prompt", _log.Logs);
    }

    [Fact]
    internal async Task RunInteractiveAsync_DirectEntryLeftEmpty_ReportsAndLogsTheError()
    {
        Console.SetIn(new StringReader("0\n   \n"));

        int exitCode = await QueryRun.RunInteractiveAsync(Settings("Dev"), _logger);

        Assert.Equal(1, exitCode);
        Assert.Contains("QueryToCsv: no query entered.", _error.ToString(), StringComparison.Ordinal);
        Assert.Contains("ERROR|no query entered.", _log.Logs);
    }

    [Fact]
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
    internal async Task RunInteractiveAsync_InputEndsAtTheEncodingPrompt_EndsTheRunAndLogsIt()
    {
        File.WriteAllText(Path.Combine(_dir.Path, "report.sql"), "SELECT 1");
        Console.SetIn(new StringReader("1\ny\n"));

        int exitCode = await QueryRun.RunInteractiveAsync(Settings("Dev"), _logger);

        Assert.Equal(1, exitCode);
        Assert.DoesNotContain(_log.Logs, entry => entry.StartsWith("INFO|Header:", StringComparison.Ordinal));
        Assert.Contains("INFO|Input ended at a prompt", _log.Logs);
    }

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
