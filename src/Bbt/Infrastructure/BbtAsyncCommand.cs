using Bbt.Core.Bitbucket;
using Spectre.Cli;

namespace Bbt.Infrastructure;

public abstract class BbtAsyncCommand<TSettings> : AsyncCommand<TSettings>
    where TSettings : BbtSettings
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
            Console.Error.WriteLine($"{(int)ex.StatusCode} {ex.StatusCode}: {ex.ApiMessage ?? ex.Message}{detail}");
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

