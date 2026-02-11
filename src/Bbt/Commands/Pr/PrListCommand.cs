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

public sealed class PrListCommand : BbtAsyncCommand<PrListCommand.Settings>
{
    public sealed class Settings : BbtRepoSettings
    {
        [Description("PR state filter (OPEN, MERGED, DECLINED, SUPERSEDED). Default: OPEN.")]
        [CommandOption("--state <STATE>")]
        public string State { get; init; } = "OPEN";

        [Description("Maximum number of pull requests to return.")]
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

        var prs = await BitbucketPager.GetAllAsync(
            async (next, ct) => await client.ListPullRequestsAsync(repoContext.Workspace, repoContext.Repo, state: settings.State, pageUrl: next, cancellationToken: ct),
            limit: settings.Limit,
            cancellationToken: default);

        var items = prs.Select(ModelMappers.ToPullRequestListItem).ToList();

        switch (settings.GetOutputMode())
        {
            case OutputMode.Quiet:
                OutputWriter.WriteQuietLines(items.Select(i => i.Id.ToString()));
                return 0;
            case OutputMode.Json:
                await new OutputWriter(processRunner).WriteJsonAsync(items, settings);
                return 0;
            default:
                var table = new Table().Border(TableBorder.Simple);
                table.AddColumn("ID");
                table.AddColumn("Title");
                table.AddColumn("Author");
                table.AddColumn("Branch");
                table.AddColumn("Updated");

                foreach (var pr in items)
                {
                    var author = pr.Author?.DisplayName ?? pr.Author?.Nickname ?? pr.Author?.Uuid ?? "";
                    var branch = $"{pr.SourceBranch ?? "?"} -> {pr.DestinationBranch ?? "?"}";
                    var updated = pr.UpdatedOn?.ToString("u") ?? "";
                    table.AddRow(
                        TerminalSanitizer.EscapeMarkup(pr.Id.ToString()),
                        TerminalSanitizer.EscapeMarkup(pr.Title),
                        TerminalSanitizer.EscapeMarkup(author),
                        TerminalSanitizer.EscapeMarkup(branch),
                        TerminalSanitizer.EscapeMarkup(updated));
                }

                Spectre.Console.AnsiConsole.Render(table);
                return 0;
        }
    }
}
