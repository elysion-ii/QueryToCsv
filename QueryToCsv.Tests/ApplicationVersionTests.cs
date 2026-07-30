using System.Reflection;
using Xunit;

namespace QueryToCsv.Tests;

public class ApplicationVersionTests
{
    [Fact]
    public void ProductVersion_ApplicationAssembly_ReturnsInformationalVersionWithoutMetadata()
    {
        var informationalVersion = typeof(ApplicationVersion).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!
            .InformationalVersion;
        var expected = informationalVersion.Split('+', 2)[0];

        Assert.Equal(expected, ApplicationVersion.ProductVersion);
        Assert.DoesNotContain('+', ApplicationVersion.ProductVersion);
    }

    [Fact]
    public void DisplayText_ApplicationAssembly_ReturnsNameAndProductVersion()
    {
        Assert.Equal(
            $"QueryToCsv {ApplicationVersion.ProductVersion}",
            ApplicationVersion.DisplayText);
    }
}
