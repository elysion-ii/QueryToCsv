using Xunit;

namespace QueryToCsv.Tests;

public class CliInvocationTests
{
    public static TheoryData<string[], string> Modes() => new()
    {
        { [], "Interactive" },
        { ["-h"], "Help" },
        { ["--help"], "Help" },
        { ["--help", "anything"], "Help" },
        { ["-V"], "Version" },
        { ["--version"], "Version" },
        { ["-V", "--query"], "Version" },
        { ["--open", "queries"], "Open" },
        { ["--open=output"], "Open" },
        { ["--query", "SELECT 1"], "OneLiner" },
    };

    public static TheoryData<string[], string> RejectedInvocations() => new()
    {
        { ["--open"], "option '--open' requires a value." },
        { ["--open="], "option '--open' requires a value." },
        { ["--open", ""], "option '--open' requires a value." },
        { ["--open", "queries", "extra"], "unexpected argument 'extra'" },
        { ["--open=queries", "extra"], "unexpected argument 'extra'" },
        { ["--query", "SELECT 1", "--open", "log"], "unknown option '--open'" },
        { ["-v"], "unknown option '-v'" },
        { ["-q", "SELECT 1"], "unknown option '-q'" },
        { ["version"], "unknown option 'version'" },
    };

    public static TheoryData<string[], string> OpenInvocations() => new()
    {
        { ["--open", "queries"], "queries" },
        { ["--open=queries"], "queries" },
        { ["--open", "QUERIES"], "QUERIES" },
        { ["--open=report.csv"], "report.csv" },
    };

    [Theory]
    [MemberData(nameof(Modes))]
    [Trait("Requirement", "QTC-1")]
    internal void Parse_KnownMode_ReturnsExpectedMode(string[] args, string expectedMode)
    {
        (CliInvocation? invocation, string? error) = CliInvocation.Parse(args);

        Assert.Null(error);
        Assert.NotNull(invocation);
        Assert.Equal(expectedMode, invocation.Mode.ToString());
    }

    [Theory]
    [MemberData(nameof(OpenInvocations))]
    [Trait("Requirement", "QTC-4")]
    internal void Parse_OpenMode_ReturnsTarget(string[] args, string expectedTarget)
    {
        (CliInvocation? invocation, string? error) = CliInvocation.Parse(args);

        Assert.Null(error);
        Assert.NotNull(invocation);
        Assert.Equal(expectedTarget, invocation.OpenTarget);
    }

    [Theory]
    [MemberData(nameof(RejectedInvocations))]
    [Trait("Requirement", "QTC-1")]
    [Trait("Requirement", "QTC-4")]
    internal void Parse_InvalidInvocation_ReturnsExpectedError(string[] args, string expectedError)
    {
        (CliInvocation? invocation, string? error) = CliInvocation.Parse(args);

        Assert.Null(invocation);
        Assert.Equal(expectedError, error);
    }
}
