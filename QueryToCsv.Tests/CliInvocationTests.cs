using Xunit;

namespace QueryToCsv.Tests;

public class CliInvocationTests
{
    public static TheoryData<string[], string> Modes() => new()
    {
        { [], "Interactive" },
        { ["-h"], "Help" },
        { ["--help"], "Help" },
        { ["-V"], "Version" },
        { ["--version"], "Version" },
        { ["--open", "queries"], "Open" },
        { ["--open=output"], "Open" },
        { ["--query", "SELECT 1"], "OneLiner" },
    };

    public static TheoryData<string[], string> RejectedInvocations() => new()
    {
        { ["--open"], "option '--open' requires a value." },
        { ["--open="], "option '--open' requires a value." },
        { ["--open", "queries", "extra"], "unexpected argument 'extra'" },
        { ["-v"], "unknown option '-v'" },
        { ["-q", "SELECT 1"], "unknown option '-q'" },
        { ["version"], "unknown option 'version'" },
    };

    public static TheoryData<string[], string> OpenInvocations() => new()
    {
        { ["--open", "queries"], "queries" },
        { ["--open=queries"], "queries" },
        { ["--open=report.csv"], "report.csv" },
    };

    [Theory]
    [MemberData(nameof(Modes))]
    public void Parse_KnownMode_ReturnsExpectedMode(string[] args, string expectedMode)
    {
        var (invocation, error) = CliInvocation.Parse(args);

        Assert.Null(error);
        Assert.NotNull(invocation);
        Assert.Equal(expectedMode, invocation.Mode.ToString());
    }

    [Theory]
    [MemberData(nameof(OpenInvocations))]
    public void Parse_OpenMode_ReturnsTarget(string[] args, string expectedTarget)
    {
        var (invocation, error) = CliInvocation.Parse(args);

        Assert.Null(error);
        Assert.NotNull(invocation);
        Assert.Equal(expectedTarget, invocation.OpenTarget);
    }

    [Theory]
    [MemberData(nameof(RejectedInvocations))]
    public void Parse_InvalidInvocation_ReturnsExpectedError(string[] args, string expectedError)
    {
        var (invocation, error) = CliInvocation.Parse(args);

        Assert.Null(invocation);
        Assert.Equal(expectedError, error);
    }
}
