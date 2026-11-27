using System.Diagnostics.CodeAnalysis;
using Xunit;

namespace QueryToCsv.Tests;

// Tests that replace the process-wide standard input and error streams run one at a time.
[CollectionDefinition(Name, DisableParallelization = true)]
[SuppressMessage(
    "Performance",
    "CA1515",
    Justification = "xUnit reads collection definitions only from public types; an internal one is ignored and the tests run in parallel.")]
public sealed class ConsoleStreamsDefinition
{
    internal const string Name = "ConsoleStreams";
}
