using Bbt.Core.Bitbucket;
using System.Net;
using Spectre.Cli;

namespace Bbt.Infrastructure;

public abstract class BbtAsyncCommand<TSettings> : AsyncCommand<TSettings>
    where TSettings : BbtOutputSettings
{
    public sealed override async Task<int> ExecuteAsync(CommandContext context, TSettings settings)
    {
        try
        {
            return await ExecuteCommandAsync(context, settings);
        }
        catch (BitbucketApiException ex)
        {
            var detail = string.IsNullOrWhiteSpace(ex.ApiDetail) ? string.Empty : $"\n{ex.ApiDetail}";
            var authHint = ex.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
                ? $"\nHint: verify credentials and token scopes: {BitbucketTokenScopes.MinimumCsv}"
                : string.Empty;
            Console.Error.WriteLine($"{(int)ex.StatusCode} {ex.StatusCode}: {ex.ApiMessage ?? ex.Message}{detail}{authHint}");
            return 1;
        }
        catch (InvalidOperationException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 2;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    protected abstract Task<int> ExecuteCommandAsync(CommandContext context, TSettings settings);
}
