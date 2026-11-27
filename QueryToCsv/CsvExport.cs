using System.Data.SqlTypes;
using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Data.SqlClient;

namespace QueryToCsv;

// Writes a result set as CSV (docs/specs/QueryToCsv.md, QTC-19 to QTC-22).
internal static class CsvExport
{
    private const string SqlVariantTypeName = "sql_variant";

    // The CLR types a column may surface as; any other type (a CLR UDT such as hierarchyid)
    // cannot be written faithfully (QTC-20).
    private static readonly HashSet<Type> ExportableTypes =
    [
        typeof(string), typeof(bool), typeof(byte), typeof(short), typeof(int), typeof(long),
        typeof(decimal), typeof(double), typeof(float), typeof(DateTime), typeof(DateTimeOffset),
        typeof(TimeSpan), typeof(Guid), typeof(byte[]),
    ];

    // Writes the rows to a new file at path and returns how many rows were written.
    internal static async Task<int> WriteAsync(
        SqlDataReader reader,
        string path,
        Encoding encoding,
        bool includeHeader,
        CsvSettings csvSettings,
        CancellationToken cancellationToken = default)
    {
        int fieldCount = reader.FieldCount;
        string[] names = [.. Enumerable.Range(0, fieldCount).Select(reader.GetName)];
        string[] typeNames = [.. Enumerable.Range(0, fieldCount).Select(i => ColumnTypeName(reader, i, names[i]))];

        CsvConfiguration csvConfig = new(CultureInfo.InvariantCulture)
        {
            Delimiter = csvSettings.Delimiter,
            NewLine = csvSettings.NewLine == "LF" ? "\n" : "\r\n",
            ShouldQuote = args => ShouldQuote(args.Field, csvSettings.Delimiter),
        };

        await using FileStream stream = new(
            path, FileMode.CreateNew, FileAccess.Write, FileShare.None, bufferSize: 4096, FileOptions.Asynchronous);
        await using StreamWriter writer = new(stream, encoding);
        await using CsvWriter csv = new(writer, csvConfig);

        if (includeHeader)
        {
            for (int i = 0; i < fieldCount; i++)
                csv.WriteField(Encodable(names[i], encoding, row: 0, names[i]));
            await csv.NextRecordAsync();
        }

        int rowCount = 0;
        while (await reader.ReadAsync(cancellationToken))
        {
            rowCount++;
            for (int i = 0; i < fieldCount; i++)
            {
                string field = await reader.IsDBNullAsync(i, cancellationToken)
                    ? csvSettings.NullValue
                    : await FieldTextAsync(reader, i, names[i], typeNames[i], csvSettings.DateFormat, cancellationToken);
                csv.WriteField(Encodable(field, encoding, rowCount, names[i]));
            }
            await csv.NextRecordAsync();
        }

        return rowCount;
    }

    // RFC 4180 quoting for every line break, not only the configured terminator: a lone
    // CR or LF left unquoted splits the record for every reader (QTC-19).
    internal static bool ShouldQuote(string? field, string delimiter)
    {
        if (string.IsNullOrEmpty(field))
            return false;

        return field.Contains(delimiter, StringComparison.Ordinal)
               || field.AsSpan().IndexOfAny('"', '\r', '\n') >= 0
               || field[0] == ' '
               || field[^1] == ' ';
    }

    private static string ColumnTypeName(SqlDataReader reader, int ordinal, string name)
    {
        string typeName = reader.GetDataTypeName(ordinal);
        Type? fieldType;
        try
        {
            fieldType = reader.GetFieldType(ordinal);
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or TypeLoadException)
        {
            fieldType = null;
        }

        bool exportable = fieldType is not null
            && (ExportableTypes.Contains(fieldType)
                || (fieldType == typeof(object) && typeName.Equals(SqlVariantTypeName, StringComparison.OrdinalIgnoreCase)));
        if (!exportable)
            throw new RunFailureException(UnsupportedTypeMessage(name, typeName));

        return typeName;
    }

    private static async Task<string> FieldTextAsync(
        SqlDataReader reader,
        int ordinal,
        string name,
        string typeName,
        string? dateFormat,
        CancellationToken cancellationToken)
    {
        object value;
        try
        {
            value = await reader.GetFieldValueAsync<object>(ordinal, cancellationToken);
        }
        catch (OverflowException)
        {
            // decimal(38, s) holds values beyond System.Decimal; the exact text comes from
            // the provider's own decimal type, which writes invariant digits.
            SqlDecimal exact = reader.GetSqlDecimal(ordinal);
            return exact.ToString();
        }

        return CsvValueFormat.Format(value, typeName, dateFormat)
               ?? throw new RunFailureException(UnsupportedTypeMessage(name, typeName));
    }

    // The field itself, once the encoding is known to represent every character of it;
    // the writer would otherwise fail later without saying which value
    // (docs/specs/QueryToCsv.md, QTC-22).
    private static string Encodable(string field, Encoding encoding, int row, string column)
    {
        try
        {
            encoding.GetByteCount(field);
            return field;
        }
        catch (EncoderFallbackException ex)
        {
            throw new RunFailureException(
                $"the value in row {row}, column \"{column}\" cannot be written in {encoding.WebName}.", ex);
        }
    }

    private static string UnsupportedTypeMessage(string name, string typeName)
        => $"column \"{name}\" has type {typeName}, which cannot be exported; convert it in the query.";
}
