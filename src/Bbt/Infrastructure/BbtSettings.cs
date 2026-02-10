using System.ComponentModel;
using Spectre.Cli;

namespace Bbt.Infrastructure;

public abstract class BbtOutputSettings : CommandSettings
{
    [Description("Output JSON only.")]
    [CommandOption("--json")]
    public bool Json { get; init; }

    [Description("Comma-separated top-level JSON fields (requires --json).")]
    [CommandOption("--fields <FIELDS>")]
    public string? Fields { get; init; }

    [Description("Run jq expression on JSON output (requires --json and jq).")]
    [CommandOption("--jq <EXPR>")]
    public string? Jq { get; init; }

    [Description("Minimal output for scripting.")]
    [CommandOption("--quiet")]
    public bool Quiet { get; init; }

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

public abstract class BbtNetworkSettings : BbtOutputSettings
{
    [Description("Print request/response diagnostics to stderr.")]
    [CommandOption("--verbose")]
    public bool Verbose { get; init; }

    [Description("Disable transient retry/backoff.")]
    [CommandOption("--no-retry")]
    public bool NoRetry { get; init; }
}

public abstract class BbtWorkspaceSettings : BbtNetworkSettings
{
    [Description("Override workspace slug for this command.")]
    [CommandOption("--workspace <WORKSPACE>")]
    public string? Workspace { get; init; }
}

public abstract class BbtRepoSettings : BbtWorkspaceSettings
{
    [Description("Override repository slug for this command.")]
    [CommandOption("--repo <REPO>")]
    public string? Repo { get; init; }
}
