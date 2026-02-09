using Bbt.Core.Config;
using Bbt.Core.Git;
using Bbt.Core.Util;

namespace Bbt.Core.Context;

public sealed class RepoContextResolver
{
    private readonly BbtConfigStore _configStore;
    private readonly GitClient _gitClient;

    public RepoContextResolver(BbtConfigStore configStore, GitClient gitClient)
    {
        _configStore = configStore;
        _gitClient = gitClient;
    }

    public async Task<ResolvedRepoContext?> TryResolveAsync(
        string? workspaceOverride,
        string? repoOverride,
        string? profileOverride,
        CancellationToken cancellationToken = default)
    {
        string? workspace = string.IsNullOrWhiteSpace(workspaceOverride) ? null : workspaceOverride;
        string? repo = string.IsNullOrWhiteSpace(repoOverride) ? null : repoOverride;
        var sources = new List<string>();

        if (workspace is not null)
        {
            sources.Add("workspace:cli");
        }

        if (repo is not null)
        {
            sources.Add("repo:cli");
        }

        if (workspace is null && BbtEnvironment.TryGetNonEmpty("BBT_WORKSPACE", out var envWorkspace))
        {
            workspace = envWorkspace;
            sources.Add("workspace:env");
        }

        if (repo is null && BbtEnvironment.TryGetNonEmpty("BBT_REPO", out var envRepo))
        {
            repo = envRepo;
            sources.Add("repo:env");
        }

        if (workspace is not null && repo is not null)
        {
            return new ResolvedRepoContext(workspace, repo, Source: string.Join(" ", sources));
        }

        var config = await _configStore.LoadAsync(cancellationToken);
        var profileName = profileOverride ?? config.CurrentProfile;
        if (config.Profiles.TryGetValue(profileName, out var profile))
        {
            if (workspace is null && !string.IsNullOrWhiteSpace(profile.DefaultWorkspace))
            {
                workspace = profile.DefaultWorkspace;
                sources.Add($"workspace:profile:{profileName}");
            }

            if (repo is null && !string.IsNullOrWhiteSpace(profile.DefaultRepo))
            {
                repo = profile.DefaultRepo;
                sources.Add($"repo:profile:{profileName}");
            }
        }

        if (workspace is not null && repo is not null)
        {
            return new ResolvedRepoContext(workspace, repo, Source: string.Join(" ", sources));
        }

        if (!await _gitClient.IsInsideWorkTreeAsync(cancellationToken))
        {
            return null;
        }

        var origin = await _gitClient.TryGetOriginUrlAsync(cancellationToken);
        if (origin is null)
        {
            return null;
        }

        if (BitbucketRemoteParser.TryParse(origin, out var ws, out var parsedRepo))
        {
            if (workspace is null)
            {
                workspace = ws;
                sources.Add("workspace:git");
            }

            if (repo is null)
            {
                repo = parsedRepo;
                sources.Add("repo:git");
            }
        }

        if (workspace is null || repo is null)
        {
            return null;
        }

        return new ResolvedRepoContext(workspace, repo, Source: string.Join(" ", sources));
    }
}
