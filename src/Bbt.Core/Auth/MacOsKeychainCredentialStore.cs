using System.Runtime.InteropServices;
using System.Text;

namespace Bbt.Core.Auth;

public sealed class MacOsKeychainCredentialStore : ICredentialStore
{
    private const string ServiceName = "bbt";
    private const string SecurityLibrary = "/System/Library/Frameworks/Security.framework/Security";
    private const string CoreFoundationLibrary = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
    private const uint CfStringEncodingUtf8 = 0x08000100;

    private const int ErrSecSuccess = 0;
    private const int ErrSecItemNotFound = -25300;
    private const int ErrSecDuplicateItem = -25299;

    public string Description => "macOS Keychain";

    public Task StoreTokenAsync(string profile, string token, CancellationToken cancellationToken = default)
    {
        EnsureMacOs();

        var serviceBytes = Encoding.UTF8.GetBytes(ServiceName);
        var accountBytes = Encoding.UTF8.GetBytes(profile);
        var passwordBytes = Encoding.UTF8.GetBytes(token);

        TryModifyExistingOrThrow(serviceBytes, accountBytes, passwordBytes, out var foundExisting);
        if (foundExisting)
        {
            return Task.CompletedTask;
        }

        for (var attempt = 0; attempt < 2; attempt++)
        {
            var addStatus = SecKeychainAddGenericPassword(
                IntPtr.Zero,
                (uint)serviceBytes.Length,
                serviceBytes,
                (uint)accountBytes.Length,
                accountBytes,
                (uint)passwordBytes.Length,
                passwordBytes,
                out var addedItem);

            try
            {
                if (addStatus == ErrSecSuccess)
                {
                    return Task.CompletedTask;
                }

                if (addStatus != ErrSecDuplicateItem)
                {
                    throw new InvalidOperationException($"Failed to store token in Keychain: {FormatStatus(addStatus)}");
                }
            }
            finally
            {
                if (addedItem != IntPtr.Zero)
                {
                    CFRelease(addedItem);
                }
            }

            TryModifyExistingOrThrow(serviceBytes, accountBytes, passwordBytes, out foundExisting);
            if (foundExisting)
            {
                return Task.CompletedTask;
            }
        }

        throw new InvalidOperationException("Failed to store token in Keychain due to repeated duplicate item errors.");
    }

    public Task<string?> GetTokenAsync(string profile, CancellationToken cancellationToken = default)
    {
        EnsureMacOs();

        var serviceBytes = Encoding.UTF8.GetBytes(ServiceName);
        var accountBytes = Encoding.UTF8.GetBytes(profile);

        var status = SecKeychainFindGenericPassword(
            IntPtr.Zero,
            (uint)serviceBytes.Length,
            serviceBytes,
            (uint)accountBytes.Length,
            accountBytes,
            out var passwordLength,
            out var passwordData,
            out var itemRef);

        try
        {
            if (status == ErrSecItemNotFound)
            {
                return Task.FromResult<string?>(null);
            }

            if (status != ErrSecSuccess)
            {
                throw new InvalidOperationException($"Failed to read token from Keychain: {FormatStatus(status)}");
            }

            if (passwordData == IntPtr.Zero || passwordLength == 0)
            {
                return Task.FromResult<string?>(null);
            }

            var bytes = new byte[passwordLength];
            Marshal.Copy(passwordData, bytes, 0, (int)passwordLength);
            var token = Encoding.UTF8.GetString(bytes);
            return Task.FromResult<string?>(string.IsNullOrWhiteSpace(token) ? null : token);
        }
        finally
        {
            if (passwordData != IntPtr.Zero)
            {
                SecKeychainItemFreeContent(IntPtr.Zero, passwordData);
            }

            if (itemRef != IntPtr.Zero)
            {
                CFRelease(itemRef);
            }
        }
    }

    public Task DeleteTokenAsync(string profile, CancellationToken cancellationToken = default)
    {
        EnsureMacOs();

        var serviceBytes = Encoding.UTF8.GetBytes(ServiceName);
        var accountBytes = Encoding.UTF8.GetBytes(profile);

        var status = SecKeychainFindGenericPassword(
            IntPtr.Zero,
            (uint)serviceBytes.Length,
            serviceBytes,
            (uint)accountBytes.Length,
            accountBytes,
            out var _,
            out var passwordData,
            out var itemRef);

        try
        {
            if (status == ErrSecItemNotFound)
            {
                return Task.CompletedTask;
            }

            if (status != ErrSecSuccess)
            {
                throw new InvalidOperationException($"Failed to locate Keychain item for deletion: {FormatStatus(status)}");
            }

            var deleteStatus = SecKeychainItemDelete(itemRef);
            if (deleteStatus != ErrSecSuccess && deleteStatus != ErrSecItemNotFound)
            {
                throw new InvalidOperationException($"Failed to delete token from Keychain: {FormatStatus(deleteStatus)}");
            }

            return Task.CompletedTask;
        }
        finally
        {
            if (passwordData != IntPtr.Zero)
            {
                SecKeychainItemFreeContent(IntPtr.Zero, passwordData);
            }

            if (itemRef != IntPtr.Zero)
            {
                CFRelease(itemRef);
            }
        }
    }

    private static void TryModifyExistingOrThrow(byte[] serviceBytes, byte[] accountBytes, byte[] passwordBytes, out bool foundExisting)
    {
        foundExisting = false;

        var status = SecKeychainFindGenericPassword(
            IntPtr.Zero,
            (uint)serviceBytes.Length,
            serviceBytes,
            (uint)accountBytes.Length,
            accountBytes,
            out var _,
            out var passwordData,
            out var itemRef);

        try
        {
            if (status == ErrSecItemNotFound)
            {
                foundExisting = false;
                return;
            }

            if (status != ErrSecSuccess)
            {
                throw new InvalidOperationException($"Failed to look up existing Keychain item: {FormatStatus(status)}");
            }

            foundExisting = true;

            var modifyStatus = SecKeychainItemModifyAttributesAndData(itemRef, IntPtr.Zero, (uint)passwordBytes.Length, passwordBytes);
            if (modifyStatus != ErrSecSuccess)
            {
                throw new InvalidOperationException($"Failed to update token in Keychain: {FormatStatus(modifyStatus)}");
            }
        }
        finally
        {
            if (passwordData != IntPtr.Zero)
            {
                SecKeychainItemFreeContent(IntPtr.Zero, passwordData);
            }

            if (itemRef != IntPtr.Zero)
            {
                CFRelease(itemRef);
            }
        }
    }

    private static void EnsureMacOs()
    {
        if (!OperatingSystem.IsMacOS())
        {
            throw new PlatformNotSupportedException("MacOsKeychainCredentialStore can only be used on macOS.");
        }
    }

    private static string FormatStatus(int status)
    {
        var message = TryGetStatusMessage(status);
        return message is null ? status.ToString() : $"{status} ({message})";
    }

    private static string? TryGetStatusMessage(int status)
    {
        var cfString = SecCopyErrorMessageString(status, IntPtr.Zero);
        if (cfString == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            return CfStringToString(cfString);
        }
        finally
        {
            CFRelease(cfString);
        }
    }

    private static string CfStringToString(IntPtr cfString)
    {
        var length = CFStringGetLength(cfString);
        var maxSize = CFStringGetMaximumSizeForEncoding(length, CfStringEncodingUtf8) + 1;
        var buffer = Marshal.AllocHGlobal((int)maxSize);

        try
        {
            if (!CFStringGetCString(cfString, buffer, maxSize, CfStringEncodingUtf8))
            {
                return string.Empty;
            }

            return Marshal.PtrToStringUTF8(buffer) ?? string.Empty;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    [DllImport(SecurityLibrary)]
    private static extern IntPtr SecCopyErrorMessageString(int status, IntPtr reserved);

    [DllImport(SecurityLibrary)]
    private static extern int SecKeychainFindGenericPassword(
        IntPtr keychainOrArray,
        uint serviceNameLength,
        byte[] serviceName,
        uint accountNameLength,
        byte[] accountName,
        out uint passwordLength,
        out IntPtr passwordData,
        out IntPtr itemRef);

    [DllImport(SecurityLibrary)]
    private static extern int SecKeychainAddGenericPassword(
        IntPtr keychain,
        uint serviceNameLength,
        byte[] serviceName,
        uint accountNameLength,
        byte[] accountName,
        uint passwordLength,
        byte[] passwordData,
        out IntPtr itemRef);

    [DllImport(SecurityLibrary)]
    private static extern int SecKeychainItemModifyAttributesAndData(
        IntPtr itemRef,
        IntPtr attrList,
        uint length,
        byte[] data);

    [DllImport(SecurityLibrary)]
    private static extern int SecKeychainItemDelete(IntPtr itemRef);

    [DllImport(SecurityLibrary)]
    private static extern int SecKeychainItemFreeContent(IntPtr attrList, IntPtr data);

    [DllImport(CoreFoundationLibrary)]
    private static extern void CFRelease(IntPtr cf);

    [DllImport(CoreFoundationLibrary)]
    private static extern nint CFStringGetLength(IntPtr theString);

    [DllImport(CoreFoundationLibrary)]
    private static extern nint CFStringGetMaximumSizeForEncoding(nint length, uint encoding);

    [DllImport(CoreFoundationLibrary)]
    private static extern bool CFStringGetCString(IntPtr theString, IntPtr buffer, nint bufferSize, uint encoding);
}
