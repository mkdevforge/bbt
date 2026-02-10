using System.ComponentModel;
using Bbt.Core.Auth;
using Bbt.Core.Bitbucket;
using Bbt.Core.Bitbucket.Models;
using Bbt.Core.Config;
using Bbt.Core.IO;
using Bbt.Core.Util;
using Bbt.Infrastructure;
using Bbt.Models;
using Spectre.Cli;
using Spectre.Console;

namespace Bbt.Commands.Auth;

public sealed class AuthLoginCommand : BbtAsyncCommand<AuthLoginCommand.Settings>
{
    public sealed class Settings : BbtSettings
    {
        [Description("Profile name to store/update (defaults to workspace or 'default').")]
        [CommandOption("--profile <PROFILE>")]
        public string? Profile { get; init; }

        [Description("Atlassian account email for API authentication.")]
        [CommandOption("--email <EMAIL>")]
        public string? Email { get; init; }

        [Description("Bitbucket API token (prompted if omitted).")]
        [CommandOption("--token <TOKEN>")]
        public string? Token { get; init; }
    }

    protected override async Task<int> ExecuteCommandAsync(CommandContext context, Settings settings)
    {
        var processRunner = new ProcessRunner();
        var credentialStore = CredentialStoreFactory.CreateDefault(processRunner);
        var configStore = new BbtConfigStore();

        var profileName = settings.Profile
            ?? settings.Workspace
            ?? BbtEnvironment.GetNonEmptyOrNull("BBT_WORKSPACE")
            ?? "default";

        var email = settings.Email ?? BbtEnvironment.GetNonEmptyOrNull("BBT_EMAIL");
        if (string.IsNullOrWhiteSpace(email))
        {
            email = Spectre.Console.AnsiConsole.Prompt(new TextPrompt<string>("Atlassian email:").Validate(s =>
                string.IsNullOrWhiteSpace(s) ? Spectre.Console.ValidationResult.Error("Email is required.") : Spectre.Console.ValidationResult.Success()));
        }

        var token = settings.Token ?? BbtEnvironment.GetNonEmptyOrNull("BBT_TOKEN");
        if (string.IsNullOrWhiteSpace(token))
        {
            token = Spectre.Console.AnsiConsole.Prompt(new TextPrompt<string>("Bitbucket API token:").Secret().Validate(s =>
                string.IsNullOrWhiteSpace(s) ? Spectre.Console.ValidationResult.Error("Token is required.") : Spectre.Console.ValidationResult.Success()));
        }

        var workspace = settings.Workspace ?? BbtEnvironment.GetNonEmptyOrNull("BBT_WORKSPACE");
        var baseUrl = BbtEnvironment.GetNonEmptyOrNull("BBT_BASE_URL") ?? "https://api.bitbucket.org/2.0";
        var baseUri = new Uri(baseUrl.EndsWith('/') ? baseUrl : $"{baseUrl}/", UriKind.Absolute);

        using var client = new BitbucketClient(new BitbucketClientOptions(
            BaseUri: baseUri,
            Email: email,
            Token: token,
            Verbose: settings.Verbose,
            NoRetry: settings.NoRetry,
            VerboseLog: settings.Verbose ? msg => Console.Error.WriteLine(msg) : null));

        BitbucketAccount user = await client.GetCurrentUserAsync();
        BitbucketWorkspace? ws = null;
        if (!string.IsNullOrWhiteSpace(workspace))
        {
            ws = await client.GetWorkspaceAsync(workspace);
        }

        var config = await configStore.LoadAsync();
        config.CurrentProfile = profileName;
        config.Profiles.TryGetValue(profileName, out var existing);
        existing ??= new BbtProfile();
        existing.Email = email;
        existing.BaseUrl = baseUrl;
        if (!string.IsNullOrWhiteSpace(workspace))
        {
            existing.DefaultWorkspace = workspace;
        }

        config.Profiles[profileName] = existing;
        await configStore.SaveAsync(config);

        await credentialStore.StoreTokenAsync(profileName, token);

        if (credentialStore is FileCredentialStore)
        {
            Console.Error.WriteLine($"Warning: using unencrypted token file fallback in `{BbtPaths.GetTokenDirectory()}`.");
        }

        var output = new
        {
            profile = profileName,
            email,
            defaultWorkspace = existing.DefaultWorkspace,
            defaultRepo = existing.DefaultRepo,
            baseUrl,
            credentialStore = credentialStore.Description,
            user = new UserSummary(user.DisplayName, user.Nickname, user.Uuid),
            workspace = ws is null ? null : new { slug = ws.Slug, name = ws.Name },
        };

        switch (settings.GetOutputMode())
        {
            case OutputMode.Quiet:
                OutputWriter.WriteQuiet(profileName);
                return 0;
            case OutputMode.Json:
                await new OutputWriter(processRunner).WriteJsonAsync(output, settings);
                return 0;
            default:
                var who = user.DisplayName ?? user.Nickname ?? user.Uuid ?? "unknown user";
                Spectre.Console.AnsiConsole.MarkupLine($"Logged in as [green]{Markup.Escape(who)}[/] (profile [yellow]{Markup.Escape(profileName)}[/]) using {credentialStore.Description}.");
                if (ws is not null)
                {
                    Spectre.Console.AnsiConsole.MarkupLine($"Default workspace: [yellow]{Markup.Escape(ws.Slug ?? workspace ?? string.Empty)}[/]");
                }

                return 0;
        }
    }
}
