using Bbt.Core.Config;
using Bbt.Core.Context;
using Bbt.Core.Git;
using Bbt.Core.IO;
using Xunit;

namespace Bbt.Core.Tests.Context;

[Collection("EnvironmentVariables")]
public sealed class RepoContextResolverTests
{
    [Fact]
    public async Task TryResolveAsync_UsesProfileDefaults_WhenNoOverrides()
    {
        using var env = new EnvironmentScope("BBT_WORKSPACE", "BBT_REPO");

        await WithResolverAsync(
            CreateConfig("profile-ws", "profile-repo"),
            async resolver =>
            {
                var context = await resolver.TryResolveAsync(
                    workspaceOverride: null,
                    repoOverride: null,
                    profileOverride: null);

                Assert.NotNull(context);
                Assert.Equal("profile-ws", context!.Workspace);
                Assert.Equal("profile-repo", context.Repo);
                var source = context.Source ?? string.Empty;
                Assert.Contains("workspace:profile:default", source);
                Assert.Contains("repo:profile:default", source);
            });
    }

    [Fact]
    public async Task TryResolveAsync_MixesWorkspaceOverrideAndProfileRepo()
    {
        using var env = new EnvironmentScope("BBT_WORKSPACE", "BBT_REPO");

        await WithResolverAsync(
            CreateConfig("profile-ws", "profile-repo"),
            async resolver =>
            {
                var context = await resolver.TryResolveAsync(
                    workspaceOverride: "cli-ws",
                    repoOverride: null,
                    profileOverride: null);

                Assert.NotNull(context);
                Assert.Equal("cli-ws", context!.Workspace);
                Assert.Equal("profile-repo", context.Repo);
                var source = context.Source ?? string.Empty;
                Assert.Contains("workspace:cli", source);
                Assert.Contains("repo:profile:default", source);
            });
    }

    [Fact]
    public async Task TryResolveAsync_MixesProfileWorkspaceAndRepoOverride()
    {
        using var env = new EnvironmentScope("BBT_WORKSPACE", "BBT_REPO");

        await WithResolverAsync(
            CreateConfig("profile-ws", "profile-repo"),
            async resolver =>
            {
                var context = await resolver.TryResolveAsync(
                    workspaceOverride: null,
                    repoOverride: "cli-repo",
                    profileOverride: null);

                Assert.NotNull(context);
                Assert.Equal("profile-ws", context!.Workspace);
                Assert.Equal("cli-repo", context.Repo);
                var source = context.Source ?? string.Empty;
                Assert.Contains("workspace:profile:default", source);
                Assert.Contains("repo:cli", source);
            });
    }

    [Fact]
    public async Task TryResolveAsync_PrefersEnvironmentOverProfileDefaults()
    {
        using var env = new EnvironmentScope("BBT_WORKSPACE", "BBT_REPO");
        env.Set("BBT_WORKSPACE", "env-ws");
        env.Set("BBT_REPO", "env-repo");

        await WithResolverAsync(
            CreateConfig("profile-ws", "profile-repo"),
            async resolver =>
            {
                var context = await resolver.TryResolveAsync(
                    workspaceOverride: null,
                    repoOverride: null,
                    profileOverride: null);

                Assert.NotNull(context);
                Assert.Equal("env-ws", context!.Workspace);
                Assert.Equal("env-repo", context.Repo);
                var source = context.Source ?? string.Empty;
                Assert.Contains("workspace:env", source);
                Assert.Contains("repo:env", source);
                Assert.DoesNotContain("profile", source.ToLowerInvariant());
            });
    }

    [Fact]
    public async Task TryResolveAsync_PrefersCliOverridesOverEnvironment()
    {
        using var env = new EnvironmentScope("BBT_WORKSPACE", "BBT_REPO");
        env.Set("BBT_WORKSPACE", "env-ws");
        env.Set("BBT_REPO", "env-repo");

        await WithResolverAsync(
            CreateConfig("profile-ws", "profile-repo"),
            async resolver =>
            {
                var context = await resolver.TryResolveAsync(
                    workspaceOverride: "cli-ws",
                    repoOverride: null,
                    profileOverride: null);

                Assert.NotNull(context);
                Assert.Equal("cli-ws", context!.Workspace);
                Assert.Equal("env-repo", context.Repo);
                var source = context.Source ?? string.Empty;
                Assert.Contains("workspace:cli", source);
                Assert.Contains("repo:env", source);
            });
    }

    private static async Task WithResolverAsync(BbtConfig config, Func<RepoContextResolver, Task> action)
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), $"bbt-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var configPath = Path.Combine(tempDirectory, "config.json");
            var configStore = new BbtConfigStore(configPath);
            await configStore.SaveAsync(config);

            var resolver = new RepoContextResolver(configStore, new GitClient(new ProcessRunner()));
            await action(resolver);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private static BbtConfig CreateConfig(string workspace, string repo)
    {
        return new BbtConfig
        {
            CurrentProfile = "default",
            Profiles = new Dictionary<string, BbtProfile>(StringComparer.OrdinalIgnoreCase)
            {
                ["default"] = new BbtProfile
                {
                    Email = "test@example.com",
                    DefaultWorkspace = workspace,
                    DefaultRepo = repo,
                }
            }
        };
    }

    private sealed class EnvironmentScope : IDisposable
    {
        private readonly Dictionary<string, string?> _originalValues = new(StringComparer.Ordinal);

        public EnvironmentScope(params string[] names)
        {
            foreach (var name in names)
            {
                _originalValues[name] = Environment.GetEnvironmentVariable(name);
                Environment.SetEnvironmentVariable(name, null);
            }
        }

        public void Set(string name, string? value)
        {
            if (!_originalValues.ContainsKey(name))
            {
                _originalValues[name] = Environment.GetEnvironmentVariable(name);
            }

            Environment.SetEnvironmentVariable(name, value);
        }

        public void Dispose()
        {
            foreach (var entry in _originalValues)
            {
                Environment.SetEnvironmentVariable(entry.Key, entry.Value);
            }
        }
    }
}

[CollectionDefinition("EnvironmentVariables", DisableParallelization = true)]
public sealed class EnvironmentVariablesCollection
{
}
