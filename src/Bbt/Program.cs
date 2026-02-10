using Spectre.Cli;
using Bbt.Commands.Api;
using Bbt.Commands.Auth;
using Bbt.Commands.Pr;

var app = new CommandApp();

app.Configure(config =>
{
    config.SetApplicationName("bbt");

    config.AddBranch("auth", auth =>
    {
        auth.SetDescription("Authentication and profiles.");
        auth.AddCommand<AuthLoginCommand>("login").WithDescription("Log in and store credentials in a profile.");
        auth.AddCommand<AuthSwitchCommand>("switch").WithDescription("Switch the active profile.");
        auth.AddCommand<AuthStatusCommand>("status").WithDescription("Show current profile and credential status.");
        auth.AddCommand<AuthLogoutCommand>("logout").WithDescription("Remove token and profile data.");
    });

    config.AddBranch("pr", pr =>
    {
        pr.SetDescription("Pull request operations.");
        pr.AddCommand<PrListCommand>("list").WithDescription("List pull requests.");
        pr.AddCommand<PrViewCommand>("view").WithDescription("View pull request details.");
        pr.AddCommand<PrDiffCommand>("diff").WithDescription("Show pull request diff.");
        pr.AddCommand<PrCommentsCommand>("comments").WithDescription("List pull request comments.");
        pr.AddCommand<PrCommentCommand>("comment").WithDescription("Post a pull request comment.");
        pr.AddCommand<PrReviewCommand>("review").WithDescription("Set pull request review status.");
    });

    config.AddCommand<ApiCommand>("api").WithDescription("Raw Bitbucket API access (`bbt api <METHOD> <PATH>`).");
});

return await app.RunAsync(args);
