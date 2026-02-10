namespace Bbt.Infrastructure;

public static class BitbucketTokenScopes
{
    public const string MinimumScopesHelp = "read:repository:bitbucket, read:workspace:bitbucket, read:user:bitbucket, read:pullrequest:bitbucket, write:pullrequest:bitbucket";

    public static readonly string[] Minimum =
    [
        "read:repository:bitbucket",
        "read:workspace:bitbucket",
        "read:user:bitbucket",
        "read:pullrequest:bitbucket",
        "write:pullrequest:bitbucket"
    ];

    public static string MinimumCsv => MinimumScopesHelp;
}
