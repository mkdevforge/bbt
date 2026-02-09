using System.Text.RegularExpressions;

namespace Bbt.Core.Git;

public static partial class BitbucketRemoteParser
{
    public static bool TryParse(string originUrl, out string workspace, out string repo)
    {
        workspace = string.Empty;
        repo = string.Empty;

        if (string.IsNullOrWhiteSpace(originUrl))
        {
            return false;
        }

        // HTTPS/SSH URLs handled by Uri.
        if (Uri.TryCreate(originUrl, UriKind.Absolute, out var uri))
        {
            if (!uri.Host.EndsWith("bitbucket.org", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var segments = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length < 2)
            {
                return false;
            }

            workspace = segments[0];
            repo = TrimGit(segments[1]);
            return !(string.IsNullOrWhiteSpace(workspace) || string.IsNullOrWhiteSpace(repo));
        }

        // SCP-like SSH: git@bitbucket.org:workspace/repo.git
        var match = SshScpLikeRegex().Match(originUrl.Trim());
        if (!match.Success)
        {
            return false;
        }

        workspace = match.Groups["workspace"].Value;
        repo = TrimGit(match.Groups["repo"].Value);
        return !(string.IsNullOrWhiteSpace(workspace) || string.IsNullOrWhiteSpace(repo));
    }

    private static string TrimGit(string value)
    {
        return value.EndsWith(".git", StringComparison.OrdinalIgnoreCase) ? value[..^4] : value;
    }

    [GeneratedRegex(@"^[^@]+@bitbucket\.org:(?<workspace>[^/]+)/(?<repo>.+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex SshScpLikeRegex();
}

