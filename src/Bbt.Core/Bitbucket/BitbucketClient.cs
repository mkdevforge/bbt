using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bbt.Core.Bitbucket.Models;
using Bbt.Core.Util;

namespace Bbt.Core.Bitbucket;

public sealed class BitbucketClient : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _http;
    private readonly AuthenticationHeaderValue _authorizationHeader;
    private readonly BitbucketClientOptions _options;
    private readonly bool _allowAuthToBitbucketHosts;
    private const string AllowInsecureHttpEnv = "BBT_ALLOW_INSECURE_HTTP";
    private const string DisableCrlCheckEnv = "BBT_DISABLE_CRL_CHECK";

    public BitbucketClient(BitbucketClientOptions options, HttpMessageHandler? handler = null)
    {
        _options = options;
        EnsureSecureTransportOrThrow(options.BaseUri, "base URL");
        var checkCrl = BbtEnvironment.GetNonEmptyOrNull(DisableCrlCheckEnv) is null;
        if (handler is HttpClientHandler httpHandler)
        {
            httpHandler.AllowAutoRedirect = false;
        }

        handler ??= new HttpClientHandler
        {
            AllowAutoRedirect = false,
            CheckCertificateRevocationList = checkCrl,
        };
        _http = new HttpClient(handler);
        _http.BaseAddress = options.BaseUri;
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _http.DefaultRequestHeaders.UserAgent.ParseAdd($"bbt/{typeof(BitbucketClient).Assembly.GetName().Version}");

        var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{options.Email}:{options.Token}"));
        _authorizationHeader = new AuthenticationHeaderValue("Basic", basic);
        _allowAuthToBitbucketHosts = IsBitbucketHost(options.BaseUri.Host);
    }

    public void Dispose()
    {
        _http.Dispose();
    }

    public async Task<BitbucketAccount> GetCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        return await SendJsonAsync<BitbucketAccount>(() => new HttpRequestMessage(HttpMethod.Get, "user"), cancellationToken);
    }

    public async Task<BitbucketWorkspace> GetWorkspaceAsync(string workspace, CancellationToken cancellationToken = default)
    {
        return await SendJsonAsync<BitbucketWorkspace>(
            () => new HttpRequestMessage(HttpMethod.Get, $"workspaces/{Uri.EscapeDataString(workspace)}"),
            cancellationToken);
    }

    public async Task<BitbucketPaginated<BitbucketPullRequest>> ListPullRequestsAsync(
        string workspace,
        string repo,
        string state,
        int pageLen = 50,
        string? pageUrl = null,
        CancellationToken cancellationToken = default)
    {
        var url = pageUrl ?? $"repositories/{Uri.EscapeDataString(workspace)}/{Uri.EscapeDataString(repo)}/pullrequests?state={Uri.EscapeDataString(state)}&pagelen={pageLen}";
        return await SendJsonAsync<BitbucketPaginated<BitbucketPullRequest>>(() => new HttpRequestMessage(HttpMethod.Get, url), cancellationToken);
    }

    public async Task<BitbucketPullRequest> GetPullRequestAsync(string workspace, string repo, int pullRequestId, CancellationToken cancellationToken = default)
    {
        return await SendJsonAsync<BitbucketPullRequest>(
            () => new HttpRequestMessage(HttpMethod.Get, $"repositories/{Uri.EscapeDataString(workspace)}/{Uri.EscapeDataString(repo)}/pullrequests/{pullRequestId}"),
            cancellationToken);
    }

    public async Task<BitbucketPaginated<BitbucketPullRequestActivity>> ListPullRequestActivityAsync(
        string workspace,
        string repo,
        int pullRequestId,
        int pageLen = 50,
        string? pageUrl = null,
        CancellationToken cancellationToken = default)
    {
        var url = pageUrl ?? $"repositories/{Uri.EscapeDataString(workspace)}/{Uri.EscapeDataString(repo)}/pullrequests/{pullRequestId}/activity?pagelen={pageLen}";
        return await SendJsonAsync<BitbucketPaginated<BitbucketPullRequestActivity>>(() => new HttpRequestMessage(HttpMethod.Get, url), cancellationToken);
    }

    public async Task<string> GetPullRequestDiffAsync(string workspace, string repo, int pullRequestId, CancellationToken cancellationToken = default)
    {
        var startUrl = $"repositories/{Uri.EscapeDataString(workspace)}/{Uri.EscapeDataString(repo)}/pullrequests/{pullRequestId}/diff";

        using var response = await SendRawAsync(
            () =>
            {
                var request = new HttpRequestMessage(HttpMethod.Get, startUrl);
                request.Headers.Accept.Clear();
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/plain"));
                return request;
            },
            allowRedirect: false,
            cancellationToken);
        if ((int)response.StatusCode is >= 300 and < 400)
        {
            var location = response.Headers.Location;
            if (location is null)
            {
                throw new BitbucketApiException(response.StatusCode, "Diff response was a redirect without a Location header.", null, null, null);
            }

            var requestUri = response.RequestMessage?.RequestUri ?? new Uri(_http.BaseAddress!, startUrl);
            var resolved = ResolveRedirectLocation(requestUri, location);
            _options.VerboseLog?.Invoke($"{(int)response.StatusCode} -> {resolved}");

            return await FollowRedirectForTextAsync(requestUri, location, maxHops: 5, cancellationToken);
        }

        if (!response.IsSuccessStatusCode)
        {
            await ThrowForErrorAsync(response, cancellationToken);
        }

        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    public async Task<BitbucketPaginated<BitbucketComment>> ListPullRequestCommentsAsync(
        string workspace,
        string repo,
        int pullRequestId,
        int pageLen = 50,
        int? page = null,
        string? sort = null,
        string? q = null,
        string? pageUrl = null,
        CancellationToken cancellationToken = default)
    {
        var url = pageUrl;
        if (string.IsNullOrWhiteSpace(url))
        {
            var qs = new List<string> { $"pagelen={pageLen}" };
            if (page is not null)
            {
                qs.Add($"page={page.Value}");
            }

            if (!string.IsNullOrWhiteSpace(sort))
            {
                qs.Add($"sort={Uri.EscapeDataString(sort)}");
            }

            if (!string.IsNullOrWhiteSpace(q))
            {
                qs.Add($"q={Uri.EscapeDataString(q)}");
            }

            url = $"repositories/{Uri.EscapeDataString(workspace)}/{Uri.EscapeDataString(repo)}/pullrequests/{pullRequestId}/comments?{string.Join("&", qs)}";
        }

        return await SendJsonAsync<BitbucketPaginated<BitbucketComment>>(() => new HttpRequestMessage(HttpMethod.Get, url), cancellationToken);
    }

    public async Task<BitbucketComment> CreatePullRequestCommentAsync(
        string workspace,
        string repo,
        int pullRequestId,
        CreatePullRequestCommentRequest body,
        CancellationToken cancellationToken = default)
    {
        var url = $"repositories/{Uri.EscapeDataString(workspace)}/{Uri.EscapeDataString(repo)}/pullrequests/{pullRequestId}/comments";
        var json = JsonSerializer.Serialize(body);

        return await SendJsonAsync<BitbucketComment>(
            () =>
            {
                var request = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json"),
                };
                return request;
            },
            cancellationToken);
    }

    public async Task<BitbucketParticipant> ApprovePullRequestAsync(string workspace, string repo, int pullRequestId, CancellationToken cancellationToken = default)
    {
        var url = $"repositories/{Uri.EscapeDataString(workspace)}/{Uri.EscapeDataString(repo)}/pullrequests/{pullRequestId}/approve";
        return await SendJsonAsync<BitbucketParticipant>(() => new HttpRequestMessage(HttpMethod.Post, url), cancellationToken);
    }

    public async Task UnapprovePullRequestAsync(string workspace, string repo, int pullRequestId, CancellationToken cancellationToken = default)
    {
        var url = $"repositories/{Uri.EscapeDataString(workspace)}/{Uri.EscapeDataString(repo)}/pullrequests/{pullRequestId}/approve";
        await SendNoContentAsync(() => new HttpRequestMessage(HttpMethod.Delete, url), cancellationToken);
    }

    public async Task<BitbucketParticipant> RequestChangesAsync(string workspace, string repo, int pullRequestId, CancellationToken cancellationToken = default)
    {
        var url = $"repositories/{Uri.EscapeDataString(workspace)}/{Uri.EscapeDataString(repo)}/pullrequests/{pullRequestId}/request-changes";
        return await SendJsonAsync<BitbucketParticipant>(() => new HttpRequestMessage(HttpMethod.Post, url), cancellationToken);
    }

    public async Task UnrequestChangesAsync(string workspace, string repo, int pullRequestId, CancellationToken cancellationToken = default)
    {
        var url = $"repositories/{Uri.EscapeDataString(workspace)}/{Uri.EscapeDataString(repo)}/pullrequests/{pullRequestId}/request-changes";
        await SendNoContentAsync(() => new HttpRequestMessage(HttpMethod.Delete, url), cancellationToken);
    }

    private async Task<T> SendJsonAsync<T>(Func<HttpRequestMessage> createRequest, CancellationToken cancellationToken)
    {
        using var response = await SendRawAsync(createRequest, allowRedirect: true, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            await ThrowForErrorAsync(response, cancellationToken);
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var value = JsonSerializer.Deserialize<T>(json, JsonOptions);
        if (value is null)
        {
            throw new InvalidOperationException("Response JSON was empty or invalid.");
        }

        return value;
    }

    private async Task SendNoContentAsync(Func<HttpRequestMessage> createRequest, CancellationToken cancellationToken)
    {
        using var response = await SendRawAsync(createRequest, allowRedirect: true, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NoContent)
        {
            return;
        }

        if (!response.IsSuccessStatusCode)
        {
            await ThrowForErrorAsync(response, cancellationToken);
        }
    }

    private async Task<HttpResponseMessage> SendRawAsync(Func<HttpRequestMessage> createRequest, bool allowRedirect, CancellationToken cancellationToken)
    {
        return await SendWithRetryAsync(
            createRequest,
            allowRedirect,
            cancellationToken);
    }

    private async Task<string> FollowRedirectForTextAsync(Uri requestUri, Uri location, int maxHops, CancellationToken cancellationToken)
    {
        var current = ResolveRedirectLocation(requestUri, location);
        for (var hop = 0; hop < maxHops; hop++)
        {
            using var response = await SendRawAsync(
                () =>
                {
                    var request = new HttpRequestMessage(HttpMethod.Get, current);
                    request.Headers.Accept.Clear();
                    request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/plain"));
                    return request;
                },
                allowRedirect: false,
                cancellationToken);
            if ((int)response.StatusCode is >= 300 and < 400)
            {
                var next = response.Headers.Location;
                if (next is null)
                {
                    throw new BitbucketApiException(response.StatusCode, "Redirect response was missing a Location header.", null, null, null);
                }

                current = ResolveRedirectLocation(current, next);
                _options.VerboseLog?.Invoke($"{(int)response.StatusCode} -> {current}");
                continue;
            }

            if (!response.IsSuccessStatusCode)
            {
                await ThrowForErrorAsync(response, cancellationToken);
            }

            return await response.Content.ReadAsStringAsync(cancellationToken);
        }

        throw new InvalidOperationException("Too many redirects while fetching diff.");
    }

    private async Task<HttpResponseMessage> SendWithRetryAsync(
        Func<HttpRequestMessage> createRequest,
        bool allowRedirect,
        CancellationToken cancellationToken)
    {
        if (_options.NoRetry)
        {
            using var request = createRequest();
            return await SendOnceAsync(request, allowRedirect, cancellationToken);
        }

        const int maxAttempts = 4;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                using var request = createRequest();
                var response = await SendOnceAsync(request, allowRedirect, cancellationToken);

                if (!IsTransient(response.StatusCode))
                {
                    return response;
                }

                if (attempt == maxAttempts)
                {
                    return response;
                }

                var delay = GetRetryDelay(response, attempt);
                _options.VerboseLog?.Invoke($"Retrying in {delay.TotalSeconds:0.0}s...");
                response.Dispose();
                await Task.Delay(delay, cancellationToken);
                continue;
            }
            catch (HttpRequestException ex) when (attempt < maxAttempts)
            {
                var delay = TimeSpan.FromSeconds(Math.Min(30, Math.Pow(2, attempt)));
                _options.VerboseLog?.Invoke($"Network error: {ex.Message}. Retrying in {delay.TotalSeconds:0.0}s...");
                await Task.Delay(delay, cancellationToken);
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested && attempt < maxAttempts)
            {
                var delay = TimeSpan.FromSeconds(Math.Min(30, Math.Pow(2, attempt)));
                _options.VerboseLog?.Invoke($"Request timed out. Retrying in {delay.TotalSeconds:0.0}s...");
                await Task.Delay(delay, cancellationToken);
            }
        }

        throw new InvalidOperationException("Unreachable retry loop exit.");
    }

    private async Task<HttpResponseMessage> SendOnceAsync(HttpRequestMessage request, bool allowRedirect, CancellationToken cancellationToken)
    {
        var requestUri = ResolveAbsoluteUri(request.RequestUri);
        if (requestUri is not null && request.Headers.Authorization is null && ShouldSendAuthorization(requestUri))
        {
            EnsureSecureTransportOrThrow(requestUri, "request URL");
            request.Headers.Authorization = _authorizationHeader;
        }

        _options.VerboseLog?.Invoke($"{request.Method} {requestUri ?? request.RequestUri}");
        var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        _options.VerboseLog?.Invoke($"<- {(int)response.StatusCode} {response.ReasonPhrase}");

        if (_options.Verbose)
        {
            foreach (var header in response.Headers)
            {
                if (header.Key.StartsWith("X-RateLimit", StringComparison.OrdinalIgnoreCase))
                {
                    _options.VerboseLog?.Invoke($"   {header.Key}: {string.Join(",", header.Value)}");
                }
            }

            if (response.Headers.TryGetValues("Retry-After", out var retryAfter))
            {
                _options.VerboseLog?.Invoke($"   Retry-After: {string.Join(",", retryAfter)}");
            }
        }

        if (!allowRedirect && (int)response.StatusCode is >= 300 and < 400)
        {
            return response;
        }

        return response;
    }

    private bool ShouldSendAuthorization(Uri uri)
    {
        if (uri.Host.Equals(_options.BaseUri.Host, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return _allowAuthToBitbucketHosts && IsBitbucketHost(uri.Host);
    }

    private Uri? ResolveAbsoluteUri(Uri? uri)
    {
        if (uri is null)
        {
            return null;
        }

        if (uri.IsAbsoluteUri)
        {
            return uri;
        }

        return _http.BaseAddress is null ? uri : new Uri(_http.BaseAddress, uri);
    }

    private static Uri ResolveRedirectLocation(Uri currentRequestUri, Uri location)
    {
        if (location.IsAbsoluteUri)
        {
            return location;
        }

        return new Uri(currentRequestUri, location);
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

    private static async Task ThrowForErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var statusCode = response.StatusCode;
        var raw = await response.Content.ReadAsStringAsync(cancellationToken);

        string? apiMessage = null;
        string? apiDetail = null;
        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.TryGetProperty("error", out var error))
            {
                if (error.TryGetProperty("message", out var message))
                {
                    apiMessage = message.GetString();
                }

                if (error.TryGetProperty("detail", out var detail))
                {
                    apiDetail = detail.GetString();
                }
            }
        }
        catch
        {
            // Ignore parse errors; fall back to raw text.
        }

        var msg = apiMessage ?? $"Bitbucket API request failed with {(int)statusCode} {statusCode}.";
        throw new BitbucketApiException(statusCode, msg, apiMessage, apiDetail, raw);
    }
}
