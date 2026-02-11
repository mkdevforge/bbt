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

public sealed class PrViewCommand : BbtAsyncCommand<PrViewCommand.Settings>
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
        var pr = await client.GetPullRequestAsync(repoContext.Workspace, repoContext.Repo, prId);
        var view = ModelMappers.ToPullRequestView(pr);

        switch (settings.GetOutputMode())
        {
            case OutputMode.Quiet:
                OutputWriter.WriteQuiet(view.Id.ToString());
                return 0;
            case OutputMode.Json:
                await new OutputWriter(processRunner).WriteJsonAsync(view, settings);
                return 0;
            default:
                Spectre.Console.AnsiConsole.MarkupLine($"[yellow]#{view.Id}[/] {TerminalSanitizer.EscapeMarkup(view.Title)}");
                Spectre.Console.AnsiConsole.MarkupLine($"State: {TerminalSanitizer.EscapeMarkup(view.State)}");
                if (!string.IsNullOrWhiteSpace(view.HtmlUrl))
                {
                    Spectre.Console.AnsiConsole.MarkupLine($"URL: {TerminalSanitizer.EscapeMarkup(view.HtmlUrl)}");
                }

                var author = view.Author?.DisplayName ?? view.Author?.Nickname ?? view.Author?.Uuid;
                if (!string.IsNullOrWhiteSpace(author))
                {
                    Spectre.Console.AnsiConsole.MarkupLine($"Author: {TerminalSanitizer.EscapeMarkup(author)}");
                }

                Spectre.Console.AnsiConsole.MarkupLine($"Branch: {TerminalSanitizer.EscapeMarkup(view.SourceBranch ?? "?")} -> {TerminalSanitizer.EscapeMarkup(view.DestinationBranch ?? "?")}");

                var description = TerminalSanitizer.Sanitize(view.Description);
                if (!string.IsNullOrWhiteSpace(description))
                {
                    Spectre.Console.AnsiConsole.WriteLine();
                    Spectre.Console.AnsiConsole.WriteLine(description);
                }

                return 0;
        }
    }
}
