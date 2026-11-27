using System.ComponentModel;
using System.Runtime.InteropServices;

namespace QueryToCsv;

// Reads generic credentials from Windows Credential Manager for the account running the
// process, so a SQL Server password never has to sit in a configuration file
// (docs/specs/QueryToCsv.md, QTC-8).
internal static partial class CredentialStore
{
    private const uint GenericCredentialType = 1;
    private const int ElementNotFound = 1168;

    internal sealed record Secret(string UserName, string Password);

    // The credential stored under the target name, or null when none exists.
    internal static Secret? Read(string targetName)
    {
        if (!CredRead(targetName, GenericCredentialType, 0, out nint handle))
        {
            int error = Marshal.GetLastPInvokeError();
            if (error == ElementNotFound)
                return null;

            throw new Win32Exception(error);
        }

        try
        {
            NativeCredential credential = Marshal.PtrToStructure<NativeCredential>(handle);
            string userName = Marshal.PtrToStringUni(credential.UserName) ?? "";
            string password = credential.CredentialBlobSize == 0
                ? ""
                : Marshal.PtrToStringUni(credential.CredentialBlob, (int)credential.CredentialBlobSize / sizeof(char));
            return new Secret(userName, password);
        }
        finally
        {
            CredFree(handle);
        }
    }

    [LibraryImport("advapi32.dll", EntryPoint = "CredReadW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CredRead(string targetName, uint type, uint flags, out nint credential);

    [LibraryImport("advapi32.dll", EntryPoint = "CredFree")]
    private static partial void CredFree(nint buffer);

    // CREDENTIALW from wincred.h; only the fields read here matter, the rest keep the layout.
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeCredential
    {
        internal uint Flags;
        internal uint Type;
        internal nint TargetName;
        internal nint Comment;
        internal long LastWritten;
        internal uint CredentialBlobSize;
        internal nint CredentialBlob;
        internal uint Persist;
        internal uint AttributeCount;
        internal nint Attributes;
        internal nint TargetAlias;
        internal nint UserName;
    }
}
