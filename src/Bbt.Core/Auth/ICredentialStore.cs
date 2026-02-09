namespace Bbt.Core.Auth;

public interface ICredentialStore
{
    string Description { get; }

    Task StoreTokenAsync(string profile, string token, CancellationToken cancellationToken = default);
    Task<string?> GetTokenAsync(string profile, CancellationToken cancellationToken = default);
    Task DeleteTokenAsync(string profile, CancellationToken cancellationToken = default);
}

