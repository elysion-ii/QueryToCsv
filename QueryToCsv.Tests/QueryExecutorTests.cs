using Xunit;

namespace QueryToCsv.Tests;

public class QueryExecutorTests
{
    // sql -> accepted as a single SELECT statement (true) or rejected (false)
    public static TheoryData<string, bool> Statements() => new()
    {
        // Nothing to run
        { "", false },
        { "   ", false },
        { "-- a comment and nothing else", false },
        // A single SELECT
        { "SELECT 1", true },
        { "SELECT * FROM Users", true },
        { "select top 10 * from Users order by Id", true },
        { "SELECT u.Id FROM Users u JOIN Orders o ON o.UserId = u.Id", true },
        { "WITH c AS (SELECT 1 AS n) SELECT n FROM c", true },
        { "SELECT 1;", true },
        // Identifiers that look like keywords stay usable
        { "SELECT CreateDate, UpdateUser, DeletedFlag FROM Users", true },
        { "SELECT * FROM UpdateLog", true },
        { "SELECT [Update], [Delete] FROM Users", true },
        // Comments and string literals never change the decision
        { "-- DELETE FROM Users\nSELECT * FROM Users", true },
        { "/* UPDATE Users SET x = 1 */ SELECT 1", true },
        { "SELECT 'DROP TABLE Users' AS Note", true },
        { "SELECT 'it''s a DELETE' AS Note", true },
        // Not a SELECT
        { "INSERT INTO Users (Id) VALUES (1)", false },
        { "UPDATE Users SET Name = 'x'", false },
        { "DELETE FROM Users", false },
        { "DROP TABLE Users", false },
        { "TRUNCATE TABLE Users", false },
        { "ALTER TABLE Users ADD Col INT", false },
        { "CREATE TABLE T (Id INT)", false },
        { "MERGE Target USING Source ON 1 = 1 WHEN MATCHED THEN DELETE;", false },
        { "EXEC sp_who", false },
        { "EXECUTE sp_who", false },
        { "sp_who", false },
        { "GRANT SELECT ON Users TO public", false },
        { "REVOKE SELECT ON Users FROM public", false },
        { "DENY SELECT ON Users TO public", false },
        { "DBCC CHECKDB", false },
        { "SHUTDOWN", false },
        { "BULK INSERT Users FROM 'c:\\data.txt'", false },
        { "WITH c AS (SELECT 1 AS n) DELETE FROM Users", false },
        // A SELECT that writes or reads outside the connected database
        { "SELECT * INTO #Temp FROM Users", false },
        { "SELECT * FROM OPENROWSET('SQLNCLI', 'Server=x;', 'SELECT 1') AS r", false },
        { "SELECT * FROM OPENROWSET(BULK 'c:\\data.txt', SINGLE_CLOB) AS f", false },
        { "SELECT * FROM OPENQUERY(Linked, 'SELECT 1')", false },
        { "SELECT * FROM OPENDATASOURCE('SQLNCLI', 'Data Source=x;').db.dbo.T", false },
        // A second statement after the SELECT, with or without a separator
        { "SELECT 1; DELETE FROM Users", false },
        { "SELECT 1; SELECT 2", false },
        { "SELECT 1 SHUTDOWN", false },
        { "SELECT 1 DISABLE TRIGGER ALL ON dbo.Users", false },
        { "SELECT 1\nGO\nSELECT 2", false },
        // Not valid T-SQL
        { "SELECT FROM", false },
    };

    [Theory]
    [MemberData(nameof(Statements))]
    internal void IsSelectOnly_Statement_MatchesExpectation(string sql, bool expected)
    {
        Assert.Equal(expected, QueryExecutor.IsSelectOnly(sql));
    }

    [Theory]
    [InlineData(null, "20260302_153045.csv")]
    [InlineData("", "20260302_153045.csv")]
    [InlineData("sales_report", "sales_report_20260302_153045.csv")]
    [InlineData("user list", "user list_20260302_153045.csv")]
    internal void BuildOutputPath_NoCollision_UsesQueryNameAndTimestamp(string? baseName, string expectedFileName)
    {
        using TempDirectory dir = new();
        DateTime timestamp = new(2026, 3, 2, 15, 30, 45, DateTimeKind.Unspecified);

        string path = QueryExecutor.BuildOutputPath(dir.Path, baseName, timestamp);

        Assert.Equal(Path.Combine(dir.Path, expectedFileName), path);
    }

    [Fact]
    internal void BuildOutputPath_ExistingFiles_AppendsTheFirstFreeSuffix()
    {
        using TempDirectory dir = new();
        DateTime timestamp = new(2026, 3, 2, 15, 30, 45, DateTimeKind.Unspecified);
        File.WriteAllText(Path.Combine(dir.Path, "sales_report_20260302_153045.csv"), "");
        File.WriteAllText(Path.Combine(dir.Path, "sales_report_20260302_153045_2.csv"), "");

        string path = QueryExecutor.BuildOutputPath(dir.Path, "sales_report", timestamp);

        Assert.Equal(Path.Combine(dir.Path, "sales_report_20260302_153045_3.csv"), path);
    }

    [Fact]
    internal void BuildOutputPath_DirectInputCollision_AppendsSuffixToTheTimestamp()
    {
        using TempDirectory dir = new();
        DateTime timestamp = new(2026, 3, 2, 15, 30, 45, DateTimeKind.Unspecified);
        File.WriteAllText(Path.Combine(dir.Path, "20260302_153045.csv"), "");

        string path = QueryExecutor.BuildOutputPath(dir.Path, null, timestamp);

        Assert.Equal(Path.Combine(dir.Path, "20260302_153045_2.csv"), path);
    }
}
