using System.ComponentModel;
using Bbt.Core.Auth;
using Bbt.Core.Bitbucket;
using Bbt.Core.Config;
using Bbt.Core.Context;
using Bbt.Core.Git;
using Bbt.Core.IO;
using Bbt.Infrastructure;
using Bbt.Models;
using Spectre.Cli;
using Spectre.Console;

namespace Bbt.Commands.Pr;

public sealed class PrThreadsCommand : BbtAsyncCommand<PrThreadsCommand.Settings>
{
    public sealed class Settings : BbtRepoSettings
    {
        [Description("Pull request id (optional; inferred from current branch if omitted).")]
        [CommandArgument(0, "[ID]")]
        public int? Id { get; init; }

        [Description("Maximum number of threads to return. Default: 20.")]
        [CommandOption("--limit <N>")]
        public int Limit { get; init; } = 20;

        [Description("Bitbucket sort expression for thread discovery (e.g. -created_on). Default: -created_on.")]
        [CommandOption("--sort <EXPR>")]
        public string Sort { get; init; } = "-created_on";

        [Description("Filter threads where any comment contains text (server-side discovery).")]
        [CommandOption("--contains <TEXT>")]
        public string? Contains { get; init; }

        [Description("Bitbucket q expression for discovery (server-side filtering).")]
        [CommandOption("-q|--query <EXPR>")]
        public string? Query { get; init; }

        [Description("Bitbucket page length (1-100). Default: 100.")]
        [CommandOption("--pagelen <N>")]
        public int PageLen { get; init; } = 100;

        public override Spectre.Cli.ValidationResult Validate()
        {
            var baseResult = base.Validate();
            if (!baseResult.Successful)
            {
                return baseResult;
            }

            if (Limit <= 0)
            {
                return Spectre.Cli.ValidationResult.Error("--limit must be >= 1.");
            }

            if (PageLen is < 1 or > 100)
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

        var discoverySort = string.IsNullOrWhiteSpace(settings.Sort) ? "-created_on" : settings.Sort.Trim();
        var discoveryQuery = BuildDiscoveryQuery(settings.Contains, settings.Query);

        // When using a filter (--contains/-q) or a custom discovery sort, use a discovery listing to establish
        // the ordering of "anchor" comments that define which threads we want and in which order.
        // Thread contents are always fetched by scanning unfiltered newest-first.
        var needDiscoveryAnchors = discoveryQuery is not null || !string.Equals(discoverySort, "-created_on", StringComparison.Ordinal);
        var anchorIds = needDiscoveryAnchors
            ? await FetchAnchorCommentIdsAsync(
                client,
                repoContext.Workspace,
                repoContext.Repo,
                prId,
                sort: discoverySort,
                q: discoveryQuery,
                pageLen: settings.PageLen,
                maxAnchors: Math.Min(Math.Max(settings.Limit * 50, settings.Limit), 500),
                cancellationToken: CancellationToken.None)
            : null;

        if (anchorIds is { Count: 0 })
        {
            return await WriteThreadsAsync(processRunner, settings, []);
        }

        // We can't fetch "root + replies" with a server-side query because Bitbucket's PR comments endpoint
        // doesn't support filtering on parent.id. Instead:
        // - If no filter was requested: scan newest-first until we can resolve `--limit` distinct thread roots.
        // - If a filter was requested: use server-side filtering only to select "anchor" comments, then scan
        //   unfiltered newest-first until we can resolve `--limit` distinct thread roots that contain anchors.
        var scan = await ScanForThreadsAsync(
            client,
            repoContext.Workspace,
            repoContext.Repo,
            prId,
            limit: settings.Limit,
            pageLen: settings.PageLen,
            anchorIdsInOrder: anchorIds,
            cancellationToken: CancellationToken.None);

        var threads = BuildThreads(scan.SelectedRootIds, scan.SelectedRootIdSet, scan.FetchedComments, scan.ParentIdById);
        return await WriteThreadsAsync(processRunner, settings, threads);
    }

    private static async Task<int> WriteThreadsAsync(ProcessRunner processRunner, Settings settings, List<PullRequestCommentThread> threads)
    {
        switch (settings.GetOutputMode())
        {
            case OutputMode.Quiet:
                OutputWriter.WriteQuietLines(threads.Select(t => t.RootId.ToString()));
                return 0;
            case OutputMode.Json:
                await new OutputWriter(processRunner).WriteJsonAsync(threads, settings);
                return 0;
            default:
                foreach (var thread in threads)
                {
                    WriteThreadHuman(thread);

                    Spectre.Console.AnsiConsole.WriteLine();
                }

                return 0;
        }
    }

    private static void WriteThreadHuman(PullRequestCommentThread thread)
    {
        WriteCommentBlock(thread.Root, indent: 0);

        var childrenByParent = thread.Replies
            .Where(r => r.ParentId is not null)
            .GroupBy(r => r.ParentId!.Value)
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(c => c.CreatedOn ?? DateTimeOffset.MinValue).ToList());

        WriteChildren(thread.Root.Id, depth: 1);

        void WriteChildren(long parentId, int depth)
        {
            if (!childrenByParent.TryGetValue(parentId, out var children))
            {
                return;
            }

            foreach (var child in children)
            {
                WriteCommentBlock(child, indent: depth * 2);
                WriteChildren(child.Id, depth + 1);
            }
        }
    }

    private static void WriteCommentBlock(PullRequestComment comment, int indent)
    {
        var prefix = indent <= 0 ? string.Empty : new string(' ', indent);
        var who = comment.User?.DisplayName ?? comment.User?.Nickname ?? comment.User?.Uuid ?? "unknown";
        var replyTo = comment.ParentId is null ? string.Empty : $" reply-to #{comment.ParentId}";
        var where = comment.Inline?.Path is null
            ? ""
            : $" ({comment.Inline.Path}:{comment.Inline.StartTo ?? comment.Inline.StartFrom ?? comment.Inline.To ?? comment.Inline.From}-{comment.Inline.To ?? comment.Inline.From})";

        Spectre.Console.AnsiConsole.MarkupLine($"{prefix}[yellow]#{comment.Id}[/] {TerminalSanitizer.EscapeMarkup(who)}{TerminalSanitizer.EscapeMarkup(where)}{TerminalSanitizer.EscapeMarkup(replyTo)}");

        var body = TerminalSanitizer.Sanitize(comment.Body);
        if (!string.IsNullOrWhiteSpace(body))
        {
            foreach (var line in body.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
            {
                Spectre.Console.AnsiConsole.WriteLine($"{prefix}{line}");
            }
        }
    }

    private sealed record ThreadScanResult(
        List<long> SelectedRootIds,
        HashSet<long> SelectedRootIdSet,
        List<Bbt.Core.Bitbucket.Models.BitbucketComment> FetchedComments,
        Dictionary<long, long?> ParentIdById);

    private static async Task<List<long>> FetchAnchorCommentIdsAsync(
        BitbucketClient client,
        string workspace,
        string repo,
        int pullRequestId,
        string sort,
        string? q,
        int pageLen,
        int maxAnchors,
        CancellationToken cancellationToken)
    {
        var anchors = new List<long>(capacity: Math.Min(maxAnchors, 256));
        string? next = null;

        while (anchors.Count < maxAnchors)
        {
            var page = await client.ListPullRequestCommentsAsync(
                workspace,
                repo,
                pullRequestId,
                pageLen: pageLen,
                sort: sort,
                q: q,
                pageUrl: next,
                cancellationToken: cancellationToken);

            foreach (var c in page.Values)
            {
                anchors.Add(c.Id);
                if (anchors.Count >= maxAnchors)
                {
                    break;
                }
            }

            if (anchors.Count >= maxAnchors || string.IsNullOrWhiteSpace(page.Next))
            {
                break;
            }

            next = page.Next;
        }

        return anchors;
    }

    private static async Task<ThreadScanResult> ScanForThreadsAsync(
        BitbucketClient client,
        string workspace,
        string repo,
        int pullRequestId,
        int limit,
        int pageLen,
        List<long>? anchorIdsInOrder,
        CancellationToken cancellationToken)
    {
        var candidateIds = anchorIdsInOrder is null ? new List<long>() : new List<long>(anchorIdsInOrder);
        var candidateIndex = 0;

        var fetched = new List<Bbt.Core.Bitbucket.Models.BitbucketComment>();
        var parentIdById = new Dictionary<long, long?>();

        var selectedRoots = new List<long>();
        var selectedRootSet = new HashSet<long>();

        string? next = null;
        while (selectedRoots.Count < limit)
        {
            // No more candidates available (filtered mode).
            if (anchorIdsInOrder is not null && candidateIndex >= candidateIds.Count)
            {
                break;
            }

            var page = await client.ListPullRequestCommentsAsync(
                workspace,
                repo,
                pullRequestId,
                pageLen: pageLen,
                sort: "-created_on",
                pageUrl: next,
                cancellationToken: cancellationToken);

            foreach (var c in page.Values)
            {
                fetched.Add(c);
                parentIdById[c.Id] = c.Parent?.Id;
                if (anchorIdsInOrder is null)
                {
                    candidateIds.Add(c.Id);
                }
            }

            while (selectedRoots.Count < limit && candidateIndex < candidateIds.Count)
            {
                var candidateId = candidateIds[candidateIndex];
                if (!TryResolveRootId(candidateId, parentIdById, out var rootId))
                {
                    break;
                }

                if (selectedRootSet.Add(rootId))
                {
                    selectedRoots.Add(rootId);
                }

                candidateIndex++;
            }

            if (selectedRoots.Count >= limit || string.IsNullOrWhiteSpace(page.Next))
            {
                break;
            }

            next = page.Next;
        }

        return new ThreadScanResult(selectedRoots, selectedRootSet, fetched, parentIdById);
    }

    private static bool TryResolveRootId(long commentId, Dictionary<long, long?> parentIdById, out long rootId)
    {
        rootId = default;
        var current = commentId;
        var guard = 0;

        while (true)
        {
            // Guard against pathological cycles (shouldn't happen, but avoid infinite loops).
            if (++guard > 256)
            {
                return false;
            }

            if (!parentIdById.TryGetValue(current, out var parentId))
            {
                return false;
            }

            if (parentId is null)
            {
                rootId = current;
                return true;
            }

            current = parentId.Value;
        }
    }

    private static List<PullRequestCommentThread> BuildThreads(
        List<long> selectedRootIds,
        HashSet<long> selectedRootIdSet,
        List<Bbt.Core.Bitbucket.Models.BitbucketComment> fetchedComments,
        Dictionary<long, long?> parentIdById)
    {
        var byRoot = new Dictionary<long, List<Bbt.Core.Bitbucket.Models.BitbucketComment>>();
        foreach (var c in fetchedComments)
        {
            if (!TryResolveRootId(c.Id, parentIdById, out var rootId))
            {
                continue;
            }

            if (!selectedRootIdSet.Contains(rootId))
            {
                continue;
            }

            if (!byRoot.TryGetValue(rootId, out var list))
            {
                list = [];
                byRoot[rootId] = list;
            }

            list.Add(c);
        }

        var threads = new List<PullRequestCommentThread>();
        foreach (var rootId in selectedRootIds)
        {
            if (!byRoot.TryGetValue(rootId, out var list))
            {
                continue;
            }

            var root = list.FirstOrDefault(c => c.Parent is null && c.Id == rootId) ?? list.FirstOrDefault(c => c.Id == rootId);
            if (root is null)
            {
                continue;
            }

            var rootModel = ModelMappers.ToPullRequestComment(root);
            var replies = list
                .Where(c => c.Id != rootId)
                .OrderBy(c => c.CreatedOn ?? DateTimeOffset.MinValue)
                .Select(ModelMappers.ToPullRequestComment)
                .ToList();

            var lastActivityOn = list
                .Select(c => c.UpdatedOn ?? c.CreatedOn)
                .Where(x => x is not null)
                .Select(x => x!.Value)
                .DefaultIfEmpty()
                .Max();

            threads.Add(new PullRequestCommentThread(
                RootId: rootId,
                Root: rootModel,
                Replies: replies,
                LastActivityOn: lastActivityOn == default ? null : lastActivityOn));
        }

        return threads;
    }

    private static string? BuildDiscoveryQuery(string? contains, string? query)
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
