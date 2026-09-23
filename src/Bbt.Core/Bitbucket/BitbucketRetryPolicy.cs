using System.Net;

namespace Bbt.Core.Bitbucket;

public static class BitbucketRetryPolicy
{
    /// <summary>
    /// POST/PATCH may already have been applied when a gateway error, timeout, or dropped connection occurs,
    /// so resending them risks duplicates (e.g. two pull requests or comments).
    /// </summary>
    public static bool IsIdempotent(HttpMethod method)
    {
        return method != HttpMethod.Post && method != HttpMethod.Patch;
    }

    /// <summary>
    /// 429 means the request was rejected before processing, so it is safe to retry for any method.
    /// Gateway errors are ambiguous and only retried for idempotent methods.
    /// </summary>
    public static bool ShouldRetry(HttpStatusCode statusCode, bool idempotent)
    {
        if (statusCode == HttpStatusCode.TooManyRequests)
        {
            return true;
        }

        return idempotent && statusCode is HttpStatusCode.BadGateway
            or HttpStatusCode.ServiceUnavailable
            or HttpStatusCode.GatewayTimeout;
    }

    public static HttpRequestException CreateOutcomeUnknownException(HttpMethod method, Exception inner)
    {
        return new HttpRequestException(
            $"{method} request failed before a response was received ({inner.Message}). It was not retried because it may already have been applied; check the current state before running it again.",
            inner);
    }
}
