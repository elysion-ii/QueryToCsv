using System.Reflection;
using Xunit;

namespace QueryToCsv.Tests;

public class ApplicationVersionTests
{
    [Fact]
    internal void ProductVersion_ApplicationAssembly_ReturnsInformationalVersionWithoutMetadata()
    {
        string informationalVersion = typeof(ApplicationVersion).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!
            .InformationalVersion;
        string expected = informationalVersion.Split('+', 2)[0];

        Assert.Equal(expected, ApplicationVersion.ProductVersion);
        Assert.DoesNotContain('+', ApplicationVersion.ProductVersion);
    }

    [Fact]
    internal void DisplayText_ApplicationAssembly_ReturnsNameAndProductVersion()
    {
        Assert.Equal(
            $"QueryToCsv {ApplicationVersion.ProductVersion}",
            ApplicationVersion.DisplayText);
    }
}
