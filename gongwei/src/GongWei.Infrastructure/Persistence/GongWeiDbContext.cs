using System.Reflection;
using GongWei.Domain.Characters;
using GongWei.Domain.Common;
using GongWei.Domain.Economy;
using GongWei.Domain.Events;
using GongWei.Domain.Identity;
using GongWei.Domain.Intrigue;
using GongWei.Domain.Operations;
using GongWei.Domain.Reproduction;
using GongWei.Domain.World;
using GongWei.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace GongWei.Infrastructure.Persistence;

/// <summary>
/// The single EF Core context over the <c>game</c> schema. Its shape must stay
/// semantically identical to db/authoritative/v1.1/schema_v1.1.sql, which the Postgres
/// integration tests assert (README_v1.1 §5).
/// </summary>
public class GongWeiDbContext(DbContextOptions<GongWeiDbContext> options)
    : DbContext(options), Application.Abstractions.IGongWeiDb
{
    public const string SchemaName = "game";

    // --- identity & characters ---
    public DbSet<User> Users => Set<User>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<AdminRoleAssignment> AdminRoleAssignments => Set<AdminRoleAssignment>();
    public DbSet<PresetPortrait> PresetPortraits => Set<PresetPortrait>();
    public DbSet<MediaAsset> MediaAssets => Set<MediaAsset>();
    public DbSet<PlayerPortraitSubmission> PlayerPortraitSubmissions => Set<PlayerPortraitSubmission>();
    public DbSet<LineLoginAttempt> LineLoginAttempts => Set<LineLoginAttempt>();

    public DbSet<AdminCredential> AdminCredentials => Set<AdminCredential>();
    public DbSet<Rank> Ranks => Set<Rank>();
    public DbSet<CharacterTitleDefinition> CharacterTitleDefinitions => Set<CharacterTitleDefinition>();
    public DbSet<Residence> Residences => Set<Residence>();
    public DbSet<CharacterApplication> CharacterApplications => Set<CharacterApplication>();
    public DbSet<CharacterApplicationRevision> CharacterApplicationRevisions => Set<CharacterApplicationRevision>();
    public DbSet<Character> Characters => Set<Character>();
    public DbSet<CharacterTitleAssignment> CharacterTitleAssignments => Set<CharacterTitleAssignment>();
    public DbSet<CharacterStats> CharacterStats => Set<CharacterStats>();
    public DbSet<CharacterStatusHistory> CharacterStatusHistories => Set<CharacterStatusHistory>();
    public DbSet<RankHistory> RankHistories => Set<RankHistory>();
    public DbSet<CharacterResidenceHistory> CharacterResidenceHistories => Set<CharacterResidenceHistory>();
    public DbSet<AbilityLabelDefinition> AbilityLabelDefinitions => Set<AbilityLabelDefinition>();
    public DbSet<CharacterProgress> CharacterProgress => Set<CharacterProgress>();
    public DbSet<CharacterChronicleEntry> CharacterChronicleEntries => Set<CharacterChronicleEntry>();

    // --- world, NPC & events ---
    public DbSet<WorldState> WorldState => Set<WorldState>();
    public DbSet<GameSetting> GameSettings => Set<GameSetting>();
    public DbSet<GameSettingRevision> GameSettingRevisions => Set<GameSettingRevision>();
    public DbSet<WorldLocation> WorldLocations => Set<WorldLocation>();
    public DbSet<Npc> Npcs => Set<Npc>();
    public DbSet<NpcRevision> NpcRevisions => Set<NpcRevision>();
    public DbSet<EventRoom> EventRooms => Set<EventRoom>();
    public DbSet<EventParticipant> EventParticipants => Set<EventParticipant>();
    public DbSet<EventPost> EventPosts => Set<EventPost>();
    public DbSet<EventPostRevision> EventPostRevisions => Set<EventPostRevision>();
    public DbSet<EventResult> EventResults => Set<EventResult>();
    public DbSet<ExternalPlaySubmission> ExternalPlaySubmissions => Set<ExternalPlaySubmission>();

    // --- economy ---
    public DbSet<Currency> Currencies => Set<Currency>();
    public DbSet<Wallet> Wallets => Set<Wallet>();
    public DbSet<LedgerTransaction> LedgerTransactions => Set<LedgerTransaction>();
    public DbSet<LedgerEntry> LedgerEntries => Set<LedgerEntry>();
    public DbSet<ItemDefinition> ItemDefinitions => Set<ItemDefinition>();
    public DbSet<InventoryEntry> InventoryEntries => Set<InventoryEntry>();
    public DbSet<InventoryTransaction> InventoryTransactions => Set<InventoryTransaction>();
    public DbSet<MarketOffer> MarketOffers => Set<MarketOffer>();
    public DbSet<Purchase> Purchases => Set<Purchase>();

    // --- reproduction & intrigue ---
    public DbSet<ReproductionControl> ReproductionControl => Set<ReproductionControl>();
    public DbSet<HeirWaitPoolEntry> HeirWaitPoolEntries => Set<HeirWaitPoolEntry>();
    public DbSet<AudienceRequest> AudienceRequests => Set<AudienceRequest>();
    public DbSet<Pregnancy> Pregnancies => Set<Pregnancy>();
    public DbSet<Birth> Births => Set<Birth>();
    public DbSet<OffspringLink> OffspringLinks => Set<OffspringLink>();
    public DbSet<IntrigueAction> IntrigueActions => Set<IntrigueAction>();
    public DbSet<StatusEffect> StatusEffects => Set<StatusEffect>();
    public DbSet<Death> Deaths => Set<Death>();

    // --- operations ---
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<Announcement> Announcements => Set<Announcement>();
    public DbSet<ApprovalRequest> ApprovalRequests => Set<ApprovalRequest>();
    public DbSet<ApprovalDecision> ApprovalDecisions => Set<ApprovalDecision>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<ScheduledJob> ScheduledJobs => Set<ScheduledJob>();
    public DbSet<JobRun> JobRuns => Set<JobRun>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(SchemaName);
        modelBuilder.HasPostgresExtension("pgcrypto");
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        ApplyEnumConversions(modelBuilder);
        ApplySnakeCaseColumnNames(modelBuilder);
    }

    /// <summary>
    /// Every enum in the model round-trips through its snake_cased name. Doing this in
    /// one pass keeps the per-entity configurations free of 200 repetitive conversion calls.
    /// </summary>
    private static void ApplyEnumConversions(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                var clrType = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
                if (!clrType.IsEnum)
                {
                    continue;
                }

                var converterType = typeof(SnakeCaseEnumConverter<>).MakeGenericType(clrType);
                property.SetValueConverter((ValueConverter)Activator.CreateInstance(converterType)!);
                property.SetColumnType("text");
            }
        }
    }

    /// <summary>
    /// Maps PascalCase properties onto the snake_case columns in schema_v1.1.sql.
    /// A column name set explicitly in a configuration wins, so the handful of
    /// mismatches (WorldState.Id -> singleton_id) stay where they are declared.
    /// </summary>
    private const string ColumnNameAnnotation = "Relational:ColumnName";

    private static void ApplySnakeCaseColumnNames(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.FindAnnotation(ColumnNameAnnotation) is not null)
                {
                    continue;
                }

                property.SetColumnName(EnumNaming.ToSnakeCase(property.Name));
            }
        }
    }
}
