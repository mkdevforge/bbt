using System.ComponentModel;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bbt.Core.Auth;
using Bbt.Core.Config;
using Bbt.Core.Context;
using Bbt.Core.Git;
using Bbt.Core.IO;
using Bbt.Core.Util;
using Bbt.Infrastructure;
using Spectre.Cli;

namespace Bbt.Commands.Api;

public sealed class ApiCommand : BbtAsyncCommand<ApiCommand.Settings>
{
    private const string AllowInsecureHttpEnv = "BBT_ALLOW_INSECURE_HTTP";
    private const string DisableCrlCheckEnv = "BBT_DISABLE_CRL_CHECK";

    public sealed class Settings : BbtRepoSettings
    {
        [Description("Bitbucket API path or absolute URL. Supports {workspace}/{repo} placeholders.")]
        [CommandArgument(1, "<PATH>")]
        public string Path { get; init; } = string.Empty;

        [Description("HTTP method (GET, POST, PUT, PATCH, DELETE, HEAD, OPTIONS).")]
        [CommandArgument(0, "<METHOD>")]
        public string Method { get; init; } = string.Empty;

        [Description("JSON request body file for POST/PUT/PATCH.")]
        [CommandOption("--input <FILE>")]
        public string? InputFile { get; init; }

        [Description("Follow paginated responses and emit merged values array.")]
        [CommandOption("--paginate")]
        public bool Paginate { get; init; }
    }

    protected override async Task<int> ExecuteCommandAsync(CommandContext context, Settings settings)
    {
        var processRunner = new ProcessRunner();
        var credentialStore = CredentialStoreFactory.CreateDefault(processRunner);
        var configStore = new BbtConfigStore();
        var (methodArg, pathArg) = NormalizeMethodAndPath(settings.Method, settings.Path);

        var auth = await AuthContextResolver.ResolveAsync(configStore, credentialStore, profileOverride: null, requireToken: true);

        var resolvedPath = await ReplacePlaceholdersAsync(pathArg, settings, configStore, processRunner);
        var uri = ResolveUri(auth.BaseUri, resolvedPath);

        var authHeaderValue = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{auth.Email}:{auth.Token}"));
        var authorizationHeader = new AuthenticationHeaderValue("Basic", authHeaderValue);
        var allowAuthToBitbucketHosts = IsBitbucketHost(auth.BaseUri.Host);

        using var handler = new HttpClientHandler
        {
            AllowAutoRedirect = false,
            CheckCertificateRevocationList = BbtEnvironment.GetNonEmptyOrNull(DisableCrlCheckEnv) is null,
        };
        using var http = new HttpClient(handler);
        http.BaseAddress = auth.BaseUri;
        http.DefaultRequestHeaders.UserAgent.ParseAdd($"bbt/{typeof(ApiCommand).Assembly.GetName().Version}");
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var method = ParseMethod(methodArg);
        string? inputContent = null;
        if (!string.IsNullOrWhiteSpace(settings.InputFile))
        {
            inputContent = await File.ReadAllTextAsync(settings.InputFile);
        }

        if (settings.Paginate)
        {
            var values = await FetchAllValuesAsync(
                http,
                method,
                uri,
                inputContent,
                settings,
                authorizationHeader,
                allowAuthToBitbucketHosts,
                auth.BaseUri);

            if (settings.GetOutputMode() == OutputMode.Json)
            {
                await new OutputWriter(processRunner).WriteJsonAsync(values, settings);
            }
            else if (settings.GetOutputMode() == OutputMode.Quiet)
            {
                return 0;
            }
            else
            {
                Console.Out.WriteLine(values.ToJsonString(BbtJson.OutputSerializerOptions));
            }

            return 0;
        }

        using var response = await SendWithRetryAndRedirectsAsync(
            http,
            method,
            uri,
            inputContent,
            settings,
            authorizationHeader,
            allowAuthToBitbucketHosts,
            auth.BaseUri,
            CancellationToken.None);

        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            Console.Error.WriteLine($"{(int)response.StatusCode} {response.StatusCode}");
            if (!string.IsNullOrWhiteSpace(body))
            {
                Console.Error.WriteLine(TerminalSanitizer.Sanitize(body));
            }

            return 1;
        }

        switch (settings.GetOutputMode())
        {
            case OutputMode.Json:
                if (!TryParseJson(body, out var node))
                {
                    throw new InvalidOperationException("Response was not valid JSON.");
                }

                await new OutputWriter(processRunner).WriteJsonAsync(node, settings);
                return 0;
            case OutputMode.Quiet:
                return 0;
            default:
                Console.Out.WriteLine(TerminalSanitizer.Sanitize(body));
                return 0;
        }
    }

    private static (string Method, string Path) NormalizeMethodAndPath(string first, string second)
    {
        if (TryParseMethod(first, out _))
        {
            return (first, second);
        }

        if (TryParseMethod(second, out _))
        {
            return (second, first);
        }

        return (first, second);
    }

    private static async Task<string> ReplacePlaceholdersAsync(string path, Settings settings, BbtConfigStore configStore, ProcessRunner processRunner)
    {
        var needsWorkspace = path.Contains("{workspace}", StringComparison.OrdinalIgnoreCase);
        var needsRepo = path.Contains("{repo}", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("{repo_slug}", StringComparison.OrdinalIgnoreCase);

        if (!needsWorkspace && !needsRepo)
        {
            return path;
        }

        if (!needsRepo && needsWorkspace)
        {
            var workspaceContext = await TryResolveWorkspaceAsync(settings, configStore, processRunner);
            if (workspaceContext is null)
            {
                throw new InvalidOperationException("Could not resolve workspace for placeholder replacement. Use --workspace, set BBT_WORKSPACE, or run inside a git repo with a Bitbucket origin remote.");
            }

            ResolvedContextReporter.LogWorkspaceContext(settings, workspaceContext.Value.Workspace, workspaceContext.Value.Source);
            return path.Replace("{workspace}", workspaceContext.Value.Workspace, StringComparison.OrdinalIgnoreCase);
        }

        var gitClient = new GitClient(processRunner);
        var repoResolver = new RepoContextResolver(configStore, gitClient);
        var repoContext = await repoResolver.TryResolveAsync(settings.Workspace, settings.Repo, profileOverride: null);
        if (repoContext is null)
        {
            throw new InvalidOperationException("Could not resolve workspace/repo for placeholder replacement. Use --workspace/--repo or set BBT_WORKSPACE/BBT_REPO.");
        }

        ResolvedContextReporter.LogRepoContext(settings, repoContext);

        return path
            .Replace("{workspace}", repoContext.Workspace, StringComparison.OrdinalIgnoreCase)
            .Replace("{repo}", repoContext.Repo, StringComparison.OrdinalIgnoreCase)
            .Replace("{repo_slug}", repoContext.Repo, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<(string Workspace, string Source)?> TryResolveWorkspaceAsync(Settings settings, BbtConfigStore configStore, ProcessRunner processRunner)
    {
        var workspaceOverride = string.IsNullOrWhiteSpace(settings.Workspace) ? null : settings.Workspace;
        if (workspaceOverride is not null)
        {
            return (workspaceOverride, "workspace:cli");
        }

        if (BbtEnvironment.TryGetNonEmpty("BBT_WORKSPACE", out var envWorkspace))
        {
            return (envWorkspace, "workspace:env");
        }

        var config = await configStore.LoadAsync(CancellationToken.None);
        if (config.Profiles.TryGetValue(config.CurrentProfile, out var profile) &&
            !string.IsNullOrWhiteSpace(profile.DefaultWorkspace))
        {
            return (profile.DefaultWorkspace, $"workspace:profile:{config.CurrentProfile}");
        }

        var gitClient = new GitClient(processRunner);
        if (!await gitClient.IsInsideWorkTreeAsync(CancellationToken.None))
        {
            return null;
        }

        var origin = await gitClient.TryGetOriginUrlAsync(CancellationToken.None);
        if (origin is null)
        {
            return null;
        }

        if (BitbucketRemoteParser.TryParse(origin, out var ws, out _))
        {
            return (ws, "workspace:git");
        }

        return null;
    }

    private static Uri ResolveUri(Uri baseUri, string path)
    {
        if (Uri.TryCreate(path, UriKind.Absolute, out var absolute) &&
            (absolute.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
            absolute.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
        {
            return absolute;
        }

        var trimmed = path.Trim().TrimStart('/');
        if (trimmed.StartsWith("2.0/", StringComparison.Ordinal))
        {
            trimmed = trimmed["2.0/".Length..];
        }

        return new Uri(baseUri, trimmed);
    }

    private static HttpMethod ParseMethod(string method)
    {
        if (string.IsNullOrWhiteSpace(method))
        {
            throw new InvalidOperationException("METHOD is required.");
        }

        if (!TryParseMethod(method, out var parsed))
        {
            throw new InvalidOperationException($"Unsupported method '{method}'.");
        }

        return parsed;
    }

    private static bool TryParseMethod(string method, out HttpMethod parsed)
    {
        parsed = method.ToUpperInvariant() switch
        {
            "GET" => HttpMethod.Get,
            "POST" => HttpMethod.Post,
            "PUT" => HttpMethod.Put,
            "PATCH" => HttpMethod.Patch,
            "DELETE" => HttpMethod.Delete,
            "HEAD" => HttpMethod.Head,
            "OPTIONS" => HttpMethod.Options,
            _ => null!
        };

        return parsed is not null;
    }

    private static async Task<JsonArray> FetchAllValuesAsync(
        HttpClient http,
        HttpMethod method,
        Uri uri,
        string? inputContent,
        Settings settings,
        AuthenticationHeaderValue authorizationHeader,
        bool allowAuthToBitbucketHosts,
        Uri baseUri)
    {
        var values = new JsonArray();
        Uri? next = uri;

        while (next is not null)
        {
            using var response = await SendWithRetryAndRedirectsAsync(
                http,
                method,
                next,
                inputContent,
                settings,
                authorizationHeader,
                allowAuthToBitbucketHosts,
                baseUri,
                CancellationToken.None);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                Console.Error.WriteLine($"{(int)response.StatusCode} {response.StatusCode}");
                if (!string.IsNullOrWhiteSpace(body))
                {
                    Console.Error.WriteLine(TerminalSanitizer.Sanitize(body));
                }

                throw new InvalidOperationException("Paginated request failed.");
            }

            if (!TryParseJson(body, out var node) || node is not JsonObject obj)
            {
                throw new InvalidOperationException("--paginate requires a JSON object response.");
            }

            if (!obj.TryGetPropertyValue("values", out var valuesNode) || valuesNode is not JsonArray pageValues)
            {
                throw new InvalidOperationException("--paginate requires the response to contain a 'values' array.");
            }

            foreach (var element in pageValues)
            {
                values.Add(element?.DeepClone());
            }

            next = null;
            if (obj.TryGetPropertyValue("next", out var nextNode) && nextNode is JsonValue v)
            {
                var nextStr = v.GetValue<string?>();
                if (!string.IsNullOrWhiteSpace(nextStr) && Uri.TryCreate(nextStr, UriKind.Absolute, out var nextUri))
                {
                    next = nextUri;
                }
            }

            // `--limit` behavior is intentionally not implemented for `bbt api --paginate` v0.1.
        }

        return values;
    }

    private static async Task<HttpResponseMessage> SendWithRetryAndRedirectsAsync(
        HttpClient http,
        HttpMethod method,
        Uri uri,
        string? inputContent,
        Settings settings,
        AuthenticationHeaderValue authorizationHeader,
        bool allowAuthToBitbucketHosts,
        Uri baseUri,
        CancellationToken cancellationToken)
    {
        var current = uri;
        const int maxHops = 5;

        for (var hop = 0; hop < maxHops; hop++)
        {
            var response = await SendWithRetryAsync(
                http,
                () => CreateRequest(method, current, inputContent),
                settings,
                authorizationHeader,
                allowAuthToBitbucketHosts,
                baseUri,
                cancellationToken);

            if (ShouldFollowRedirect(method, response.StatusCode) && response.Headers.Location is not null)
            {
                var next = ResolveRedirectLocation(current, response.Headers.Location);
                if (settings.Verbose)
                {
                    Console.Error.WriteLine($"{(int)response.StatusCode} -> {next}");
                }

                response.Dispose();
                current = next;
                continue;
            }

            return response;
        }

        throw new InvalidOperationException("Too many redirects.");
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, Uri uri, string? inputContent)
    {
        var request = new HttpRequestMessage(method, uri);
        if (!string.IsNullOrWhiteSpace(inputContent))
        {
            request.Content = new StringContent(inputContent, Encoding.UTF8, "application/json");
        }

        return request;
    }

    private static async Task<HttpResponseMessage> SendWithRetryAsync(
        HttpClient http,
        Func<HttpRequestMessage> createRequest,
        Settings settings,
        AuthenticationHeaderValue authorizationHeader,
        bool allowAuthToBitbucketHosts,
        Uri baseUri,
        CancellationToken cancellationToken)
    {
        if (settings.NoRetry)
        {
            using var request = createRequest();
            return await SendOnceAsync(http, request, settings, authorizationHeader, allowAuthToBitbucketHosts, baseUri, cancellationToken);
        }

        const int maxAttempts = 4;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                using var request = createRequest();
                var response = await SendOnceAsync(http, request, settings, authorizationHeader, allowAuthToBitbucketHosts, baseUri, cancellationToken);

                if (!IsTransient(response.StatusCode))
                {
                    return response;
                }

                if (attempt == maxAttempts)
                {
                    return response;
                }

                var delay = GetRetryDelay(response, attempt);
                if (settings.Verbose)
                {
                    Console.Error.WriteLine($"Retrying in {delay.TotalSeconds:0.0}s...");
                }

                response.Dispose();
                await Task.Delay(delay, cancellationToken);
            }
            catch (HttpRequestException ex) when (attempt < maxAttempts)
            {
                var delay = TimeSpan.FromSeconds(Math.Min(30, Math.Pow(2, attempt)));
                if (settings.Verbose)
                {
                    Console.Error.WriteLine($"Network error: {ex.Message}. Retrying in {delay.TotalSeconds:0.0}s...");
                }

                await Task.Delay(delay, cancellationToken);
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested && attempt < maxAttempts)
            {
                var delay = TimeSpan.FromSeconds(Math.Min(30, Math.Pow(2, attempt)));
                if (settings.Verbose)
                {
                    Console.Error.WriteLine($"Request timed out. Retrying in {delay.TotalSeconds:0.0}s...");
                }

                await Task.Delay(delay, cancellationToken);
            }
        }

        throw new InvalidOperationException("Unreachable retry loop exit.");
    }

    private static async Task<HttpResponseMessage> SendOnceAsync(
        HttpClient http,
        HttpRequestMessage request,
        Settings settings,
        AuthenticationHeaderValue authorizationHeader,
        bool allowAuthToBitbucketHosts,
        Uri baseUri,
        CancellationToken cancellationToken)
    {
        var resolvedUri = ResolveAbsoluteUri(baseUri, request.RequestUri);
        if (resolvedUri is not null && request.Headers.Authorization is null && ShouldSendAuthorization(resolvedUri, baseUri, allowAuthToBitbucketHosts))
        {
            EnsureSecureTransportOrThrow(resolvedUri, "request URL");
            request.Headers.Authorization = authorizationHeader;
        }

        if (settings.Verbose)
        {
            Console.Error.WriteLine($"{request.Method} {resolvedUri ?? request.RequestUri}");
        }

        var response = await http.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken);

        if (settings.Verbose)
        {
            Console.Error.WriteLine($"<- {(int)response.StatusCode} {response.ReasonPhrase}");
            foreach (var header in response.Headers)
            {
                if (header.Key.StartsWith("X-RateLimit", StringComparison.OrdinalIgnoreCase))
                {
                    Console.Error.WriteLine($"   {header.Key}: {string.Join(",", header.Value)}");
                }
            }

            if (response.Headers.TryGetValues("Retry-After", out var retryAfter))
            {
                Console.Error.WriteLine($"   Retry-After: {string.Join(",", retryAfter)}");
            }
        }

        return response;
    }

    private static bool ShouldFollowRedirect(HttpMethod method, HttpStatusCode statusCode)
    {
        if (method != HttpMethod.Get && method != HttpMethod.Head)
        {
            return false;
        }

        return (int)statusCode is >= 300 and < 400;
    }

    private static Uri ResolveRedirectLocation(Uri currentRequestUri, Uri location)
    {
        if (location.IsAbsoluteUri)
        {
            return location;
        }

        return new Uri(currentRequestUri, location);
    }

    private static Uri? ResolveAbsoluteUri(Uri baseUri, Uri? uri)
    {
        if (uri is null)
        {
            return null;
        }

        return uri.IsAbsoluteUri ? uri : new Uri(baseUri, uri);
    }

    private static bool ShouldSendAuthorization(Uri uri, Uri baseUri, bool allowAuthToBitbucketHosts)
    {
        if (uri.Host.Equals(baseUri.Host, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return allowAuthToBitbucketHosts && IsBitbucketHost(uri.Host);
    }

    private static bool IsBitbucketHost(string host)
    {
        return host.Equals("bitbucket.org", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".bitbucket.org", StringComparison.OrdinalIgnoreCase);
    }

    private static void EnsureSecureTransportOrThrow(Uri uri, string description)
    {
        if (BbtEnvironment.GetNonEmptyOrNull(AllowInsecureHttpEnv) is not null)
        {
            return;
        }

        if (!uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Refusing to use non-HTTPS {description} '{uri}'. Set {AllowInsecureHttpEnv}=1 to override.");
        }
    }

    private static bool IsTransient(HttpStatusCode statusCode)
    {
        return statusCode is HttpStatusCode.TooManyRequests
            or HttpStatusCode.BadGateway
            or HttpStatusCode.ServiceUnavailable
            or HttpStatusCode.GatewayTimeout;
    }

    private static TimeSpan GetRetryDelay(HttpResponseMessage response, int attempt)
    {
        if (response.Headers.TryGetValues("Retry-After", out var values))
        {
            var raw = values.FirstOrDefault();
            if (raw is not null && int.TryParse(raw, out var seconds) && seconds >= 0)
            {
                return TimeSpan.FromSeconds(Math.Min(seconds, 60));
            }
        }

        var baseDelay = TimeSpan.FromSeconds(Math.Min(30, Math.Pow(2, attempt)));
        var jitter = TimeSpan.FromMilliseconds(RandomNumberGenerator.GetInt32(0, 250));
        return baseDelay + jitter;
    }

    private static bool TryParseJson(string body, out JsonNode? node)
    {
        node = null;
        if (string.IsNullOrWhiteSpace(body))
        {
            return false;
        }

        try
        {
            node = JsonNode.Parse(body);
            return node is not null;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
