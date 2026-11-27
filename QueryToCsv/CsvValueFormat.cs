using System.Globalization;

namespace QueryToCsv;

// How one database value is written as CSV text (docs/specs/QueryToCsv.md, QTC-20 and
// QTC-21). Values are written as stored, culture-invariant, with no time zone conversion.
internal static class CsvValueFormat
{
    private const string DateFormat = "yyyy-MM-dd";
    private const string DateTimeFormat = "yyyy-MM-ddTHH:mm:ss.fffffff";
    private const string DateTimeOffsetFormat = "yyyy-MM-ddTHH:mm:ss.fffffffzzz";
    private const string TimeFormat = @"hh\:mm\:ss\.fffffff";
    private const string SqlDateTypeName = "date";

    // Characters that make a spreadsheet evaluate the cell as a formula when they come first
    // (docs/rules/file-exchange.md, Neutralize spreadsheet formula triggers).
    private static readonly char[] FormulaTriggers = ['=', '+', '-', '@', '\t', '\r'];

    private const string FormulaGuard = "'";

    // Text for a non-null value, or null when the value's type is not one QueryToCsv
    // exports. sqlTypeName is the column's SQL Server type name; dateFormat is
    // CsvSettings.DateFormat.
    internal static string? Format(object value, string sqlTypeName, string? dateFormat)
    {
        return value switch
        {
            string text => sqlTypeName.Equals("xml", StringComparison.OrdinalIgnoreCase) ? text : NeutralizeFormula(text),
            bool flag => flag ? "1" : "0",
            byte or short or int or long => ((IFormattable)value).ToString(null, CultureInfo.InvariantCulture),
            decimal number => number.ToString(CultureInfo.InvariantCulture),
            double or float => ((IFormattable)value).ToString("R", CultureInfo.InvariantCulture),
            DateTime instant => instant.ToString(
                dateFormat ?? (sqlTypeName.Equals(SqlDateTypeName, StringComparison.OrdinalIgnoreCase) ? DateFormat : DateTimeFormat),
                CultureInfo.InvariantCulture),
            DateTimeOffset instant => instant.ToString(dateFormat ?? DateTimeOffsetFormat, CultureInfo.InvariantCulture),
            TimeSpan time => time.ToString(TimeFormat, CultureInfo.InvariantCulture),
            Guid id => id.ToString("D", CultureInfo.InvariantCulture),
            byte[] bytes => "0x" + Convert.ToHexString(bytes),
            _ => null,
        };
    }

    internal static string NeutralizeFormula(string text)
        => text.Length > 0 && Array.IndexOf(FormulaTriggers, text[0]) >= 0 ? FormulaGuard + text : text;
}
