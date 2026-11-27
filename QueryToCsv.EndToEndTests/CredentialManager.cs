using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace QueryToCsv.EndToEndTests;

// A generic credential in Windows Credential Manager that exists for one test and is
// deleted afterwards, the way an operator stores one with cmdkey.
internal sealed partial class CredentialManager : IDisposable
{
    private const uint GenericCredentialType = 1;
    private const uint PersistSession = 1;

    internal CredentialManager(string userName, string password)
    {
        TargetName = $"QueryToCsvEndToEnd/{Guid.NewGuid():N}";
        byte[] blob = Encoding.Unicode.GetBytes(password);
        nint target = Marshal.StringToCoTaskMemUni(TargetName);
        nint user = Marshal.StringToCoTaskMemUni(userName);
        nint secret = Marshal.AllocCoTaskMem(blob.Length);
        try
        {
            Marshal.Copy(blob, 0, secret, blob.Length);
            NativeCredential credential = new()
            {
                Type = GenericCredentialType,
                TargetName = target,
                CredentialBlobSize = (uint)blob.Length,
                CredentialBlob = secret,
                Persist = PersistSession,
                UserName = user,
            };
            if (!CredWrite(in credential, 0))
                throw new Win32Exception(Marshal.GetLastPInvokeError());
        }
        finally
        {
            Marshal.FreeCoTaskMem(target);
            Marshal.FreeCoTaskMem(user);
            Marshal.FreeCoTaskMem(secret);
        }
    }

    internal string TargetName { get; }

    public void Dispose() => CredDelete(TargetName, GenericCredentialType, 0);

    [LibraryImport("advapi32.dll", EntryPoint = "CredWriteW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CredWrite(in NativeCredential credential, uint flags);

    [LibraryImport("advapi32.dll", EntryPoint = "CredDeleteW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CredDelete(string targetName, uint type, uint flags);

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
