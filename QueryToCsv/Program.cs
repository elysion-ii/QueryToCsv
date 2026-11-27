using System.Diagnostics;
using System.Text;
using NLog;
using QueryToCsv;

// Native SNI.dll is not published (see docs/rules/dotnet-client.md, NATIVEDEP), so SqlClient
// must be switched to its managed networking stack before it opens its first connection.
const string ManagedNetworkingSwitch = "Switch.Microsoft.Data.SqlClient.UseManagedNetworkingOnWindows";
AppContext.SetSwitch(ManagedNetworkingSwitch, true);

// Shift-JIS lives in the CodePages provider, and every mode that reads configuration
// checks the configured encoding name against it, --open included.
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

(CliInvocation? invocation, string? parseError) = CliInvocation.Parse(args);
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

Logger logger = LogSetup.Configure();
logger.Info($"Application started (v{ApplicationVersion.ProductVersion})");

try
{
    int exitCode = await QueryRun.RunAsync(invocation.RunArgs, logger);

    if (exitCode == 0)
        logger.Info("Application finished (exit code: 0)");
    else
        logger.Error($"Application finished (exit code: {exitCode})");

    return exitCode;
}
catch (Exception ex)
{
    logger.Error(ex, "Unhandled exception");
    ConsoleMessages.Write("unexpected application failure.");
    logger.Error("Application finished (exit code: 1)");
    return 1;
}
finally
{
    LogManager.Shutdown();
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
    string baseDir = AppContext.BaseDirectory;
    string normalizedTarget = target.ToLowerInvariant();

    string path;
    bool isFile;

    switch (normalizedTarget)
    {
        case "queries":
        case "output":
            {
                (AppSettings settings, IReadOnlyList<string> notices) = AppSettings.Load();
                foreach (string notice in notices)
                    ConsoleMessages.Write(notice);

                path = normalizedTarget is "queries" ? settings.QueryFolder : settings.OutputFolder;
                isFile = false;
                break;
            }
        case "config":
            path = Path.Combine(baseDir, AppSettings.FileName);
            isFile = true;
            break;
        case "log":
            path = Path.Combine(baseDir, LogSetup.FolderName);
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
                ConsoleMessages.Write($"file not found: {path}");
                return 1;
            }
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        else
        {
            if (!Directory.Exists(path))
            {
                string message = normalizedTarget is "output"
                    ? "output folder does not exist yet. Run a query first to create it."
                    : $"folder not found: {path}";
                ConsoleMessages.Write(message);
                return 1;
            }
            Process.Start("explorer.exe", path);
        }
    }
    catch (Exception)
    {
        ConsoleMessages.Write($"failed to open target: {path}");
        return 1;
    }

    return 0;
}
