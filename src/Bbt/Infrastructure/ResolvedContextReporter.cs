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

        var source = string.IsNullOrWhiteSpace(context.Source) ? "unknown" : context.Source;
        Console.Error.WriteLine($"Context: workspace={context.Workspace} repo={context.Repo} source={source}");
    }

    public static void LogWorkspaceContext(BbtNetworkSettings settings, string workspace, string source)
    {
        if (!settings.Verbose)
        {
            return;
        }

        var resolvedSource = string.IsNullOrWhiteSpace(source) ? "unknown" : source;
        Console.Error.WriteLine($"Context: workspace={workspace} source={resolvedSource}");
    }
}
