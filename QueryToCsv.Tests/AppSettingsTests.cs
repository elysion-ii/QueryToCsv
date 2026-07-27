using Xunit;

namespace QueryToCsv.Tests;

public class AppSettingsTests
{
    // delimiter, newLine, sqlFileEncoding, dateFormat -> accepted
    public static TheoryData<string, string, string, string?, bool> CsvAndEncodingSettings() => new()
    {
        { ",", "CRLF", "UTF-8", null, true },
        { ";", "LF", "UTF-8", "yyyy-MM-dd HH:mm:ss", true },
        { "\t", "CRLF", "Shift-JIS", "yyyyMMdd", true },
        { "|", "CRLF", "utf-16", null, true },
        // Delimiter must be exactly one character
        { "", "CRLF", "UTF-8", null, false },
        { ",,", "CRLF", "UTF-8", null, false },
        { "\\t", "CRLF", "UTF-8", null, false },
        // NewLine is a closed, case-sensitive vocabulary
        { ",", "", "UTF-8", null, false },
        { ",", "crlf", "UTF-8", null, false },
        { ",", "CR", "UTF-8", null, false },
        { ",", "\r\n", "UTF-8", null, false },
        // SqlFileEncoding must name an encoding the runtime knows
        { ",", "CRLF", "not-an-encoding", null, false },
        // DateFormat must be a usable format string
        { ",", "CRLF", "UTF-8", "!", false },
    };

    [Theory]
    [MemberData(nameof(CsvAndEncodingSettings))]
    public void Validate_CsvAndEncodingSettings_MatchesExpectation(
        string delimiter,
        string newLine,
        string sqlFileEncoding,
        string? dateFormat,
        bool expected)
    {
        using var dir = new TempDirectory();
        var settings = ValidSettings(dir.Path);
        settings.SqlFileEncoding = sqlFileEncoding;
        settings.CsvSettings.Delimiter = delimiter;
        settings.CsvSettings.NewLine = newLine;
        settings.CsvSettings.DateFormat = dateFormat;

        Assert.Equal(expected, settings.Validate());
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(30, true)]
    [InlineData(3600, true)]
    public void Validate_QueryTimeout_RequiresAPositiveValue(int timeout, bool expected)
    {
        using var dir = new TempDirectory();
        var settings = ValidSettings(dir.Path);
        settings.QueryTimeout = timeout;

        Assert.Equal(expected, settings.Validate());
    }

    [Theory]
    [InlineData("", "Server=x;Database=y;", false)]
    [InlineData("   ", "Server=x;Database=y;", false)]
    [InlineData("Dev Server", "", false)]
    [InlineData("Dev Server", "   ", false)]
    [InlineData("Dev Server", "Server=x;Database=y;", true)]
    public void Validate_ConnectionEntry_RequiresNameAndConnectionString(
        string name,
        string connectionString,
        bool expected)
    {
        using var dir = new TempDirectory();
        var settings = ValidSettings(dir.Path);
        settings.Connections = [new ConnectionEntry { Name = name, ConnectionString = connectionString }];

        Assert.Equal(expected, settings.Validate());
    }

    [Fact]
    public void Validate_NoConnections_ReturnsFalse()
    {
        using var dir = new TempDirectory();
        var settings = ValidSettings(dir.Path);
        settings.Connections = [];

        Assert.False(settings.Validate());
    }

    [Fact]
    public void Validate_SecondConnectionInvalid_ReturnsFalse()
    {
        using var dir = new TempDirectory();
        var settings = ValidSettings(dir.Path);
        settings.Connections =
        [
            new ConnectionEntry { Name = "Dev", ConnectionString = "Server=x;Database=y;" },
            new ConnectionEntry { Name = "Prod", ConnectionString = "" },
        ];

        Assert.False(settings.Validate());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_BlankQueryFolder_ReturnsFalse(string queryFolder)
    {
        using var dir = new TempDirectory();
        var settings = ValidSettings(dir.Path);
        settings.QueryFolder = queryFolder;

        Assert.False(settings.Validate());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_BlankOutputFolder_ReturnsFalse(string outputFolder)
    {
        using var dir = new TempDirectory();
        var settings = ValidSettings(dir.Path);
        settings.OutputFolder = outputFolder;

        Assert.False(settings.Validate());
    }

    [Fact]
    public void Validate_MissingQueryFolder_ReturnsFalse()
    {
        using var dir = new TempDirectory();
        var settings = ValidSettings(dir.Path);
        settings.QueryFolder = Path.Combine(dir.Path, "does-not-exist");

        Assert.False(settings.Validate());
    }

    [Fact]
    public void Validate_MissingOutputFolder_ReturnsTrue()
    {
        // The output folder is created on demand at write time, so its absence is not a config error
        using var dir = new TempDirectory();
        var settings = ValidSettings(dir.Path);
        settings.OutputFolder = Path.Combine(dir.Path, "not-created-yet");

        Assert.True(settings.Validate());
    }

    [Fact]
    public void Constructor_NoConfiguration_AppliesDocumentedDefaults()
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

    private static AppSettings ValidSettings(string existingFolder) => new()
    {
        Connections = [new ConnectionEntry { Name = "Dev Server", ConnectionString = "Server=x;Database=y;" }],
        QueryFolder = existingFolder,
        OutputFolder = Path.Combine(existingFolder, "output"),
    };
}
