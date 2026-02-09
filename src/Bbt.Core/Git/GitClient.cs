using Bbt.Core.IO;

namespace Bbt.Core.Git;

public sealed class GitClient
{
    private readonly ProcessRunner _processRunner;

    public GitClient(ProcessRunner processRunner)
    {
        _processRunner = processRunner;
    }

    public async Task<bool> IsInsideWorkTreeAsync(CancellationToken cancellationToken = default)
    {
        var result = await _processRunner.RunAsync("git", ["rev-parse", "--is-inside-work-tree"], cancellationToken: cancellationToken);
        return result.ExitCode == 0 && result.Stdout.Trim().Equals("true", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<string?> TryGetOriginUrlAsync(CancellationToken cancellationToken = default)
    {
        var result = await _processRunner.RunAsync("git", ["remote", "get-url", "origin"], cancellationToken: cancellationToken);
        if (result.ExitCode != 0)
        {
            return null;
        }

        var value = result.Stdout.Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    public async Task<string?> TryGetCurrentBranchAsync(CancellationToken cancellationToken = default)
    {
        var result = await _processRunner.RunAsync("git", ["rev-parse", "--abbrev-ref", "HEAD"], cancellationToken: cancellationToken);
        if (result.ExitCode != 0)
        {
            return null;
        }

        var value = result.Stdout.Trim();
        if (string.IsNullOrWhiteSpace(value) || value.Equals("HEAD", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return value;
    }
}

