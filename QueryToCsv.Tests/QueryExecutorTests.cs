using Xunit;

namespace QueryToCsv.Tests;

public class QueryExecutorTests
{
    // sql -> accepted (true) or rejected as non-SELECT (false)
    public static TheoryData<string, bool> Statements() => new()
    {
        { "", true },
        { "SELECT 1", true },
        { "SELECT * FROM Users", true },
        { "select top 10 * from Users order by Id", true },
        { "SELECT u.Id FROM Users u JOIN Orders o ON o.UserId = u.Id", true },
        // Prohibited keywords are matched on word boundaries, so these column names stay legal
        { "SELECT CreateDate, UpdateUser, DeletedFlag FROM Users", true },
        { "SELECT * FROM UpdateLog", true },
        // Comments and string literals are stripped before the keyword scan
        { "-- DELETE FROM Users\nSELECT * FROM Users", true },
        { "/* UPDATE Users SET x = 1 */ SELECT 1", true },
        { "SELECT 'DROP TABLE Users' AS Note", true },
        { "SELECT 'it''s a DELETE' AS Note", true },
        // Data-modifying and out-of-scope statements
        { "INSERT INTO Users (Id) VALUES (1)", false },
        { "UPDATE Users SET Name = 'x'", false },
        { "DELETE FROM Users", false },
        { "DROP TABLE Users", false },
        { "TRUNCATE TABLE Users", false },
        { "ALTER TABLE Users ADD Col INT", false },
        { "CREATE TABLE T (Id INT)", false },
        { "MERGE Target USING Source ON 1 = 1", false },
        { "EXEC sp_who", false },
        { "EXECUTE sp_who", false },
        { "GRANT SELECT ON Users TO public", false },
        { "REVOKE SELECT ON Users FROM public", false },
        { "DENY SELECT ON Users TO public", false },
        { "SELECT * INTO #Temp FROM Users", false },
        { "SELECT * FROM OPENROWSET('SQLNCLI', '', 'SELECT 1')", false },
        { "SELECT * FROM OPENQUERY(Linked, 'SELECT 1')", false },
        { "SELECT * FROM OPENDATASOURCE('SQLNCLI', '').db.dbo.T", false },
        { "BULK INSERT Users FROM 'c:\\data.txt'", false },
        // A SELECT followed by a modifying statement is still rejected
        { "SELECT 1; DELETE FROM Users", false },
    };

    [Theory]
    [MemberData(nameof(Statements))]
    public void IsSelectOnly_Statement_MatchesExpectation(string sql, bool expected)
    {
        Assert.Equal(expected, QueryExecutor.IsSelectOnly(sql));
    }

    [Theory]
    [InlineData(null, "20260302_153045.csv")]
    [InlineData("", "20260302_153045.csv")]
    [InlineData("sales_report", "sales_report_20260302_153045.csv")]
    [InlineData("user list", "user list_20260302_153045.csv")]
    public void BuildOutputPath_NoCollision_UsesQueryNameAndTimestamp(string? baseName, string expectedFileName)
    {
        using var dir = new TempDirectory();
        var timestamp = new DateTime(2026, 3, 2, 15, 30, 45, DateTimeKind.Unspecified);

        var path = QueryExecutor.BuildOutputPath(dir.Path, baseName, timestamp);

        Assert.Equal(Path.Combine(dir.Path, expectedFileName), path);
    }

    [Fact]
    public void BuildOutputPath_ExistingFiles_AppendsTheFirstFreeSuffix()
    {
        using var dir = new TempDirectory();
        var timestamp = new DateTime(2026, 3, 2, 15, 30, 45, DateTimeKind.Unspecified);
        File.WriteAllText(Path.Combine(dir.Path, "sales_report_20260302_153045.csv"), "");
        File.WriteAllText(Path.Combine(dir.Path, "sales_report_20260302_153045_2.csv"), "");

        var path = QueryExecutor.BuildOutputPath(dir.Path, "sales_report", timestamp);

        Assert.Equal(Path.Combine(dir.Path, "sales_report_20260302_153045_3.csv"), path);
    }

    [Fact]
    public void BuildOutputPath_DirectInputCollision_AppendsSuffixToTheTimestamp()
    {
        using var dir = new TempDirectory();
        var timestamp = new DateTime(2026, 3, 2, 15, 30, 45, DateTimeKind.Unspecified);
        File.WriteAllText(Path.Combine(dir.Path, "20260302_153045.csv"), "");

        var path = QueryExecutor.BuildOutputPath(dir.Path, null, timestamp);

        Assert.Equal(Path.Combine(dir.Path, "20260302_153045_2.csv"), path);
    }
}
