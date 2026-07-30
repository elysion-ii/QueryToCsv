using System.Reflection;

namespace QueryToCsv;

internal static class ApplicationVersion
{
    private const char BuildMetadataSeparator = '+';

    internal const string ApplicationName = "QueryToCsv";

    internal static string ProductVersion
    {
        get
        {
            var informationalVersion = typeof(ApplicationVersion).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion
                ?? throw new InvalidOperationException(
                    "AssemblyInformationalVersionAttribute is required for version display.");

            var metadataIndex = informationalVersion.IndexOf(BuildMetadataSeparator);
            return metadataIndex >= 0
                ? informationalVersion[..metadataIndex]
                : informationalVersion;
        }
    }

    internal static string DisplayText => $"{ApplicationName} {ProductVersion}";
}
