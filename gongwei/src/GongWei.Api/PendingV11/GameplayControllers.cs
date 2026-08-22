using GongWei.Api.Contracts;
using GongWei.Api.Http;
using GongWei.Application.Abstractions;
using GongWei.Application.Economy;
using GongWei.Application.Events;
using GongWei.Application.Reproduction;
using GongWei.Domain.Common;
using GongWei.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GongWei.Api.Controllers;

[ApiController]
[Route("api/v1/world")]
[AllowAnonymous]
public sealed class WorldController(GongWeiDbContext db) : ControllerBase
{
    [HttpGet("state")]
    public async Task<ActionResult<WorldStateResponse>> State(CancellationToken ct)
    {
        var world = await db.WorldState.AsNoTracking().FirstAsync(ct);

        Response.Headers.ETag = ETagHelper.Format(world.Version);

        return Ok(new WorldStateResponse(
            world.CurrentChapterNo,
            world.CurrentChapterTitle,
            world.CourtYear,
            world.CourtMonth,
            world.CourtDay,
            EnumNaming.ToDbValue(world.Season)));
    }

    [HttpGet("map")]
    public async Task<ActionResult<IReadOnlyList<MapLocationResponse>>> Map(CancellationToken ct)
    {
        var locations = await db.WorldLocations
            .Where(l => l.IsActive)
            .OrderBy(l => l.DisplayName)
            .Select(l => new MapLocationResponse(
                l.Id, l.Code, l.DisplayName,
                l.Kind == LocationKind.Palace ? "palace" : l.Kind.ToString().ToLower(),
                l.MapX, l.MapY, l.Description, l.IconKey))
            .ToListAsync(ct);

        return Ok(locations);
    }

    [HttpGet("announcements")]
    public async Task<ActionResult<IReadOnlyList<AnnouncementResponse>>> Announcements(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;

        var announcements = await db.Announcements
            .Where(a => a.PublishedAt != null && a.StartsAt <= now && (a.EndsAt == null || a.EndsAt > now))
            .OrderByDescending(a => a.IsPinned)
            .ThenByDescending(a => a.StartsAt)
            .Select(a => new AnnouncementResponse(
                a.Id, a.Title, a.Body,
                a.Severity == AnnouncementSeverity.Info ? "info" : a.Severity.ToString().ToLower(),
                a.IsPinned, a.StartsAt, a.EndsAt))
            .ToListAsync(ct);

        return Ok(announcements);
    }

    /// <summary>Players only ever read published story content (spec §6.9).</summary>
    [HttpGet("story/chapters")]
    public async Task<ActionResult<IReadOnlyList<StoryChapterResponse>>> Chapters(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;

        var chapters = await db.StoryChapters
            .Where(c => c.Status == PublishStatus.Published
                        && c.Arc!.Status == PublishStatus.Published
                        && (c.OpensAt == null || c.OpensAt <= now))
            .OrderBy(c => c.Arc!.SortOrder)
            .ThenBy(c => c.ChapterNo)
            .Select(c => new StoryChapterResponse(
                c.Id, c.Code, c.ChapterNo, c.Title, c.Summary, c.EntryNodeId))
            .ToListAsync(ct);

        return Ok(chapters);
    }

    [HttpGet("story/chapters/{chapterId:guid}/nodes")]
    public async Task<ActionResult<IReadOnlyList<StoryNodeResponse>>> Nodes(
        Guid chapterId,
        CancellationToken ct)
    {
        var isPublished = await db.StoryChapters.AnyAsync(
            c => c.Id == chapterId
                 && c.Status == PublishStatus.Published
                 && c.Arc!.Status == PublishStatus.Published,
            ct);

        if (!isPublished)
        {
            throw DomainException.Conflict(
                ErrorCodes.ContentNotPublished, "That chapter is not published.");
        }

        var nodes = await db.StoryNodes
            .Where(n => n.ChapterId == chapterId)
            .OrderBy(n => n.SortOrder)
            .Select(n => new StoryNodeResponse(
                n.Id, n.Code,
                n.NodeType == StoryNodeType.Narrative ? "narrative" : n.NodeType.ToString().ToLower(),
                n.Title, n.Body, n.IsEntry, n.EventRoomId, n.Options))
            .ToListAsync(ct);

        return Ok(nodes);
    }
}

[ApiController]
[Route("api/v1/events")]
public sealed class EventsController(
    EventService events,
    GongWeiDbContext db,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<EventSummaryResponse>>> List(
        [FromQuery] string? status,
        CancellationToken ct = default)
    {
        var myCharacterId = await CurrentCharacterIdAsync(ct);

        var query = db.EventRooms.Where(e => e.Status != EventRoomStatus.Draft);

        if (status is not null)
        {
            var parsed = EnumNaming.FromDbValue<EventRoomStatus>(status);
            query = query.Where(e => e.Status == parsed);
        }

        var rooms = await query
            .OrderByDescending(e => e.OpensAt)
            .Take(200)
            .Select(e => new EventSummaryResponse(
                e.Id, e.Code, e.Title, e.Summary,
                e.Status == EventRoomStatus.Open ? "open" : e.Status.ToString().ToLower(),
                e.OpensAt, e.ClosesAt,
                e.Participants.Count(p => p.LeftAt == null && p.DisqualifiedAt == null),
                e.MaxParticipants,
                myCharacterId != null && e.Participants.Any(p => p.CharacterId == myCharacterId),
                e.Version))
            .ToListAsync(ct);

        return Ok(rooms);
    }

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<EventDetailResponse>> Get(Guid id, CancellationToken ct)
    {
        var myCharacterId = await CurrentCharacterIdAsync(ct);

        var room = await db.EventRooms
                       .Where(e => e.Id == id && e.Status != EventRoomStatus.Draft)
                       .Select(e => new
                       {
                           Room = e,
                           ParticipantCount = e.Participants.Count(
                               p => p.LeftAt == null && p.DisqualifiedAt == null),
                           HasJoined = myCharacterId != null
                                       && e.Participants.Any(p => p.CharacterId == myCharacterId),
                           LocationName = e.LocationId == null
                               ? null
                               : db.WorldLocations
                                   .Where(l => l.Id == e.LocationId)
                                   .Select(l => l.DisplayName)
                                   .FirstOrDefault()
                       })
                       .FirstOrDefaultAsync(ct)
                   ?? throw DomainException.NotFound("Event", id);

        Response.Headers.ETag = ETagHelper.Format(room.Room.Version);

        var summary = new EventSummaryResponse(
            room.Room.Id, room.Room.Code, room.Room.Title, room.Room.Summary,
            EnumNaming.ToDbValue(room.Room.Status),
            room.Room.OpensAt, room.Room.ClosesAt,
            room.ParticipantCount, room.Room.MaxParticipants, room.HasJoined, room.Room.Version);

        return Ok(new EventDetailResponse(
            summary,
            room.Room.Body,
            room.LocationName,
            room.Room.MaxPostsPerCharacter,
            EnumNaming.ToDbValue(room.Room.Visibility)));
    }

    [HttpPost("{id:guid}/join")]
    [Authorize]
    [RequireIdempotency]
    public async Task<IActionResult> Join(Guid id, [FromBody] JoinEventRequest request, CancellationToken ct)
    {
        await events.JoinAsync(id, request.CharacterId, ct);
        return NoContent();
    }

    [HttpGet("{id:guid}/posts")]
    [AllowAnonymous]
    public async Task<ActionResult<CursorPage<EventPostResponse>>> Posts(
        Guid id,
        [FromQuery] string? cursor,
        [FromQuery] int limit = 50,
        CancellationToken ct = default)
    {
        limit = Math.Clamp(limit, 1, 100);

        var query = db.EventPosts
            .Where(p => p.EventRoomId == id && p.Status == EventPostStatus.Visible);

        if (cursor is not null && DateTimeOffset.TryParse(cursor, out var after))
        {
            query = query.Where(p => p.CreatedAt > after);
        }

        var posts = await query
            .OrderBy(p => p.CreatedAt)
            .ThenBy(p => p.Id)
            .Take(limit + 1)
            .Select(p => new { Post = p, CharacterName = p.Character!.DisplayName })
            .ToListAsync(ct);

        var hasMore = posts.Count > limit;
        var page = posts.Take(limit).ToList();

        return Ok(new CursorPage<EventPostResponse>(
            page.Select(p => EventPostResponse.From(p.Post, p.CharacterName)).ToList(),
            hasMore ? page[^1].Post.CreatedAt.ToString("O") : null));
    }

    [HttpPost("{id:guid}/posts")]
    [Authorize]
    [RequireIdempotency]
    public async Task<ActionResult<EventPostResponse>> CreatePost(
        Guid id,
        [FromBody] CreateEventPostRequest request,
        CancellationToken ct)
    {
        var post = await events.PostAsync(id, request.CharacterId, request.Body, request.ClientRequestId, ct);

        var characterName = await db.Characters
            .Where(c => c.Id == post.CharacterId)
            .Select(c => c.DisplayName)
            .FirstAsync(ct);

        Response.Headers.ETag = ETagHelper.Format(post.Version);
        return Created($"/api/v1/events/{id}/posts/{post.Id}", EventPostResponse.From(post, characterName));
    }

    [HttpPatch("posts/{postId:guid}")]
    [Authorize]
    public async Task<ActionResult<EventPostResponse>> EditPost(
        Guid postId,
        [FromBody] EditEventPostRequest request,
        CancellationToken ct)
    {
        var version = ETagHelper.RequireIfMatch(Request);
        var post = await events.EditPostAsync(postId, version, request.Body, ct);

        var characterName = await db.Characters
            .Where(c => c.Id == post.CharacterId)
            .Select(c => c.DisplayName)
            .FirstAsync(ct);

        Response.Headers.ETag = ETagHelper.Format(post.Version);
        return Ok(EventPostResponse.From(post, characterName));
    }

    private async Task<Guid?> CurrentCharacterIdAsync(CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId)
        {
            return null;
        }

        return await MeController.CurrentCharacterQuery(db, userId)
            .Select(c => (Guid?)c.Id)
            .FirstOrDefaultAsync(ct);
    }
}

[ApiController]
[Route("api/v1/economy")]
[Authorize]
public sealed class EconomyController(MarketService market, GongWeiDbContext db) : ControllerBase
{
    [HttpGet("offers")]
    public async Task<ActionResult<IReadOnlyList<MarketOfferResponse>>> Offers(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;

        var offers = await db.MarketOffers
            .Where(o => o.IsActive && o.StartsAt <= now && (o.EndsAt == null || o.EndsAt > now))
            .OrderBy(o => o.UnitPrice)
            .Select(o => new MarketOfferResponse(
                o.Id, o.Code, o.ItemDefinitionId,
                o.ItemDefinition!.DisplayName, o.ItemDefinition.Description,
                o.CurrencyCode, o.UnitPrice, o.StockRemaining, o.PerCharacterLimit,
                o.StartsAt, o.EndsAt))
            .ToListAsync(ct);

        return Ok(offers);
    }

    [HttpPost("purchases")]
    [RequireIdempotency]
    public async Task<ActionResult<PurchaseResult>> Purchase(
        [FromBody] PurchaseRequest request,
        CancellationToken ct)
    {
        var key = Request.Headers[IdempotencyMiddleware.HeaderName].ToString();

        var result = await market.PurchaseAsync(
            request.CharacterId, request.OfferId, request.Quantity, key, ct);

        return Ok(result);
    }

    [HttpGet("characters/{characterId:guid}/inventory")]
    public async Task<ActionResult<IReadOnlyList<InventoryItemResponse>>> Inventory(
        Guid characterId,
        CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;

        var items = await db.InventoryEntries
            .Where(e => e.CharacterId == characterId
                        && e.Quantity > 0
                        && (e.ExpiresAt == null || e.ExpiresAt > now))
            .Select(e => new InventoryItemResponse(
                e.Id, e.ItemDefinitionId, e.ItemDefinition!.DisplayName,
                e.ItemDefinition.Category == ItemCategory.Gift ? "gift" : e.ItemDefinition.Category.ToString().ToLower(),
                e.Quantity, e.ItemDefinition.IsConsumable, e.ExpiresAt))
            .ToListAsync(ct);

        return Ok(items);
    }

    [HttpPost("inventory/{entryId:guid}/use")]
    [RequireIdempotency]
    public async Task<IActionResult> UseItem(
        Guid entryId,
        [FromBody] UseItemRequest request,
        CancellationToken ct)
    {
        await market.UseItemAsync(request.CharacterId, entryId, request.Quantity, ct);
        return NoContent();
    }

    [HttpGet("characters/{characterId:guid}/ledger")]
    public async Task<ActionResult<CursorPage<LedgerEntryResponse>>> Ledger(
        Guid characterId,
        [FromQuery] string? cursor,
        [FromQuery] int limit = 50,
        CancellationToken ct = default)
    {
        limit = Math.Clamp(limit, 1, 100);

        var query = db.LedgerEntries.Where(e => e.CharacterId == characterId);

        if (cursor is not null && DateTimeOffset.TryParse(cursor, out var before))
        {
            query = query.Where(e => e.CreatedAt < before);
        }

        var entries = await query
            .OrderByDescending(e => e.CreatedAt)
            .ThenByDescending(e => e.Id)
            .Take(limit + 1)
            .Select(e => new
            {
                e.Id,
                e.CurrencyCode,
                e.Amount,
                e.BalanceAfter,
                Reason = e.Transaction!.Reason,
                e.CreatedAt
            })
            .ToListAsync(ct);

        var hasMore = entries.Count > limit;
        var page = entries.Take(limit).ToList();

        return Ok(new CursorPage<LedgerEntryResponse>(
            page.Select(e => new LedgerEntryResponse(
                e.Id, e.CurrencyCode, e.Amount, e.BalanceAfter,
                EnumNaming.ToDbValue(e.Reason), e.CreatedAt)).ToList(),
            hasMore ? page[^1].CreatedAt.ToString("O") : null));
    }
}

[ApiController]
[Route("api/v1/reproduction")]
[Authorize]
public sealed class ReproductionController(
    ReproductionService reproduction,
    GongWeiDbContext db) : ControllerBase
{
    [HttpGet("status")]
    public async Task<ActionResult<ReproductionStatusResponse>> Status(CancellationToken ct)
    {
        var status = await reproduction.GetStatusAsync(ct);

        return Ok(new ReproductionStatusResponse(
            status.IsOpen, status.HoldReason, status.WaitingCount,
            status.OngoingPregnancyCount, status.AvailableSlots));
    }

    [HttpPost("audiences")]
    [RequireIdempotency]
    public async Task<IActionResult> RequestAudience(
        [FromBody] AudienceRequestBody request,
        CancellationToken ct)
    {
        var key = Request.Headers[IdempotencyMiddleware.HeaderName].ToString();
        var created = await reproduction.RequestAudienceAsync(request.CharacterId, request.Kind, key, ct);

        return Accepted(new
        {
            id = created.Id,
            status = EnumNaming.ToDbValue(created.Status),
            requestedAt = created.RequestedAt
        });
    }

    [HttpGet("pregnancies/mine")]
    public async Task<ActionResult<PregnancyResponse>> MyPregnancy(
        [FromQuery] Guid characterId,
        CancellationToken ct)
    {
        var pregnancy = await db.Pregnancies
            .Where(p => p.MotherCharacterId == characterId)
            .OrderByDescending(p => p.ConceivedAt)
            .FirstOrDefaultAsync(ct);

        if (pregnancy is null)
        {
            return NoContent();
        }

        Response.Headers.ETag = ETagHelper.Format(pregnancy.Version);
        return Ok(PregnancyResponse.From(pregnancy));
    }

    /// <summary>
    /// The wait pool is public information: how many heirs are queued shapes everyone's
    /// plans. Individual identities are not exposed.
    /// </summary>
    [HttpGet("wait-pool")]
    public async Task<IActionResult> WaitPool(CancellationToken ct)
    {
        var waiting = await db.HeirWaitPoolEntries
            .Where(e => e.Status == WaitPoolStatus.Waiting)
            .CountAsync(ct);

        return Ok(new { waitingCount = waiting });
    }
}

[ApiController]
[Route("api/v1/notifications")]
[Authorize]
public sealed class NotificationsController(GongWeiDbContext db, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<CursorPage<NotificationResponse>>> List(
        [FromQuery] bool unreadOnly = false,
        [FromQuery] string? cursor = null,
        [FromQuery] int limit = 30,
        CancellationToken ct = default)
    {
        var userId = currentUser.RequireUserId();
        limit = Math.Clamp(limit, 1, 100);

        var query = db.Notifications.Where(n => n.UserId == userId);

        if (unreadOnly)
        {
            query = query.Where(n => n.ReadAt == null);
        }

        if (cursor is not null && DateTimeOffset.TryParse(cursor, out var before))
        {
            query = query.Where(n => n.CreatedAt < before);
        }

        var notifications = await query
            .OrderByDescending(n => n.CreatedAt)
            .ThenByDescending(n => n.Id)
            .Take(limit + 1)
            .ToListAsync(ct);

        var hasMore = notifications.Count > limit;
        var page = notifications.Take(limit).ToList();

        return Ok(new CursorPage<NotificationResponse>(
            page.Select(n => new NotificationResponse(
                n.Id, EnumNaming.ToDbValue(n.Kind), n.Title, n.Body, n.LinkPath,
                n.IsUnread, n.CreatedAt)).ToList(),
            hasMore ? page[^1].CreatedAt.ToString("O") : null));
    }

    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct)
    {
        var userId = currentUser.RequireUserId();

        var updated = await db.Notifications
            .Where(n => n.Id == id && n.UserId == userId && n.ReadAt == null)
            .ExecuteUpdateAsync(n => n.SetProperty(x => x.ReadAt, DateTimeOffset.UtcNow), ct);

        return updated == 0 ? NoContent() : NoContent();
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct)
    {
        var userId = currentUser.RequireUserId();

        var updated = await db.Notifications
            .Where(n => n.UserId == userId && n.ReadAt == null)
            .ExecuteUpdateAsync(n => n.SetProperty(x => x.ReadAt, DateTimeOffset.UtcNow), ct);

        return Ok(new { markedRead = updated });
    }
}
