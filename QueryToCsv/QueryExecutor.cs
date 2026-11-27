using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Data.SqlClient;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using NLog;

namespace QueryToCsv;

internal static class QueryExecutor
{
    private const int QueryTimeoutErrorNumber = -2;

    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    internal static async Task<int> ExecuteAsync(
        AppSettings settings,
        string connectionString,
        string sql,
        string? baseName,
        bool includeHeader,
        Encoding csvEncoding,
        CancellationToken cancellationToken = default)
    {
        string label = baseName ?? "Direct Input";

        if (!IsSelectOnly(sql))
        {
            Logger.Error($"Rejected: not a single SELECT statement in {label}");
            ConsoleMessages.Write("only SELECT statements are allowed.");
            return 1;
        }

        string outputPath = BuildOutputPath(settings, baseName);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        string tempPath = outputPath + ".tmp";
        try
        {
            await using SqlConnection connection = new(connectionString);
            Logger.Info("Connecting to SQL Server...");
            await Console.Error.WriteLineAsync("Connecting...");
            await connection.OpenAsync(cancellationToken);

            await using SqlCommand command = new(sql, connection) { CommandTimeout = settings.QueryTimeout };

            Logger.Info($"Executing: {label}");
            await Console.Error.WriteLineAsync("Executing query...");
            await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);

            await Console.Error.WriteLineAsync("Writing CSV...");
            int rowCount = await WriteCsvAsync(reader, tempPath, csvEncoding, includeHeader, settings.CsvSettings, cancellationToken);

            File.Move(tempPath, outputPath);

            Logger.Info($"CSV written: {outputPath} ({rowCount} rows)");

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
        catch (SqlException ex) when (ex.Number == QueryTimeoutErrorNumber)
        {
            Logger.Error("Query timed out");
            ConsoleMessages.Write("query timed out.");
            return 1;
        }
        catch (SqlException ex)
        {
            Logger.Error(ex, "SQL execution failed");
            ConsoleMessages.Write("query execution failed.");
            return 1;
        }
        finally
        {
            try
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, $"Failed to delete temporary file: {tempPath}");
            }
        }
    }

    // Decided on the parse tree, never on keywords in the text: T-SQL runs one statement
    // after another with no separator, so only the parser sees where the SELECT ends
    // (docs/specs/QueryToCsv.md, QueryToCsv-007).
    internal static bool IsSelectOnly(string sql)
    {
        TSql170Parser parser = new(initialQuotedIdentifiers: true);
        using StringReader reader = new(sql);
        TSqlFragment fragment = parser.Parse(reader, out IList<ParseError> errors);

        if (errors.Count > 0 || fragment is not TSqlScript script)
            return false;

        if (script.Batches.Count != 1 || script.Batches[0].Statements.Count != 1)
            return false;

        if (script.Batches[0].Statements[0] is not SelectStatement select || select.Into is not null)
            return false;

        ExternalRowsetFinder finder = new();
        select.Accept(finder);
        return !finder.Found;
    }

    private static string BuildOutputPath(AppSettings settings, string? baseName)
        => BuildOutputPath(settings.OutputFolder, baseName, DateTime.Now);

    internal static string BuildOutputPath(string outputDir, string? baseName, DateTime timestampSource)
    {
        string timestamp = timestampSource.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);

        string prefix = string.IsNullOrEmpty(baseName) ? timestamp : $"{baseName}_{timestamp}";

        string candidate = Path.Combine(outputDir, $"{prefix}.csv");
        if (!File.Exists(candidate))
            return candidate;

        for (int i = 2; ; i++)
        {
            candidate = Path.Combine(outputDir, $"{prefix}_{i}.csv");
            if (!File.Exists(candidate))
                return candidate;
        }
    }

    private static async Task<int> WriteCsvAsync(
        SqlDataReader reader,
        string outputPath,
        Encoding encoding,
        bool includeHeader,
        CsvSettings csvSettings,
        CancellationToken cancellationToken)
    {
        string newLine = csvSettings.NewLine == "LF" ? "\n" : "\r\n";
        CsvConfiguration csvConfig = new(CultureInfo.InvariantCulture)
        {
            Delimiter = csvSettings.Delimiter,
            NewLine = newLine,
        };

        await using FileStream stream = new(
            outputPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 4096, FileOptions.Asynchronous);
        await using StreamWriter writer = new(stream, encoding);
        await using CsvWriter csv = new(writer, csvConfig);

        int fieldCount = reader.FieldCount;

        if (includeHeader)
        {
            for (int i = 0; i < fieldCount; i++)
                csv.WriteField(reader.GetName(i));
            await csv.NextRecordAsync();
        }

        int rowCount = 0;
        while (await reader.ReadAsync(cancellationToken))
        {
            for (int i = 0; i < fieldCount; i++)
            {
                if (await reader.IsDBNullAsync(i, cancellationToken))
                {
                    csv.WriteField(csvSettings.NullValue);
                }
                else
                {
                    csv.WriteField(await FormatValueAsync(reader, i, csvSettings.DateFormat, cancellationToken));
                }
            }
            await csv.NextRecordAsync();
            rowCount++;
        }

        return rowCount;
    }

    private static async Task<string> FormatValueAsync(
        SqlDataReader reader,
        int ordinal,
        string? dateFormat,
        CancellationToken cancellationToken)
    {
        Type type = reader.GetFieldType(ordinal);

        if (type == typeof(DateTime))
        {
            DateTime value = await reader.GetFieldValueAsync<DateTime>(ordinal, cancellationToken);
            return dateFormat is not null
                ? value.ToString(dateFormat, CultureInfo.InvariantCulture)
                : value.ToString(CultureInfo.InvariantCulture);
        }

        if (type == typeof(DateTimeOffset))
        {
            DateTimeOffset value = await reader.GetFieldValueAsync<DateTimeOffset>(ordinal, cancellationToken);
            return dateFormat is not null
                ? value.ToString(dateFormat, CultureInfo.InvariantCulture)
                : value.ToString(CultureInfo.InvariantCulture);
        }

        object rawValue = await reader.GetFieldValueAsync<object>(ordinal, cancellationToken);

        if (rawValue is IFormattable formattable)
            return formattable.ToString(null, CultureInfo.InvariantCulture);

        return rawValue.ToString() ?? "";
    }

    // Collects whether the SELECT reads a rowset from outside the connected database.
    private sealed class ExternalRowsetFinder : TSqlFragmentVisitor
    {
        internal bool Found { get; private set; }

        public override void Visit(OpenRowsetTableReference node) => Found = true;

        public override void Visit(BulkOpenRowset node) => Found = true;

        public override void Visit(OpenQueryTableReference node) => Found = true;

        public override void Visit(AdHocTableReference node) => Found = true;
    }
}
