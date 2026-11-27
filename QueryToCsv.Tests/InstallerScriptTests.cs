using System.Text.RegularExpressions;
using QueryToCsv.TestSupport;
using Xunit;

namespace QueryToCsv.Tests;

// The installer is checked through its script rather than by installing: a test install
// registers under the same AppId as a real QueryToCsv installation on the machine and
// would replace or remove it, which no test may do (docs/rules/testing.md, Environment
// Independence). Each check fails when the directive that produces the behavior is
// removed or changed.
public class InstallerScriptTests
{
    private static readonly string Script = File.ReadAllText(Path.Combine(Repository.Root, "build", "Setup_QueryToCsv.iss"));

    [Fact]
    [Trait("Requirement", "QTC-27")]
    internal void Setup_InstallsPerUserWithoutAdministratorRights()
    {
        Assert.Matches(@"(?m)^DefaultDirName=\{userpf\}\\\{#MyAppName\}\s*$", Script);
        Assert.Matches(@"(?m)^PrivilegesRequired=lowest\s*$", Script);
    }

    [Fact]
    [Trait("Requirement", "QTC-27")]
    internal void Files_KeepTheOperatorsConfigurationAndFoldersOnUpgradeAndUninstall()
    {
        Assert.Matches(@"(?m)^Source: ""QueryToCsv\\appsettings\.json"";.*Flags: onlyifdoesntexist uninsneveruninstall\s*$", Script);
        Assert.Matches(@"(?m)^Name: ""\{app\}\\queries""; Flags: uninsneveruninstall\s*$", Script);
        Assert.Matches(@"(?m)^Name: ""\{app\}\\output""; Flags: uninsneveruninstall\s*$", Script);
        // The runtime folders of the build output are not shipped, so uninstall has no
        // record of logs/ and leaves it.
        Assert.Matches(@"(?m)^Source: ""QueryToCsv\\\*""; Excludes: ""appsettings\.json,logs\\\*,queries\\\*,output\\\*"";", Script);
    }

    [Fact]
    [Trait("Requirement", "QTC-27")]
    internal void Path_IsOptionalAndRewrittenOnlyForTheInstallationFolder()
    {
        Assert.Matches(@"(?m)^Name: ""addtopath"";", Script);
        string update = Procedure("UpdateUserPath");
        Assert.Contains("if Uppercase(Entry) = TargetUpper then", update, StringComparison.Ordinal);
        Assert.Contains("else if not Removed then\n    Exit;", update, StringComparison.Ordinal);
        Assert.Contains("RegWriteStringValue(HKEY_CURRENT_USER, 'Environment', 'Path', Rebuilt)", update, StringComparison.Ordinal);
        Assert.DoesNotContain("(Entry <> '')", update, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "QTC-28")]
    internal void RunningApplication_IsClosedGracefullyAndAnUnattendedRunFailsWhenItStays()
    {
        Assert.Matches(@"(?m)^CloseApplications=yes\s*$", Script);
        Assert.Matches(@"(?m)^\s*RM_SHUTDOWN_GRACEFUL = 0;", Script);
        Assert.DoesNotMatch(@"RmShutdown\([^)]*,\s*1\s*,", Script);

        string prepare = Procedure("PrepareToInstall");
        Assert.Contains("if not WizardSilent then", prepare, StringComparison.Ordinal);
        Assert.Contains("if CountRunningProcesses <= 0 then", prepare, StringComparison.Ordinal);
        Assert.Contains("if not CloseRunningProcesses then\n    Result := '", prepare, StringComparison.Ordinal);

        string uninstall = Procedure("InitializeUninstall");
        Assert.Contains("if CountRunningProcesses <= 0 then\n      Exit;", uninstall, StringComparison.Ordinal);
        Assert.Contains("Result := CloseRunningProcesses;", uninstall, StringComparison.Ordinal);
        Assert.Contains("ButtonLabels[2] := CustomMessage('AppRunningCancel');", uninstall, StringComparison.Ordinal);
    }

    // The text of one function or procedure, from its header to the end that closes it.
    private static string Procedure(string name)
    {
        Match match = Regex.Match(Script, $@"(?ms)^(function|procedure) {name}\b.*?^end;");
        Assert.True(match.Success, $"{name} is missing from the installer script.");
        return match.Value.Replace("\r\n", "\n", StringComparison.Ordinal);
    }
}
