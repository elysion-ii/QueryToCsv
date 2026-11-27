using System.Globalization;
using NLog;

namespace QueryToCsv;

// Where the CSV is assembled, how it reaches OutputFolder, and what is cleaned up after
// an interrupted run (docs/specs/QueryToCsv.md, QTC-17 and QTC-18).
internal static class OutputFile
{
    // The ending every temporary file this application leaves in OutputFolder carries;
    // cleanup touches only files with it.
    internal const string TemporarySuffix = ".QueryToCsv.tmp";

    // Older leftovers belong to a run that no longer exists: no export outlives a day.
    internal static readonly TimeSpan LeftoverAge = TimeSpan.FromHours(24);

    private const string TimestampFormat = "yyyyMMdd_HHmmss";

    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    // The application-owned folder under the user's temporary folder.
    internal static string StagingDirectory => Path.Combine(Path.GetTempPath(), ApplicationVersion.ApplicationName);

    // A new, unique path in StagingDirectory for one run's CSV.
    internal static string CreateStagingPath(string stagingDirectory)
    {
        Directory.CreateDirectory(stagingDirectory);
        return Path.Combine(stagingDirectory, $"{Guid.NewGuid():N}.csv");
    }

    // The file name for the attempt-th candidate: attempt 1 has no suffix, later attempts
    // append _2, _3, ...
    internal static string CandidateName(string? baseName, DateTime timestamp, int attempt)
    {
        string stamp = timestamp.ToString(TimestampFormat, CultureInfo.InvariantCulture);
        string prefix = string.IsNullOrEmpty(baseName) ? stamp : $"{baseName}_{stamp}";
        return attempt == 1 ? $"{prefix}.csv" : $"{prefix}_{attempt.ToString(CultureInfo.InvariantCulture)}.csv";
    }

    // Copies the finished file beside its destination, then claims the first free name with
    // a rename that fails rather than replaces, so no existing file is ever overwritten and
    // a run that loses a race takes the next name (OUTPUT-KEPT). Returns the final path.
    internal static string Place(string stagedFile, string outputDirectory, string? baseName, DateTime timestamp)
    {
        Directory.CreateDirectory(outputDirectory);
        string temporary = Path.Combine(outputDirectory, $".{Guid.NewGuid():N}{TemporarySuffix}");
        try
        {
            File.Copy(stagedFile, temporary);
            for (int attempt = 1; ; attempt++)
            {
                string candidate = Path.Combine(outputDirectory, CandidateName(baseName, timestamp, attempt));
                if (File.Exists(candidate))
                    continue;

                try
                {
                    File.Move(temporary, candidate, overwrite: false);
                    return candidate;
                }
                catch (IOException) when (File.Exists(candidate))
                {
                    // Another run claimed this name between the check and the rename.
                }
            }
        }
        finally
        {
            DeleteQuietly(temporary);
        }
    }

    // Deletes temporary files older than LeftoverAge that earlier runs left behind: any file
    // in this application's staging folder, and files in OutputFolder with TemporarySuffix.
    // Best effort; a file that cannot be deleted waits for a later run.
    internal static void DeleteLeftovers(string stagingDirectory, string outputDirectory, DateTime utcNow)
    {
        DateTime cutoff = utcNow - LeftoverAge;
        DeleteOlderThan(stagingDirectory, "*", cutoff);
        DeleteOlderThan(outputDirectory, "*" + TemporarySuffix, cutoff);
    }

    internal static void DeleteQuietly(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Logger.Warn(ex, $"Failed to delete temporary file: {path}");
        }
    }

    private static void DeleteOlderThan(string directory, string pattern, DateTime cutoffUtc)
    {
        List<string> stale;
        try
        {
            if (!Directory.Exists(directory))
                return;

            stale = [.. Directory.GetFiles(directory, pattern).Where(path => File.GetLastWriteTimeUtc(path) < cutoffUtc)];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Logger.Warn(ex, $"Failed to list temporary files: {directory}");
            return;
        }

        foreach (string path in stale)
            DeleteQuietly(path);
    }
}
