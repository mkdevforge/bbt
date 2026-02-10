using System.ComponentModel;
using Bbt.Core.Auth;
using Bbt.Core.Bitbucket;
using Bbt.Core.Bitbucket.Models;
using Bbt.Core.Config;
using Bbt.Core.Context;
using Bbt.Core.Git;
using Bbt.Core.IO;
using Bbt.Infrastructure;
using Bbt.Models;
using Spectre.Cli;
using Spectre.Console;

namespace Bbt.Commands.Pr;

public sealed class PrReviewCommand : BbtAsyncCommand<PrReviewCommand.Settings>
{
    public sealed class Settings : BbtRepoSettings
    {
        [Description("Target pull request id.")]
        [CommandArgument(0, "<ID>")]
        public int Id { get; init; }

        [Description("Approve the pull request.")]
        [CommandOption("--approve")]
        public bool Approve { get; init; }

        [Description("Remove your approval.")]
        [CommandOption("--unapprove")]
        public bool Unapprove { get; init; }

        [Description("Request changes on the pull request.")]
        [CommandOption("--request-changes")]
        public bool RequestChanges { get; init; }

        [Description("Remove your request for changes.")]
        [CommandOption("--unrequest-changes")]
        public bool UnrequestChanges { get; init; }

        [Description("Optional global review comment body (posted before review action).")]
        [CommandOption("--body <TEXT>")]
        public string? Body { get; init; }

        [Description("Read optional global review comment body from file (posted before review action).")]
        [CommandOption("--body-file <PATH>")]
        public string? BodyFile { get; init; }

        public override Spectre.Cli.ValidationResult Validate()
        {
            var baseResult = base.Validate();
            if (!baseResult.Successful)
            {
                return baseResult;
            }

            var actions = new[] { Approve, Unapprove, RequestChanges, UnrequestChanges }.Count(x => x);
            if (actions != 1)
            {
                return Spectre.Cli.ValidationResult.Error("Specify exactly one of --approve, --unapprove, --request-changes, --unrequest-changes.");
            }

            if (!string.IsNullOrWhiteSpace(Body) && !string.IsNullOrWhiteSpace(BodyFile))
            {
                return Spectre.Cli.ValidationResult.Error("Specify at most one of --body or --body-file.");
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

        int? commentId = null;
        if (!string.IsNullOrWhiteSpace(settings.Body) || !string.IsNullOrWhiteSpace(settings.BodyFile))
        {
            var body = settings.Body ?? await File.ReadAllTextAsync(settings.BodyFile!);
            var comment = await client.CreatePullRequestCommentAsync(
                repoContext.Workspace,
                repoContext.Repo,
                settings.Id,
                new CreatePullRequestCommentRequest { Content = new CreatePullRequestCommentContent { Raw = body } });
            commentId = comment.Id;
        }

        BitbucketParticipant? participant = null;
        string action;

        if (settings.Approve)
        {
            action = "approve";
            participant = await client.ApprovePullRequestAsync(repoContext.Workspace, repoContext.Repo, settings.Id);
        }
        else if (settings.Unapprove)
        {
            action = "unapprove";
            await client.UnapprovePullRequestAsync(repoContext.Workspace, repoContext.Repo, settings.Id);
        }
        else if (settings.RequestChanges)
        {
            action = "requestChanges";
            participant = await client.RequestChangesAsync(repoContext.Workspace, repoContext.Repo, settings.Id);
        }
        else
        {
            action = "unrequestChanges";
            await client.UnrequestChangesAsync(repoContext.Workspace, repoContext.Repo, settings.Id);
        }

        var output = new
        {
            pullRequestId = settings.Id,
            action,
            commentId,
            participant = participant is null
                ? null
                : new
                {
                    user = ModelMappers.ToUserSummary(participant.User),
                    role = participant.Role,
                    approved = participant.Approved,
                    state = participant.State,
                    participatedOn = participant.ParticipatedOn,
                }
        };

        switch (settings.GetOutputMode())
        {
            case OutputMode.Json:
                await new OutputWriter(processRunner).WriteJsonAsync(output, settings);
                return 0;
            case OutputMode.Quiet:
                return 0;
            default:
                var commentPart = commentId is null ? string.Empty : $" (comment #{commentId})";
                Spectre.Console.AnsiConsole.MarkupLine($"PR [yellow]#{settings.Id}[/]: {Markup.Escape(action)}{Markup.Escape(commentPart)}");
                return 0;
        }
    }
}
