using Xunit;

namespace QueryToCsv.Tests;

public class ConsoleUiTests
{
    // name -> code page, and whether the encoding emits a BOM
    public static TheoryData<string, int, bool> KnownEncodings() => new()
    {
        { "utf-8", 65001, false },
        { "utf8", 65001, false },
        { "UTF-8", 65001, false },
        { "utf-8-bom", 65001, true },
        { "utf8-bom", 65001, true },
        { "UTF-8-BOM", 65001, true },
        { "utf-16", 1200, true },
        { "utf16", 1200, true },
        { "shift-jis", 932, false },
        { "shiftjis", 932, false },
        { "shift_jis", 932, false },
        { "Shift-JIS", 932, false },
    };

    [Theory]
    [MemberData(nameof(KnownEncodings))]
    public void ResolveEncoding_KnownName_ReturnsMatchingEncoding(string name, int expectedCodePage, bool expectsBom)
    {
        var encoding = ConsoleUi.ResolveEncoding(name);

        Assert.NotNull(encoding);
        Assert.Equal(expectedCodePage, encoding.CodePage);
        Assert.Equal(expectsBom, encoding.GetPreamble().Length > 0);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("utf")]
    [InlineData("utf-32")]
    [InlineData("euc-jp")]
    [InlineData("cp932")]
    [InlineData("ascii")]
    public void ResolveEncoding_UnknownName_ReturnsNull(string name)
    {
        Assert.Null(ConsoleUi.ResolveEncoding(name));
    }

    [Fact]
    public void ResolveEncoding_ShiftJis_RoundTripsJapaneseText()
    {
        var encoding = ConsoleUi.ResolveEncoding("shift-jis");

        Assert.NotNull(encoding);
        Assert.Equal("売上", encoding.GetString(encoding.GetBytes("売上")));
    }
}
