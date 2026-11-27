using System.Text;
using QueryToCsv.TestSupport;
using Xunit;

namespace QueryToCsv.EndToEndTests;

// Runs that reach the test-run SQL Server over an encrypted, certificate-pinned connection,
// authenticating with a credential stored in Windows Credential Manager.
[Collection(SqlServerDefinition.Name)]
public sealed class ExportTests : IDisposable
{
    private readonly SqlServerFixture _server;
    private readonly AppInstallation _app = new();
    private readonly CredentialManager _credential;

    public ExportTests(SqlServerFixture server)
    {
        _server = server;
        _credential = new CredentialManager("sa", server.Password);
        _app.WriteSettings(server, _credential.TargetName);
    }

    public void Dispose()
    {
        _credential.Dispose();
        _app.Dispose();
    }

    [Fact]
    [Trait("Requirement", "QTC-8")]
    [Trait("Requirement", "QTC-9")]
    [Trait("Requirement", "QTC-16")]
    [Trait("Requirement", "QTC-17")]
    [Trait("Requirement", "QTC-19")]
    [Trait("Requirement", "QTC-20")]
    [Trait("Requirement", "QTC-21")]
    [Trait("Requirement", "QTC-23")]
    [Trait("Requirement", "QTC-24")]
    internal async Task OneLiner_InlineQuery_WritesTheCsvAndReportsIt()
    {
        AppResult result = await _app.RunAsync(
            ["--query", "SELECT n, CAST('2026-03-02' AS date) AS d, CAST(n % 2 AS bit) AS odd, '=x' AS f FROM (VALUES (1), (2)) AS v(n) ORDER BY n"]);

        Assert.Equal(0, result.ExitCode);
        string csv = Assert.Single(_app.OutputFiles());
        Assert.Matches(@"^\d{8}_\d{6}\.csv$", Path.GetFileName(csv));
        Assert.Equal($"Done: {csv}{Environment.NewLine}Rows: 2{Environment.NewLine}", result.Output);
        Assert.Equal("n,d,odd,f\r\n1,2026-03-02,1,'=x\r\n2,2026-03-02,0,'=x\r\n", File.ReadAllText(csv));
        Assert.Contains("Writing CSV...", result.Error, StringComparison.Ordinal);

        string log = _app.LogText();
        Assert.Contains("Query selected: [Inline]", log, StringComparison.Ordinal);
        Assert.Contains($"CSV written: {csv} (2 rows)", log, StringComparison.Ordinal);
        Assert.DoesNotContain(_server.Password, log, StringComparison.Ordinal);
        Assert.DoesNotContain("Password", log, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Requirement", "QTC-14")]
    [Trait("Requirement", "QTC-17")]
    [Trait("Requirement", "QTC-22")]
    internal async Task OneLiner_SqlFile_IsNamedAfterTheFileAndNeverOverwrites()
    {
        Encoding shiftJis = CodePagesShiftJis();
        // A byte order mark wins over SqlFileEncoding, which stays UTF-8 here.
        File.WriteAllBytes(Path.Combine(_app.QueryFolder, "sales.sql"), [0xFF, 0xFE, .. Encoding.Unicode.GetBytes("SELECT N'売上' AS x")]);

        AppResult first = await _app.RunAsync(["-f", "sales.sql", "--no-header", "-e", "shift-jis"]);
        AppResult second = await _app.RunAsync(["--file=sales.sql", "--no-header", "--encoding=shift-jis"]);

        Assert.Equal(0, first.ExitCode);
        Assert.Equal(0, second.ExitCode);
        string[] files = [.. _app.OutputFiles().Select(path => Path.GetFileName(path)).Order(StringComparer.Ordinal)];
        Assert.Equal(2, files.Length);
        Assert.Matches(@"^sales_\d{8}_\d{6}(_2)?\.csv$", files[0]);
        Assert.All(_app.OutputFiles(), path => Assert.Equal("売上\r\n", shiftJis.GetString(File.ReadAllBytes(path))));
    }

    [Fact]
    [Trait("Requirement", "QTC-22")]
    internal async Task OneLiner_UnrepresentableCharacter_EndsTheRunWithoutACsv()
    {
        AppResult result = await _app.RunAsync(["--query", "SELECT N'😀' AS emoji", "-e", "shift-jis"]);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("QueryToCsv: the value in row 1, column \"emoji\" cannot be written in shift_jis.", result.Error, StringComparison.Ordinal);
        Assert.Empty(_app.OutputFiles());
        Assert.Empty(Directory.GetFiles(_app.StagingFolder));
    }

    [Fact]
    [Trait("Requirement", "QTC-11")]
    [Trait("Requirement", "QTC-12")]
    internal async Task Interactive_AnsweredPrompts_WriteTheChosenQueryInTheChosenEncoding()
    {
        _app.WriteSettings(new
        {
            Connections = new[]
            {
                new { Name = "Other", ConnectionString = "Server=nowhere.invalid;Integrated Security=True;", Credential = (string?)null },
                new { Name = "Test", ConnectionString = _server.ConnectionStringWithoutCredentials, Credential = (string?)_credential.TargetName },
            },
        });
        File.WriteAllText(Path.Combine(_app.QueryFolder, "b.sql"), "SELECT 2 AS b");
        File.WriteAllText(Path.Combine(_app.QueryFolder, "a.sql"), "SELECT 1 AS a");

        AppResult result = await _app.RunAsync([], standardInput: "3\n2\n 1 \nmaybe\nY\n2\n");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("1. Other (nowhere.invalid - ?)", result.Error, StringComparison.Ordinal);
        Assert.Contains("Please enter a number between 1 and 2.", result.Error, StringComparison.Ordinal);
        Assert.Contains("Please enter y or n.", result.Error, StringComparison.Ordinal);
        string csv = Assert.Single(_app.OutputFiles());
        Assert.StartsWith("a_", Path.GetFileName(csv), StringComparison.Ordinal);
        Assert.Equal([0xEF, 0xBB, 0xBF, .. "a\r\n1\r\n"u8.ToArray()], File.ReadAllBytes(csv));

        // The same connection name in another case selects it in one-liner mode.
        AppResult oneLiner = await _app.RunAsync(["-c", "TEST", "-f", "b.sql"]);
        Assert.Equal(0, oneLiner.ExitCode);
    }

    [Fact]
    [Trait("Requirement", "QTC-7")]
    [Trait("Requirement", "QTC-13")]
    internal async Task OneLiner_TrustServerCertificate_ConnectsAndWarnsWithoutTheConnectionString()
    {
        string connectionString = _server.ConnectionStringTrustingAnyCertificate;
        _app.WriteSettings(new
        {
            Connections = new[] { new { Name = "Takai", ConnectionString = connectionString, Credential = _credential.TargetName } },
        });

        AppResult result = await _app.RunAsync(["--query", "SELECT 1 AS a"]);

        Assert.Equal(0, result.ExitCode);
        string csv = Assert.Single(_app.OutputFiles());
        Assert.Equal("a\r\n1\r\n", File.ReadAllText(csv));

        const string Warning =
            "Connections[0] \"Takai\" contains an insecure setting: TrustServerCertificate=True accepts any server "
            + "certificate, so the connection can be intercepted. Give the server a certificate the client trusts, or pin "
            + "it with \"Server Certificate\".";
        string[] errorLines = result.Error.Split(Environment.NewLine);
        Assert.Single(errorLines, line => line == "QueryToCsv: " + Warning);
        string log = _app.LogText();
        Assert.Contains("[WARN ] " + Warning, log, StringComparison.Ordinal);

        foreach (string text in new[] { result.Output, result.Error, log })
        {
            Assert.DoesNotContain(connectionString, text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(_server.Server, text, StringComparison.Ordinal);
        }
    }

    [Fact]
    [Trait("Requirement", "QTC-8")]
    internal async Task MissingCredential_EndsTheRunBeforeConnecting()
    {
        _app.WriteSettings(_server, "QueryToCsvEndToEnd/not-stored");

        AppResult result = await _app.RunAsync(["--query", "SELECT 1 AS a"]);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains(
            "QueryToCsv: credential \"QueryToCsvEndToEnd/not-stored\" was not found in Windows Credential Manager.",
            result.Error,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Connecting...", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "QTC-16")]
    internal async Task QueryLongerThanTheTimeout_EndsAsTimedOut()
    {
        _app.WriteSettings(new
        {
            QueryTimeout = 1,
            Connections = new[] { new { Name = "Test", ConnectionString = _server.ConnectionStringWithoutCredentials, Credential = _credential.TargetName } },
        });

        AppResult result = await _app.RunAsync(
            ["--query", "SELECT COUNT_BIG(*) AS n FROM sys.all_columns a CROSS JOIN sys.all_columns b CROSS JOIN sys.all_columns c"]);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("QueryToCsv: query timed out.", result.Error, StringComparison.Ordinal);
        Assert.Empty(_app.OutputFiles());
    }

    [Fact]
    [Trait("Requirement", "QTC-18")]
    internal async Task OutputFolderThatIsAFile_IsReportedAndLeavesNoTemporaryFile()
    {
        string blocked = Path.Combine(_app.Folder, "blocked");
        File.WriteAllText(blocked, "");
        _app.WriteSettings(new
        {
            OutputFolder = "blocked",
            Connections = new[] { new { Name = "Test", ConnectionString = _server.ConnectionStringWithoutCredentials, Credential = _credential.TargetName } },
        });

        AppResult result = await _app.RunAsync(["--query", "SELECT 1 AS a"]);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains($"QueryToCsv: cannot write the output file in \"{blocked}\".", result.Error, StringComparison.Ordinal);
        Assert.Empty(Directory.GetFiles(_app.StagingFolder));
    }

    [Fact]
    [Trait("Requirement", "QTC-18")]
    internal async Task LeftoversOfAnEarlierRun_AreDeletedWhenOlderThanADay()
    {
        Directory.CreateDirectory(_app.OutputFolder);
        Directory.CreateDirectory(_app.StagingFolder);
        string oldTemporary = Path.Combine(_app.OutputFolder, ".old.QueryToCsv.tmp");
        string oldStaged = Path.Combine(_app.StagingFolder, "old.csv");
        string recentStaged = Path.Combine(_app.StagingFolder, "recent.csv");
        foreach (string path in new[] { oldTemporary, oldStaged, recentStaged })
            File.WriteAllText(path, "");
        File.SetLastWriteTimeUtc(oldTemporary, DateTime.UtcNow.AddDays(-2));
        File.SetLastWriteTimeUtc(oldStaged, DateTime.UtcNow.AddDays(-2));

        AppResult result = await _app.RunAsync(["--query", "SELECT 1 AS a"]);

        Assert.Equal(0, result.ExitCode);
        Assert.False(File.Exists(oldTemporary));
        Assert.False(File.Exists(oldStaged));
        Assert.True(File.Exists(recentStaged));
    }

    [Fact]
    [Trait("Requirement", "QTC-26")]
    internal async Task Executable_IsOneFileAndUnpacksNothingIntoTheTemporaryFolder()
    {
        string buildFolder = Path.GetDirectoryName(AppInstallation.BuiltExecutable)!;
        Assert.Equal(
            ["QueryToCsv.exe", "appsettings.json"],
            Directory.GetFiles(buildFolder).Select(path => Path.GetFileName(path)).Order(StringComparer.Ordinal));

        AppResult result = await _app.RunAsync(["--query", "SELECT 1 AS a"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(["QueryToCsv"], Directory.GetFileSystemEntries(_app.TempFolder).Select(Path.GetFileName));
        Assert.Empty(Directory.GetFileSystemEntries(_app.StagingFolder));
    }

    private static Encoding CodePagesShiftJis()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding("shift_jis");
    }
}
