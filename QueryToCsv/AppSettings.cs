using System.Globalization;
using System.Text;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace QueryToCsv;

internal sealed class AppSettings
{
    internal const string FileName = "appsettings.json";
    internal const string DefaultQueryFolderName = "queries";
    internal const string DefaultOutputFolderName = "output";
    internal const string DefaultSqlFileEncoding = "UTF-8";
    internal const int DefaultQueryTimeout = 30;
    internal const int DefaultLogRetentionDays = 30;

    // Ten years: retention stays a bounded window, and the oldest kept date stays far
    // inside the calendar range (docs/specs/QueryToCsv.md, QTC-6).
    internal const int MaxLogRetentionDays = 3650;

    private const string QueryFolderKey = "QueryFolder";
    private const string OutputFolderKey = "OutputFolder";
    private const string QueryTimeoutKey = "QueryTimeout";
    private const string LogRetentionDaysKey = "LogRetentionDays";
    private const string SqlFileEncodingKey = "SqlFileEncoding";
    private const string DelimiterKey = "CsvSettings:Delimiter";
    private const string NullValueKey = "CsvSettings:NullValue";
    private const string NewLineKey = "CsvSettings:NewLine";
    private const string DateFormatKey = "CsvSettings:DateFormat";

    internal IReadOnlyList<ConnectionEntry> Connections { get; init; } = [];
    internal string QueryFolder { get; init; } = "";
    internal string OutputFolder { get; init; } = "";
    internal int QueryTimeout { get; init; } = DefaultQueryTimeout;
    internal string SqlFileEncoding { get; init; } = DefaultSqlFileEncoding;
    internal int LogRetentionDays { get; init; } = DefaultLogRetentionDays;
    internal CsvSettings CsvSettings { get; init; } = new();

    // Returns the settings to run on, plus one notice per configured value that was
    // rejected. A missing or unreadable file is itself a notice, never a failure
    // (docs/rules/configuration.md, Configuration Values).
    internal static (AppSettings Settings, IReadOnlyList<string> Notices) Load()
    {
        string baseDirectory = AppContext.BaseDirectory;

        if (!File.Exists(Path.Combine(baseDirectory, FileName)))
        {
            return (Defaults(baseDirectory),
                [$"{FileName} not found; continuing with built-in defaults."]);
        }

        IConfiguration config;
        try
        {
            config = new ConfigurationBuilder()
                .SetBasePath(baseDirectory)
                .AddJsonFile(FileName)
                .Build();
        }
        catch (Exception)
        {
            return (Defaults(baseDirectory),
                [$"failed to load {FileName}; continuing with built-in defaults."]);
        }

        return FromConfiguration(config, baseDirectory);
    }

    internal static (AppSettings Settings, IReadOnlyList<string> Notices) FromConfiguration(
        IConfiguration config,
        string baseDirectory)
    {
        List<string> notices = [];

        T Accept<T>((T Value, string? Notice) resolved)
        {
            if (resolved.Notice is not null)
                notices.Add(resolved.Notice);

            return resolved.Value;
        }

        // Members initialize in the order written, which is the order notices are reported in.
        AppSettings settings = new()
        {
            Connections =
            [
                .. config.GetSection("Connections").GetChildren()
                    .Select(entry => new ConnectionEntry
                    {
                        Name = entry["Name"] ?? "",
                        ConnectionString = entry["ConnectionString"] ?? "",
                        Credential = entry["Credential"],
                    }),
            ],
            QueryFolder = Accept(ResolveFolder(
                config, QueryFolderKey, Path.Combine(baseDirectory, DefaultQueryFolderName), baseDirectory, mustExist: true)),
            OutputFolder = Accept(ResolveFolder(
                config, OutputFolderKey, Path.Combine(baseDirectory, DefaultOutputFolderName), baseDirectory, mustExist: false)),
            QueryTimeout = Accept(ResolveWholeNumber(config, QueryTimeoutKey, DefaultQueryTimeout, int.MaxValue)),
            LogRetentionDays = Accept(ResolveWholeNumber(config, LogRetentionDaysKey, DefaultLogRetentionDays, MaxLogRetentionDays)),
            SqlFileEncoding = Accept(ResolveEncodingName(config, SqlFileEncodingKey)),
            CsvSettings = new CsvSettings
            {
                Delimiter = Accept(ResolveDelimiter(config, DelimiterKey)),
                NullValue = config[NullValueKey] ?? CsvSettings.DefaultNullValue,
                NewLine = Accept(ResolveNewLine(config, NewLineKey)),
                DateFormat = Accept(ResolveDateFormat(config, DateFormatKey)),
            },
        };

        return (settings, notices);
    }

    // The first reason the connections cannot be used, checked in entry order; or, when
    // they can, one warning per entry whose server certificate goes unvalidated. A run
    // that ends on an error connects nowhere, so it carries no warning. Connections is
    // required input rather than a defaulted value: no built-in value can name the server
    // an operator means to query (docs/rules/QueryToCsv.md, Required Input;
    // docs/specs/QueryToCsv.md, QTC-7).
    internal (string? Error, IReadOnlyList<string> Warnings) ValidateConnections()
    {
        if (Connections.Count == 0)
            return ("Connections must contain at least one entry.", []);

        List<string> warnings = [];
        for (int i = 0; i < Connections.Count; i++)
        {
            (string? error, string? warning) = CheckConnection(Connections[i], i);
            if (error is not null)
                return (error, []);

            if (warning is not null)
                warnings.Add(warning);
        }

        return (null, warnings);
    }

    private (string? Error, string? Warning) CheckConnection(ConnectionEntry entry, int index)
    {
        string prefix = $"Connections[{index}]";

        if (string.IsNullOrWhiteSpace(entry.Name))
            return ($"{prefix}.Name is required.", null);

        for (int earlier = 0; earlier < index; earlier++)
        {
            if (string.Equals(Connections[earlier].Name, entry.Name, StringComparison.OrdinalIgnoreCase))
                return ($"{prefix}.Name \"{entry.Name}\" is already used by Connections[{earlier}].", null);
        }

        if (string.IsNullOrWhiteSpace(entry.ConnectionString))
            return ($"{prefix}.ConnectionString is required.", null);

        SqlConnectionStringBuilder builder;
        try
        {
            builder = new SqlConnectionStringBuilder(entry.ConnectionString);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or KeyNotFoundException)
        {
            return ($"{prefix}.ConnectionString is not a valid connection string.", null);
        }

        // An unencrypted connection can be read on the way
        // (docs/rules/db.md, Encrypted connections verify the server).
        if (SqlConnectionEncryptOption.Optional.Equals(builder.Encrypt))
            return ($"{prefix}.ConnectionString must not turn encryption off.", null);

        // A password in a plain-text file can be read by anyone who can read the folder
        // (docs/rules/security.md, Store secrets in platform secret stores).
        if (builder.Password.Length > 0)
        {
            return ($"{prefix}.ConnectionString must not contain a password; store it in Windows Credential Manager "
                + $"and name it in {prefix}.Credential.", null);
        }

        if (builder.UserID.Length > 0)
            return ($"{prefix}.ConnectionString must not contain a user name; it comes from {prefix}.Credential.", null);

        if (entry.Credential is not null && string.IsNullOrWhiteSpace(entry.Credential))
            return ($"{prefix}.Credential is blank.", null);

        // The operator may accept an unvalidated certificate, but never silently
        // (docs/rules/QueryToCsv.md, Server Certificate Validation).
        if (builder.TrustServerCertificate)
        {
            return (null, $"{prefix} \"{entry.Name}\" contains an insecure setting: TrustServerCertificate=True accepts "
                + "any server certificate, so the connection can be intercepted. Give the server a certificate the "
                + "client trusts, or pin it with \"Server Certificate\".");
        }

        return (null, null);
    }

    private static AppSettings Defaults(string baseDirectory) => new()
    {
        QueryFolder = Path.Combine(baseDirectory, DefaultQueryFolderName),
        OutputFolder = Path.Combine(baseDirectory, DefaultOutputFolderName),
    };

    // A relative folder resolves against the executable's directory, never against the
    // working directory (docs/rules/dotnet.md, FILEPATH). QueryFolder has to exist to be
    // usable; OutputFolder is created when the CSV is written.
    private static (string Value, string? Notice) ResolveFolder(
        IConfiguration config,
        string key,
        string fallback,
        string baseDirectory,
        bool mustExist)
    {
        string? raw = config[key];
        if (raw is null)
            return (fallback, null);

        if (string.IsNullOrWhiteSpace(raw))
            return (fallback, $"{DisplayKey(key)} is blank; using \"{fallback}\".");

        string resolved;
        try
        {
            resolved = Path.GetFullPath(raw, baseDirectory);
        }
        catch (ArgumentException)
        {
            return (fallback, $"{DisplayKey(key)} \"{raw}\" is not a usable folder path; using \"{fallback}\".");
        }

        if (mustExist && !Directory.Exists(resolved))
            return (fallback, $"{DisplayKey(key)} \"{resolved}\" does not name an existing folder; using \"{fallback}\".");

        return (resolved, null);
    }

    private static (int Value, string? Notice) ResolveWholeNumber(IConfiguration config, string key, int fallback, int max)
    {
        string? raw = config[key];
        if (raw is null)
            return (fallback, null);

        if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) && value >= 1 && value <= max)
            return (value, null);

        string range = max.ToString(CultureInfo.InvariantCulture);
        return (fallback, $"{DisplayKey(key)} \"{raw}\" is not a whole number from 1 to {range}; using {fallback}.");
    }

    private static (string Value, string? Notice) ResolveEncodingName(IConfiguration config, string key)
    {
        string? raw = config[key];
        if (raw is null)
            return (DefaultSqlFileEncoding, null);

        try
        {
            Encoding.GetEncoding(raw);
            return (raw, null);
        }
        catch (ArgumentException)
        {
            return (DefaultSqlFileEncoding,
                $"{DisplayKey(key)} \"{raw}\" is not an encoding the runtime recognizes; using \"{DefaultSqlFileEncoding}\".");
        }
    }

    private static (string Value, string? Notice) ResolveDelimiter(IConfiguration config, string key)
    {
        string? raw = config[key];
        if (raw is null)
            return (CsvSettings.DefaultDelimiter, null);

        // A double quote, a line break, and a space already mean something in CSV quoting,
        // or are trimmed by readers (docs/specs/QueryToCsv.md, QTC-6).
        if (raw.Length == 1 && raw[0] is not ('"' or '\r' or '\n' or ' '))
            return (raw, null);

        return (CsvSettings.DefaultDelimiter,
            $"{DisplayKey(key)} \"{raw}\" is not one character other than a double quote, a line break, or a space; "
            + $"using \"{CsvSettings.DefaultDelimiter}\".");
    }

    private static (string Value, string? Notice) ResolveNewLine(IConfiguration config, string key)
    {
        string? raw = config[key];
        if (raw is null)
            return (CsvSettings.DefaultNewLine, null);

        if (raw is "CRLF" or "LF")
            return (raw, null);

        return (CsvSettings.DefaultNewLine,
            $"{DisplayKey(key)} \"{raw}\" is not \"CRLF\" or \"LF\"; using \"{CsvSettings.DefaultNewLine}\".");
    }

    private static (string? Value, string? Notice) ResolveDateFormat(IConfiguration config, string key)
    {
        string? raw = config[key];
        if (raw is null)
            return (null, null);

        try
        {
            DateTime.UnixEpoch.ToString(raw, CultureInfo.InvariantCulture);
            return (raw, null);
        }
        catch (FormatException)
        {
            return (null,
                $"{DisplayKey(key)} \"{raw}\" is not a usable date format; dates use the ISO 8601 default.");
        }
    }

    // Configuration paths are colon-separated; a message names the setting the way
    // appsettings.json spells it.
    private static string DisplayKey(string key) => key.Replace(':', '.');
}
