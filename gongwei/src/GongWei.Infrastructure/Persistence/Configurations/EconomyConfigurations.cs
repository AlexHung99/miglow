using GongWei.Domain.Characters;
using GongWei.Domain.Common;
using GongWei.Domain.Economy;
using GongWei.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GongWei.Infrastructure.Persistence.Configurations;

public class CurrencyConfiguration : IEntityTypeConfiguration<Currency>
{
    public void Configure(EntityTypeBuilder<Currency> b)
    {
        b.ToTable("currencies");

        b.HasKey(x => x.Code);
        b.Property(x => x.Code).HasMaxLength(30);
        b.Property(x => x.DisplayName).HasMaxLength(50).IsRequired();
        b.Property(x => x.IsActive).HasDefaultValue(true);
        b.Property(x => x.CreatedAt).CreatedNow();
    }
}

public class WalletConfiguration : IEntityTypeConfiguration<Wallet>
{
    public void Configure(EntityTypeBuilder<Wallet> b)
    {
        b.ToTable("wallets", t =>
        {
            t.HasCheckConstraint("ck_wallets_balance", "balance >= 0");
            t.HasCheckConstraint("ck_wallets_version", "version > 0");
        });

        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ClientGeneratedKey();
        b.Property(x => x.CurrencyCode).HasMaxLength(30).IsRequired();
        b.Property(x => x.Balance).HasDefaultValue(0L);
        b.DatabaseManagedVersion();

        b.HasOne(x => x.Character).WithMany()
            .HasForeignKey(x => x.CharacterId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Currency>().WithMany()
            .HasForeignKey(x => x.CurrencyCode)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.CharacterId, x.CurrencyCode }).IsUnique();
    }
}

public class LedgerTransactionConfiguration : IEntityTypeConfiguration<LedgerTransaction>
{
    public void Configure(EntityTypeBuilder<LedgerTransaction> b)
    {
        b.ToTable("ledger_transactions", t =>
            t.HasCheckConstraint("ck_lt_type",
                "transaction_type IN ('stipend', 'purchase', 'reward', 'item_use', " +
                "'admin_grant', 'admin_correction', 'refund')"));

        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ClientGeneratedKey();
        b.Property(x => x.TransactionType).HasMaxLength(40);
        b.Property(x => x.ReferenceType).HasMaxLength(60);
        // Mandatory: no admin money movement is anonymous (§6.11).
        b.Property(x => x.ReasonCode).HasMaxLength(80).IsRequired();
        b.Property(x => x.ReasonText).HasMaxLength(1000);
        b.Property(x => x.RequestId).HasMaxLength(80);
        b.Property(x => x.CreatedAt).CreatedNow();

        b.HasOne<User>().WithMany().HasForeignKey(x => x.InitiatedBy).OnDelete(DeleteBehavior.SetNull);
    }
}

public class LedgerEntryConfiguration : IEntityTypeConfiguration<LedgerEntry>
{
    public void Configure(EntityTypeBuilder<LedgerEntry> b)
    {
        b.ToTable("ledger_entries", t =>
        {
            t.HasCheckConstraint("ck_le_amount", "amount <> 0");
            t.HasCheckConstraint("ck_le_balance_after", "balance_after >= 0");
        });

        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ClientGeneratedKey();
        b.Property(x => x.CreatedAt).CreatedNow();

        b.HasOne(x => x.Transaction).WithMany(t => t.Entries)
            .HasForeignKey(x => x.TransactionId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Wallet).WithMany()
            .HasForeignKey(x => x.WalletId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.WalletId, x.CreatedAt, x.Id })
            .HasDatabaseName("ix_ledger_entries_wallet");
    }
}

public class ItemDefinitionConfiguration : IEntityTypeConfiguration<ItemDefinition>
{
    public void Configure(EntityTypeBuilder<ItemDefinition> b)
    {
        b.ToTable("item_definitions", t =>
        {
            t.HasCheckConstraint("ck_id_version_no", "version_no > 0");
            t.HasCheckConstraint("ck_id_category",
                "category IN ('clothing', 'medicine', 'poison', 'gift', 'quest', 'material', 'other')");
            t.HasCheckConstraint("ck_id_stack_limit", "stack_limit > 0");
            t.HasCheckConstraint("ck_id_effect_payload", "jsonb_typeof(effect_payload) = 'object'");
            t.HasCheckConstraint("ck_id_usage_rules", "jsonb_typeof(usage_rules) = 'object'");
        });

        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ClientGeneratedKey();
        b.Property(x => x.Code).HasMaxLength(80).IsRequired();
        b.Property(x => x.VersionNo).HasDefaultValue(1);
        b.Property(x => x.DisplayName).HasMaxLength(100).IsRequired();
        b.Property(x => x.Description).HasMaxLength(1500).HasDefaultValue(string.Empty);
        b.Property(x => x.Category).HasMaxLength(30);
        b.Property(x => x.StackLimit).HasDefaultValue(999);
        b.Property(x => x.IsConsumable).HasDefaultValue(false);
        b.Property(x => x.EffectPayload).JsonObject();
        b.Property(x => x.UsageRules).JsonObject();
        b.Property(x => x.IsActive).HasDefaultValue(true);
        b.Property(x => x.CreatedAt).CreatedNow();

        // Versioned: a new effect creates a new row, it never rewrites history (§6.5).
        b.HasIndex(x => new { x.Code, x.VersionNo }).IsUnique();
    }
}

public class InventoryEntryConfiguration : IEntityTypeConfiguration<InventoryEntry>
{
    public void Configure(EntityTypeBuilder<InventoryEntry> b)
    {
        b.ToTable("inventory_entries", t =>
        {
            t.HasCheckConstraint("ck_ie_quantity", "quantity >= 0");
            t.HasCheckConstraint("ck_ie_version", "version > 0");
        });

        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ClientGeneratedKey();
        b.Property(x => x.AcquiredAt).CreatedNow();
        b.DatabaseManagedVersion();

        b.HasOne(x => x.Character).WithMany()
            .HasForeignKey(x => x.CharacterId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.ItemDefinition).WithMany()
            .HasForeignKey(x => x.ItemDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);

        // NULLS NOT DISTINCT so a no-expiry stack is also unique per character+item.
        b.HasIndex(x => new { x.CharacterId, x.ItemDefinitionId, x.ExpiresAt })
            .IsUnique()
            .AreNullsDistinct(false);
        b.HasIndex(x => new { x.CharacterId, x.ItemDefinitionId })
            .HasDatabaseName("ix_inventory_entries_character_available")
            .HasFilter("quantity > 0");
    }
}

public class InventoryTransactionConfiguration : IEntityTypeConfiguration<InventoryTransaction>
{
    public void Configure(EntityTypeBuilder<InventoryTransaction> b)
    {
        b.ToTable("inventory_transactions", t =>
        {
            t.HasCheckConstraint("ck_it_type",
                "transaction_type IN ('purchase', 'reward', 'use', 'expire', " +
                "'admin_grant', 'admin_correction', 'refund')");
            t.HasCheckConstraint("ck_it_delta", "quantity_delta <> 0");
            t.HasCheckConstraint("ck_it_after", "quantity_after >= 0");
            t.HasCheckConstraint("ck_it_effect_snapshot", "jsonb_typeof(effect_snapshot) = 'object'");
        });

        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ClientGeneratedKey();
        b.Property(x => x.TransactionType).HasMaxLength(30);
        b.Property(x => x.EffectSnapshot).JsonObject();
        b.Property(x => x.ReferenceType).HasMaxLength(60);
        b.Property(x => x.ReasonCode).HasMaxLength(80);
        b.Property(x => x.ReasonText).HasMaxLength(1000);
        b.Property(x => x.RequestId).HasMaxLength(80);
        b.Property(x => x.CreatedAt).CreatedNow();

        b.HasOne<InventoryEntry>().WithMany()
            .HasForeignKey(x => x.InventoryEntryId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.InitiatedBy).OnDelete(DeleteBehavior.SetNull);

        b.HasIndex(x => new { x.InventoryEntryId, x.CreatedAt })
            .HasDatabaseName("ix_inventory_transactions_entry");
    }
}

public class MarketOfferConfiguration : IEntityTypeConfiguration<MarketOffer>
{
    public void Configure(EntityTypeBuilder<MarketOffer> b)
    {
        b.ToTable("market_offers", t =>
        {
            t.HasCheckConstraint("ck_mo_unit_price", "unit_price >= 0");
            t.HasCheckConstraint("ck_mo_stock_total", "stock_total IS NULL OR stock_total >= 0");
            t.HasCheckConstraint("ck_mo_stock_sold", "stock_sold >= 0");
            t.HasCheckConstraint("ck_mo_limit",
                "per_character_limit IS NULL OR per_character_limit > 0");
            t.HasCheckConstraint("ck_mo_eligibility", "jsonb_typeof(eligibility_rules) = 'object'");
            t.HasCheckConstraint("ck_mo_sold_within_total",
                "stock_total IS NULL OR stock_sold <= stock_total");
            t.HasCheckConstraint("ck_mo_window",
                "ends_at IS NULL OR starts_at IS NULL OR ends_at > starts_at");
            t.HasCheckConstraint("ck_mo_version", "version > 0");
        });

        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ClientGeneratedKey();
        b.Property(x => x.CurrencyCode).HasMaxLength(30).IsRequired();
        b.Property(x => x.StockSold).HasDefaultValue(0);
        b.Property(x => x.EligibilityRules).JsonObject();
        b.Property(x => x.IsActive).HasDefaultValue(true);
        b.Property(x => x.CreatedAt).CreatedNow();
        b.DatabaseManagedVersion();

        b.Ignore(x => x.StockRemaining);

        b.HasOne(x => x.ItemDefinition).WithMany()
            .HasForeignKey(x => x.ItemDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Currency>().WithMany()
            .HasForeignKey(x => x.CurrencyCode)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.IsActive, x.StartsAt, x.EndsAt })
            .HasDatabaseName("ix_market_offers_active_window");
    }
}

public class PurchaseConfiguration : IEntityTypeConfiguration<Purchase>
{
    public void Configure(EntityTypeBuilder<Purchase> b)
    {
        b.ToTable("purchases", t =>
        {
            t.HasCheckConstraint("ck_pur_quantity", "quantity > 0");
            t.HasCheckConstraint("ck_pur_unit_price", "unit_price >= 0");
            t.HasCheckConstraint("ck_pur_total_price", "total_price >= 0");
            t.HasCheckConstraint("ck_pur_total_matches", "total_price = unit_price * quantity");
        });

        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ClientGeneratedKey();
        b.Property(x => x.CurrencyCode).HasMaxLength(30).IsRequired();
        b.Property(x => x.IdempotencyKey).HasMaxLength(100).IsRequired();
        b.Property(x => x.PurchasedAt).CreatedNow();

        b.HasOne<Character>().WithMany()
            .HasForeignKey(x => x.CharacterId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne<MarketOffer>().WithMany()
            .HasForeignKey(x => x.MarketOfferId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Currency>().WithMany()
            .HasForeignKey(x => x.CurrencyCode)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne<LedgerTransaction>().WithMany()
            .HasForeignKey(x => x.LedgerTransactionId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => x.LedgerTransactionId).IsUnique();
        b.HasIndex(x => new { x.CharacterId, x.IdempotencyKey }).IsUnique();
    }
}
