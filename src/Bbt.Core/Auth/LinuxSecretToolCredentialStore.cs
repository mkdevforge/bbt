using Bbt.Core.IO;

namespace Bbt.Core.Auth;

public sealed class LinuxSecretToolCredentialStore : ICredentialStore
{
    private const string Label = "bbt";
    private readonly ProcessRunner _processRunner;

    public LinuxSecretToolCredentialStore(ProcessRunner processRunner)
    {
        _processRunner = processRunner;
    }

    public string Description => "libsecret (secret-tool)";

    public async Task StoreTokenAsync(string profile, string token, CancellationToken cancellationToken = default)
    {
        var result = await _processRunner.RunAsync(
            "secret-tool",
            ["store", "--label", Label, "service", Label, "profile", profile],
            stdin: token,
            cancellationToken: cancellationToken);

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"Failed to store token in secret-tool: {result.Stderr.Trim()}");
        }
    }

    public async Task<string?> GetTokenAsync(string profile, CancellationToken cancellationToken = default)
    {
        var result = await _processRunner.RunAsync(
            "secret-tool",
            ["lookup", "service", Label, "profile", profile],
            cancellationToken: cancellationToken);

        if (result.ExitCode != 0)
        {
            return null;
        }

        var token = result.Stdout.Trim();
        return string.IsNullOrWhiteSpace(token) ? null : token;
    }

    public async Task DeleteTokenAsync(string profile, CancellationToken cancellationToken = default)
    {
        var result = await _processRunner.RunAsync(
            "secret-tool",
            ["clear", "service", Label, "profile", profile],
            cancellationToken: cancellationToken);

        if (result.ExitCode != 0)
        {
            var stderr = result.Stderr.Trim();
            if (stderr.Contains("No items found", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            throw new InvalidOperationException($"Failed to delete token from secret-tool: {stderr}");
        }
    }
}

