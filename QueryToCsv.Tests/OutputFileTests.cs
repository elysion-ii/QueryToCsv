using QueryToCsv.TestSupport;
using Xunit;

namespace QueryToCsv.Tests;

public class OutputFileTests
{
    private static readonly DateTime Timestamp = new(2026, 3, 2, 15, 30, 45, DateTimeKind.Unspecified);
    private static readonly DateTime UtcNow = new(2026, 3, 2, 6, 30, 45, DateTimeKind.Utc);

    [Theory]
    [InlineData(null, 1, "20260302_153045.csv")]
    [InlineData("", 1, "20260302_153045.csv")]
    [InlineData(null, 2, "20260302_153045_2.csv")]
    [InlineData("sales_report", 1, "sales_report_20260302_153045.csv")]
    [InlineData("sales_report", 3, "sales_report_20260302_153045_3.csv")]
    [InlineData("user list", 1, "user list_20260302_153045.csv")]
    [Trait("Requirement", "QTC-17")]
    internal void CandidateName_QueryAndAttempt_UsesNameTimestampAndSuffix(string? baseName, int attempt, string expected)
    {
        Assert.Equal(expected, OutputFile.CandidateName(baseName, Timestamp, attempt));
    }

    [Fact]
    [Trait("Requirement", "QTC-17")]
    [Trait("Requirement", "QTC-18")]
    internal void Place_ExistingFiles_TakesTheFirstFreeNameAndKeepsTheOthers()
    {
        using TempDirectory dir = new();
        string output = Path.Combine(dir.Path, "out", "nested");
        Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(output, "sales_20260302_153045.csv"), "first");
        File.WriteAllText(Path.Combine(output, "sales_20260302_153045_2.csv"), "second");
        string staged = Staged(dir, "third");

        string placed = OutputFile.Place(staged, output, "sales", Timestamp);

        Assert.Equal(Path.Combine(output, "sales_20260302_153045_3.csv"), placed);
        Assert.Equal("first", File.ReadAllText(Path.Combine(output, "sales_20260302_153045.csv")));
        Assert.Equal("second", File.ReadAllText(Path.Combine(output, "sales_20260302_153045_2.csv")));
        Assert.Equal("third", File.ReadAllText(placed));
        Assert.Empty(Directory.GetFiles(output, "*" + OutputFile.TemporarySuffix));
    }

    [Fact]
    [Trait("Requirement", "QTC-18")]
    internal void Place_MissingOutputFolder_CreatesIt()
    {
        using TempDirectory dir = new();
        string output = Path.Combine(dir.Path, "a", "b");

        string placed = OutputFile.Place(Staged(dir, "x"), output, null, Timestamp);

        Assert.Equal(Path.Combine(output, "20260302_153045.csv"), placed);
    }

    // OUTPUT-KEPT: runs that finish with the same name all succeed, each with its own file.
    [Fact]
    [Trait("Requirement", "QTC-17")]
    internal async Task Place_ConcurrentRunsWithTheSameName_EachTakesItsOwnFile()
    {
        using TempDirectory dir = new();
        string output = Path.Combine(dir.Path, "out");
        string[] staged = [.. Enumerable.Range(0, 8).Select(i => Staged(dir, $"run{i}"))];

        string[] placed = await Task.WhenAll(staged.Select(path => Task.Run(() => OutputFile.Place(path, output, "q", Timestamp))));

        Assert.Equal(8, placed.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(
            Enumerable.Range(0, 8).Select(i => $"run{i}").Order(StringComparer.Ordinal),
            placed.Select(File.ReadAllText).Order(StringComparer.Ordinal));
    }

    [Fact]
    [Trait("Requirement", "QTC-18")]
    internal void DeleteLeftovers_OldOwnFiles_AreDeletedAndEverythingElseKept()
    {
        using TempDirectory dir = new();
        string staging = Path.Combine(dir.Path, "staging");
        string output = Path.Combine(dir.Path, "out");
        Directory.CreateDirectory(staging);
        Directory.CreateDirectory(output);

        string oldStaged = Touch(staging, "a.csv", hoursOld: 25);
        string newStaged = Touch(staging, "b.csv", hoursOld: 23);
        string oldTemporary = Touch(output, ".x" + OutputFile.TemporarySuffix, hoursOld: 25);
        string newTemporary = Touch(output, ".y" + OutputFile.TemporarySuffix, hoursOld: 1);
        string oldCsv = Touch(output, "report.csv", hoursOld: 1000);
        string otherTmp = Touch(output, "report.csv.tmp", hoursOld: 1000);

        OutputFile.DeleteLeftovers(staging, output, UtcNow);

        Assert.False(File.Exists(oldStaged));
        Assert.False(File.Exists(oldTemporary));
        Assert.True(File.Exists(newStaged));
        Assert.True(File.Exists(newTemporary));
        Assert.True(File.Exists(oldCsv));
        Assert.True(File.Exists(otherTmp));
    }

    [Fact]
    [Trait("Requirement", "QTC-18")]
    internal void DeleteLeftovers_MissingFolders_DoNothing()
    {
        using TempDirectory dir = new();

        OutputFile.DeleteLeftovers(Path.Combine(dir.Path, "none"), Path.Combine(dir.Path, "none2"), UtcNow);
    }

    private static string Staged(TempDirectory dir, string content)
    {
        string path = OutputFile.CreateStagingPath(Path.Combine(dir.Path, "staging"));
        File.WriteAllText(path, content);
        return path;
    }

    private static string Touch(string folder, string name, int hoursOld)
    {
        string path = Path.Combine(folder, name);
        File.WriteAllText(path, "");
        File.SetLastWriteTimeUtc(path, UtcNow.AddHours(-hoursOld));
        return path;
    }
}
