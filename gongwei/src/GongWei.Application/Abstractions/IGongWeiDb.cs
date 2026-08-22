using GongWei.Domain.Characters;
using GongWei.Domain.Economy;
using GongWei.Domain.Events;
using GongWei.Domain.Identity;
using GongWei.Domain.Intrigue;
using GongWei.Domain.Operations;
using GongWei.Domain.Reproduction;
using GongWei.Domain.World;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace GongWei.Application.Abstractions;

/// <summary>
/// The persistence contract the Application layer codes against. Infrastructure owns
/// the Npgsql provider and the mapping; nothing above this interface knows about them.
/// </summary>
public interface IGongWeiDb
{
    DbSet<User> Users { get; }
    DbSet<UserSession> UserSessions { get; }
    DbSet<AdminRoleAssignment> AdminRoleAssignments { get; }
    DbSet<PresetPortrait> PresetPortraits { get; }
    DbSet<MediaAsset> MediaAssets { get; }
    DbSet<PlayerPortraitSubmission> PlayerPortraitSubmissions { get; }
    DbSet<LineLoginAttempt> LineLoginAttempts { get; }
    DbSet<AdminCredential> AdminCredentials { get; }
    DbSet<Rank> Ranks { get; }
    DbSet<CharacterTitleDefinition> CharacterTitleDefinitions { get; }
    DbSet<Residence> Residences { get; }
    DbSet<CharacterApplication> CharacterApplications { get; }
    DbSet<CharacterApplicationRevision> CharacterApplicationRevisions { get; }
    DbSet<Character> Characters { get; }
    DbSet<CharacterTitleAssignment> CharacterTitleAssignments { get; }
    DbSet<CharacterStats> CharacterStats { get; }
    DbSet<CharacterStatusHistory> CharacterStatusHistories { get; }
    DbSet<RankHistory> RankHistories { get; }
    DbSet<CharacterResidenceHistory> CharacterResidenceHistories { get; }
    DbSet<AbilityLabelDefinition> AbilityLabelDefinitions { get; }
    DbSet<CharacterProgress> CharacterProgress { get; }
    DbSet<CharacterChronicleEntry> CharacterChronicleEntries { get; }

    DbSet<WorldState> WorldState { get; }
    DbSet<GameSetting> GameSettings { get; }
    DbSet<GameSettingRevision> GameSettingRevisions { get; }
    DbSet<WorldLocation> WorldLocations { get; }
    DbSet<Npc> Npcs { get; }
    DbSet<NpcRevision> NpcRevisions { get; }
    DbSet<EventRoom> EventRooms { get; }
    DbSet<EventParticipant> EventParticipants { get; }
    DbSet<EventPost> EventPosts { get; }
    DbSet<EventPostRevision> EventPostRevisions { get; }
    DbSet<EventResult> EventResults { get; }
    DbSet<ExternalPlaySubmission> ExternalPlaySubmissions { get; }

    DbSet<Currency> Currencies { get; }
    DbSet<Wallet> Wallets { get; }
    DbSet<LedgerTransaction> LedgerTransactions { get; }
    DbSet<LedgerEntry> LedgerEntries { get; }
    DbSet<ItemDefinition> ItemDefinitions { get; }
    DbSet<InventoryEntry> InventoryEntries { get; }
    DbSet<InventoryTransaction> InventoryTransactions { get; }
    DbSet<MarketOffer> MarketOffers { get; }
    DbSet<Purchase> Purchases { get; }

    DbSet<ReproductionControl> ReproductionControl { get; }
    DbSet<HeirWaitPoolEntry> HeirWaitPoolEntries { get; }
    DbSet<AudienceRequest> AudienceRequests { get; }
    DbSet<Pregnancy> Pregnancies { get; }
    DbSet<Birth> Births { get; }
    DbSet<OffspringLink> OffspringLinks { get; }
    DbSet<IntrigueAction> IntrigueActions { get; }
    DbSet<StatusEffect> StatusEffects { get; }
    DbSet<Death> Deaths { get; }

    DbSet<Notification> Notifications { get; }
    DbSet<Announcement> Announcements { get; }
    DbSet<ApprovalRequest> ApprovalRequests { get; }
    DbSet<ApprovalDecision> ApprovalDecisions { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<IdempotencyRecord> IdempotencyRecords { get; }
    DbSet<OutboxMessage> OutboxMessages { get; }
    DbSet<ScheduledJob> ScheduledJobs { get; }
    DbSet<JobRun> JobRuns { get; }

    DatabaseFacade Database { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
