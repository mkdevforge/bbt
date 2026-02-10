using System.ComponentModel;
using Bbt.Core.Auth;
using Bbt.Core.Bitbucket;
using Bbt.Core.Config;
using Bbt.Core.Context;
using Bbt.Core.Git;
using Bbt.Core.IO;
using Bbt.Infrastructure;
using Spectre.Cli;
using Spectre.Console;

namespace Bbt.Commands.Pr;

public sealed class PrCommentsCommand : BbtAsyncCommand<PrCommentsCommand.Settings>
{
    public sealed class Settings : BbtSettings
    {
        [Description("Pull request id (optional; inferred from current branch if omitted).")]
        [CommandArgument(0, "[ID]")]
        public int? Id { get; init; }

        [Description("Maximum number of comments to return.")]
        [CommandOption("--limit <N>")]
        public int? Limit { get; init; }
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

        var comments = await BitbucketPager.GetAllAsync(
            async (next, ct) => await client.ListPullRequestCommentsAsync(repoContext.Workspace, repoContext.Repo, prId, pageUrl: next, cancellationToken: ct),
            limit: settings.Limit,
            cancellationToken: default);

        var items = comments.Select(ModelMappers.ToPullRequestComment).ToList();

        switch (settings.GetOutputMode())
        {
            case OutputMode.Quiet:
                OutputWriter.WriteQuietLines(items.Select(c => c.Id.ToString()));
                return 0;
            case OutputMode.Json:
                await new OutputWriter(processRunner).WriteJsonAsync(items, settings);
                return 0;
            default:
                foreach (var c in items)
                {
                    var who = c.User?.DisplayName ?? c.User?.Nickname ?? c.User?.Uuid ?? "unknown";
                    var where = c.Inline?.Path is null
                        ? ""
                        : $" ({c.Inline.Path}:{c.Inline.StartTo ?? c.Inline.StartFrom ?? c.Inline.To ?? c.Inline.From}-{c.Inline.To ?? c.Inline.From})";
                    Spectre.Console.AnsiConsole.MarkupLine($"[yellow]#{c.Id}[/] {Markup.Escape(who)}{Markup.Escape(where)}");
                    if (!string.IsNullOrWhiteSpace(c.Body))
                    {
                        Spectre.Console.AnsiConsole.WriteLine(c.Body);
                    }

                    Spectre.Console.AnsiConsole.WriteLine();
                }

                return 0;
        }
    }
}
