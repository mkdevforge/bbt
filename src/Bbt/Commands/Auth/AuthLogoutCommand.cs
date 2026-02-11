using System.ComponentModel;
using Bbt.Core.Auth;
using Bbt.Core.Config;
using Bbt.Core.IO;
using Bbt.Infrastructure;
using Spectre.Cli;
using Spectre.Console;

namespace Bbt.Commands.Auth;

public sealed class AuthLogoutCommand : BbtAsyncCommand<AuthLogoutCommand.Settings>
{
    public sealed class Settings : BbtOutputSettings
    {
        [Description("Profile to log out (defaults to current profile).")]
        [CommandOption("--profile <PROFILE>")]
        public string? Profile { get; init; }
    }

    protected override async Task<int> ExecuteCommandAsync(CommandContext context, Settings settings)
    {
        var processRunner = new ProcessRunner();
        var credentialStore = CredentialStoreFactory.CreateDefault(processRunner);
        var configStore = new BbtConfigStore();
        var config = await configStore.LoadAsync();

        var profileName = settings.Profile ?? config.CurrentProfile;

        await credentialStore.DeleteTokenAsync(profileName);
        config.Profiles.Remove(profileName);

        if (config.CurrentProfile.Equals(profileName, StringComparison.OrdinalIgnoreCase))
        {
            config.CurrentProfile = config.Profiles.Keys.FirstOrDefault() ?? "default";
        }

        await configStore.SaveAsync(config);

        switch (settings.GetOutputMode())
        {
            case OutputMode.Quiet:
                OutputWriter.WriteQuiet(profileName);
                return 0;
            case OutputMode.Json:
                await new OutputWriter(processRunner).WriteJsonAsync(new { loggedOutProfile = profileName }, settings);
                return 0;
            default:
                Spectre.Console.AnsiConsole.MarkupLine($"Logged out profile [yellow]{TerminalSanitizer.EscapeMarkup(profileName)}[/].");
                return 0;
        }
    }
}
