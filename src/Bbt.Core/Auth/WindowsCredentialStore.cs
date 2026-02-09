using System.Runtime.InteropServices;
using System.Text;

namespace Bbt.Core.Auth;

public sealed class WindowsCredentialStore : ICredentialStore
{
    public string Description => "Windows Credential Manager";

    public Task StoreTokenAsync(string profile, string token, CancellationToken cancellationToken = default)
    {
        EnsureWindows();

        var targetName = GetTargetName(profile);
        var byteArray = Encoding.Unicode.GetBytes(token);
        var blobPtr = Marshal.AllocCoTaskMem(byteArray.Length);
        Marshal.Copy(byteArray, 0, blobPtr, byteArray.Length);

        var credential = new NativeCredential
        {
            Type = 1, // CRED_TYPE_GENERIC
            TargetName = targetName,
            CredentialBlobSize = (uint)byteArray.Length,
            CredentialBlob = blobPtr,
            Persist = 2, // CRED_PERSIST_LOCAL_MACHINE
            UserName = profile,
        };

        try
        {
            if (!CredWrite(ref credential, 0))
            {
                throw new InvalidOperationException($"CredWrite failed: {Marshal.GetLastWin32Error()}");
            }

            return Task.CompletedTask;
        }
        finally
        {
            if (credential.CredentialBlob != IntPtr.Zero)
            {
                Marshal.FreeCoTaskMem(credential.CredentialBlob);
            }
        }
    }

    public Task<string?> GetTokenAsync(string profile, CancellationToken cancellationToken = default)
    {
        EnsureWindows();

        var targetName = GetTargetName(profile);
        if (!CredRead(targetName, 1, 0, out var credPtr))
        {
            return Task.FromResult<string?>(null);
        }

        try
        {
            var cred = Marshal.PtrToStructure<NativeCredential>(credPtr);
            if (cred.CredentialBlob == IntPtr.Zero || cred.CredentialBlobSize == 0)
            {
                return Task.FromResult<string?>(null);
            }

            var bytes = new byte[cred.CredentialBlobSize];
            Marshal.Copy(cred.CredentialBlob, bytes, 0, (int)cred.CredentialBlobSize);
            var token = Encoding.Unicode.GetString(bytes).TrimEnd('\0');
            return Task.FromResult<string?>(string.IsNullOrWhiteSpace(token) ? null : token);
        }
        finally
        {
            CredFree(credPtr);
        }
    }

    public Task DeleteTokenAsync(string profile, CancellationToken cancellationToken = default)
    {
        EnsureWindows();

        var targetName = GetTargetName(profile);
        CredDelete(targetName, 1, 0);
        return Task.CompletedTask;
    }

    private static void EnsureWindows()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("WindowsCredentialStore can only be used on Windows.");
        }
    }

    private static string GetTargetName(string profile) => $"bbt:{profile}";

    [DllImport("Advapi32.dll", SetLastError = true, EntryPoint = "CredWriteW", CharSet = CharSet.Unicode)]
    private static extern bool CredWrite([In] ref NativeCredential userCredential, [In] uint flags);

    [DllImport("Advapi32.dll", SetLastError = true, EntryPoint = "CredReadW", CharSet = CharSet.Unicode)]
    private static extern bool CredRead(string target, uint type, uint reservedFlag, out IntPtr credentialPtr);

    [DllImport("Advapi32.dll", SetLastError = true, EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode)]
    private static extern bool CredDelete(string target, uint type, uint flags);

    [DllImport("Advapi32.dll", SetLastError = true)]
    private static extern void CredFree([In] IntPtr buffer);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NativeCredential
    {
        public uint Flags;
        public uint Type;
        public string TargetName;
        public string Comment;
        public long LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        public string TargetAlias;
        public string UserName;
    }
}
