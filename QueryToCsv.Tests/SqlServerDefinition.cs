using System.Diagnostics.CodeAnalysis;
using QueryToCsv.TestSupport;
using Xunit;

namespace QueryToCsv.Tests;

// Boundary tests share one SQL Server container for the run (SqlServerFixture).
[CollectionDefinition(Name)]
[SuppressMessage(
    "Performance",
    "CA1515",
    Justification = "xUnit reads collection definitions only from public types.")]
public sealed class SqlServerDefinition : ICollectionFixture<SqlServerFixture>
{
    internal const string Name = "SqlServer";

    // Tests that need the server carry this trait, so a runner without Docker can select
    // the other tests (docs/references/code-map.md).
    internal const string BoundaryCategory = "Boundary";
}
