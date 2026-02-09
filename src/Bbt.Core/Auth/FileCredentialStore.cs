using System.Runtime.Versioning;

namespace Bbt.Core.Auth;

public sealed class FileCredentialStore : ICredentialStore
{
    private readonly string _tokenDirectory;

    public FileCredentialStore(string tokenDirectory)
    {
        _tokenDirectory = tokenDirectory;
    }

    public string Description => "Token file (fallback)";

    public async Task StoreTokenAsync(string profile, string token, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_tokenDirectory);
        var path = Path.Combine(_tokenDirectory, $"{Sanitize(profile)}.token");
        await File.WriteAllTextAsync(path, token, cancellationToken);

        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            TrySet0600(path);
        }
    }

    public async Task<string?> GetTokenAsync(string profile, CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(_tokenDirectory, $"{Sanitize(profile)}.token");
        if (!File.Exists(path))
        {
            return null;
        }

        var token = (await File.ReadAllTextAsync(path, cancellationToken)).Trim();
        return string.IsNullOrWhiteSpace(token) ? null : token;
    }

    public Task DeleteTokenAsync(string profile, CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(_tokenDirectory, $"{Sanitize(profile)}.token");
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    private static string Sanitize(string profile)
    {
        return profile.Replace(Path.DirectorySeparatorChar, '_').Replace(Path.AltDirectorySeparatorChar, '_');
    }

    [SupportedOSPlatform("linux")]
    [SupportedOSPlatform("macos")]
    private static void TrySet0600(string path)
    {
        try
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        catch
        {
            // Best-effort only.
        }
    }
}
