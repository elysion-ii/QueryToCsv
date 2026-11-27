using System.Runtime.CompilerServices;
using System.Text;

namespace QueryToCsv.Tests;

internal static class TestSetup
{
    // The application makes both settings at startup, and tests exercise the same paths
    // without going through Program.cs: Shift-JIS lives in the CodePages provider, and the
    // native SNI library is not shipped, so SqlClient must use its managed networking.
    [ModuleInitializer]
    internal static void ApplyStartupSettings()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        AppContext.SetSwitch("Switch.Microsoft.Data.SqlClient.UseManagedNetworkingOnWindows", true);
    }
}
