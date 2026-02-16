using Bbt.Core.Context;
using Bbt.Infrastructure;
using Xunit;

namespace Bbt.Core.Tests.Infrastructure;

[Collection("EnvironmentVariables")]
public sealed class ResolvedContextReporterTests
{
    [Fact]
    public void LogRepoContext_SanitizesControlCharacters()
    {
        var settings = new TestNetworkSettings { Verbose = true };
        var context = new ResolvedRepoContext("ws\u001b[31m", "repo\u0007", "source\u001b[0m");

        using var stderr = new StringWriter();
        var original = Console.Error;
        try
        {
            Console.SetError(stderr);
            ResolvedContextReporter.LogRepoContext(settings, context);
        }
        finally
        {
            Console.SetError(original);
        }

        var output = stderr.ToString();
        Assert.DoesNotContain('\u001b', output);
        Assert.DoesNotContain('\u0007', output);
        Assert.Contains("workspace=ws[31m", output);
        Assert.Contains("repo=repo", output);
        Assert.Contains("source=source[0m", output);
    }

    [Fact]
    public void LogWorkspaceContext_SanitizesControlCharacters()
    {
        var settings = new TestNetworkSettings { Verbose = true };

        using var stderr = new StringWriter();
        var original = Console.Error;
        try
        {
            Console.SetError(stderr);
            ResolvedContextReporter.LogWorkspaceContext(settings, "ws\u001b[31m", "src\u0001");
        }
        finally
        {
            Console.SetError(original);
        }

        var output = stderr.ToString();
        Assert.DoesNotContain('\u001b', output);
        Assert.DoesNotContain('\u0001', output);
        Assert.Contains("workspace=ws[31m", output);
        Assert.Contains("source=src", output);
    }

    private sealed class TestNetworkSettings : BbtNetworkSettings;
}
