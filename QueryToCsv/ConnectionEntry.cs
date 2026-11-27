namespace QueryToCsv;

internal sealed class ConnectionEntry
{
    internal string Name { get; init; } = "";
    internal string ConnectionString { get; init; } = "";

    // The Windows Credential Manager target holding the SQL Server user name and
    // password, or null for a connection that authenticates through its connection string
    // (docs/specs/QueryToCsv.md, QTC-8).
    internal string? Credential { get; init; }
}
