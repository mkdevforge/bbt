using Bbt.Core.Auth;
using Bbt.Core.Bitbucket;
using Bbt.Core.Config;
using Bbt.Core.IO;
using Bbt.Core.Util;

namespace Bbt.Infrastructure;

public sealed record AuthContext(
    string ProfileName,
    string Email,
    string Token,
    Uri BaseUri,
    ICredentialStore CredentialStore,
    BbtConfig Config,
    BbtProfile? Profile);

public static class AuthContextResolver
{
    public static async Task<AuthContext> ResolveAsync(
        BbtConfigStore configStore,
        ICredentialStore credentialStore,
        string? profileOverride,
        bool requireToken,
        CancellationToken cancellationToken = default)
    {
        var config = await configStore.LoadAsync(cancellationToken);
        var profileName = string.IsNullOrWhiteSpace(profileOverride) ? config.CurrentProfile : profileOverride!;

        config.Profiles.TryGetValue(profileName, out var profile);

        var baseUrl = BbtEnvironment.GetNonEmptyOrNull("BBT_BASE_URL")
            ?? profile?.BaseUrl
            ?? "https://api.bitbucket.org/2.0";

        var email = BbtEnvironment.GetNonEmptyOrNull("BBT_EMAIL")
            ?? profile?.Email
            ?? string.Empty;

        var token = BbtEnvironment.GetNonEmptyOrNull("BBT_TOKEN")
            ?? await credentialStore.GetTokenAsync(profileName, cancellationToken)
            ?? string.Empty;

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new InvalidOperationException("Missing email. Run `bbt auth login` or set BBT_EMAIL.");
        }

        if (requireToken && string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException("Missing token. Run `bbt auth login` or set BBT_TOKEN.");
        }

        var baseUri = new Uri(baseUrl.EndsWith('/') ? baseUrl : $"{baseUrl}/", UriKind.Absolute);

        return new AuthContext(
            ProfileName: profileName,
            Email: email,
            Token: token,
            BaseUri: baseUri,
            CredentialStore: credentialStore,
            Config: config,
            Profile: profile);
    }

    public static BitbucketClient CreateClient(AuthContext auth, bool verbose, bool noRetry)
    {
        Action<string>? log = verbose ? msg => Console.Error.WriteLine(msg) : null;
        return new BitbucketClient(new BitbucketClientOptions(
            BaseUri: auth.BaseUri,
            Email: auth.Email,
            Token: auth.Token,
            Verbose: verbose,
            NoRetry: noRetry,
            VerboseLog: log));
    }
}
