namespace Bbt.Core.Bitbucket;

public sealed record BitbucketClientOptions(
    Uri BaseUri,
    string Email,
    string Token,
    bool Verbose,
    bool NoRetry,
    Action<string>? VerboseLog);

