namespace QueryToCsv;

internal sealed class CsvSettings
{
    internal const string DefaultDelimiter = ",";
    internal const string DefaultNullValue = "";
    internal const string DefaultNewLine = "CRLF";

    internal string Delimiter { get; init; } = DefaultDelimiter;
    internal string NullValue { get; init; } = DefaultNullValue;
    internal string NewLine { get; init; } = DefaultNewLine;
    internal string? DateFormat { get; init; }
}
