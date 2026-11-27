using System.Globalization;
using NLog;
using NLog.Config;
using NLog.Targets;

namespace QueryToCsv;

internal static class LogSetup
{
    internal const string FolderName = "logs";

    private const string DateStampFormat = "yyyyMMdd";

    // NLog resolves ${date} for every entry, so a new file begins each local day.
    private const string FileNamePattern =
        ApplicationVersion.ApplicationName + "_${date:format=" + DateStampFormat + "}.log";

    // ISO 8601 with the machine's UTC offset, so entries from machines in different time
    // zones compare correctly (docs/rules/values.md, Time and Dates). NLog layouts need
    // the colons inside a format escaped.
    private const string EntryLayout =
        @"${date:format=yyyy-MM-ddTHH\:mm\:ss.fffzzz} [${level:uppercase=true:padding=-5}] ${message}${onexception:inner= ${exception:format=tostring}}";

    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    internal static string PreferredDirectory => Path.Combine(AppContext.BaseDirectory, FolderName);

    // Null when the platform reports no local application data folder: a path built from
    // an empty base would resolve against the working directory
    // (docs/rules/standard.md, Application-Owned Paths).
    internal static string? FallbackDirectory
    {
        get
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return localAppData.Length == 0
                ? null
                : Path.Combine(localAppData, ApplicationVersion.ApplicationName, FolderName);
        }
    }

    // The folder log files are written to, or null when no log file can be written
    // (docs/specs/QueryToCsv.md, QTC-24).
    internal static string? WritableDirectory => ResolveDirectory(PreferredDirectory, FallbackDirectory);

    internal static Logger Configure()
    {
        LoggingConfiguration config = new();

        string? directory = WritableDirectory;
        if (directory is not null)
        {
            FileTarget fileTarget = new("file")
            {
                FileName = Path.Combine(directory, FileNamePattern),
                Layout = EntryLayout,
            };
            config.AddTarget(fileTarget);
            config.AddRule(LogLevel.Info, LogLevel.Fatal, fileTarget);
        }

        LogManager.Configuration = config;
        return LogManager.GetCurrentClassLogger();
    }

    internal static void DeleteExpiredLogs(int retentionDays)
    {
        string? directory = WritableDirectory;
        if (directory is not null)
            DeleteExpiredLogs(directory, retentionDays, DateTime.Now);
    }

    // NLog's own MaxArchiveDays deletes nothing when the date lives in FileName instead
    // of an archive pattern (measured: QueryToCsv_20250101.log survived a run with
    // MaxArchiveDays=30), so the retention limit is applied here. Cleanup never ends the
    // run (docs/rules/client.md, Logs on the Host Machine).
    internal static void DeleteExpiredLogs(string directory, int retentionDays, DateTime now)
    {
        DateTime oldestKept = now.Date.AddDays(1 - retentionDays);

        List<string> expired;
        try
        {
            if (!Directory.Exists(directory))
                return;

            // The listing is materialized before the first delete
            // (docs/rules/standard.md, Snapshot Before Mutate).
            expired = [.. Directory.GetFiles(directory, $"{ApplicationVersion.ApplicationName}_*.log")
                .Where(path => IsExpired(path, oldestKept))];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Logger.Warn(ex, $"Failed to list log files: {directory}");
            return;
        }

        foreach (string path in expired)
        {
            try
            {
                File.Delete(path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Logger.Warn(ex, $"Failed to delete an expired log file: {path}");
            }
        }
    }

    // The directory beside the executable is not writable in every installation, and a
    // run that cannot log still has to run (docs/specs/QueryToCsv.md, QTC-24).
    internal static string? ResolveDirectory(string preferred, string? fallback)
    {
        if (IsWritable(preferred))
            return preferred;

        return fallback is not null && IsWritable(fallback) ? fallback : null;
    }

    private static bool IsExpired(string path, DateTime oldestKept)
    {
        string stamp = Path.GetFileNameWithoutExtension(path)[(ApplicationVersion.ApplicationName.Length + 1)..];

        return DateTime.TryParseExact(
                   stamp, DateStampFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime date)
               && date < oldestKept;
    }

    private static bool IsWritable(string directory)
    {
        try
        {
            Directory.CreateDirectory(directory);

            string probe = Path.Combine(directory, $"{Guid.NewGuid():N}.probe");
            using (File.Create(probe))
            {
            }

            File.Delete(probe);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            return false;
        }
    }
}
