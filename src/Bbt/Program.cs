using Spectre.Cli;
using Bbt.Commands.Api;
using Bbt.Commands.Auth;
using Bbt.Commands.Llms;
using Bbt.Commands.Pr;
using Bbt.Infrastructure;

var app = new CommandApp();

app.Configure(config =>
{
    config.SetApplicationName("bbt");

    config.AddBranch("auth", auth =>
    {
        auth.SetDescription("Authentication and profiles.");
        auth.AddCommand<AuthLoginCommand>("login").WithDescription($"Log in and store credentials (interactive by default). Minimum token scopes: {BitbucketTokenScopes.MinimumScopesHelp}");
        auth.AddCommand<AuthSwitchCommand>("switch").WithDescription("Switch the active profile.");
        auth.AddCommand<AuthStatusCommand>("status").WithDescription("Show current profile and credential status.");
        auth.AddCommand<AuthLogoutCommand>("logout").WithDescription("Remove token and profile data.");
    });

    config.AddBranch("pr", pr =>
    {
        pr.SetDescription("Pull request operations.");
        pr.AddCommand<PrListCommand>("list").WithDescription("List pull requests (default: --state OPEN).");
        pr.AddCommand<PrViewCommand>("view").WithDescription("View pull request details (id inferred from current branch if omitted).");
        pr.AddCommand<PrDiffCommand>("diff").WithDescription("Show pull request diff (raw in human mode, structured in --json; id inferred from current branch if omitted).");
        pr.AddCommand<PrCommentsCommand>("comments").WithDescription("List pull request comments (default: newest-first, one page unless --paginate/--limit requires more; id inferred from current branch if omitted).");
        pr.AddCommand<PrThreadsCommand>("threads").WithDescription("List pull request comment threads (root + replies; ordered by discovery sort (default: -created_on); id inferred from current branch if omitted).");
        pr.AddCommand<PrCommentCommand>("comment").WithDescription("Post a pull request comment (global/inline/reply; inline default: --side to).");
        pr.AddCommand<PrReviewCommand>("review").WithDescription("Set pull request review status (approve/request changes; --body posts global comment first).");
    });

    config.AddCommand<ApiCommand>("api").WithDescription("Raw Bitbucket API access (accepts either `<PATH> <METHOD>` or `<METHOD> <PATH>`).");
    config.AddCommand<LlmsCommand>("llms").WithDescription("Print full CLI capabilities in one output for LLM/tool context.");
});

return await app.RunAsync(args);
