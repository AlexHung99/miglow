using System.Data;
using GongWei.Application.Abstractions;
using GongWei.Domain.Characters;
using GongWei.Domain.Common;
using GongWei.Domain.Events;
using Microsoft.EntityFrameworkCore;

namespace GongWei.Application.Events;

/// <summary>
/// Event text follows draft → submitted → under_review → approved (§6.10). Nothing is
/// visible to other players until it is approved, and every state change keeps a
/// permanent revision.
/// </summary>
public sealed class EventPostService(
    IGongWeiDb db,
    IClock clock,
    ICurrentUser currentUser,
    IAuditWriter audit,
    IOutboxWriter outbox)
{
    // ------------------------------------------------------------------ player

    /// <summary>
    /// Creates a draft. A draft may be blank, and repeated saves are expected — the front
    /// end throttles autosave so not every keystroke becomes a revision.
    /// </summary>
    public async Task<EventPost> SaveDraftAsync(
        Guid eventRoomId,
        Guid characterId,
        string bodyMarkdown,
        string clientRequestId,
        CancellationToken ct = default)
    {
        var userId = currentUser.RequireUserId();
        var now = clock.UtcNow;

        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);

        var replay = await db.EventPosts.FirstOrDefaultAsync(
            p => p.EventRoomId == eventRoomId
                 && p.CharacterId == characterId
                 && p.ClientRequestId == clientRequestId,
            ct);

        if (replay is not null)
        {
            await tx.CommitAsync(ct);
            return replay;
        }

        var room = await db.EventRooms.FirstOrDefaultAsync(e => e.Id == eventRoomId, ct)
                   ?? throw DomainException.NotFound("Event", eventRoomId);

        var character = await LoadOwnCharacterAsync(characterId, userId, ct);
        character.EnsureCanAct();

        if (!room.AcceptsPostsAt(now))
        {
            throw DomainException.Conflict(ErrorCodes.EventNotOpen, $"《{room.Title}》目前不接受投稿。");
        }

        await EnsureParticipantAsync(eventRoomId, characterId, ct);
        EnsureBodyLength(bodyMarkdown);

        var post = new EventPost
        {
            EventRoomId = eventRoomId,
            CharacterId = characterId,
            BodyMarkdown = bodyMarkdown,
            Status = EventPostStatus.Draft,
            ClientRequestId = clientRequestId,
            CreatedAt = now
        };

        db.EventPosts.Add(post);
        await AddRevisionAsync(post, EventPostRevisionKind.DraftSave, userId, now, ct);

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return post;
    }

    /// <summary>Updates a draft or a post that was sent back for revision.</summary>
    public async Task<EventPost> UpdateDraftAsync(
        Guid postId,
        long expectedVersion,
        string bodyMarkdown,
        CancellationToken ct = default)
    {
        var userId = currentUser.RequireUserId();
        var now = clock.UtcNow;

        var post = await LoadOwnPostAsync(postId, userId, ct);
        post.EnsureVersion(expectedVersion);

        if (!post.IsAuthorEditable)
        {
            throw DomainException.Conflict(
                ErrorCodes.ConflictState,
                $"投稿目前為 {EnumNaming.ToDbValue(post.Status)}，送出後需等待審核或退修才能修改。");
        }

        EnsureBodyLength(bodyMarkdown);

        post.BodyMarkdown = bodyMarkdown;
        post.EditedAt = now;

        await AddRevisionAsync(post, EventPostRevisionKind.DraftSave, userId, now, ct);
        await db.SaveChangesAsync(ct);

        return post;
    }

    /// <summary>Locks the text and queues it for review. The author cannot edit after this.</summary>
    public async Task<EventPost> SubmitAsync(
        Guid postId,
        long expectedVersion,
        CancellationToken ct = default)
    {
        var userId = currentUser.RequireUserId();
        var now = clock.UtcNow;

        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);

        var post = await LoadOwnPostAsync(postId, userId, ct);
        post.EnsureVersion(expectedVersion);

        if (post.Status is not (EventPostStatus.Draft or EventPostStatus.NeedsRevision))
        {
            throw DomainException.Conflict(
                ErrorCodes.ConflictState, $"投稿目前為 {EnumNaming.ToDbValue(post.Status)}，無法送出。");
        }

        var room = await db.EventRooms.FirstOrDefaultAsync(e => e.Id == post.EventRoomId, ct)
                   ?? throw DomainException.NotFound("Event", post.EventRoomId);

        if (!room.AcceptsPostsAt(now))
        {
            throw DomainException.Conflict(ErrorCodes.EventNotOpen, $"《{room.Title}》已停止收件。");
        }

        await EnsureParticipantAsync(post.EventRoomId, post.CharacterId, ct);

        if (string.IsNullOrWhiteSpace(post.BodyMarkdown))
        {
            throw DomainException.FieldErrors(new Dictionary<string, string[]>
            {
                ["bodyMarkdown"] = ["送出前正文不可為空白"]
            });
        }

        post.Status = EventPostStatus.Submitted;
        post.SubmittedAt = now;

        await AddRevisionAsync(post, EventPostRevisionKind.Submit, userId, now, ct);

        audit.Write("event_post.submit", "event_post", post.Id);
        outbox.Enqueue("event_post.submitted", "event_post", post.Id,
            new { postId = post.Id, post.EventRoomId, post.CharacterId });

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return post;
    }

    /// <summary>
    /// Withdraws a draft, or a submission a reviewer has not claimed yet. The text and its
    /// revisions are kept permanently — only the status changes (§6.10 step 6).
    /// </summary>
    public async Task<EventPost> WithdrawAsync(
        Guid postId,
        long expectedVersion,
        string? reason,
        CancellationToken ct = default)
    {
        var userId = currentUser.RequireUserId();
        var now = clock.UtcNow;

        var post = await LoadOwnPostAsync(postId, userId, ct);
        post.EnsureVersion(expectedVersion);

        if (!post.IsWithdrawable)
        {
            throw DomainException.Conflict(
                ErrorCodes.ConflictState,
                $"投稿目前為 {EnumNaming.ToDbValue(post.Status)}，已無法撤回。");
        }

        post.Status = EventPostStatus.Withdrawn;
        post.ReviewNote = reason;

        audit.Write("event_post.withdraw", "event_post", post.Id, reason: reason);
        await db.SaveChangesAsync(ct);

        return post;
    }

    // ------------------------------------------------------------------- admin

    /// <summary>Submitted → UnderReview, so two reviewers do not work the same post.</summary>
    public async Task<EventPost> ClaimAsync(Guid postId, CancellationToken ct = default)
    {
        currentUser.RequireRole(AdminRole.GameMaster, AdminRole.Moderator);

        var reviewerId = currentUser.RequireUserId();
        var now = clock.UtcNow;

        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);

        await db.LockRowAsync("event_posts", postId, ct);

        var post = await LoadAsync(postId, ct);

        if (post.Status == EventPostStatus.UnderReview && post.ReviewedBy == reviewerId)
        {
            await tx.CommitAsync(ct);
            return post;
        }

        if (post.Status != EventPostStatus.Submitted)
        {
            throw DomainException.Conflict(
                ErrorCodes.ConflictState, $"只有已送出的投稿可以認領，目前為 {EnumNaming.ToDbValue(post.Status)}。");
        }

        post.Status = EventPostStatus.UnderReview;
        post.ReviewedBy = reviewerId;

        audit.Write("event_post.claim", "event_post", post.Id);

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return post;
    }

    public async Task<EventPost> RequestRevisionAsync(
        Guid postId,
        string note,
        CancellationToken ct = default)
    {
        currentUser.RequireRole(AdminRole.GameMaster, AdminRole.Moderator);

        var reviewerId = currentUser.RequireUserId();
        var now = clock.UtcNow;

        if (string.IsNullOrWhiteSpace(note))
        {
            throw DomainException.FieldErrors(new Dictionary<string, string[]>
            {
                ["note"] = ["退修必須說明原因"]
            });
        }

        var post = await LoadAsync(postId, ct);
        EnsureReviewable(post);

        post.Status = EventPostStatus.NeedsRevision;
        post.ReviewedBy = reviewerId;
        post.ReviewedAt = now;
        post.ReviewNote = note;

        await AddRevisionAsync(post, EventPostRevisionKind.RevisionRequest, reviewerId, now, ct);

        audit.Write("event_post.request_revision", "event_post", post.Id, reason: note);
        outbox.Enqueue("event_post.needs_revision", "event_post", post.Id,
            new { postId = post.Id, post.CharacterId, note });

        await db.SaveChangesAsync(ct);
        return post;
    }

    /// <summary>Approval is what sets published_at and puts the post into the public feed.</summary>
    public async Task<EventPost> ApproveAsync(
        Guid postId,
        string? note,
        CancellationToken ct = default)
    {
        currentUser.RequireRole(AdminRole.GameMaster, AdminRole.Moderator);

        var reviewerId = currentUser.RequireUserId();
        var now = clock.UtcNow;

        var post = await LoadAsync(postId, ct);
        EnsureReviewable(post);

        post.Status = EventPostStatus.Approved;
        post.ReviewedBy = reviewerId;
        post.ReviewedAt = now;
        post.ReviewNote = note;
        post.PublishedAt = now;

        await AddRevisionAsync(post, EventPostRevisionKind.Approval, reviewerId, now, ct);

        audit.Write("event_post.approve", "event_post", post.Id, reason: note);
        outbox.Enqueue("event_post.approved", "event_post", post.Id,
            new { postId = post.Id, post.EventRoomId, post.CharacterId });

        await db.SaveChangesAsync(ct);
        return post;
    }

    public async Task<EventPost> RejectAsync(Guid postId, string note, CancellationToken ct = default)
    {
        currentUser.RequireRole(AdminRole.GameMaster, AdminRole.Moderator);

        var reviewerId = currentUser.RequireUserId();
        var now = clock.UtcNow;

        if (string.IsNullOrWhiteSpace(note))
        {
            throw DomainException.FieldErrors(new Dictionary<string, string[]>
            {
                ["note"] = ["拒絕必須說明原因"]
            });
        }

        var post = await LoadAsync(postId, ct);
        EnsureReviewable(post);

        post.Status = EventPostStatus.Rejected;
        post.ReviewedBy = reviewerId;
        post.ReviewedAt = now;
        post.ReviewNote = note;

        audit.Write("event_post.reject", "event_post", post.Id, reason: note);
        outbox.Enqueue("event_post.rejected", "event_post", post.Id,
            new { postId = post.Id, post.CharacterId, note });

        await db.SaveChangesAsync(ct);
        return post;
    }

    /// <summary>Hides an already-published post without deleting the original text.</summary>
    public async Task<EventPost> ModerateAsync(
        Guid postId,
        string note,
        CancellationToken ct = default)
    {
        currentUser.RequireRole(AdminRole.GameMaster, AdminRole.Moderator);

        var moderatorId = currentUser.RequireUserId();
        var now = clock.UtcNow;

        var post = await LoadAsync(postId, ct);

        post.Status = EventPostStatus.Moderated;
        post.ModeratedBy = moderatorId;
        post.ModerationNote = note;
        post.PublishedAt = null;

        await AddRevisionAsync(post, EventPostRevisionKind.Moderation, moderatorId, now, ct);

        audit.Write("event_post.moderate", "event_post", post.Id, reason: note);
        await db.SaveChangesAsync(ct);

        return post;
    }

    // ----------------------------------------------------------------- helpers

    private static void EnsureReviewable(EventPost post)
    {
        if (post.Status is not (EventPostStatus.Submitted or EventPostStatus.UnderReview))
        {
            throw DomainException.Conflict(
                ErrorCodes.ConflictState,
                $"投稿目前為 {EnumNaming.ToDbValue(post.Status)}，不在可審核狀態。");
        }
    }

    private static void EnsureBodyLength(string body)
    {
        if (body.Length > EventPost.MaxBodyLength)
        {
            throw DomainException.FieldErrors(new Dictionary<string, string[]>
            {
                ["bodyMarkdown"] = [$"正文不可超過 {EventPost.MaxBodyLength} 字"]
            });
        }
    }

    private async Task AddRevisionAsync(
        EventPost post,
        EventPostRevisionKind kind,
        Guid changedBy,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var next = await db.EventPostRevisions
            .Where(r => r.EventPostId == post.Id)
            .MaxAsync(r => (int?)r.RevisionNo, ct) ?? 0;

        db.EventPostRevisions.Add(new EventPostRevision
        {
            EventPostId = post.Id,
            RevisionNo = next + 1,
            BodyMarkdown = post.BodyMarkdown,
            RevisionKind = kind,
            ChangedBy = changedBy,
            CreatedAt = now
        });
    }

    private async Task EnsureParticipantAsync(Guid eventRoomId, Guid characterId, CancellationToken ct)
    {
        var participant = await db.EventParticipants.FirstOrDefaultAsync(
                              p => p.EventRoomId == eventRoomId && p.CharacterId == characterId, ct)
                          ?? throw DomainException.Conflict(
                              ErrorCodes.EventNotOpen, "請先加入事件才能投稿。");

        if (!participant.IsEligible)
        {
            throw DomainException.Conflict(ErrorCodes.EventNotOpen, "此角色已無法在本事件投稿。");
        }
    }

    private async Task<EventPost> LoadAsync(Guid id, CancellationToken ct) =>
        await db.EventPosts.FirstOrDefaultAsync(p => p.Id == id, ct)
        ?? throw DomainException.NotFound("Event post", id);

    private async Task<EventPost> LoadOwnPostAsync(Guid postId, Guid userId, CancellationToken ct)
    {
        var post = await LoadAsync(postId, ct);

        var owned = await db.Characters.AnyAsync(c => c.Id == post.CharacterId && c.UserId == userId, ct);

        if (!owned)
        {
            throw DomainException.NotFound("Event post", postId);
        }

        return post;
    }

    private async Task<Character> LoadOwnCharacterAsync(Guid characterId, Guid userId, CancellationToken ct)
    {
        var character = await db.Characters.FirstOrDefaultAsync(c => c.Id == characterId, ct)
                        ?? throw DomainException.NotFound("Character", characterId);

        if (character.UserId != userId)
        {
            throw DomainException.NotFound("Character", characterId);
        }

        return character;
    }
}
