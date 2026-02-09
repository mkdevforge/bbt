namespace Bbt.Core.Context;

public sealed record ResolvedRepoContext(
    string Workspace,
    string Repo,
    string? Source);

