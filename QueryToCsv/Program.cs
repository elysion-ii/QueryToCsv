using System.Diagnostics;
using System.Text;
using NLog;
using NLog.Config;
using NLog.Targets;
using QueryToCsv;

var (invocation, parseError) = CliInvocation.Parse(args);
if (parseError is not null)
{
    ConsoleMessages.WriteUsageError(parseError);
    return 2;
}

switch (invocation!.Mode)
{
    case CliMode.Help:
        return PrintHelp();
    case CliMode.Version:
        Console.WriteLine(ApplicationVersion.DisplayText);
        return 0;
    case CliMode.Open:
        return HandleOpen(invocation.OpenTarget!);
}

var runArgs = invocation.RunArgs;

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

Encoding? commandLineEncoding = null;
if (runArgs is not null)
{
    commandLineEncoding = ConsoleUi.ResolveEncoding(runArgs.EncodingName);
    if (commandLineEncoding is null)
    {
        ConsoleMessages.WriteUsageError(
            $"unknown encoding \"{runArgs.EncodingName}\". " +
            "Use: utf-8, utf-8-bom, utf-16, shift-jis");
        return 2;
    }
}

var logger = ConfigureNLog(30);
logger.Info($"Application started (v{ApplicationVersion.ProductVersion})");

try
{
    if (runArgs is null)
    {
        Console.WriteLine("=== QueryToCsv ===");
        Console.WriteLine();
    }

    var settings = AppSettings.Load();
    if (settings is null)
    {
        logger.Error("Application finished (exit code: 1)");
        return 1;
    }

    logger = ConfigureNLog(settings.LogRetentionDays);
    logger.Info("Settings loaded");

    if (!settings.Validate())
    {
        logger.Error("Application finished (exit code: 1)");
        return 1;
    }

    if (runArgs is not null)
    {
        var result = RunOneLiner(settings, runArgs, commandLineEncoding!, logger);
        if (result == 0)
            logger.Info("Application finished (exit code: 0)");
        else
            logger.Error($"Application finished (exit code: {result})");
        return result;
    }

    var connectionIndex = ConsoleUi.SelectConnection(settings.Connections);
    var connectionString = settings.Connections[connectionIndex].ConnectionString;
    logger.Info($"Connection selected: {settings.Connections[connectionIndex].Name}");
    Console.WriteLine();

    var sqlFiles = Directory.GetFiles(settings.QueryFolder, "*.sql");
    Array.Sort(sqlFiles, (a, b) => string.Compare(Path.GetFileName(a), Path.GetFileName(b), StringComparison.OrdinalIgnoreCase));

    var fileNames = sqlFiles.Select(Path.GetFileName).ToArray()!;
    var selectedIndex = ConsoleUi.SelectQuery(fileNames!);

    string sql;
    string? baseName;

    if (selectedIndex == -1)
    {
        Console.WriteLine();
        sql = ConsoleUi.InputQuery();
        baseName = null;
        logger.Info("Query selected: [Direct Input]");
    }
    else
    {
        var sqlFilePath = sqlFiles[selectedIndex];
        var sqlEncoding = Encoding.GetEncoding(settings.SqlFileEncoding);
        sql = File.ReadAllText(sqlFilePath, sqlEncoding);
        baseName = Path.GetFileNameWithoutExtension(sqlFilePath);
        logger.Info($"Query selected: {fileNames[selectedIndex]}");
    }
    Console.WriteLine();

    var includeHeader = ConsoleUi.AskIncludeHeader();
    Console.WriteLine();

    var csvEncoding = ConsoleUi.SelectEncoding();
    logger.Info($"Header: {(includeHeader ? "yes" : "no")}, Encoding: {csvEncoding.EncodingName}");
    Console.WriteLine();

    var exitCode = QueryExecutor.Execute(settings, connectionString, sql, baseName, includeHeader, csvEncoding);

    if (exitCode == 0)
        logger.Info("Application finished (exit code: 0)");
    else
        logger.Error($"Application finished (exit code: {exitCode})");

    return exitCode;
}
catch (Exception ex)
{
    logger.Error(ex, "Unhandled exception");
    ConsoleMessages.WriteError("unexpected application failure.");
    logger.Error("Application finished (exit code: 1)");
    return 1;
}
finally
{
    LogManager.Shutdown();
}

static int RunOneLiner(
    AppSettings settings,
    CliRunArgs runArgs,
    Encoding csvEncoding,
    Logger logger)
{
    string connectionString;
    string connectionName;

    if (runArgs.ConnectionName is not null)
    {
        var entry = settings.Connections.FirstOrDefault(c =>
            c.Name.Equals(runArgs.ConnectionName, StringComparison.OrdinalIgnoreCase));
        if (entry is null)
        {
            ConsoleMessages.WriteUsageError(
                $"connection \"{runArgs.ConnectionName}\" not found.");
            return 2;
        }
        connectionString = entry.ConnectionString;
        connectionName = entry.Name;
    }
    else if (settings.Connections.Count == 1)
    {
        connectionString = settings.Connections[0].ConnectionString;
        connectionName = settings.Connections[0].Name;
    }
    else
    {
        ConsoleMessages.WriteUsageError(
            "option '--connection' is required when multiple connections are configured.");
        return 2;
    }

    logger.Info($"Connection selected: {connectionName}");

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
        var filePath = runArgs.SqlFile!;

        if (!Path.IsPathRooted(filePath))
            filePath = Path.Combine(settings.QueryFolder, filePath);

        if (!File.Exists(filePath))
        {
            ConsoleMessages.WriteError($"SQL file not found: {filePath}");
            return 1;
        }

        var sqlEncoding = Encoding.GetEncoding(settings.SqlFileEncoding);
        sql = File.ReadAllText(filePath, sqlEncoding);
        baseName = Path.GetFileNameWithoutExtension(filePath);
        logger.Info($"Query selected: {Path.GetFileName(filePath)}");
    }

    logger.Info($"Header: {(runArgs.IncludeHeader ? "yes" : "no")}, Encoding: {csvEncoding.EncodingName}");

    return QueryExecutor.Execute(settings, connectionString, sql, baseName, runArgs.IncludeHeader, csvEncoding);
}

static int PrintHelp()
{
    Console.WriteLine(ApplicationVersion.DisplayText);
    Console.WriteLine();
    Console.WriteLine("Export a SQL Server result set to CSV.");
    Console.WriteLine();
    Console.WriteLine("Usage:");
    Console.WriteLine("  QueryToCsv");
    Console.WriteLine("  QueryToCsv --query <sql> [options]");
    Console.WriteLine("  QueryToCsv --file <file> [options]");
    Console.WriteLine("  QueryToCsv --open <target>");
    Console.WriteLine("  QueryToCsv [--help | --version]");
    Console.WriteLine();
    Console.WriteLine("Options:");
    Console.WriteLine("  -c, --connection <name>   Connection name from appsettings.json");
    Console.WriteLine("                            (required if multiple connections exist)");
    Console.WriteLine("      --query <sql>         Inline SQL query string");
    Console.WriteLine("  -f, --file <name|path>    SQL file in QueryFolder, or absolute path");
    Console.WriteLine("  -e, --encoding <name>     CSV encoding: utf-8 (default), utf-8-bom,");
    Console.WriteLine("                            utf-16, shift-jis");
    Console.WriteLine("      --header              Include header row (default)");
    Console.WriteLine("      --no-header           Exclude header row");
    Console.WriteLine("  -h, --help                Show this help");
    Console.WriteLine("  -V, --version             Show version");
    Console.WriteLine();
    Console.WriteLine("Open targets:");
    Console.WriteLine("  queries       Open the queries folder in Explorer");
    Console.WriteLine("  output        Open the output folder in Explorer");
    Console.WriteLine("  config        Open appsettings.json in default editor");
    Console.WriteLine("  log           Open the logs folder in Explorer");
    Console.WriteLine("  <file path>   Open a specific file with its default app");
    Console.WriteLine();
    Console.WriteLine("Examples:");
    Console.WriteLine("  QueryToCsv --query \"SELECT TOP 10 * FROM Sales\"");
    Console.WriteLine("  QueryToCsv --file monthly-sales.sql --encoding utf-8-bom");
    Console.WriteLine();
    Console.WriteLine("Cancelling:");
    Console.WriteLine("  Ctrl+C        Exit at any time");
    Console.WriteLine("  Ctrl+Z+Enter  Exit at any input prompt");
    return 0;
}

static int HandleOpen(string target)
{
    var baseDir = AppContext.BaseDirectory;
    var normalizedTarget = target.ToLowerInvariant();

    string path;
    bool isFile;

    switch (normalizedTarget)
    {
        case "queries":
        case "output":
            {
                var settings = AppSettings.Load();
                if (settings is null)
                    return 1;

                var isQueries = normalizedTarget is "queries";
                path = isQueries ? settings.QueryFolder : settings.OutputFolder;

                if (string.IsNullOrWhiteSpace(path))
                {
                    var key = isQueries ? "QueryFolder" : "OutputFolder";
                    ConsoleMessages.WriteError(
                        $"{key} is not configured in appsettings.json.");
                    return 1;
                }

                isFile = false;
                break;
            }
        case "config":
            path = Path.Combine(baseDir, "appsettings.json");
            isFile = true;
            break;
        case "log":
            path = Path.Combine(baseDir, "logs");
            isFile = false;
            break;
        default:
            path = Path.IsPathRooted(target)
                ? target
                : Path.Combine(baseDir, target);
            isFile = true;
            break;
    }

    try
    {
        if (isFile)
        {
            if (!File.Exists(path))
            {
                ConsoleMessages.WriteError($"file not found: {path}");
                return 1;
            }
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        else
        {
            if (!Directory.Exists(path))
            {
                var msg = normalizedTarget is "output"
                    ? "output folder does not exist yet. Run a query first to create it."
                    : $"folder not found: {path}";
                ConsoleMessages.WriteError(msg);
                return 1;
            }
            Process.Start("explorer.exe", path);
        }
    }
    catch (Exception)
    {
        ConsoleMessages.WriteError($"failed to open target: {path}");
        return 1;
    }

    return 0;
}

static Logger ConfigureNLog(int maxArchiveDays)
{
    var logDir = Path.Combine(AppContext.BaseDirectory, "logs");

    var config = new LoggingConfiguration();

    var fileTarget = new FileTarget("file")
    {
        FileName = Path.Combine(logDir, "QueryToCsv.log"),
        ArchiveEvery = FileArchivePeriod.Day,
        ArchiveFileName = Path.Combine(logDir, "QueryToCsv.{#}.log"),
        ArchiveNumbering = ArchiveNumberingMode.Date,
        ArchiveDateFormat = "yyyyMMdd",
        MaxArchiveDays = maxArchiveDays,
        Layout = "${longdate} [${level:uppercase=true:padding=-5}] ${message}${onexception:inner= ${exception:format=tostring}}",
    };

    config.AddTarget(fileTarget);
    config.AddRule(LogLevel.Info, LogLevel.Fatal, fileTarget);

    LogManager.Configuration = config;
    return LogManager.GetCurrentClassLogger();
}
