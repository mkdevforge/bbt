using System.ComponentModel;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Bbt.Infrastructure;
using Spectre.Cli;

namespace Bbt.Commands.Llms;

public sealed class LlmsCommand : Command<LlmsCommand.Settings>
{
    private sealed record LlmsOption(string Name, string Description);
    private sealed record LlmsCommandEntry(string Name, string Description);
    private sealed record LlmsAuth(string Scheme, string[] MinimumTokenScopes);
    private sealed record LlmsBehavior(string[] ContextResolutionOrder, string PullRequestIdInference, string MutationSafety, string FieldsSelection);
    private sealed record LlmsDocument(
        string Tool,
        string Version,
        string Scope,
        LlmsAuth Auth,
        LlmsOption[] GlobalOptions,
        LlmsCommandEntry[] Commands,
        LlmsBehavior Behavior);

    public sealed class Settings : CommandSettings
    {
        [Description("Emit machine-readable JSON instead of Markdown.")]
        [CommandOption("--json")]
        public bool Json { get; init; }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        var version = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion
            ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString()
            ?? "unknown";

        var doc = BuildDocument(version);
        if (settings.Json)
        {
            Console.Out.WriteLine(JsonSerializer.Serialize(doc, BbtJson.OutputSerializerOptions));
            return 0;
        }

        Console.Out.WriteLine(BuildMarkdown(doc));
        return 0;
    }

    private static LlmsDocument BuildDocument(string version)
    {
        return new LlmsDocument(
            Tool: "bbt",
            Version: version,
            Scope: "Bitbucket Cloud only",
            Auth: new LlmsAuth(
                Scheme: "HTTP Basic (email:token)",
                MinimumTokenScopes: BitbucketTokenScopes.Minimum),
            GlobalOptions:
            [
                new LlmsOption("--workspace <slug>", "Override workspace context (pr/api commands)."),
                new LlmsOption("--repo <slug>", "Override repository context (pr/api commands)."),
                new LlmsOption("--json", "Emit JSON output."),
                new LlmsOption("--fields <csv>", "Select top-level JSON fields (requires --json)."),
                new LlmsOption("--jq <expr>", "Run jq expression on JSON output (requires --json and jq)."),
                new LlmsOption("--quiet", "Minimal output for scripting."),
                new LlmsOption("--verbose", "Request/response diagnostics to stderr."),
                new LlmsOption("--no-retry", "Disable transient retry/backoff.")
            ],
            Commands:
            [
                new LlmsCommandEntry("bbt auth login [--email <email>] [--token <token>]", "Log in and store credentials (interactive by default). Optional: --workspace validates and stores default workspace; --profile names the profile (default: workspace slug or 'default')."),
                new LlmsCommandEntry("bbt auth switch <profile>", "Switch active profile."),
                new LlmsCommandEntry("bbt auth status [--check]", "Show profile/token status and optional API validation."),
                new LlmsCommandEntry("bbt auth logout [--profile <name>]", "Delete token and profile."),
                new LlmsCommandEntry("bbt pr list [--state <OPEN|MERGED|DECLINED|SUPERSEDED>] [--limit <n>]", "List pull requests (default state: OPEN)."),
                new LlmsCommandEntry("bbt pr view [<id>]", "View pull request details. Infers id from current branch when omitted."),
                new LlmsCommandEntry("bbt pr diff [<id>] [--include-raw]", "Show raw diff in human mode, structured diff in JSON mode."),
                new LlmsCommandEntry("bbt pr comments [<id>] [--limit <n>] [--sort <expr>] [--page <n>] [--pagelen <n>] [--paginate] [--contains <text> | -q/--query <expr>]", "List pull request comments (default: newest-first and one page unless --paginate/--limit requires more)."),
                new LlmsCommandEntry("bbt pr threads [<id>] [--limit <n>] [--sort <expr>] [--pagelen <n>] [--contains <text> | -q/--query <expr>]", "List pull request comment threads (root + replies, including nested replies). Threads are ordered by discovery sort (default: -created_on); with --contains/-q, ordering is based on the newest matching comment. Default: --limit 20, --pagelen 100. Filtering: --contains matches any comment in the thread (server-side discovery). Output: --quiet prints root ids; --json emits {rootId, root, replies, lastActivityOn}."),
                new LlmsCommandEntry("bbt pr comment <id> (--body <text> | --body-file <path>) [--reply-to <comment-id>] [--file <path> --line <n> [--line-end <n>] [--side <to|from>]]", "Post global, inline, or reply comment (default inline side: to)."),
                new LlmsCommandEntry("bbt pr review <id> (--approve|--unapprove|--request-changes|--unrequest-changes) [--body <text>|--body-file <path>]", "Apply review status; optional body posts global comment first."),
                new LlmsCommandEntry("bbt api <PATH> <METHOD> [--input <file>] [--paginate]", "Raw Bitbucket API access with placeholder replacement for {workspace}/{repo}. <METHOD> <PATH> order is also accepted."),
                new LlmsCommandEntry("bbt llms [--json]", "Print this full CLI capability context.")
            ],
            Behavior: new LlmsBehavior(
                ContextResolutionOrder:
                [
                    "CLI flags (--workspace/--repo)",
                    "Environment variables (BBT_WORKSPACE/BBT_REPO)",
                    "Current profile defaults",
                    "Git origin parsing"
                ],
                PullRequestIdInference: "For pr view/diff/comments/threads, omitted id resolves from current branch's open PR.",
                MutationSafety: "pr comment and pr review require explicit pull request id.",
                FieldsSelection: "For arrays, selected fields missing on some items are returned as null; unknown fields still fail."));
    }

    private static string BuildMarkdown(LlmsDocument doc)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# bbt LLM reference");
        sb.AppendLine();
        sb.AppendLine($"- Tool: `{doc.Tool}`");
        sb.AppendLine($"- Version: `{doc.Version}`");
        sb.AppendLine($"- Scope: {doc.Scope}");
        sb.AppendLine();
        sb.AppendLine("## Authentication");
        sb.AppendLine("- Scheme: HTTP Basic using `email:token`.");
        sb.AppendLine("- `bbt auth login` prompts for missing `--email`/`--token` by default.");
        sb.AppendLine("- Minimum token scopes:");
        foreach (var scope in doc.Auth.MinimumTokenScopes)
        {
            sb.AppendLine($"  - `{scope}`");
        }

        sb.AppendLine();
        sb.AppendLine("## Common options");
        foreach (var opt in doc.GlobalOptions)
        {
            sb.AppendLine($"- `{opt.Name}`: {opt.Description}");
        }

        sb.AppendLine();
        sb.AppendLine("## Commands");
        foreach (var cmd in doc.Commands)
        {
            sb.AppendLine($"- `{cmd.Name}`");
            sb.AppendLine($"  {cmd.Description}");
        }

        sb.AppendLine();
        sb.AppendLine("## Behavior");
        sb.AppendLine("- Context resolution order:");
        foreach (var step in doc.Behavior.ContextResolutionOrder)
        {
            sb.AppendLine($"  - {step}");
        }

        sb.AppendLine($"- PR id inference: {doc.Behavior.PullRequestIdInference}");
        sb.AppendLine($"- Mutation safety: {doc.Behavior.MutationSafety}");
        sb.AppendLine($"- --fields behavior: {doc.Behavior.FieldsSelection}");
        sb.AppendLine("- Workspace/repo can resolve from different sources in the same invocation.");
        return sb.ToString().TrimEnd();
    }
}
