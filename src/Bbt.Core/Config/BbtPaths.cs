using System.Runtime.InteropServices;

namespace Bbt.Core.Config;

public static class BbtPaths
{
    public static string GetConfigDirectory()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "bbt");
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            var appSupport = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appSupport, "bbt");
        }

        var xdg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        if (!string.IsNullOrWhiteSpace(xdg))
        {
            return Path.Combine(xdg, "bbt");
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".config", "bbt");
    }

    public static string GetConfigFilePath()
    {
        return Path.Combine(GetConfigDirectory(), "config.json");
    }

    public static string GetTokenDirectory()
    {
        return Path.Combine(GetConfigDirectory(), "tokens");
    }

    public static string GetTokenFilePath(string profile)
    {
        var safeName = profile.Replace(Path.DirectorySeparatorChar, '_').Replace(Path.AltDirectorySeparatorChar, '_');
        return Path.Combine(GetTokenDirectory(), $"{safeName}.token");
    }
}

