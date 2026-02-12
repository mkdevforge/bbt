namespace Bbt.Models;

public sealed record PullRequestComment(
    long Id,
    UserSummary? User,
    string? Body,
    string? HtmlUrl,
    DateTimeOffset? CreatedOn,
    DateTimeOffset? UpdatedOn,
    bool? Deleted,
    long? ParentId,
    PullRequestCommentInline? Inline);

public sealed record PullRequestCommentInline(
    string? Path,
    int? To,
    int? From,
    int? StartTo,
    int? StartFrom);

public sealed record PullRequestCommentThread(
    long RootId,
    PullRequestComment Root,
    List<PullRequestComment> Replies,
    DateTimeOffset? LastActivityOn);
