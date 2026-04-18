using System.Text.Json.Serialization;

namespace Bbt.Models;

public sealed record PullRequestListItem(
    int Id,
    string Title,
    string State,
    UserSummary? Author,
    string? SourceBranch,
    string? DestinationBranch,
    string? HtmlUrl,
    DateTimeOffset? CreatedOn,
    DateTimeOffset? UpdatedOn);

public sealed record PullRequestView(
    int Id,
    string Title,
    string State,
    string? Description,
    UserSummary? Author,
    string? SourceBranch,
    string? DestinationBranch,
    string? HtmlUrl,
    DateTimeOffset? CreatedOn,
    DateTimeOffset? UpdatedOn,
    List<UserSummary>? Reviewers,
    List<PullRequestParticipant>? Participants);

public sealed record PullRequestSummary(
    int PrId,
    string Workspace,
    string Repo,
    string Title,
    string State,
    UserSummary? Author,
    string? SourceBranch,
    string? TargetBranch,
    string? HtmlUrl,
    DateTimeOffset? OpenedAt,
    DateTimeOffset? UpdatedAt,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    DateTimeOffset? MergedAt,
    List<UserSummary> Reviewers,
    int Approvals,
    int ChangesRequested,
    int CommentCount,
    int FilesChanged,
    int LinesAdded,
    int LinesRemoved);

public sealed record PullRequestParticipant(
    UserSummary? User,
    string? Role,
    bool? Approved,
    string? State,
    DateTimeOffset? ParticipatedOn);
