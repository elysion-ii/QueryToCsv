using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using QueryToCsv.TestSupport;

namespace QueryToCsv.EndToEndTests;

// The built executable placed in a test-owned folder with its own appsettings.json,
// queries, output, and temporary folder, and launched as its own process.
internal sealed partial class AppInstallation : IDisposable
{
    private const string ExecutableName = "QueryToCsv.exe";

    // Condition-based: a run ends long before this unless it hangs.
    private static readonly TimeSpan RunTimeout = TimeSpan.FromSeconds(120);

    private readonly TempDirectory _root = new();

    internal AppInstallation()
    {
        Directory.CreateDirectory(Folder);
        Directory.CreateDirectory(QueryFolder);
        Directory.CreateDirectory(TempFolder);

        // A hard link starts the same file without copying it.
        string target = Path.Combine(Folder, ExecutableName);
        if (!CreateHardLink(target, BuiltExecutable, 0))
            File.Copy(BuiltExecutable, target);
    }

    // The executable build/Build.ps1 publishes; end-to-end tests run after it.
    internal static string BuiltExecutable
    {
        get
        {
            string path = Path.Combine(Repository.Root, "build", "QueryToCsv", ExecutableName);
            return File.Exists(path)
                ? path
                : throw new InvalidOperationException($"{path} does not exist; run build/Build.ps1 before the end-to-end tests.");
        }
    }

    internal string Folder => Path.Combine(_root.Path, "app");

    internal string QueryFolder => Path.Combine(Folder, "queries");

    internal string OutputFolder => Path.Combine(Folder, "output");

    internal string LogFolder => Path.Combine(Folder, "logs");

    internal string TempFolder => Path.Combine(_root.Path, "tmp");

    internal string StagingFolder => Path.Combine(TempFolder, "QueryToCsv");

    internal void WriteSettings(object settings)
        => File.WriteAllText(Path.Combine(Folder, "appsettings.json"), JsonSerializer.Serialize(settings));

    // Settings with one connection to the test server; credential names a stored credential.
    internal void WriteSettings(SqlServerFixture server, string credential, object? csvSettings = null, string name = "Test")
        => WriteSettings(new Dictionary<string, object?>
        {
            ["Connections"] = new[]
            {
                new { Name = name, ConnectionString = server.ConnectionStringWithoutCredentials, Credential = credential },
            },
            ["CsvSettings"] = csvSettings,
        });

    internal string[] OutputFiles()
        => Directory.Exists(OutputFolder) ? Directory.GetFiles(OutputFolder) : [];

    internal string LogText()
        => Directory.Exists(LogFolder)
            ? string.Concat(Directory.GetFiles(LogFolder, "QueryToCsv_*.log").Select(File.ReadAllText))
            : "";

    internal async Task<AppResult> RunAsync(string[] args, string? standardInput = null)
    {
        ProcessStartInfo start = new(Path.Combine(Folder, ExecutableName))
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            // A working directory unrelated to the installation: nothing may depend on it.
            WorkingDirectory = _root.Path,
        };
        foreach (string arg in args)
            start.ArgumentList.Add(arg);
        start.Environment["TMP"] = TempFolder;
        start.Environment["TEMP"] = TempFolder;

        using Process process = Process.Start(start) ?? throw new InvalidOperationException("The process did not start.");
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> error = process.StandardError.ReadToEndAsync();

        if (standardInput is not null)
            await process.StandardInput.WriteAsync(standardInput);
        process.StandardInput.Close();

        using CancellationTokenSource timeout = new(RunTimeout);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"QueryToCsv did not exit within {RunTimeout}.");
        }

        return new AppResult(process.ExitCode, await output, await error);
    }

    public void Dispose() => _root.Dispose();

    [LibraryImport("kernel32.dll", EntryPoint = "CreateHardLinkW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CreateHardLink(string fileName, string existingFileName, nint securityAttributes);
}
