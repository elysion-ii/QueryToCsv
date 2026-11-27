using System.Text;
using QueryToCsv.TestSupport;
using Xunit;

namespace QueryToCsv.Tests;

// Runs real queries against the test-run SQL Server and reads back the bytes written
// (docs/rules/testing.md, Boundary Tests). Every query selects literal values, so no
// schema is needed.
[Collection(SqlServerDefinition.Name)]
[Trait("Category", SqlServerDefinition.BoundaryCategory)]
public sealed class CsvExportBoundaryTests(SqlServerFixture server) : IDisposable
{
    private readonly TempDirectory _dir = new();

    public void Dispose() => _dir.Dispose();

    [Fact]
    [Trait("Requirement", "QTC-16")]
    [Trait("Requirement", "QTC-19")]
    internal async Task ExportAsync_Rows_AreWrittenInServerOrderWithHeader()
    {
        (int rows, string text) = await ExportAsync("SELECT n, 'r' + CAST(n AS varchar(3)) AS label FROM (VALUES (3), (1), (2)) AS v(n) ORDER BY n");

        Assert.Equal(3, rows);
        Assert.Equal("n,label\r\n1,r1\r\n2,r2\r\n3,r3\r\n", text);
    }

    [Fact]
    [Trait("Requirement", "QTC-19")]
    internal async Task ExportAsync_NoRows_WritesOnlyTheHeaderOrNothing()
    {
        (int withHeader, string headerOnly) = await ExportAsync("SELECT 1 AS a, 2 AS b WHERE 1 = 0");
        (int withoutHeader, string empty) = await ExportAsync("SELECT 1 AS a WHERE 1 = 0", header: false);

        Assert.Equal(0, withHeader);
        Assert.Equal("a,b\r\n", headerOnly);
        Assert.Equal(0, withoutHeader);
        Assert.Equal("", empty);
    }

    [Theory]
    [InlineData("CRLF", "\r\n")]
    [InlineData("LF", "\n")]
    [Trait("Requirement", "QTC-19")]
    internal async Task ExportAsync_FieldsNeedingQuotes_AreQuotedForEveryLineBreak(string newLine, string terminator)
    {
        CsvSettings csv = new() { NewLine = newLine, Delimiter = ";", NullValue = "NULL" };

        (_, string text) = await ExportAsync(
            "SELECT 'a' + CHAR(10) + 'b' AS lf, 'c' + CHAR(13) + 'd' AS cr, 'x;y' AS delim, 'say \"hi\"' AS q, "
            + "' pad' AS lead, 'plain' AS p, NULL AS n, 1",
            csv);

        Assert.Equal(
            $"lf;cr;delim;q;lead;p;n;{terminator}"
            + $"\"a\nb\";\"c\rd\";\"x;y\";\"say \"\"hi\"\"\";\" pad\";plain;NULL;1{terminator}",
            text);
    }

    [Fact]
    [Trait("Requirement", "QTC-20")]
    internal async Task ExportAsync_ServerTypes_AreWrittenInTheirInvariantForms()
    {
        (_, string text) = await ExportAsync(
            "SELECT CAST(0x01AB AS varbinary(4)) AS b, CAST(1 AS bit) AS t, CAST(1e20 AS float) AS f, CAST(0.1 AS real) AS r, "
            + "CAST(12.34 AS money) AS m, CAST(-1.50 AS decimal(5,2)) AS d, "
            + "CAST(12345678901234567890123456789012345678 AS decimal(38,0)) AS big, "
            + "CAST('13:45:00.123' AS time) AS tm, CAST('2026-03-02' AS date) AS dt, "
            + "CAST('2026-03-02T15:30:45.1234567' AS datetime2) AS d2, CAST('2026-03-02T15:30:45' AS datetime) AS d1, "
            + "CAST('2026-03-02T15:30:45+09:00' AS datetimeoffset) AS dto, "
            + "CAST('6F9619FF-8B86-D011-B42D-00C04FC964FF' AS uniqueidentifier) AS g, CAST('ab' AS char(4)) AS c, "
            + "CAST('<a/>' AS xml) AS x, CAST(5 AS sql_variant) AS v",
            header: false);

        Assert.Equal(
            "0x01AB,1,1E+20,0.1,12.3400,-1.50,12345678901234567890123456789012345678,13:45:00.1230000,2026-03-02,"
            + "2026-03-02T15:30:45.1234567,2026-03-02T15:30:45.0000000,2026-03-02T15:30:45.0000000+09:00,"
            + "6f9619ff-8b86-d011-b42d-00c04fc964ff,\"ab  \",<a />,5\r\n",
            text);
    }

    [Fact]
    [Trait("Requirement", "QTC-20")]
    internal async Task ExportAsync_DateFormat_ReplacesDateFormsButNotTime()
    {
        CsvSettings csv = new() { DateFormat = "yyyy/MM/dd" };

        (_, string text) = await ExportAsync(
            "SELECT CAST('2026-03-02' AS date) AS a, CAST('2026-03-02T15:30:45' AS datetime2) AS b, CAST('13:45' AS time) AS c",
            csv,
            header: false);

        Assert.Equal("2026/03/02,2026/03/02,13:45:00.0000000\r\n", text);
    }

    [Theory]
    [InlineData("SELECT hierarchyid::GetRoot() AS h", "column \"h\" has type")]
    [InlineData("SELECT geography::Point(0, 0, 4326) AS g", "column \"g\" has type")]
    [Trait("Requirement", "QTC-20")]
    internal async Task ExportAsync_ClrType_FailsNamingTheColumn(string sql, string expectedStart)
    {
        RunFailureException failure = await Assert.ThrowsAsync<RunFailureException>(() => ExportAsync(sql));

        Assert.StartsWith(expectedStart, failure.Message, StringComparison.Ordinal);
        Assert.EndsWith("which cannot be exported; convert it in the query.", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "QTC-21")]
    internal async Task ExportAsync_FormulaTriggers_AreNeutralizedInTextOnly()
    {
        (_, string text) = await ExportAsync("SELECT N'=1+1' AS a, '-x' AS b, -1 AS c, '@x' AS d", header: false);

        Assert.Equal("'=1+1,'-x,-1,'@x\r\n", text);
    }

    [Theory]
    [InlineData("utf-8", new byte[] { 0x61, 0xC3, 0xA9 })]
    [InlineData("utf-8-bom", new byte[] { 0xEF, 0xBB, 0xBF, 0x61, 0xC3, 0xA9 })]
    [InlineData("utf-16", new byte[] { 0xFF, 0xFE, 0x61, 0x00, 0xE9, 0x00 })]
    [Trait("Requirement", "QTC-22")]
    internal async Task ExportAsync_Encoding_WritesTheBytesAndBomOfThatEncoding(string encodingName, byte[] expectedStart)
    {
        string path = await ExportToFileAsync("SELECT N'aé' AS x", header: false, encoding: ConsoleUi.ResolveEncoding(encodingName));

        Assert.Equal(expectedStart, File.ReadAllBytes(path)[..expectedStart.Length]);
    }

    [Fact]
    [Trait("Requirement", "QTC-22")]
    internal async Task ExportAsync_ShiftJis_WritesJapaneseAndRejectsWhatItCannotRepresent()
    {
        Encoding shiftJis = ConsoleUi.ResolveEncoding("shift-jis")!;
        string path = await ExportToFileAsync("SELECT N'売上' AS x", header: false, encoding: shiftJis);
        Assert.Equal("売上\r\n", shiftJis.GetString(File.ReadAllBytes(path)));

        RunFailureException failure = await Assert.ThrowsAsync<RunFailureException>(
            () => ExportToFileAsync("SELECT N'ok' AS a, N'x' UNION ALL SELECT N'ok', N'😀'", encoding: shiftJis));
        Assert.Equal("the value in row 2, column \"\" cannot be written in shift_jis.", failure.Message);
    }

    [Fact]
    [Trait("Requirement", "QTC-16")]
    internal async Task ExportAsync_ServerError_FailsAsQueryExecutionFailed()
    {
        RunFailureException failure = await Assert.ThrowsAsync<RunFailureException>(() => ExportAsync("SELECT 1 / 0 AS x"));

        Assert.Equal("query execution failed.", failure.Message);
    }

    [Fact]
    [Trait("Requirement", "QTC-16")]
    internal async Task ExportAsync_QueryLongerThanTheTimeout_FailsAsTimedOut()
    {
        string path = Path.Combine(_dir.Path, "slow.csv");

        RunFailureException failure = await Assert.ThrowsAsync<RunFailureException>(() => QueryExecutor.ExportAsync(
            server.ConnectionString,
            "SELECT COUNT_BIG(*) AS n FROM sys.all_columns a CROSS JOIN sys.all_columns b CROSS JOIN sys.all_columns c",
            queryTimeout: 1,
            includeHeader: true,
            ConsoleUi.ResolveEncoding("utf-8")!,
            new CsvSettings(),
            path));

        Assert.Equal("query timed out.", failure.Message);
    }

    [Fact]
    [Trait("Requirement", "QTC-16")]
    internal async Task ExportAsync_ServerUnreachable_FailsAsQueryExecutionFailed()
    {
        string unreachable = "Server=127.0.0.1,1;Database=master;Encrypt=Mandatory;Connect Timeout=2;User ID=sa;Password=x;";

        RunFailureException failure = await Assert.ThrowsAsync<RunFailureException>(() => QueryExecutor.ExportAsync(
            unreachable, "SELECT 1 AS a", 30, true, ConsoleUi.ResolveEncoding("utf-8")!, new CsvSettings(),
            Path.Combine(_dir.Path, "never.csv")));

        Assert.Equal("query execution failed.", failure.Message);
    }

    [Fact]
    [Trait("Requirement", "QTC-25")]
    internal async Task ExportAsync_Cancelled_ThrowsCancellation()
    {
        using CancellationTokenSource cancellation = new(TimeSpan.FromSeconds(1));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => QueryExecutor.ExportAsync(
            server.ConnectionString,
            "SELECT COUNT_BIG(*) AS n FROM sys.all_columns a CROSS JOIN sys.all_columns b CROSS JOIN sys.all_columns c",
            30, true, ConsoleUi.ResolveEncoding("utf-8")!, new CsvSettings(), Path.Combine(_dir.Path, "cancelled.csv"),
            cancellation.Token));
    }

    private async Task<(int Rows, string Text)> ExportAsync(string sql, CsvSettings? csv = null, bool header = true)
    {
        string path = Path.Combine(_dir.Path, $"{Guid.NewGuid():N}.csv");
        int rows = await QueryExecutor.ExportAsync(
            server.ConnectionString, sql, 30, header, ConsoleUi.ResolveEncoding("utf-8")!, csv ?? new CsvSettings(), path);
        return (rows, await File.ReadAllTextAsync(path));
    }

    private async Task<string> ExportToFileAsync(string sql, bool header = true, Encoding? encoding = null)
    {
        string path = Path.Combine(_dir.Path, $"{Guid.NewGuid():N}.csv");
        await QueryExecutor.ExportAsync(
            server.ConnectionString, sql, 30, header, encoding ?? ConsoleUi.ResolveEncoding("utf-8")!, new CsvSettings(), path);
        return path;
    }
}
