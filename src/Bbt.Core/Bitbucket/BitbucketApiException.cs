using System.Net;

namespace Bbt.Core.Bitbucket;

public sealed class BitbucketApiException : Exception
{
    public HttpStatusCode StatusCode { get; }
    public string? ApiMessage { get; }
    public string? ApiDetail { get; }
    public string? RawBody { get; }

    public BitbucketApiException(HttpStatusCode statusCode, string message, string? apiMessage, string? apiDetail, string? rawBody)
        : base(message)
    {
        StatusCode = statusCode;
        ApiMessage = apiMessage;
        ApiDetail = apiDetail;
        RawBody = rawBody;
    }
}

