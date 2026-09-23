using System.ComponentModel;
using Bbt.Core.Auth;
using Bbt.Core.Bitbucket;
using Bbt.Core.Bitbucket.Models;
using Bbt.Core.Config;
using Bbt.Core.Context;
using Bbt.Core.Git;
using Bbt.Core.IO;
using Bbt.Infrastructure;
using Spectre.Cli;
using Spectre.Console;

namespace Bbt.Commands.Pr;

public sealed class PrCreateCommand : BbtAsyncCommand<PrCreateCommand.Settings>
{
    public sealed class Settings : BbtRepoSettings
    {
        [Description("Pull request title (default: subject of the latest commit on the source branch).")]
        [CommandOption("-t|--title <TEXT>")]
        public string? Title { get; init; }

        [Description("Pull request description.")]
        [CommandOption("--body <TEXT>")]
        public string? Body { get; init; }

        [Description("Read pull request description from file path.")]
        [CommandOption("--body-file <PATH>")]
        public string? BodyFile { get; init; }

        [Description("Source branch (default: current git branch). Must already be pushed to Bitbucket.")]
        [CommandOption("-s|--source <BRANCH>")]
        public string? Source { get; init; }

        [Description("Destination branch (default: repository main branch).")]
        [CommandOption("-d|--destination <BRANCH>")]
        public string? Destination { get; init; }

        [Description("Reviewer UUID ('{...}') or Atlassian account id. Repeatable.")]
        [CommandOption("-r|--reviewer <ID>")]
        public string[] Reviewers { get; init; } = [];

        [Description("Close the source branch when the pull request is merged.")]
        [CommandOption("--close-source-branch")]
        public bool CloseSourceBranch { get; init; }

        [Description("Create the pull request as a draft.")]
        [CommandOption("--draft")]
        public bool Draft { get; init; }

        public override Spectre.Cli.ValidationResult Validate()
        {
            var baseResult = base.Validate();
            if (!baseResult.Successful)
            {
                return baseResult;
            }

            if (Body is not null && BodyFile is not null)
            {
                return Spectre.Cli.ValidationResult.Error("Specify at most one of --body or --body-file.");
            }

            if (Title is not null && string.IsNullOrWhiteSpace(Title))
            {
                return Spectre.Cli.ValidationResult.Error("--title cannot be empty.");
            }

            if (Source is not null && string.IsNullOrWhiteSpace(Source))
            {
                return Spectre.Cli.ValidationResult.Error("--source cannot be empty.");
            }

            if (Destination is not null && string.IsNullOrWhiteSpace(Destination))
            {
                return Spectre.Cli.ValidationResult.Error("--destination cannot be empty.");
            }

            if (Reviewers.Any(string.IsNullOrWhiteSpace))
            {
                return Spectre.Cli.ValidationResult.Error("--reviewer cannot be empty.");
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

        var source = settings.Source?.Trim() ?? await gitClient.TryGetCurrentBranchAsync();
        if (string.IsNullOrWhiteSpace(source))
        {
            throw new InvalidOperationException("Could not determine the source branch. Use --source <BRANCH> or run on a checked-out branch.");
        }

        var title = settings.Title?.Trim() ?? await TryDeriveTitleAsync(gitClient, repoContext, source);
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new InvalidOperationException($"Could not derive a title from the latest commit on '{source}' (branch not found locally, or the current checkout is not {repoContext.Workspace}/{repoContext.Repo}). Use --title <TEXT>.");
        }

        var description = settings.Body;
        if (settings.BodyFile is not null)
        {
            description = await File.ReadAllTextAsync(settings.BodyFile);
        }

        var request = new CreatePullRequestRequest
        {
            Title = title,
            Description = description,
            Source = CreatePullRequestEndpoint.ForBranch(source),
            Destination = settings.Destination is null ? null : CreatePullRequestEndpoint.ForBranch(settings.Destination.Trim()),
            Reviewers = settings.Reviewers.Length == 0 ? null : settings.Reviewers.Select(CreatePullRequestReviewer.FromIdentifier).ToList(),
            CloseSourceBranch = settings.CloseSourceBranch ? true : null,
            Draft = settings.Draft ? true : null,
        };

        var auth = await AuthContextResolver.ResolveAsync(configStore, credentialStore, profileOverride: null, requireToken: true);
        using var client = AuthContextResolver.CreateClient(auth, settings.Verbose, settings.NoRetry);

        var created = await client.CreatePullRequestAsync(repoContext.Workspace, repoContext.Repo, request);
        var view = ModelMappers.ToPullRequestView(created);

        switch (settings.GetOutputMode())
        {
            case OutputMode.Quiet:
                OutputWriter.WriteQuiet(view.Id.ToString());
                return 0;
            case OutputMode.Json:
                await new OutputWriter(processRunner).WriteJsonAsync(view, settings);
                return 0;
            default:
                Spectre.Console.AnsiConsole.MarkupLine($"Created pull request [yellow]#{view.Id}[/] {TerminalSanitizer.EscapeMarkup(view.Title)}");
                Spectre.Console.AnsiConsole.MarkupLine($"Branch: {TerminalSanitizer.EscapeMarkup(view.SourceBranch ?? source)} -> {TerminalSanitizer.EscapeMarkup(view.DestinationBranch ?? "?")}");
                if (!string.IsNullOrWhiteSpace(view.HtmlUrl))
                {
                    Spectre.Console.AnsiConsole.MarkupLine($"URL: {TerminalSanitizer.EscapeMarkup(view.HtmlUrl)}");
                }

                return 0;
        }
    }

    private static async Task<string?> TryDeriveTitleAsync(GitClient gitClient, ResolvedRepoContext repoContext, string source)
    {
        try
        {
            // Local history only describes the PR when this checkout is the target repository.
            var origin = await gitClient.TryGetOriginUrlAsync();
            if (origin is null ||
                !BitbucketRemoteParser.TryParse(origin, out var workspace, out var repo) ||
                !workspace.Equals(repoContext.Workspace, StringComparison.OrdinalIgnoreCase) ||
                !repo.Equals(repoContext.Repo, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            // Prefer the pushed tip so unpushed local commits cannot supply the title.
            return await gitClient.TryGetCommitSubjectAsync($"refs/remotes/origin/{source}")
                ?? await gitClient.TryGetCommitSubjectAsync(source);
        }
        catch (Win32Exception)
        {
            // git is not installed.
            return null;
        }
    }
}
