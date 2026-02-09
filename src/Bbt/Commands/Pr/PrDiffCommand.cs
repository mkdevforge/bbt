using Bbt.Core.Auth;
using Bbt.Core.Bitbucket;
using Bbt.Core.Config;
using Bbt.Core.Context;
using Bbt.Core.Diff;
using Bbt.Core.Git;
using Bbt.Core.IO;
using Bbt.Infrastructure;
using Spectre.Cli;

namespace Bbt.Commands.Pr;

public sealed class PrDiffCommand : BbtAsyncCommand<PrDiffCommand.Settings>
{
    public sealed class Settings : BbtSettings
    {
        [CommandArgument(0, "[ID]")]
        public int? Id { get; init; }

        [CommandOption("--include-raw")]
        public bool IncludeRaw { get; init; }
    }

    protected override async Task<int> ExecuteCommandAsync(CommandContext context, Settings settings)
    {
        var processRunner = new ProcessRunner();
        var credentialStore = CredentialStoreFactory.CreateDefault(processRunner);
        var configStore = new BbtConfigStore();
        var gitClient = new GitClient(processRunner);
        var repoResolver = new RepoContextResolver(configStore, gitClient);

        var repoContext = await repoResolver.TryResolveAsync(settings.Workspace, settings.Repo, profileOverride: null);
        if (repoContext is null)
        {
            throw new InvalidOperationException("Could not resolve workspace/repo. Use --workspace/--repo, set BBT_WORKSPACE/BBT_REPO, or run inside a git repo with a Bitbucket origin remote.");
        }

        ResolvedContextReporter.LogRepoContext(settings, repoContext);

        var auth = await AuthContextResolver.ResolveAsync(configStore, credentialStore, profileOverride: null, requireToken: true);
        using var client = AuthContextResolver.CreateClient(auth, settings.Verbose, settings.NoRetry);

        var prId = await PullRequestIdResolver.ResolveAsync(client, gitClient, repoContext.Workspace, repoContext.Repo, settings.Id);
        var rawDiff = await client.GetPullRequestDiffAsync(repoContext.Workspace, repoContext.Repo, prId);

        switch (settings.GetOutputMode())
        {
            case OutputMode.Json:
                var files = UnifiedDiffParser.Parse(rawDiff);
                var model = new PullRequestDiff(
                    PullRequestId: prId,
                    Workspace: repoContext.Workspace,
                    Repo: repoContext.Repo,
                    Files: files,
                    RawDiff: settings.IncludeRaw ? rawDiff : null);
                await new OutputWriter(processRunner).WriteJsonAsync(model, settings);
                return 0;
            case OutputMode.Quiet:
                return 0;
            default:
                Console.Out.Write(rawDiff);
                return 0;
        }
    }
}
