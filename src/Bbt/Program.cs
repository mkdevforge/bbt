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
        auth.AddCommand<AuthLoginCommand>("login");
        auth.AddCommand<AuthSwitchCommand>("switch");
        auth.AddCommand<AuthStatusCommand>("status");
        auth.AddCommand<AuthLogoutCommand>("logout");
    });

    config.AddBranch("pr", pr =>
    {
        pr.SetDescription("Pull request operations.");
        pr.AddCommand<PrListCommand>("list");
        pr.AddCommand<PrViewCommand>("view");
        pr.AddCommand<PrDiffCommand>("diff");
        pr.AddCommand<PrCommentsCommand>("comments");
        pr.AddCommand<PrCommentCommand>("comment");
        pr.AddCommand<PrReviewCommand>("review");
    });

    config.AddCommand<ApiCommand>("api").WithDescription("Raw Bitbucket API access.");
});

return await app.RunAsync(args);
