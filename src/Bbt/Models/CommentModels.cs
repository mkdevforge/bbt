namespace Bbt.Models;

public sealed record PullRequestComment(
    int Id,
    UserSummary? User,
    string? Body,
    string? HtmlUrl,
    DateTimeOffset? CreatedOn,
    PullRequestCommentInline? Inline);

public sealed record PullRequestCommentInline(
    string? Path,
    int? To,
    int? From,
    int? StartTo,
    int? StartFrom);

