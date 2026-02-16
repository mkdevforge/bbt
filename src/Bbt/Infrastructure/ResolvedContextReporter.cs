using Bbt.Core.Context;

namespace Bbt.Infrastructure;

public static class ResolvedContextReporter
{
    public static void LogRepoContext(BbtNetworkSettings settings, ResolvedRepoContext context)
    {
        if (!settings.Verbose)
        {
            return;
        }

        var workspace = TerminalSanitizer.Sanitize(context.Workspace) ?? string.Empty;
        var repo = TerminalSanitizer.Sanitize(context.Repo) ?? string.Empty;
        var source = TerminalSanitizer.Sanitize(string.IsNullOrWhiteSpace(context.Source) ? "unknown" : context.Source) ?? "unknown";
        Console.Error.WriteLine($"Context: workspace={workspace} repo={repo} source={source}");
    }

    public static void LogWorkspaceContext(BbtNetworkSettings settings, string workspace, string source)
    {
        if (!settings.Verbose)
        {
            return;
        }

        var resolvedWorkspace = TerminalSanitizer.Sanitize(workspace) ?? string.Empty;
        var resolvedSource = TerminalSanitizer.Sanitize(string.IsNullOrWhiteSpace(source) ? "unknown" : source) ?? "unknown";
        Console.Error.WriteLine($"Context: workspace={resolvedWorkspace} source={resolvedSource}");
    }
}
