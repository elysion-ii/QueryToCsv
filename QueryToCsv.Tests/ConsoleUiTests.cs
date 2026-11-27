using System.Text;
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

    // answer typed at a numbered prompt (1 to 4) -> the number taken, or null to ask again
    public static TheoryData<string, int?> Choices() => new()
    {
        { "", null },
        { "   ", null },
        { "1", 1 },
        { " 2 ", 2 },
        { "\t3", 3 },
        { "04", 4 },
        { "0", null },
        { "5", null },
        { "+1", null },
        { "-1", null },
        { "1.0", null },
        { "1,0", null },
        { "x", null },
        { "１", null },
        { "99999999999", null },
    };

    [Theory]
    [MemberData(nameof(KnownEncodings))]
    [Trait("Requirement", "QTC-10")]
    internal void ResolveEncoding_KnownName_ReturnsMatchingEncoding(string name, int expectedCodePage, bool expectsBom)
    {
        Encoding? encoding = ConsoleUi.ResolveEncoding(name);

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
    [Trait("Requirement", "QTC-10")]
    internal void ResolveEncoding_UnknownName_ReturnsNull(string name)
    {
        Assert.Null(ConsoleUi.ResolveEncoding(name));
    }

    [Fact]
    [Trait("Requirement", "QTC-10")]
    internal void ResolveEncoding_ShiftJis_RoundTripsJapaneseText()
    {
        Encoding? encoding = ConsoleUi.ResolveEncoding("shift-jis");

        Assert.NotNull(encoding);
        Assert.Equal("売上", encoding.GetString(encoding.GetBytes("売上")));
    }

    // Code points, not strings: theory data passes through serialization, which turns an
    // unpaired surrogate into U+FFFD before the test sees it.
    [Theory]
    [InlineData("shift-jis", 0x1F600)]
    [InlineData("shift-jis", 0x20AC)]
    [InlineData("utf-8", 0xD800)]
    [InlineData("utf-8-bom", 0xDBFF)]
    [InlineData("utf-16", 0xDC00)]
    [Trait("Requirement", "QTC-22")]
    internal void ResolveEncoding_UnrepresentableText_ThrowsInsteadOfReplacing(string name, int codePoint)
    {
        Encoding encoding = ConsoleUi.ResolveEncoding(name)!;
        string text = codePoint is >= 0xD800 and <= 0xDFFF ? ((char)codePoint).ToString() : char.ConvertFromUtf32(codePoint);

        Assert.Throws<EncoderFallbackException>(() => encoding.GetBytes(text));
    }

    [Theory]
    [MemberData(nameof(Choices))]
    [Trait("Requirement", "QTC-12")]
    internal void ParseChoice_Answer_TakesOnlyAListedNumber(string input, int? expected)
    {
        Assert.Equal(expected, ConsoleUi.ParseChoice(input, 1, 4));
    }
}
