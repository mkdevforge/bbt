using System.ComponentModel;
using Bbt.Core.Auth;
using Bbt.Core.Bitbucket.Models;
using Bbt.Core.Config;
using Bbt.Core.IO;
using Bbt.Core.Util;
using Bbt.Infrastructure;
using Bbt.Models;
using Spectre.Cli;
using Spectre.Console;

namespace Bbt.Commands.Auth;

public sealed class AuthStatusCommand : BbtAsyncCommand<AuthStatusCommand.Settings>
{
    public sealed class Settings : BbtNetworkSettings
    {
        [Description("Call Bitbucket API to verify current credentials.")]
        [CommandOption("--check")]
        public bool Check { get; init; }
    }

    protected override async Task<int> ExecuteCommandAsync(CommandContext context, Settings settings)
    {
        var processRunner = new ProcessRunner();
        var credentialStore = CredentialStoreFactory.CreateDefault(processRunner);
        var configStore = new BbtConfigStore();

        var config = await configStore.LoadAsync();
        var profileName = config.CurrentProfile;
        config.Profiles.TryGetValue(profileName, out var profile);

        var email = BbtEnvironment.GetNonEmptyOrNull("BBT_EMAIL") ?? profile?.Email;
        var baseUrl = BbtEnvironment.GetNonEmptyOrNull("BBT_BASE_URL") ?? profile?.BaseUrl ?? "https://api.bitbucket.org/2.0";

        var envToken = BbtEnvironment.GetNonEmptyOrNull("BBT_TOKEN");
        var token = envToken ?? await credentialStore.GetTokenAsync(profileName);

        var hasToken = !string.IsNullOrWhiteSpace(token);

        BitbucketAccount? user = null;
        if (settings.Check)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(token))
            {
                throw new InvalidOperationException("Missing credentials for --check. Run `bbt auth login` or set BBT_EMAIL/BBT_TOKEN.");
            }

            var auth = new AuthContext(
                ProfileName: profileName,
                Email: email,
                Token: token,
                BaseUri: new Uri(baseUrl.EndsWith('/') ? baseUrl : $"{baseUrl}/", UriKind.Absolute),
                CredentialStore: credentialStore,
                Config: config,
                Profile: profile);

            using var client = AuthContextResolver.CreateClient(auth, verbose: settings.Verbose, noRetry: settings.NoRetry);
            user = await client.GetCurrentUserAsync();
        }

        var output = new
        {
            currentProfile = profileName,
            email,
            hasToken,
            tokenSource = envToken is null ? "store" : "env",
            defaultWorkspace = profile?.DefaultWorkspace,
            defaultRepo = profile?.DefaultRepo,
            baseUrl,
            credentialStore = credentialStore.Description,
            user = user is null ? null : new UserSummary(user.DisplayName, user.Nickname, user.Uuid),
        };

        switch (settings.GetOutputMode())
        {
            case OutputMode.Json:
                await new OutputWriter(processRunner).WriteJsonAsync(output, settings);
                return 0;
            case OutputMode.Quiet:
                OutputWriter.WriteQuiet(profileName);
                return 0;
            default:
                Spectre.Console.AnsiConsole.MarkupLine($"Profile: [yellow]{TerminalSanitizer.EscapeMarkup(profileName)}[/]");
                Spectre.Console.AnsiConsole.MarkupLine($"Email: {TerminalSanitizer.EscapeMarkup(email ?? "(not set)")}");
                Spectre.Console.AnsiConsole.MarkupLine($"Token: {(hasToken ? "[green]present[/]" : "[red]missing[/]")} (source: {(envToken is null ? "store" : "env")}, store: {credentialStore.Description})");
                Spectre.Console.AnsiConsole.MarkupLine($"Base URL: {TerminalSanitizer.EscapeMarkup(baseUrl)}");
                if (!string.IsNullOrWhiteSpace(profile?.DefaultWorkspace))
                {
                    Spectre.Console.AnsiConsole.MarkupLine($"Default workspace: [yellow]{TerminalSanitizer.EscapeMarkup(profile.DefaultWorkspace)}[/]");
                }

                if (!string.IsNullOrWhiteSpace(profile?.DefaultRepo))
                {
                    Spectre.Console.AnsiConsole.MarkupLine($"Default repo: [yellow]{TerminalSanitizer.EscapeMarkup(profile.DefaultRepo)}[/]");
                }

                if (user is not null)
                {
                    var who = user.DisplayName ?? user.Nickname ?? user.Uuid ?? "unknown";
                    Spectre.Console.AnsiConsole.MarkupLine($"Authenticated as: [green]{TerminalSanitizer.EscapeMarkup(who)}[/]");
                }

                if (settings.Check)
                {
                    Spectre.Console.AnsiConsole.MarkupLine("Auth check: [green]OK[/]");
                }
                else
                {
                    Spectre.Console.AnsiConsole.MarkupLine("Auth check: (skipped)");
                }

                return 0;
        }
    }
}
