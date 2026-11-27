using System.Globalization;
using System.Text;
using Microsoft.Data.SqlClient;

namespace QueryToCsv;

// Every prompt returns null when standard input ends (Ctrl+Z), so the caller decides
// how the run ends and records it (docs/specs/QueryToCsv.md, QTC-12).
internal static class ConsoleUi
{
    internal const int DirectEntry = -1;

    // The index of the chosen .sql file, DirectEntry for option 0, or null when input ends.
    internal static int? SelectQuery(IReadOnlyList<string> fileNames)
    {
        Console.Error.WriteLine("=== Select a query ===");
        Console.Error.WriteLine("0. Enter query directly");
        for (int i = 0; i < fileNames.Count; i++)
            Console.Error.WriteLine($"{i + 1}. {fileNames[i]}");
        Console.Error.WriteLine();

        int? number = AskNumber(0, fileNames.Count);
        return number switch
        {
            null => null,
            0 => DirectEntry,
            _ => number.Value - 1,
        };
    }

    // The typed SQL, or null when nothing but whitespace was entered. End of input ends
    // the SQL rather than the run. Lines past the SQL text limit are read but not kept, so
    // memory stays bounded and the returned text still exceeds the limit for the caller to
    // report (docs/specs/QueryToCsv.md, QTC-14).
    internal static string? InputQuery()
    {
        Console.Error.WriteLine("Enter SQL query (end with Ctrl+Z):");
        List<string> lines = [];
        long kept = 0;
        while (true)
        {
            Console.Error.Write("  > ");
            string? line = Console.ReadLine();
            if (line is null)
                break;

            if (kept <= SqlText.MaxCharacters)
            {
                lines.Add(line);
                kept += line.Length + 2;
            }
        }

        string sql = string.Join("\r\n", lines);
        return string.IsNullOrWhiteSpace(sql) ? null : sql;
    }

    internal static bool? AskIncludeHeader()
    {
        while (true)
        {
            Console.Error.Write("Include header row? (y/n): ");
            string? input = Console.ReadLine();
            if (input is null)
                return null;

            string answer = input.Trim();
            if (answer.Equals("y", StringComparison.OrdinalIgnoreCase))
                return true;
            if (answer.Equals("n", StringComparison.OrdinalIgnoreCase))
                return false;

            Console.Error.WriteLine("Please enter y or n.");
        }
    }

    internal static Encoding? SelectEncoding()
    {
        Console.Error.WriteLine("=== Select encoding ===");
        Console.Error.WriteLine("1. UTF-8");
        Console.Error.WriteLine("2. UTF-8 with BOM");
        Console.Error.WriteLine("3. UTF-16 LE");
        Console.Error.WriteLine("4. Shift-JIS");
        Console.Error.WriteLine();

        return AskNumber(1, 4) switch
        {
            null => null,
            1 => ResolveEncoding("utf-8"),
            2 => ResolveEncoding("utf-8-bom"),
            3 => ResolveEncoding("utf-16"),
            _ => ResolveEncoding("shift-jis"),
        };
    }

    // The CSV encoding for a name, or null for a name not offered. Each encoding throws on
    // a character it cannot represent instead of writing a replacement
    // (docs/specs/QueryToCsv.md, QTC-10 and QTC-22).
    internal static Encoding? ResolveEncoding(string name)
    {
        return name.ToLowerInvariant() switch
        {
            "utf-8" or "utf8" => Strict(new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)),
            "utf-8-bom" or "utf8-bom" => Strict(new UTF8Encoding(encoderShouldEmitUTF8Identifier: true)),
            "utf-16" or "utf16" => Strict(new UnicodeEncoding(bigEndian: false, byteOrderMark: true)),
            "shift-jis" or "shiftjis" or "shift_jis" => Encoding.GetEncoding(
                "shift_jis", EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback),
            _ => null,
        };
    }

    // The encoding with exception fallbacks; the constructors' own switch covers decoding
    // only, and an unpaired surrogate would still be written as U+FFFD.
    private static Encoding Strict(Encoding encoding)
    {
        Encoding strict = (Encoding)encoding.Clone();
        strict.EncoderFallback = EncoderFallback.ExceptionFallback;
        strict.DecoderFallback = DecoderFallback.ExceptionFallback;
        return strict;
    }

    // The index of the chosen connection, or null when input ends.
    internal static int? SelectConnection(IReadOnlyList<ConnectionEntry> connections)
    {
        if (connections.Count == 1)
            return 0;

        Console.Error.WriteLine("=== Select connection ===");
        for (int i = 0; i < connections.Count; i++)
        {
            string info = FormatConnectionInfo(connections[i].ConnectionString);
            Console.Error.WriteLine($"{i + 1}. {connections[i].Name} ({info})");
        }
        Console.Error.WriteLine();

        int? number = AskNumber(1, connections.Count);
        return number - 1;
    }

    // The number typed, within first..last, or null when input ends. Each invalid answer
    // reads a new line, so asking again ends with the input
    // (docs/specs/QueryToCsv.md, QTC-12).
    private static int? AskNumber(int first, int last)
    {
        while (true)
        {
            Console.Error.Write("Enter number: ");
            string? input = Console.ReadLine();
            if (input is null)
                return null;

            int? number = ParseChoice(input, first, last);
            if (number is not null)
                return number;

            Console.Error.WriteLine($"Please enter a number between {first} and {last}.");
        }
    }

    // ASCII digits only, surrounding whitespace removed: a sign, a group separator, or a
    // culture's own digits is not an answer to a numbered list.
    internal static int? ParseChoice(string input, int first, int last)
    {
        string answer = input.Trim();
        if (answer.Length == 0 || !answer.All(char.IsAsciiDigit))
            return null;

        return int.TryParse(answer, NumberStyles.None, CultureInfo.InvariantCulture, out int number)
               && number >= first && number <= last
            ? number
            : null;
    }

    // Configuration validation has already parsed every connection string (QTC-7); a part
    // it does not state shows as "?".
    private static string FormatConnectionInfo(string connectionString)
    {
        SqlConnectionStringBuilder builder = new(connectionString);
        string server = string.IsNullOrEmpty(builder.DataSource) ? "?" : builder.DataSource;
        string database = string.IsNullOrEmpty(builder.InitialCatalog) ? "?" : builder.InitialCatalog;
        return $"{server} - {database}";
    }
}
