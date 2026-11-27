using QueryToCsv.TestSupport;
using Xunit;

namespace QueryToCsv.Tests;

public class UsageGuideTests
{
    private static readonly string NewLine = Environment.NewLine;

    // document -> the text printed, written with \n for the platform's line ending
    public static TheoryData<string, string> Documents() => new()
    {
        { "", "" },
        { "# Guide\n", "# Guide\n" },
        { "---\n---\n# Guide\n", "# Guide\n" },
        { "---\nstatus: active\ncreated: 2026-11-27\n---\n\n# Guide\n\nBody\n", "# Guide\n\nBody\n" },
        { "---\r\nstatus: active\r\n---\r\n\r\n# Guide\r\nBody\r\n", "# Guide\nBody\n" },
        // Only the front matter is removed: a later horizontal rule stays.
        { "---\nstatus: active\n---\n# Guide\n\n---\n\nMore\n", "# Guide\n\n---\n\nMore\n" },
        // A document without front matter keeps a rule of its own.
        { "# Guide\n---\n", "# Guide\n---\n" },
    };

    [Theory]
    [MemberData(nameof(Documents))]
    [Trait("Requirement", "QTC-29")]
    internal void Render_Document_DropsTheFrontMatterAndUsesThePlatformNewLine(string document, string expected)
    {
        Assert.Equal(expected.Replace("\n", NewLine, StringComparison.Ordinal), UsageGuide.Render(document));
    }

    [Fact]
    [Trait("Requirement", "QTC-29")]
    internal void Render_UnclosedFrontMatter_Fails()
    {
        Assert.Throws<InvalidOperationException>(() => UsageGuide.Render("---\nstatus: active\n# Guide\n"));
    }

    [Fact]
    [Trait("Requirement", "QTC-29")]
    internal async Task ReadAsync_EmbeddedGuide_IsTheRepositoryDocumentWithoutItsFrontMatter()
    {
        string document = await File.ReadAllTextAsync(Path.Combine(Repository.Root, "docs", "guides", "usage.md"));

        string guide = await UsageGuide.ReadAsync();

        Assert.Equal(UsageGuide.Render(document), guide);
        Assert.StartsWith("# QueryToCsv Usage Guide" + NewLine, guide, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "QTC-29")]
    internal async Task ReadAsync_EmbeddedGuide_IsAsciiOnly()
    {
        string guide = await UsageGuide.ReadAsync();

        int offending = guide.AsSpan().IndexOfAnyExceptInRange('\0', '\x7F');
        Assert.True(offending < 0, offending < 0 ? "" : $"Character U+{(int)guide[offending]:X4} at offset {offending} is not ASCII.");
    }
}
