using Spectre.Cli;

namespace Bbt.Infrastructure;

public abstract class BbtSettings : CommandSettings
{
    [CommandOption("--workspace <WORKSPACE>")]
    public string? Workspace { get; init; }

    [CommandOption("--repo <REPO>")]
    public string? Repo { get; init; }

    [CommandOption("--json")]
    public bool Json { get; init; }

    [CommandOption("--fields <FIELDS>")]
    public string? Fields { get; init; }

    [CommandOption("--jq <EXPR>")]
    public string? Jq { get; init; }

    [CommandOption("--quiet")]
    public bool Quiet { get; init; }

    [CommandOption("--verbose")]
    public bool Verbose { get; init; }

    [CommandOption("--no-retry")]
    public bool NoRetry { get; init; }

    public OutputMode GetOutputMode()
    {
        if (Quiet)
        {
            return OutputMode.Quiet;
        }

        if (Json)
        {
            return OutputMode.Json;
        }

        return OutputMode.Human;
    }

    public override ValidationResult Validate()
    {
        if (Quiet && Json)
        {
            return ValidationResult.Error("Use either --quiet or --json, not both.");
        }

        if (Quiet && (!string.IsNullOrWhiteSpace(Fields) || !string.IsNullOrWhiteSpace(Jq)))
        {
            return ValidationResult.Error("--fields/--jq cannot be used with --quiet.");
        }

        if (!Json && (!string.IsNullOrWhiteSpace(Fields) || !string.IsNullOrWhiteSpace(Jq)))
        {
            return ValidationResult.Error("--fields/--jq require --json.");
        }

        return ValidationResult.Success();
    }
}

