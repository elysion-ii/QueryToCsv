using System.ComponentModel;
using System.Globalization;
using System.Text;
using Microsoft.Data.SqlClient;
using NLog;

namespace QueryToCsv;

// The interactive and one-liner runs: configuration, connection and query selection, and
// execution. Every failure is reported on standard error and recorded in the log here, so
// the log names what ended the run (docs/specs/QueryToCsv.md, QTC-13 and QTC-24).
internal static class QueryRun
{
    private const int RuntimeErrorExitCode = 1;
    private const int UsageErrorExitCode = 2;

    private const string InlineLabel = "[Inline]";
    private const string DirectInputLabel = "[Direct Input]";

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
        OutputFile.DeleteLeftovers(OutputFile.StagingDirectory, settings.OutputFolder, DateTime.UtcNow);
        logger.Info("Settings loaded");

        string? settingsError = settings.ValidationError();
        if (settingsError is not null)
            return RuntimeError(logger, settingsError);

        try
        {
            return runArgs is not null
                ? await RunOneLinerAsync(settings, runArgs, commandLineEncoding!, logger, cancellationToken)
                : await RunInteractiveAsync(settings, logger, cancellationToken);
        }
        catch (RunFailureException ex)
        {
            return RuntimeError(logger, ex.Message, ex.InnerException);
        }
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
        string label;

        if (runArgs.InlineQuery is not null)
        {
            sql = runArgs.InlineQuery;
            baseName = null;
            label = InlineLabel;
        }
        else
        {
            string? filePath = ResolveSqlFile(runArgs.SqlFile!, settings.QueryFolder);
            if (filePath is null)
                return UsageError(logger, $"SQL file \"{runArgs.SqlFile}\" is outside QueryFolder; give an absolute path instead.");

            if (!File.Exists(filePath))
                return RuntimeError(logger, $"SQL file not found: {filePath}");

            sql = await SqlText.ReadFileAsync(filePath, settings.SqlFileEncoding, cancellationToken);
            baseName = Path.GetFileNameWithoutExtension(filePath);
            label = Path.GetFileName(filePath);
        }

        logger.Info($"Query selected: {label}");
        logger.Info($"Header: {(runArgs.IncludeHeader ? "yes" : "no")}, Encoding: {csvEncoding.WebName}");

        return await ExecuteAsync(
            settings, connection, sql, baseName, label, runArgs.IncludeHeader, csvEncoding, logger, cancellationToken);
    }

    internal static async Task<int> RunInteractiveAsync(
        AppSettings settings,
        Logger logger,
        CancellationToken cancellationToken = default)
    {
        int? connectionIndex = ConsoleUi.SelectConnection(settings.Connections);
        if (connectionIndex is null)
            return InputEnded(logger, cancellationToken);

        ConnectionEntry connection = settings.Connections[connectionIndex.Value];
        logger.Info($"Connection selected: {connection.Name}");
        await Console.Error.WriteLineAsync();

        string[] sqlFiles = ListSqlFiles(settings.QueryFolder);
        string[] fileNames = [.. sqlFiles.Select(path => Path.GetFileName(path))];

        int? selectedIndex = ConsoleUi.SelectQuery(fileNames);
        if (selectedIndex is null)
            return InputEnded(logger, cancellationToken);

        string sql;
        string? baseName;
        string label;

        if (selectedIndex == ConsoleUi.DirectEntry)
        {
            await Console.Error.WriteLineAsync();
            string? typed = ConsoleUi.InputQuery();
            cancellationToken.ThrowIfCancellationRequested();
            if (typed is null)
                return RuntimeError(logger, "no query entered.");

            sql = typed;
            baseName = null;
            label = DirectInputLabel;
        }
        else
        {
            string sqlFilePath = sqlFiles[selectedIndex.Value];
            sql = await SqlText.ReadFileAsync(sqlFilePath, settings.SqlFileEncoding, cancellationToken);
            baseName = Path.GetFileNameWithoutExtension(sqlFilePath);
            label = fileNames[selectedIndex.Value];
        }
        logger.Info($"Query selected: {label}");
        await Console.Error.WriteLineAsync();

        bool? includeHeader = ConsoleUi.AskIncludeHeader();
        if (includeHeader is null)
            return InputEnded(logger, cancellationToken);
        await Console.Error.WriteLineAsync();

        Encoding? csvEncoding = ConsoleUi.SelectEncoding();
        if (csvEncoding is null)
            return InputEnded(logger, cancellationToken);

        logger.Info($"Header: {(includeHeader.Value ? "yes" : "no")}, Encoding: {csvEncoding.WebName}");
        await Console.Error.WriteLineAsync();

        return await ExecuteAsync(
            settings, connection, sql, baseName, label, includeHeader.Value, csvEncoding, logger, cancellationToken);
    }

    // The path of a -f value: an absolute path as given, a relative one inside QueryFolder,
    // or null when a relative value resolves outside it (docs/specs/QueryToCsv.md, QTC-9).
    internal static string? ResolveSqlFile(string value, string queryFolder)
    {
        if (Path.IsPathFullyQualified(value))
            return value;

        string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(queryFolder)) + Path.DirectorySeparatorChar;
        string candidate;
        try
        {
            candidate = Path.GetFullPath(value, root);
        }
        catch (ArgumentException)
        {
            return null;
        }

        return candidate.StartsWith(root, StringComparison.OrdinalIgnoreCase) ? candidate : null;
    }

    // The connection string to open: as configured, plus the user name and password from
    // Windows Credential Manager when the entry names a credential
    // (docs/specs/QueryToCsv.md, QTC-8).
    internal static string ResolveConnectionString(ConnectionEntry connection, Func<string, CredentialStore.Secret?> readCredential)
    {
        if (connection.Credential is null)
            return connection.ConnectionString;

        CredentialStore.Secret? secret;
        try
        {
            secret = readCredential(connection.Credential);
        }
        catch (Win32Exception ex)
        {
            throw new RunFailureException(CredentialNotFound(connection.Credential), ex);
        }

        if (secret is null)
            throw new RunFailureException(CredentialNotFound(connection.Credential));

        SqlConnectionStringBuilder builder = new(connection.ConnectionString)
        {
            UserID = secret.UserName,
            Password = secret.Password,
        };
        return builder.ConnectionString;
    }

    private static async Task<int> ExecuteAsync(
        AppSettings settings,
        ConnectionEntry connection,
        string sql,
        string? baseName,
        string label,
        bool includeHeader,
        Encoding csvEncoding,
        Logger logger,
        CancellationToken cancellationToken)
    {
        string? lengthError = SqlText.LengthError(sql);
        if (lengthError is not null)
            return RuntimeError(logger, lengthError);

        if (!QueryExecutor.IsSelectOnly(sql))
        {
            logger.Error($"Rejected: not a single SELECT statement in {label}");
            ConsoleMessages.Write("only SELECT statements are allowed.");
            return RuntimeErrorExitCode;
        }

        string connectionString = ResolveConnectionString(connection, CredentialStore.Read);
        DateTime timestamp = DateTime.Now;

        string stagingPath;
        try
        {
            stagingPath = OutputFile.CreateStagingPath(OutputFile.StagingDirectory);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new RunFailureException($"cannot write the temporary file in \"{OutputFile.StagingDirectory}\".", ex);
        }

        try
        {
            logger.Info($"Executing: {label}");
            int rowCount;
            try
            {
                rowCount = await QueryExecutor.ExportAsync(
                    connectionString, sql, settings.QueryTimeout, includeHeader, csvEncoding, settings.CsvSettings,
                    stagingPath, cancellationToken);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                throw new RunFailureException($"cannot write the temporary file in \"{OutputFile.StagingDirectory}\".", ex);
            }

            string outputPath;
            try
            {
                outputPath = OutputFile.Place(stagingPath, settings.OutputFolder, baseName, timestamp);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                throw new RunFailureException($"cannot write the output file in \"{settings.OutputFolder}\".", ex);
            }

            logger.Info($"CSV written: {outputPath} ({rowCount} rows)");

            // The written file and its row count are what the run produces; the hints
            // below are not (docs/rules/cli.md, Standard Streams).
            await Console.Error.WriteLineAsync();
            Console.WriteLine($"Done: {outputPath}");
            Console.WriteLine($"Rows: {rowCount.ToString("N0", CultureInfo.InvariantCulture)}");
            await Console.Error.WriteLineAsync();
            await Console.Error.WriteLineAsync($"{ApplicationVersion.ApplicationName} --open output");
            await Console.Error.WriteLineAsync($"{ApplicationVersion.ApplicationName} --open \"{outputPath}\"");
            return 0;
        }
        finally
        {
            OutputFile.DeleteQuietly(stagingPath);
        }
    }

    private static string[] ListSqlFiles(string queryFolder)
    {
        try
        {
            return Directory.Exists(queryFolder)
                ? [.. Directory.GetFiles(queryFolder, "*.sql").OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)]
                : [];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new RunFailureException($"QueryFolder \"{queryFolder}\" could not be read.", ex);
        }
    }

    private static string CredentialNotFound(string name)
        => $"credential \"{name}\" was not found in Windows Credential Manager.";

    // End of input at a prompt ends the run with no message of its own, unless Ctrl+C is
    // what ended the input (docs/specs/QueryToCsv.md, QTC-12 and QTC-25).
    private static int InputEnded(Logger logger, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        logger.Info("Input ended at a prompt");
        return RuntimeErrorExitCode;
    }

    private static int RuntimeError(Logger logger, string message, Exception? cause = null)
    {
        ConsoleMessages.Write(message);
        if (cause is null)
            logger.Error(message);
        else
            logger.Error(cause, message);
        return RuntimeErrorExitCode;
    }

    private static int UsageError(Logger logger, string message)
    {
        ConsoleMessages.WriteUsageError(message);
        logger.Error(message);
        return UsageErrorExitCode;
    }
}
