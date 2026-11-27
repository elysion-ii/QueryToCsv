using System.Text;
using NLog;

namespace QueryToCsv;

// The interactive and one-liner runs: configuration, connection and query selection, and
// execution. Every failure is reported on standard error and recorded in the log here, so
// the log names what ended the run (docs/specs/QueryToCsv.md, QueryToCsv-011).
internal static class QueryRun
{
    private const int RuntimeErrorExitCode = 1;
    private const int UsageErrorExitCode = 2;

    internal static async Task<int> RunAsync(CliRunArgs? runArgs, Logger logger, CancellationToken cancellationToken = default)
    {
        Encoding? commandLineEncoding = null;
        if (runArgs is not null)
        {
            commandLineEncoding = ConsoleUi.ResolveEncoding(runArgs.EncodingName);
            if (commandLineEncoding is null)
            {
                return UsageError(
                    logger,
                    $"unknown encoding \"{runArgs.EncodingName}\". Use: utf-8, utf-8-bom, utf-16, shift-jis");
            }
        }
        else
        {
            await Console.Error.WriteLineAsync("=== QueryToCsv ===");
            await Console.Error.WriteLineAsync();
        }

        (AppSettings settings, IReadOnlyList<string> notices) = AppSettings.Load();
        foreach (string notice in notices)
        {
            ConsoleMessages.Write(notice);
            logger.Warn(notice);
        }

        LogSetup.DeleteExpiredLogs(settings.LogRetentionDays);
        logger.Info("Settings loaded");

        string? settingsError = settings.ValidationError();
        if (settingsError is not null)
            return RuntimeError(logger, settingsError);

        return runArgs is not null
            ? await RunOneLinerAsync(settings, runArgs, commandLineEncoding!, logger, cancellationToken)
            : await RunInteractiveAsync(settings, logger, cancellationToken);
    }

    internal static async Task<int> RunOneLinerAsync(
        AppSettings settings,
        CliRunArgs runArgs,
        Encoding csvEncoding,
        Logger logger,
        CancellationToken cancellationToken = default)
    {
        ConnectionEntry connection;
        if (runArgs.ConnectionName is not null)
        {
            ConnectionEntry? named = settings.Connections.FirstOrDefault(c =>
                c.Name.Equals(runArgs.ConnectionName, StringComparison.OrdinalIgnoreCase));
            if (named is null)
                return UsageError(logger, $"connection \"{runArgs.ConnectionName}\" not found.");
            connection = named;
        }
        else if (settings.Connections.Count == 1)
        {
            connection = settings.Connections[0];
        }
        else
        {
            return UsageError(logger, "option '--connection' is required when multiple connections are configured.");
        }

        logger.Info($"Connection selected: {connection.Name}");

        string sql;
        string? baseName;

        if (runArgs.InlineQuery is not null)
        {
            sql = runArgs.InlineQuery;
            baseName = null;
            logger.Info("Query selected: [Inline]");
        }
        else
        {
            string filePath = Path.IsPathRooted(runArgs.SqlFile!)
                ? runArgs.SqlFile!
                : Path.Combine(settings.QueryFolder, runArgs.SqlFile!);

            if (!File.Exists(filePath))
                return RuntimeError(logger, $"SQL file not found: {filePath}");

            sql = await ReadSqlFileAsync(filePath, settings, cancellationToken);
            baseName = Path.GetFileNameWithoutExtension(filePath);
            logger.Info($"Query selected: {Path.GetFileName(filePath)}");
        }

        logger.Info($"Header: {(runArgs.IncludeHeader ? "yes" : "no")}, Encoding: {csvEncoding.EncodingName}");

        return await QueryExecutor.ExecuteAsync(
            settings, connection.ConnectionString, sql, baseName, runArgs.IncludeHeader, csvEncoding, cancellationToken);
    }

    internal static async Task<int> RunInteractiveAsync(
        AppSettings settings,
        Logger logger,
        CancellationToken cancellationToken = default)
    {
        int? connectionIndex = ConsoleUi.SelectConnection(settings.Connections);
        if (connectionIndex is null)
            return InputEnded(logger);

        ConnectionEntry connection = settings.Connections[connectionIndex.Value];
        logger.Info($"Connection selected: {connection.Name}");
        await Console.Error.WriteLineAsync();

        string[] sqlFiles = Directory.Exists(settings.QueryFolder)
            ? [.. Directory.GetFiles(settings.QueryFolder, "*.sql").OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)]
            : [];
        string[] fileNames = [.. sqlFiles.Select(path => Path.GetFileName(path))];

        int? selectedIndex = ConsoleUi.SelectQuery(fileNames);
        if (selectedIndex is null)
            return InputEnded(logger);

        string sql;
        string? baseName;

        if (selectedIndex == ConsoleUi.DirectEntry)
        {
            await Console.Error.WriteLineAsync();
            string? typed = ConsoleUi.InputQuery();
            if (typed is null)
                return RuntimeError(logger, "no query entered.");

            sql = typed;
            baseName = null;
            logger.Info("Query selected: [Direct Input]");
        }
        else
        {
            string sqlFilePath = sqlFiles[selectedIndex.Value];
            sql = await ReadSqlFileAsync(sqlFilePath, settings, cancellationToken);
            baseName = Path.GetFileNameWithoutExtension(sqlFilePath);
            logger.Info($"Query selected: {fileNames[selectedIndex.Value]}");
        }
        await Console.Error.WriteLineAsync();

        bool? includeHeader = ConsoleUi.AskIncludeHeader();
        if (includeHeader is null)
            return InputEnded(logger);
        await Console.Error.WriteLineAsync();

        Encoding? csvEncoding = ConsoleUi.SelectEncoding();
        if (csvEncoding is null)
            return InputEnded(logger);

        logger.Info($"Header: {(includeHeader.Value ? "yes" : "no")}, Encoding: {csvEncoding.EncodingName}");
        await Console.Error.WriteLineAsync();

        return await QueryExecutor.ExecuteAsync(
            settings, connection.ConnectionString, sql, baseName, includeHeader.Value, csvEncoding, cancellationToken);
    }

    private static Task<string> ReadSqlFileAsync(string path, AppSettings settings, CancellationToken cancellationToken)
        => File.ReadAllTextAsync(path, Encoding.GetEncoding(settings.SqlFileEncoding), cancellationToken);

    // End of input at a prompt ends the run with no message of its own
    // (docs/specs/QueryToCsv.md, QueryToCsv-004).
    private static int InputEnded(Logger logger)
    {
        logger.Info("Input ended at a prompt");
        return RuntimeErrorExitCode;
    }

    private static int RuntimeError(Logger logger, string message)
    {
        ConsoleMessages.Write(message);
        logger.Error(message);
        return RuntimeErrorExitCode;
    }

    private static int UsageError(Logger logger, string message)
    {
        ConsoleMessages.WriteUsageError(message);
        logger.Error(message);
        return UsageErrorExitCode;
    }
}
