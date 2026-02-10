using System.ComponentModel;
using Bbt.Core.Config;
using Bbt.Core.IO;
using Bbt.Infrastructure;
using Spectre.Cli;
using Spectre.Console;

namespace Bbt.Commands.Auth;

public sealed class AuthSwitchCommand : BbtAsyncCommand<AuthSwitchCommand.Settings>
{
    public sealed class Settings : BbtOutputSettings
    {
        [Description("Profile to make current.")]
        [CommandArgument(0, "<PROFILE>")]
        public string Profile { get; init; } = string.Empty;
    }

    protected override async Task<int> ExecuteCommandAsync(CommandContext context, Settings settings)
    {
        var configStore = new BbtConfigStore();
        var config = await configStore.LoadAsync();

        if (!config.Profiles.ContainsKey(settings.Profile))
        {
            throw new InvalidOperationException($"Unknown profile '{settings.Profile}'. Run `bbt auth login --profile {settings.Profile}` first.");
        }

        config.CurrentProfile = settings.Profile;
        await configStore.SaveAsync(config);

        switch (settings.GetOutputMode())
        {
            case OutputMode.Quiet:
                OutputWriter.WriteQuiet(settings.Profile);
                return 0;
            case OutputMode.Json:
                await new OutputWriter(new ProcessRunner()).WriteJsonAsync(new { currentProfile = settings.Profile }, settings);
                return 0;
            default:
                Spectre.Console.AnsiConsole.MarkupLine($"Switched to profile [yellow]{Markup.Escape(settings.Profile)}[/].");
                return 0;
        }
    }
}
