using System.Globalization;
using QueryToCsv.TestSupport;
using Xunit;

namespace QueryToCsv.Tests;

public class LogSetupTests
{
    private static readonly DateTime Now = new(2026, 3, 2, 15, 30, 45, DateTimeKind.Unspecified);

    private const int RetentionDays = 30;

    [Fact]
    [Trait("Requirement", "QTC-24")]
    internal void ResolveDirectory_PreferredDirectoryWritable_KeepsItAndLeavesNoProbeBehind()
    {
        using var dir = new TempDirectory();
        string preferred = Path.Combine(dir.Path, "logs");
        string fallback = Path.Combine(dir.Path, "appdata-logs");

        string? resolved = LogSetup.ResolveDirectory(preferred, fallback);

        Assert.Equal(preferred, resolved);
        Assert.Empty(Directory.GetFileSystemEntries(preferred));
    }

    [Fact]
    [Trait("Requirement", "QTC-24")]
    internal void ResolveDirectory_PreferredDirectoryUnusable_UsesTheFallback()
    {
        using var dir = new TempDirectory();
        string preferred = Path.Combine(Blocker(dir), "logs");
        string fallback = Path.Combine(dir.Path, "appdata-logs");

        Assert.Equal(fallback, LogSetup.ResolveDirectory(preferred, fallback));
    }

    [Fact]
    [Trait("Requirement", "QTC-24")]
    internal void ResolveDirectory_NeitherUsable_ReturnsNoDirectory()
    {
        using var dir = new TempDirectory();
        string blocker = Blocker(dir);

        Assert.Null(LogSetup.ResolveDirectory(Path.Combine(blocker, "logs"), Path.Combine(blocker, "fallback")));
        Assert.Null(LogSetup.ResolveDirectory(Path.Combine(blocker, "logs"), fallback: null));
    }

    [Theory]
    // Days between the file's own date and today -> the file survives the sweep
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(29, true)]
    [InlineData(30, false)]
    [InlineData(365, false)]
    [Trait("Requirement", "QTC-24")]
    internal void DeleteExpiredLogs_DailyFile_KeepsTheMostRecentRetentionDays(int daysOld, bool kept)
    {
        using var dir = new TempDirectory();
        // The stamp the application writes is invariant-culture, whatever the machine runs
        string stamp = Now.Date.AddDays(-daysOld).ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        string name = $"QueryToCsv_{stamp}.log";
        File.WriteAllText(Path.Combine(dir.Path, name), "");

        LogSetup.DeleteExpiredLogs(dir.Path, RetentionDays, Now);

        Assert.Equal(kept, File.Exists(Path.Combine(dir.Path, name)));
    }

    [Fact]
    [Trait("Requirement", "QTC-24")]
    internal void DeleteExpiredLogs_LargestRetention_KeepsATenYearOldFileAndDoesNotFail()
    {
        using var dir = new TempDirectory();
        string name = $"QueryToCsv_{Now.Date.AddDays(-3649).ToString("yyyyMMdd", CultureInfo.InvariantCulture)}.log";
        File.WriteAllText(Path.Combine(dir.Path, name), "");

        LogSetup.DeleteExpiredLogs(dir.Path, AppSettings.MaxLogRetentionDays, Now);

        Assert.True(File.Exists(Path.Combine(dir.Path, name)));
    }

    [Theory]
    // Only this application's own dated log files are the sweep's to remove
    [InlineData("OtherApp_20250101.log")]
    [InlineData("QueryToCsv_20250101.txt")]
    [InlineData("QueryToCsv_notadate.log")]
    [InlineData("QueryToCsv.log")]
    [InlineData("notes.txt")]
    [Trait("Requirement", "QTC-24")]
    internal void DeleteExpiredLogs_FileOutsideTheNamePattern_IsLeftAlone(string name)
    {
        using var dir = new TempDirectory();
        File.WriteAllText(Path.Combine(dir.Path, name), "");

        LogSetup.DeleteExpiredLogs(dir.Path, RetentionDays, Now);

        Assert.True(File.Exists(Path.Combine(dir.Path, name)));
    }

    [Fact]
    [Trait("Requirement", "QTC-24")]
    internal void DeleteExpiredLogs_MissingDirectory_DoesNothing()
    {
        using var dir = new TempDirectory();

        LogSetup.DeleteExpiredLogs(Path.Combine(dir.Path, "not-created-yet"), RetentionDays, Now);
    }

    // A file where a directory would go: creating the directory fails, which is what a
    // folder the installation cannot write to looks like here.
    private static string Blocker(TempDirectory dir)
    {
        string blocker = Path.Combine(dir.Path, "blocked");
        File.WriteAllText(blocker, "");
        return blocker;
    }
}
