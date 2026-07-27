using System.Runtime.CompilerServices;
using System.Text;

namespace QueryToCsv.Tests;

internal static class TestSetup
{
    // Shift-JIS lives in the CodePages provider, which the application registers at
    // startup. Tests exercise the same encoding paths without going through Program.cs,
    // so they register it too.
    [ModuleInitializer]
    internal static void RegisterCodePages()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }
}
