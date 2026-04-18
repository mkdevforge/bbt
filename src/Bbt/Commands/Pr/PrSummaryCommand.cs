using System.ComponentModel;
using Bbt.Core.Auth;
using Bbt.Core.Bitbucket;
using Bbt.Core.Config;
using Bbt.Core.Context;
using Bbt.Core.Diff;
using Bbt.Core.Git;
using Bbt.Core.IO;
using Bbt.Infrastructure;
using Spectre.Cli;
using Spectre.Console;

namespace Bbt.Commands.Pr;

public sealed class PrSummaryCommand : BbtAsyncCommand<PrSummaryCommand.Settings>
{
    public sealed class Settings : BbtRepoSettings
    {
        [Description("Pull request id (optional; inferred from current branch if omitted).")]
        [CommandArgument(0, "[ID]")]
        public int? Id { get; init; }
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
        var prTask = client.GetPullRequestAsync(repoContext.Workspace, repoContext.Repo, prId);
        var rawDiffTask = client.GetPullRequestDiffAsync(repoContext.Workspace, repoContext.Repo, prId);

        await Task.WhenAll(prTask, rawDiffTask);

        var pr = await prTask;
        var files = UnifiedDiffParser.Parse(await rawDiffTask);
        var diffStats = PullRequestDiffStatsCalculator.Calculate(files);
        var mergedAt = await ResolveMergedAtAsync(client, repoContext.Workspace, repoContext.Repo, pr);
        var summary = ModelMappers.ToPullRequestSummary(pr, repoContext.Workspace, repoContext.Repo, diffStats, mergedAt);

        switch (settings.GetOutputMode())
        {
            case OutputMode.Quiet:
                OutputWriter.WriteQuiet(summary.PrId.ToString());
                return 0;
            case OutputMode.Json:
                await new OutputWriter(processRunner).WriteJsonAsync(summary, settings);
                return 0;
            default:
                Spectre.Console.AnsiConsole.MarkupLine($"[yellow]#{summary.PrId}[/] {TerminalSanitizer.EscapeMarkup(summary.Title)}");
                Spectre.Console.AnsiConsole.MarkupLine($"State: {TerminalSanitizer.EscapeMarkup(summary.State)}");
                Spectre.Console.AnsiConsole.MarkupLine($"Branches: {TerminalSanitizer.EscapeMarkup(summary.SourceBranch ?? "?")} -> {TerminalSanitizer.EscapeMarkup(summary.TargetBranch ?? "?")}");
                Spectre.Console.AnsiConsole.MarkupLine($"Reviews: {summary.Approvals} approvals, {summary.ChangesRequested} changes requested");
                Spectre.Console.AnsiConsole.MarkupLine($"Comments: {summary.CommentCount}");
                Spectre.Console.AnsiConsole.MarkupLine($"Diff: {summary.FilesChanged} files, +{summary.LinesAdded} / -{summary.LinesRemoved}");

                if (summary.OpenedAt is not null)
                {
                    Spectre.Console.AnsiConsole.MarkupLine($"Opened: {TerminalSanitizer.EscapeMarkup(summary.OpenedAt.Value.ToString("u"))}");
                }

                if (summary.MergedAt is not null)
                {
                    Spectre.Console.AnsiConsole.MarkupLine($"Merged: {TerminalSanitizer.EscapeMarkup(summary.MergedAt.Value.ToString("u"))}");
                }
                else if (string.Equals(summary.State, "MERGED", StringComparison.OrdinalIgnoreCase))
                {
                    Spectre.Console.AnsiConsole.MarkupLine("Merged: unavailable");
                }

                if (!string.IsNullOrWhiteSpace(summary.HtmlUrl))
                {
                    Spectre.Console.AnsiConsole.MarkupLine($"URL: {TerminalSanitizer.EscapeMarkup(summary.HtmlUrl)}");
                }

                return 0;
        }
    }

    private static async Task<DateTimeOffset?> ResolveMergedAtAsync(
        BitbucketClient client,
        string workspace,
        string repo,
        Bbt.Core.Bitbucket.Models.BitbucketPullRequest pr)
    {
        if (!string.Equals(pr.State, "MERGED", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        // Bitbucket's PR object does not expose a dedicated merged timestamp in the fields we use,
        // so prefer the activity log's MERGED state transition over guessing from updated_on.
        var activities = await BitbucketPager.GetAllAsync(
            async (next, ct) => await client.ListPullRequestActivityAsync(workspace, repo, pr.Id, pageLen: 100, pageUrl: next, cancellationToken: ct),
            limit: null,
            cancellationToken: default);

        return PullRequestActivityAnalyzer.TryGetMergedAt(activities);
    }
}
