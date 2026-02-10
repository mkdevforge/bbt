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

public sealed class PrCommentCommand : BbtAsyncCommand<PrCommentCommand.Settings>
{
    public sealed class Settings : BbtSettings
    {
        [Description("Target pull request id.")]
        [CommandArgument(0, "<ID>")]
        public int Id { get; init; }

        [Description("Comment body text.")]
        [CommandOption("--body <TEXT>")]
        public string? Body { get; init; }

        [Description("Read comment body from file path.")]
        [CommandOption("--body-file <PATH>")]
        public string? BodyFile { get; init; }

        [Description("Inline file path; requires --line.")]
        [CommandOption("--file <PATH>")]
        public string? File { get; init; }

        [Description("Inline line number (1-based).")]
        [CommandOption("--line <N>")]
        public int? Line { get; init; }

        [Description("Optional inline range end line (1-based).")]
        [CommandOption("--line-end <N>")]
        public int? LineEnd { get; init; }

        [Description("Inline side: 'to' (new) or 'from' (old).")]
        [CommandOption("--side <SIDE>")]
        public string Side { get; init; } = "to";

        public override Spectre.Cli.ValidationResult Validate()
        {
            var baseResult = base.Validate();
            if (!baseResult.Successful)
            {
                return baseResult;
            }

            var hasBody = !string.IsNullOrWhiteSpace(Body);
            var hasBodyFile = !string.IsNullOrWhiteSpace(BodyFile);
            if (hasBody == hasBodyFile)
            {
                return Spectre.Cli.ValidationResult.Error("Specify exactly one of --body or --body-file.");
            }

            var hasFile = !string.IsNullOrWhiteSpace(File);
            var hasLine = Line is not null;
            if (hasFile != hasLine)
            {
                return Spectre.Cli.ValidationResult.Error("--file and --line must be provided together for inline comments.");
            }

            if (LineEnd is not null && Line is null)
            {
                return Spectre.Cli.ValidationResult.Error("--line-end requires --line.");
            }

            if (Line is not null && Line <= 0)
            {
                return Spectre.Cli.ValidationResult.Error("--line must be >= 1.");
            }

            if (LineEnd is not null && LineEnd <= 0)
            {
                return Spectre.Cli.ValidationResult.Error("--line-end must be >= 1.");
            }

            if (LineEnd is not null && Line is not null && LineEnd < Line)
            {
                return Spectre.Cli.ValidationResult.Error("--line-end must be >= --line.");
            }

            if (!Side.Equals("to", StringComparison.OrdinalIgnoreCase) && !Side.Equals("from", StringComparison.OrdinalIgnoreCase))
            {
                return Spectre.Cli.ValidationResult.Error("--side must be 'to' or 'from'.");
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

        var body = settings.Body;
        if (body is null)
        {
            body = await File.ReadAllTextAsync(settings.BodyFile!);
        }

        var request = new CreatePullRequestCommentRequest
        {
            Content = new CreatePullRequestCommentContent { Raw = body },
        };

        if (!string.IsNullOrWhiteSpace(settings.File))
        {
            request.Inline = CreateInline(settings);
        }

        var created = await client.CreatePullRequestCommentAsync(repoContext.Workspace, repoContext.Repo, settings.Id, request);
        var output = ModelMappers.ToPullRequestComment(created);

        switch (settings.GetOutputMode())
        {
            case OutputMode.Quiet:
                OutputWriter.WriteQuiet(created.Id.ToString());
                return 0;
            case OutputMode.Json:
                await new OutputWriter(processRunner).WriteJsonAsync(output, settings);
                return 0;
            default:
                var link = string.IsNullOrWhiteSpace(output.HtmlUrl) ? string.Empty : $" {output.HtmlUrl}";
                Spectre.Console.AnsiConsole.MarkupLine($"Posted comment [yellow]#{output.Id}[/].{Markup.Escape(link)}");
                return 0;
        }
    }

    private static CreatePullRequestCommentInline CreateInline(Settings settings)
    {
        var inline = new CreatePullRequestCommentInline { Path = settings.File! };

        var isFrom = settings.Side.Equals("from", StringComparison.OrdinalIgnoreCase);
        if (isFrom)
        {
            inline.From = settings.Line;
            if (settings.LineEnd is not null)
            {
                inline.StartFrom = settings.Line;
                inline.From = settings.LineEnd;
            }

            return inline;
        }

        inline.To = settings.Line;
        if (settings.LineEnd is not null)
        {
            inline.StartTo = settings.Line;
            inline.To = settings.LineEnd;
        }

        return inline;
    }
}
