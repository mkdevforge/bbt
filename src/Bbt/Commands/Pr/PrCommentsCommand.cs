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
    public sealed class Settings : BbtRepoSettings
    {
        [Description("Pull request id (optional; inferred from current branch if omitted).")]
        [CommandArgument(0, "[ID]")]
        public int? Id { get; init; }

        [Description("Maximum number of comments to return.")]
        [CommandOption("--limit <N>")]
        public int? Limit { get; init; }

        [Description("Bitbucket sort expression (e.g. -created_on). Default: -created_on.")]
        [CommandOption("--sort <EXPR>")]
        public string Sort { get; init; } = "-created_on";

        [Description("Bitbucket page number (1-based). If set without --paginate, returns that page only.")]
        [CommandOption("--page <N>")]
        public int? Page { get; init; }

        [Description("Bitbucket page length (1-100). Default: 50; 100 when --paginate is used; min(--limit,100) when --limit is set.")]
        [CommandOption("--pagelen <N>")]
        public int? PageLen { get; init; }

        [Description("Follow paginated responses and return all matching comments (bounded by --limit if set).")]
        [CommandOption("--paginate")]
        public bool Paginate { get; init; }

        [Description("Filter comments containing text (server-side). Equivalent to q=content.raw~\"TEXT\".")]
        [CommandOption("--contains <TEXT>")]
        public string? Contains { get; init; }

        [Description("Bitbucket q expression (server-side filtering).")]
        [CommandOption("-q|--query <EXPR>")]
        public string? Query { get; init; }

        public override Spectre.Cli.ValidationResult Validate()
        {
            var baseResult = base.Validate();
            if (!baseResult.Successful)
            {
                return baseResult;
            }

            if (Limit is not null && Limit <= 0)
            {
                return Spectre.Cli.ValidationResult.Error("--limit must be >= 1.");
            }

            if (Page is not null && Page <= 0)
            {
                return Spectre.Cli.ValidationResult.Error("--page must be >= 1.");
            }

            if (PageLen is not null && (PageLen < 1 || PageLen > 100))
            {
                return Spectre.Cli.ValidationResult.Error("--pagelen must be between 1 and 100.");
            }

            if (!string.IsNullOrWhiteSpace(Contains) && !string.IsNullOrWhiteSpace(Query))
            {
                return Spectre.Cli.ValidationResult.Error("Use either --contains or -q/--query, not both.");
            }

            return Spectre.Cli.ValidationResult.Success();
        }
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

        var q = BuildQuery(settings.Contains, settings.Query);
        var pageLen = ResolvePageLen(settings.PageLen, settings.Limit, settings.Paginate);
        var sort = string.IsNullOrWhiteSpace(settings.Sort) ? null : settings.Sort.Trim();

        var comments = await ListCommentsAsync(
            client,
            repoContext.Workspace,
            repoContext.Repo,
            prId,
            pageLen,
            settings.Page,
            sort,
            q,
            settings.Paginate,
            settings.Limit,
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
                    var replyTo = c.ParentId is null ? string.Empty : $" reply-to #{c.ParentId}";
                    var where = c.Inline?.Path is null
                        ? ""
                        : $" ({c.Inline.Path}:{c.Inline.StartTo ?? c.Inline.StartFrom ?? c.Inline.To ?? c.Inline.From}-{c.Inline.To ?? c.Inline.From})";
                    Spectre.Console.AnsiConsole.MarkupLine($"[yellow]#{c.Id}[/] {TerminalSanitizer.EscapeMarkup(who)}{TerminalSanitizer.EscapeMarkup(where)}{TerminalSanitizer.EscapeMarkup(replyTo)}");

                    var body = TerminalSanitizer.Sanitize(c.Body);
                    if (!string.IsNullOrWhiteSpace(body))
                    {
                        Spectre.Console.AnsiConsole.WriteLine(body);
                    }

                    Spectre.Console.AnsiConsole.WriteLine();
                }

                return 0;
        }
    }

    private static async Task<List<Bbt.Core.Bitbucket.Models.BitbucketComment>> ListCommentsAsync(
        BitbucketClient client,
        string workspace,
        string repo,
        int pullRequestId,
        int pageLen,
        int? page,
        string? sort,
        string? q,
        bool paginate,
        int? limit,
        CancellationToken cancellationToken)
    {
        // If --page is set without --paginate: fetch that page only. Limit applies within that page.
        if (page is not null && !paginate)
        {
            var single = await client.ListPullRequestCommentsAsync(
                workspace,
                repo,
                pullRequestId,
                pageLen: pageLen,
                page: page,
                sort: sort,
                q: q,
                cancellationToken: cancellationToken);
            return limit is null ? single.Values : single.Values.Take(limit.Value).ToList();
        }

        // Default behavior: 1 request for 1 page when not paginating and no limit is set.
        if (!paginate && limit is null)
        {
            var single = await client.ListPullRequestCommentsAsync(
                workspace,
                repo,
                pullRequestId,
                pageLen: pageLen,
                page: page,
                sort: sort,
                q: q,
                cancellationToken: cancellationToken);
            return single.Values;
        }

        // Paginate only when explicitly requested or when needed to satisfy --limit (when --page is not set).
        var shouldPageForLimit = !paginate && page is null && limit is not null && limit.Value > pageLen;
        if (!paginate && !shouldPageForLimit)
        {
            var single = await client.ListPullRequestCommentsAsync(
                workspace,
                repo,
                pullRequestId,
                pageLen: pageLen,
                page: page,
                sort: sort,
                q: q,
                cancellationToken: cancellationToken);
            return limit is null ? single.Values : single.Values.Take(limit.Value).ToList();
        }

        var results = new List<Bbt.Core.Bitbucket.Models.BitbucketComment>();
        string? next = null;
        var remaining = limit;
        var first = true;

        while (true)
        {
            var pageResult = await client.ListPullRequestCommentsAsync(
                workspace,
                repo,
                pullRequestId,
                pageLen: pageLen,
                page: first ? page : null,
                sort: first ? sort : null,
                q: first ? q : null,
                pageUrl: next,
                cancellationToken: cancellationToken);
            first = false;

            foreach (var item in pageResult.Values)
            {
                results.Add(item);
                if (remaining is not null)
                {
                    remaining--;
                    if (remaining <= 0)
                    {
                        return results;
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(pageResult.Next))
            {
                return results;
            }

            if (!paginate && remaining is null)
            {
                // Should not happen, but be safe: without explicit pagination and without a limit, stop after one page.
                return results;
            }

            next = pageResult.Next;
        }
    }

    private static int ResolvePageLen(int? explicitPageLen, int? limit, bool paginate)
    {
        if (explicitPageLen is not null)
        {
            return explicitPageLen.Value;
        }

        if (limit is not null)
        {
            return Math.Min(limit.Value, 100);
        }

        return paginate ? 100 : 50;
    }

    private static string? BuildQuery(string? contains, string? query)
    {
        if (!string.IsNullOrWhiteSpace(query))
        {
            return query.Trim();
        }

        if (string.IsNullOrWhiteSpace(contains))
        {
            return null;
        }

        var text = EscapeBitbucketQStringLiteral(contains.Trim());
        return $"content.raw~\"{text}\"";
    }

    private static string EscapeBitbucketQStringLiteral(string input)
    {
        return input
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\t", "\\t", StringComparison.Ordinal);
    }
}
