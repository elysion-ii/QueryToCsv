using System.Diagnostics.CodeAnalysis;
using QueryToCsv.TestSupport;
using Xunit;

namespace QueryToCsv.EndToEndTests;

// End-to-end tests share one SQL Server container for the run (SqlServerFixture).
[CollectionDefinition(Name)]
[SuppressMessage(
    "Performance",
    "CA1515",
    Justification = "xUnit reads collection definitions only from public types.")]
public sealed class SqlServerDefinition : ICollectionFixture<SqlServerFixture>
{
    internal const string Name = "SqlServer";
}
