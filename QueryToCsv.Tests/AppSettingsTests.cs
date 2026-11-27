using Microsoft.Extensions.Configuration;
using QueryToCsv.TestSupport;
using Xunit;

namespace QueryToCsv.Tests;

public class AppSettingsTests
{
    // The executable's directory stands in for a real installation; {base} in an
    // expected message is replaced with it.
    private const string BaseDirectoryPlaceholder = "{base}";

    private const string Usable = "Server=x;Database=y;Encrypt=Mandatory;";

    // key, configured value -> the single notice reported
    public static TheoryData<string, string, string> RejectedValues() => new()
    {
        { "QueryFolder", "", "QueryFolder is blank; using \"{base}\\queries\"." },
        { "QueryFolder", "   ", "QueryFolder is blank; using \"{base}\\queries\"." },
        {
            "QueryFolder", "not-there",
            "QueryFolder \"{base}\\not-there\" does not name an existing folder; using \"{base}\\queries\"."
        },
        { "OutputFolder", "", "OutputFolder is blank; using \"{base}\\output\"." },
        { "QueryTimeout", "0", WholeNumber("QueryTimeout", "0", "2147483647") },
        { "QueryTimeout", "-1", WholeNumber("QueryTimeout", "-1", "2147483647") },
        { "QueryTimeout", "abc", WholeNumber("QueryTimeout", "abc", "2147483647") },
        { "QueryTimeout", "1.5", WholeNumber("QueryTimeout", "1.5", "2147483647") },
        { "QueryTimeout", "2147483648", WholeNumber("QueryTimeout", "2147483648", "2147483647") },
        { "LogRetentionDays", "0", WholeNumber("LogRetentionDays", "0", "3650") },
        { "LogRetentionDays", "x", WholeNumber("LogRetentionDays", "x", "3650") },
        { "LogRetentionDays", "3651", WholeNumber("LogRetentionDays", "3651", "3650") },
        { "LogRetentionDays", "2000000", WholeNumber("LogRetentionDays", "2000000", "3650") },
        {
            "SqlFileEncoding", "not-an-encoding",
            "SqlFileEncoding \"not-an-encoding\" is not an encoding the runtime recognizes; using \"UTF-8\"."
        },
        { "CsvSettings:Delimiter", "", Delimiter("") },
        { "CsvSettings:Delimiter", ",,", Delimiter(",,") },
        { "CsvSettings:Delimiter", "\\t", Delimiter("\\t") },
        { "CsvSettings:Delimiter", "\"", Delimiter("\"") },
        { "CsvSettings:Delimiter", "\r", Delimiter("\r") },
        { "CsvSettings:Delimiter", "\n", Delimiter("\n") },
        { "CsvSettings:Delimiter", " ", Delimiter(" ") },
        { "CsvSettings:NewLine", "", "CsvSettings.NewLine \"\" is not \"CRLF\" or \"LF\"; using \"CRLF\"." },
        { "CsvSettings:NewLine", "crlf", "CsvSettings.NewLine \"crlf\" is not \"CRLF\" or \"LF\"; using \"CRLF\"." },
        { "CsvSettings:NewLine", "CR", "CsvSettings.NewLine \"CR\" is not \"CRLF\" or \"LF\"; using \"CRLF\"." },
        {
            "CsvSettings:DateFormat", "!",
            "CsvSettings.DateFormat \"!\" is not a usable date format; dates use the ISO 8601 default."
        },
    };

    private const string PasswordError =
        "Connections[0].ConnectionString must not contain a password; store it in Windows Credential Manager and name "
        + "it in Connections[0].Credential.";

    private const string EncryptionOffError = "Connections[0].ConnectionString must not turn encryption off.";

    // Rejected Connections entries (name, connection string, credential) -> the error reported
    public static TheoryData<string, string, string?, string> RejectedConnectionEntries() => new()
    {
        { "", Usable, null, "Connections[0].Name is required." },
        { "   ", Usable, null, "Connections[0].Name is required." },
        { "Dev", "", null, "Connections[0].ConnectionString is required." },
        { "Dev", "   ", null, "Connections[0].ConnectionString is required." },
        { "Dev", "garbage", null, "Connections[0].ConnectionString is not a valid connection string." },
        { "Dev", "Server=x;NoSuchKeyword=1;", null, "Connections[0].ConnectionString is not a valid connection string." },
        { "Dev", "Server=x;Encrypt=False;", null, EncryptionOffError },
        { "Dev", "Server=x;Encrypt=No;", null, EncryptionOffError },
        { "Dev", "Server=x;Encrypt=Optional;", null, EncryptionOffError },
        // Accepting an unvalidated certificate relaxes nothing else.
        { "Dev", "Server=x;TrustServerCertificate=True;Encrypt=False;", null, EncryptionOffError },
        { "Dev", "Server=x;Password=secret;", null, PasswordError },
        { "Dev", "Server=x;PWD=secret;", null, PasswordError },
        { "Dev", "Server=x;TrustServerCertificate=True;Password=secret;", null, PasswordError },
        {
            "Dev", "Server=x;User ID=sa;", null,
            "Connections[0].ConnectionString must not contain a user name; it comes from Connections[0].Credential."
        },
        {
            "Dev", "Server=x;UID=sa;", null,
            "Connections[0].ConnectionString must not contain a user name; it comes from Connections[0].Credential."
        },
        { "Dev", Usable, "  ", "Connections[0].Credential is blank." },
    };

    // Accepted Connections entries (connection string, credential) -> whether the entry is
    // warned about as accepting any server certificate
    public static TheoryData<string, string?, bool> AcceptedConnectionEntries() => new()
    {
        { "Server=x;Database=y;Integrated Security=True;", null, false },
        { Usable, "QueryToCsv/Dev", false },
        { "Server=x;Encrypt=True;", null, false },
        { "Server=x;Encrypt=Strict;Server Certificate=C:\\certs\\x.cer;", null, false },
        { "Server=x;TrustServerCertificate=False;", null, false },
        { "Server=x;TrustServerCertificate=True;", null, true },
        { "Server=x;Database=y;Integrated Security=True;TrustServerCertificate=yes;", null, true },
        { "Server=x;Encrypt=Mandatory;TrustServerCertificate=True;", "QueryToCsv/Dev", true },
        // The setting alone decides, whatever Encrypt says.
        { "Server=x;Encrypt=Strict;TrustServerCertificate=True;", null, true },
    };

    [Theory]
    [MemberData(nameof(RejectedValues))]
    [Trait("Requirement", "QTC-6")]
    internal void FromConfiguration_RejectedValue_ReportsItAndUsesTheDefault(
        string key,
        string value,
        string expectedNotice)
    {
        using var dir = new TempDirectory();

        (AppSettings settings, IReadOnlyList<string> notices) = AppSettings.FromConfiguration(Configuration((key, value)), dir.Path);

        Assert.Equal([expectedNotice.Replace(BaseDirectoryPlaceholder, dir.Path, StringComparison.Ordinal)], notices);
        AssertBuiltInDefaults(settings, dir.Path);
    }

    [Fact]
    [Trait("Requirement", "QTC-5")]
    internal void FromConfiguration_EmptyConfiguration_UsesTheDefaultsAndReportsNothing()
    {
        using var dir = new TempDirectory();

        (AppSettings settings, IReadOnlyList<string> notices) = AppSettings.FromConfiguration(Configuration(), dir.Path);

        Assert.Empty(notices);
        Assert.Empty(settings.Connections);
        AssertBuiltInDefaults(settings, dir.Path);
    }

    [Fact]
    [Trait("Requirement", "QTC-5")]
    internal void FromConfiguration_UnknownKey_IsIgnoredWithoutANotice()
    {
        using var dir = new TempDirectory();

        (AppSettings settings, IReadOnlyList<string> notices) = AppSettings.FromConfiguration(
            Configuration(("QueryTimout", "5")), dir.Path);

        Assert.Empty(notices);
        Assert.Equal(30, settings.QueryTimeout);
    }

    [Fact]
    [Trait("Requirement", "QTC-6")]
    internal void FromConfiguration_UsableValues_AreKeptAndReportNothing()
    {
        using var dir = new TempDirectory();
        string queryFolder = Path.Combine(dir.Path, "sql");
        Directory.CreateDirectory(queryFolder);

        (AppSettings settings, IReadOnlyList<string> notices) = AppSettings.FromConfiguration(
            Configuration(
                ("QueryFolder", "sql"),
                ("OutputFolder", "csv"),
                ("QueryTimeout", " +2147483647 "),
                ("LogRetentionDays", "3650"),
                ("SqlFileEncoding", "Shift-JIS"),
                ("CsvSettings:Delimiter", "\t"),
                ("CsvSettings:NullValue", "NULL"),
                ("CsvSettings:NewLine", "LF"),
                ("CsvSettings:DateFormat", "yyyy-MM-dd HH:mm:ss")),
            dir.Path);

        Assert.Empty(notices);
        Assert.Equal(queryFolder, settings.QueryFolder);
        Assert.Equal(Path.Combine(dir.Path, "csv"), settings.OutputFolder);
        Assert.Equal(2147483647, settings.QueryTimeout);
        Assert.Equal(3650, settings.LogRetentionDays);
        Assert.Equal("Shift-JIS", settings.SqlFileEncoding);
        Assert.Equal("\t", settings.CsvSettings.Delimiter);
        Assert.Equal("NULL", settings.CsvSettings.NullValue);
        Assert.Equal("LF", settings.CsvSettings.NewLine);
        Assert.Equal("yyyy-MM-dd HH:mm:ss", settings.CsvSettings.DateFormat);
    }

    [Fact]
    [Trait("Requirement", "QTC-6")]
    internal void FromConfiguration_AbsoluteFolders_AreKeptAsWritten()
    {
        using var dir = new TempDirectory();
        string elsewhere = Path.Combine(dir.Path, "elsewhere");
        Directory.CreateDirectory(elsewhere);

        (AppSettings settings, IReadOnlyList<string> notices) = AppSettings.FromConfiguration(
            Configuration(("QueryFolder", elsewhere), ("OutputFolder", elsewhere)),
            dir.Path);

        Assert.Empty(notices);
        Assert.Equal(elsewhere, settings.QueryFolder);
        Assert.Equal(elsewhere, settings.OutputFolder);
    }

    [Fact]
    [Trait("Requirement", "QTC-6")]
    internal void FromConfiguration_SeveralRejectedValues_ReportsEachOnce()
    {
        using var dir = new TempDirectory();

        (AppSettings _, IReadOnlyList<string> notices) = AppSettings.FromConfiguration(
            Configuration(("QueryTimeout", "0"), ("CsvSettings:NewLine", "CR")),
            dir.Path);

        Assert.Equal(
            [
                WholeNumber("QueryTimeout", "0", "2147483647"),
                "CsvSettings.NewLine \"CR\" is not \"CRLF\" or \"LF\"; using \"CRLF\".",
            ],
            notices);
    }

    [Fact]
    [Trait("Requirement", "QTC-7")]
    internal void FromConfiguration_Connections_AreReadInConfigurationOrder()
    {
        using var dir = new TempDirectory();

        (AppSettings settings, IReadOnlyList<string> _) = AppSettings.FromConfiguration(
            Configuration(
                ("Connections:0:Name", "Dev"),
                ("Connections:0:ConnectionString", "Server=dev;Database=a;"),
                ("Connections:1:Name", "Prod"),
                ("Connections:1:ConnectionString", "Server=prod;Database=b;"),
                ("Connections:1:Credential", "QueryToCsv/Prod")),
            dir.Path);

        Assert.Equal(["Dev", "Prod"], settings.Connections.Select(c => c.Name));
        Assert.Equal("Server=prod;Database=b;", settings.Connections[1].ConnectionString);
        Assert.Null(settings.Connections[0].Credential);
        Assert.Equal("QueryToCsv/Prod", settings.Connections[1].Credential);
    }

    [Theory]
    [MemberData(nameof(RejectedConnectionEntries))]
    [Trait("Requirement", "QTC-7")]
    internal void ValidateConnections_RejectedEntry_ReportsTheFirstProblemAndNoWarning(
        string name,
        string connectionString,
        string? credential,
        string expected)
    {
        var settings = new AppSettings
        {
            Connections = [new ConnectionEntry { Name = name, ConnectionString = connectionString, Credential = credential }],
        };

        (string? error, IReadOnlyList<string> warnings) = settings.ValidateConnections();

        Assert.Equal(expected, error);
        Assert.Empty(warnings);
    }

    [Theory]
    [MemberData(nameof(AcceptedConnectionEntries))]
    [Trait("Requirement", "QTC-7")]
    internal void ValidateConnections_AcceptedEntry_WarnsOnlyWhenTheCertificateGoesUnvalidated(
        string connectionString,
        string? credential,
        bool warned)
    {
        var settings = new AppSettings
        {
            Connections = [new ConnectionEntry { Name = "Dev", ConnectionString = connectionString, Credential = credential }],
        };

        (string? error, IReadOnlyList<string> warnings) = settings.ValidateConnections();

        string[] expected = warned ? [InsecureCertificateWarning(0, "Dev")] : [];
        Assert.Null(error);
        Assert.Equal(expected, warnings);
    }

    [Fact]
    [Trait("Requirement", "QTC-7")]
    internal void ValidateConnections_SeveralUnvalidatedCertificates_WarnsOncePerEntryInOrder()
    {
        var settings = new AppSettings
        {
            Connections =
            [
                new ConnectionEntry { Name = "Takai", ConnectionString = "Server=a;Integrated Security=True;TrustServerCertificate=True;" },
                new ConnectionEntry { Name = "Dev", ConnectionString = Usable },
                new ConnectionEntry { Name = "Old", ConnectionString = "Server=b;TrustServerCertificate=True;", Credential = "QueryToCsv/Old" },
            ],
        };

        (string? error, IReadOnlyList<string> warnings) = settings.ValidateConnections();

        Assert.Null(error);
        Assert.Equal([InsecureCertificateWarning(0, "Takai"), InsecureCertificateWarning(2, "Old")], warnings);
        Assert.All(warnings, warning => Assert.DoesNotContain("Server=", warning, StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Requirement", "QTC-7")]
    internal void ValidateConnections_UnvalidatedCertificateBeforeABrokenEntry_ReportsOnlyTheError()
    {
        var settings = new AppSettings
        {
            Connections =
            [
                new ConnectionEntry { Name = "Takai", ConnectionString = "Server=a;TrustServerCertificate=True;" },
                new ConnectionEntry { Name = "", ConnectionString = Usable },
            ],
        };

        (string? error, IReadOnlyList<string> warnings) = settings.ValidateConnections();

        Assert.Equal("Connections[1].Name is required.", error);
        Assert.Empty(warnings);
    }

    [Fact]
    [Trait("Requirement", "QTC-7")]
    internal void ValidateConnections_NoConnections_NamesTheMissingSetting()
    {
        var settings = new AppSettings();

        Assert.Equal("Connections must contain at least one entry.", settings.ValidateConnections().Error);
    }

    [Fact]
    [Trait("Requirement", "QTC-7")]
    internal void ValidateConnections_SeveralIncompleteEntries_ReportsOnlyTheFirstInOrder()
    {
        var settings = new AppSettings
        {
            Connections =
            [
                new ConnectionEntry { Name = "Dev", ConnectionString = Usable },
                new ConnectionEntry { Name = "Prod", ConnectionString = "" },
                new ConnectionEntry { Name = "", ConnectionString = "" },
            ],
        };

        Assert.Equal("Connections[1].ConnectionString is required.", settings.ValidateConnections().Error);
    }

    [Fact]
    [Trait("Requirement", "QTC-7")]
    internal void ValidateConnections_NameReusedIgnoringCase_NamesBothEntries()
    {
        var settings = new AppSettings
        {
            Connections =
            [
                new ConnectionEntry { Name = "Dev", ConnectionString = Usable },
                new ConnectionEntry { Name = "Prod", ConnectionString = Usable },
                new ConnectionEntry { Name = "dev", ConnectionString = Usable },
            ],
        };

        Assert.Equal("Connections[2].Name \"dev\" is already used by Connections[0].", settings.ValidateConnections().Error);
    }

    [Fact]
    [Trait("Requirement", "QTC-6")]
    internal void Constructor_NoConfiguration_AppliesDocumentedDefaults()
    {
        var settings = new AppSettings();

        Assert.Empty(settings.Connections);
        Assert.Equal(30, settings.QueryTimeout);
        Assert.Equal("UTF-8", settings.SqlFileEncoding);
        Assert.Equal(30, settings.LogRetentionDays);
        Assert.Equal(",", settings.CsvSettings.Delimiter);
        Assert.Equal("", settings.CsvSettings.NullValue);
        Assert.Equal("CRLF", settings.CsvSettings.NewLine);
        Assert.Null(settings.CsvSettings.DateFormat);
    }

    // Written out in full rather than built from the production text, so a change to the
    // message fails here (docs/specs/QueryToCsv.md, QTC-7).
    private static string InsecureCertificateWarning(int index, string name)
        => $"Connections[{index}] \"{name}\" contains an insecure setting: TrustServerCertificate=True accepts any server "
            + "certificate, so the connection can be intercepted. Give the server a certificate the client trusts, or pin "
            + "it with \"Server Certificate\".";

    private static string WholeNumber(string key, string value, string max)
        => $"{key} \"{value}\" is not a whole number from 1 to {max}; using 30.";

    private static string Delimiter(string value)
        => $"CsvSettings.Delimiter \"{value}\" is not one character other than a double quote, a line break, or a space; using \",\".";

    private static IConfiguration Configuration(params (string Key, string Value)[] entries)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(entries.Select(e => new KeyValuePair<string, string?>(e.Key, e.Value)))
            .Build();

    private static void AssertBuiltInDefaults(AppSettings settings, string baseDirectory)
    {
        Assert.Equal(Path.Combine(baseDirectory, "queries"), settings.QueryFolder);
        Assert.Equal(Path.Combine(baseDirectory, "output"), settings.OutputFolder);
        Assert.Equal(30, settings.QueryTimeout);
        Assert.Equal(30, settings.LogRetentionDays);
        Assert.Equal("UTF-8", settings.SqlFileEncoding);
        Assert.Equal(",", settings.CsvSettings.Delimiter);
        Assert.Equal("", settings.CsvSettings.NullValue);
        Assert.Equal("CRLF", settings.CsvSettings.NewLine);
        Assert.Null(settings.CsvSettings.DateFormat);
    }
}
