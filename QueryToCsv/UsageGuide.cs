using System.Text;

namespace QueryToCsv;

// The usage guide --guide prints: docs/guides/usage.md, which the build embeds in the
// executable, so the repository's document and the printed text cannot drift apart
// (docs/specs/QueryToCsv.md, QTC-29).
internal static class UsageGuide
{
    // The LogicalName the EmbeddedResource in QueryToCsv.csproj gives the document.
    internal const string ResourceName = "QueryToCsv.usage.md";

    private const string FrontMatterFence = "---\n";

    internal static async Task<string> ReadAsync(CancellationToken cancellationToken = default)
    {
        using Stream stream = typeof(UsageGuide).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"The build did not embed {ResourceName}.");
        using StreamReader reader = new(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true));
        return Render(await reader.ReadToEndAsync(cancellationToken));
    }

    // The document as printed: without its front matter, which describes the file's
    // lifecycle rather than how to use QueryToCsv, and with the platform's line endings.
    internal static string Render(string document)
    {
        string text = document.ReplaceLineEndings("\n");
        if (text.StartsWith(FrontMatterFence, StringComparison.Ordinal))
        {
            int closing = text.IndexOf("\n" + FrontMatterFence, FrontMatterFence.Length - 1, StringComparison.Ordinal);
            if (closing < 0)
                throw new InvalidOperationException("The usage guide's front matter has no closing fence.");

            text = text[(closing + 1 + FrontMatterFence.Length)..].TrimStart('\n');
        }

        return text.ReplaceLineEndings();
    }
}
