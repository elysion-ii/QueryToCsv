using System.Text;
using Microsoft.Data.SqlClient;

namespace QueryToCsv;

// Every prompt returns null when standard input ends (Ctrl+Z), so the caller decides
// how the run ends and records it (docs/specs/QueryToCsv.md, QueryToCsv-004).
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

        while (true)
        {
            Console.Error.Write("Enter number: ");
            string? input = Console.ReadLine();
            if (input is null)
                return null;

            if (int.TryParse(input, out int num))
            {
                if (num == 0)
                    return DirectEntry;

                if (num >= 1 && num <= fileNames.Count)
                    return num - 1;
            }

            Console.Error.WriteLine($"Please enter a number between 0 and {fileNames.Count}.");
        }
    }

    // The typed SQL, or null when nothing but whitespace was entered. End of input ends
    // the SQL rather than the run.
    internal static string? InputQuery()
    {
        Console.Error.WriteLine("Enter SQL query (end with Ctrl+Z):");
        List<string> lines = [];
        while (true)
        {
            Console.Error.Write("  > ");
            string? line = Console.ReadLine();
            if (line is null)
                break;
            lines.Add(line);
        }

        string sql = string.Join(Environment.NewLine, lines);
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

            if (input.Equals("y", StringComparison.OrdinalIgnoreCase))
                return true;
            if (input.Equals("n", StringComparison.OrdinalIgnoreCase))
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

        while (true)
        {
            Console.Error.Write("Enter number: ");
            string? input = Console.ReadLine();
            if (input is null)
                return null;

            Encoding? encoding = input switch
            {
                "1" => ResolveEncoding("utf-8"),
                "2" => ResolveEncoding("utf-8-bom"),
                "3" => ResolveEncoding("utf-16"),
                "4" => ResolveEncoding("shift-jis"),
                _ => null,
            };

            if (encoding is not null)
                return encoding;

            Console.Error.WriteLine("Please enter a number between 1 and 4.");
        }
    }

    internal static Encoding? ResolveEncoding(string name)
    {
        return name.ToLowerInvariant() switch
        {
            "utf-8" or "utf8" => new UTF8Encoding(false),
            "utf-8-bom" or "utf8-bom" => new UTF8Encoding(true),
            "utf-16" or "utf16" => Encoding.Unicode,
            "shift-jis" or "shiftjis" or "shift_jis" => Encoding.GetEncoding("Shift-JIS"),
            _ => null,
        };
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

        while (true)
        {
            Console.Error.Write("Enter number: ");
            string? input = Console.ReadLine();
            if (input is null)
                return null;

            if (int.TryParse(input, out int num) && num >= 1 && num <= connections.Count)
                return num - 1;

            Console.Error.WriteLine($"Please enter a number between 1 and {connections.Count}.");
        }
    }

    // A connection string the builder cannot parse still lists the entry, so the user can
    // pick it and get the connection failure reported when it is used.
    private static string FormatConnectionInfo(string connectionString)
    {
        try
        {
            SqlConnectionStringBuilder builder = new(connectionString);
            string server = string.IsNullOrEmpty(builder.DataSource) ? "?" : builder.DataSource;
            string database = string.IsNullOrEmpty(builder.InitialCatalog) ? "?" : builder.InitialCatalog;
            return $"{server} - {database}";
        }
        catch (Exception)
        {
            return "?";
        }
    }
}
