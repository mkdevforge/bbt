using System.Runtime.InteropServices;
using Bbt.Core.Config;
using Bbt.Core.IO;

namespace Bbt.Core.Auth;

public static class CredentialStoreFactory
{
    public static ICredentialStore CreateDefault(ProcessRunner processRunner)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return new MacOsKeychainCredentialStore();
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return new WindowsCredentialStore();
        }

        if (processRunner.IsOnPath("secret-tool"))
        {
            return new LinuxSecretToolCredentialStore(processRunner);
        }

        return new FileCredentialStore(BbtPaths.GetTokenDirectory());
    }
}
