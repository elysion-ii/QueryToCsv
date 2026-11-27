using Xunit;

namespace QueryToCsv.Tests;

public class CsvValueFormatTests
{
    // value, SQL Server type name -> text written with no DateFormat
    public static TheoryData<object, string, string?> Values() => new()
    {
        { "", "nvarchar", "" },
        { "abc", "nvarchar", "abc" },
        { "ab   ", "char", "ab   " },
        { "<a/>", "xml", "<a/>" },
        { true, "bit", "1" },
        { false, "bit", "0" },
        { (byte)255, "tinyint", "255" },
        { (short)-32768, "smallint", "-32768" },
        { -1, "int", "-1" },
        { long.MaxValue, "bigint", "9223372036854775807" },
        { 12.3400m, "money", "12.3400" },
        { -0.5m, "decimal", "-0.5" },
        { 1234567.89m, "decimal", "1234567.89" },
        { 0.1, "float", "0.1" },
        { 1e20, "float", "1E+20" },
        { 1e-5, "float", "1E-05" },
        { 0.1f, "real", "0.1" },
        { new DateTime(2026, 3, 2), "date", "2026-03-02" },
        { new DateTime(2026, 3, 2, 15, 30, 45).AddTicks(1234567), "datetime2", "2026-03-02T15:30:45.1234567" },
        { new DateTime(2026, 3, 2, 15, 30, 45), "datetime", "2026-03-02T15:30:45.0000000" },
        { new DateTimeOffset(2026, 3, 2, 15, 30, 45, TimeSpan.FromHours(9)), "datetimeoffset", "2026-03-02T15:30:45.0000000+09:00" },
        { new DateTimeOffset(2026, 3, 2, 15, 30, 45, TimeSpan.FromMinutes(-330)), "datetimeoffset", "2026-03-02T15:30:45.0000000-05:30" },
        { new TimeSpan(0, 13, 45, 0, 123), "time", "13:45:00.1230000" },
        { Guid.Parse("6F9619FF-8B86-D011-B42D-00C04FC964FF"), "uniqueidentifier", "6f9619ff-8b86-d011-b42d-00c04fc964ff" },
        { Array.Empty<byte>(), "varbinary", "0x" },
        { new byte[] { 0x01, 0xAB, 0xFF }, "varbinary", "0x01ABFF" },
        { 5, "sql_variant", "5" },
        { new object(), "sql_variant", null },
        { new Uri("http://x"), "sql_variant", null },
    };

    // text -> written (types char, varchar, nchar, nvarchar, text, ntext)
    public static TheoryData<string, string> Texts() => new()
    {
        { "", "" },
        { "a=1", "a=1" },
        { "=SUM(A1)", "'=SUM(A1)" },
        { "+1", "'+1" },
        { "-1", "'-1" },
        { "@cmd", "'@cmd" },
        { "\tx", "'\tx" },
        { "\rx", "'\rx" },
        { "'quoted", "'quoted" },
        { " =x", " =x" },
    };

    [Theory]
    [MemberData(nameof(Values))]
    [Trait("Requirement", "QTC-20")]
    internal void Format_ValueOfType_IsWrittenInItsInvariantForm(object value, string sqlTypeName, string? expected)
    {
        Assert.Equal(expected, CsvValueFormat.Format(value, sqlTypeName, dateFormat: null));
    }

    [Theory]
    [InlineData("date")]
    [InlineData("datetime2")]
    [Trait("Requirement", "QTC-20")]
    internal void Format_DateFormatSet_ReplacesTheDateForms(string sqlTypeName)
    {
        DateTime value = new(2026, 3, 2, 15, 30, 45);

        Assert.Equal("2026/03/02 15:30", CsvValueFormat.Format(value, sqlTypeName, "yyyy/MM/dd HH:mm"));
        Assert.Equal(
            "2026/03/02",
            CsvValueFormat.Format(new DateTimeOffset(value, TimeSpan.Zero), "datetimeoffset", "yyyy/MM/dd"));
        Assert.Equal("15:30:45.0000000", CsvValueFormat.Format(value.TimeOfDay, "time", "yyyy/MM/dd"));
    }

    [Theory]
    [MemberData(nameof(Texts))]
    [Trait("Requirement", "QTC-21")]
    internal void Format_TextValue_NeutralizesFormulaTriggers(string text, string expected)
    {
        Assert.Equal(expected, CsvValueFormat.Format(text, "nvarchar", dateFormat: null));
        Assert.Equal(expected, CsvValueFormat.Format(text, "sql_variant", dateFormat: null));
    }

    [Fact]
    [Trait("Requirement", "QTC-21")]
    internal void Format_NonTextValues_AreNotNeutralized()
    {
        Assert.Equal("-1", CsvValueFormat.Format(-1, "int", null));
        Assert.Equal("-0.5", CsvValueFormat.Format(-0.5m, "decimal", null));
        Assert.Equal("=x", CsvValueFormat.Format("=x", "xml", null));
    }

    [Theory]
    // field, delimiter -> quoted
    [InlineData(null, ",", false)]
    [InlineData("", ",", false)]
    [InlineData("abc", ",", false)]
    [InlineData("a\tb", ",", false)]
    [InlineData("a,b", ",", true)]
    [InlineData("a;b", ";", true)]
    [InlineData("a,b", ";", false)]
    [InlineData("a\"b", ",", true)]
    [InlineData("a\rb", ",", true)]
    [InlineData("a\nb", ",", true)]
    [InlineData("a\r\nb", ",", true)]
    [InlineData(" a", ",", true)]
    [InlineData("a ", ",", true)]
    [InlineData("a b", ",", false)]
    [Trait("Requirement", "QTC-19")]
    internal void ShouldQuote_Field_QuotesDelimiterQuoteLineBreaksAndOuterSpaces(string? field, string delimiter, bool expected)
    {
        Assert.Equal(expected, CsvExport.ShouldQuote(field, delimiter));
    }
}
