using System.Globalization;
using System.Text;

namespace QueryToCsv;

// Reading SQL text with a bounded size and a strict encoding
// (docs/specs/QueryToCsv.md, QTC-14).
internal static class SqlText
{
    // A single SELECT never approaches these; they keep memory bounded whatever the input.
    internal const int MaxCharacters = 1_048_576;
    internal const int MaxFileBytes = 4_194_304;

    // Byte order marks recognized at the start of a .sql file, longest first so UTF-32 LE
    // is not taken for UTF-16 LE.
    private static readonly (byte[] Mark, string EncodingName)[] ByteOrderMarks =
    [
        ([0xFF, 0xFE, 0x00, 0x00], "utf-32"),
        ([0xEF, 0xBB, 0xBF], "utf-8"),
        ([0xFF, 0xFE], "utf-16"),
        ([0xFE, 0xFF], "utf-16BE"),
    ];

    internal static string? LengthError(string sql)
        => sql.Length > MaxCharacters
            ? $"SQL text exceeds {MaxCharacters.ToString("N0", CultureInfo.InvariantCulture)} characters."
            : null;

    internal static async Task<string> ReadFileAsync(string path, string configuredEncoding, CancellationToken cancellationToken = default)
    {
        byte[] bytes;
        try
        {
            if (new FileInfo(path).Length > MaxFileBytes)
            {
                throw new RunFailureException(
                    $"SQL file \"{path}\" exceeds {MaxFileBytes.ToString("N0", CultureInfo.InvariantCulture)} bytes.");
            }

            bytes = await File.ReadAllBytesAsync(path, cancellationToken);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new RunFailureException($"SQL file \"{path}\" could not be read.", ex);
        }

        string sql = Decode(bytes, configuredEncoding, path);
        string? lengthError = LengthError(sql);
        return lengthError is null ? sql : throw new RunFailureException(lengthError);
    }

    // The text of a .sql file. A byte order mark overrides the configured encoding; an
    // invalid byte sequence is reported, never replaced, because a replaced character
    // changes the statement that runs.
    internal static string Decode(byte[] bytes, string configuredEncoding, string path)
    {
        string encodingName = configuredEncoding;
        int start = 0;
        foreach ((byte[] mark, string name) in ByteOrderMarks)
        {
            if (bytes.AsSpan().StartsWith(mark))
            {
                encodingName = name;
                start = mark.Length;
                break;
            }
        }

        Encoding encoding = Encoding.GetEncoding(encodingName, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
        try
        {
            return encoding.GetString(bytes, start, bytes.Length - start);
        }
        catch (DecoderFallbackException ex)
        {
            int offset = start + Math.Max(ex.Index, 0);
            throw new RunFailureException(
                $"SQL file \"{path}\" is not valid {encoding.WebName} at byte {offset.ToString(CultureInfo.InvariantCulture)}.", ex);
        }
    }
}
