using System.Text;
using Microsoft.Data.SqlClient;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace QueryToCsv;

// The database side of a run: the SELECT-only check on the T-SQL parse tree, and running
// the query into a CSV file.
internal static class QueryExecutor
{
    private const int QueryTimeoutErrorNumber = -2;

    // Runs the query and writes its first result set to a new file at csvPath, returning
    // the row count. A failure the operator can act on is a RunFailureException
    // (docs/specs/QueryToCsv.md, QTC-16).
    internal static async Task<int> ExportAsync(
        string connectionString,
        string sql,
        int queryTimeout,
        bool includeHeader,
        Encoding csvEncoding,
        CsvSettings csvSettings,
        string csvPath,
        CancellationToken cancellationToken = default)
    {
        // A connection that cannot be opened in time also reports the timeout number; only a
        // timeout after the connection is open is the query's own (QTC-16).
        bool opened = false;
        try
        {
            await using SqlConnection connection = new(connectionString);
            await Console.Error.WriteLineAsync("Connecting...");
            await connection.OpenAsync(cancellationToken);
            opened = true;

            await using SqlCommand command = new(sql, connection) { CommandTimeout = queryTimeout };

            await Console.Error.WriteLineAsync("Executing query...");
            await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);

            await Console.Error.WriteLineAsync("Writing CSV...");
            return await CsvExport.WriteAsync(reader, csvPath, csvEncoding, includeHeader, csvSettings, cancellationToken);
        }
        catch (SqlException) when (cancellationToken.IsCancellationRequested)
        {
            // The provider reports a cancelled command as a server error.
            throw new OperationCanceledException(cancellationToken);
        }
        catch (SqlException ex) when (opened && ex.Number == QueryTimeoutErrorNumber)
        {
            throw new RunFailureException("query timed out.", ex);
        }
        catch (SqlException ex)
        {
            throw new RunFailureException("query execution failed.", ex);
        }
    }

    // Decided on the parse tree, never on keywords in the text: T-SQL runs one statement
    // after another with no separator, so only the parser sees where the SELECT ends
    // (docs/specs/QueryToCsv.md, QTC-15).
    internal static bool IsSelectOnly(string sql)
    {
        TSql170Parser parser = new(initialQuotedIdentifiers: true);
        using StringReader reader = new(sql);
        TSqlFragment fragment = parser.Parse(reader, out IList<ParseError> errors);

        if (errors.Count > 0 || fragment is not TSqlScript script)
            return false;

        if (script.Batches.Count != 1 || script.Batches[0].Statements.Count != 1)
            return false;

        if (script.Batches[0].Statements[0] is not SelectStatement select || select.Into is not null)
            return false;

        DisallowedReferenceFinder finder = new();
        select.Accept(finder);
        return !finder.Found;
    }

    // Collects whether the SELECT reaches outside the connected server or changes its state.
    private sealed class DisallowedReferenceFinder : TSqlFragmentVisitor
    {
        internal bool Found { get; private set; }

        public override void Visit(OpenRowsetTableReference node) => Found = true;

        public override void Visit(BulkOpenRowset node) => Found = true;

        public override void Visit(OpenQueryTableReference node) => Found = true;

        public override void Visit(AdHocTableReference node) => Found = true;

        // NEXT VALUE FOR advances the sequence it reads.
        public override void Visit(NextValueForExpression node) => Found = true;

        // server.database.schema.object reaches another server.
        public override void Visit(SchemaObjectName node)
        {
            if (node.ServerIdentifier is not null)
                Found = true;
        }
    }
}
