using Bbt.Core.Bitbucket.Models;
using Bbt.Models;

namespace Bbt.Infrastructure;

public static class ModelMappers
{
    public static UserSummary? ToUserSummary(BitbucketAccount? account)
    {
        return account is null ? null : new UserSummary(account.DisplayName, account.Nickname, account.Uuid);
    }

    public static PullRequestListItem ToPullRequestListItem(BitbucketPullRequest pr)
    {
        return new PullRequestListItem(
            Id: pr.Id,
            Title: pr.Title,
            State: pr.State,
            Author: ToUserSummary(pr.Author),
            SourceBranch: pr.Source?.Branch?.Name,
            DestinationBranch: pr.Destination?.Branch?.Name,
            HtmlUrl: pr.Links?.Html?.Href,
            CreatedOn: pr.CreatedOn,
            UpdatedOn: pr.UpdatedOn);
    }

    public static PullRequestView ToPullRequestView(BitbucketPullRequest pr)
    {
        var reviewers = pr.Reviewers?.Select(ToUserSummary).Where(x => x is not null).Cast<UserSummary>().ToList();
        var participants = pr.Participants?.Select(p => new PullRequestParticipant(
            User: ToUserSummary(p.User),
            Role: p.Role,
            Approved: p.Approved,
            State: p.State,
            ParticipatedOn: p.ParticipatedOn)).ToList();

        return new PullRequestView(
            Id: pr.Id,
            Title: pr.Title,
            State: pr.State,
            Description: pr.Description,
            Author: ToUserSummary(pr.Author),
            SourceBranch: pr.Source?.Branch?.Name,
            DestinationBranch: pr.Destination?.Branch?.Name,
            HtmlUrl: pr.Links?.Html?.Href,
            CreatedOn: pr.CreatedOn,
            UpdatedOn: pr.UpdatedOn,
            Reviewers: reviewers,
            Participants: participants);
    }

    public static PullRequestComment ToPullRequestComment(BitbucketComment comment)
    {
        return new PullRequestComment(
            Id: comment.Id,
            User: ToUserSummary(comment.User),
            Body: comment.Content?.Raw,
            HtmlUrl: comment.Links?.Html?.Href,
            CreatedOn: comment.CreatedOn,
            Inline: comment.Inline is null
                ? null
                : new PullRequestCommentInline(
                    Path: comment.Inline.Path,
                    To: comment.Inline.To,
                    From: comment.Inline.From,
                    StartTo: comment.Inline.StartTo,
                    StartFrom: comment.Inline.StartFrom));
    }
}

