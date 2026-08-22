using GongWei.Domain.Characters;
using GongWei.Domain.Common;

namespace GongWei.Domain.Economy;

/// <summary>Table: currencies.</summary>
public class Currency
{
    public const string Silver = "silver";

    public string Code { get; set; } = null!;

    public string DisplayName { get; set; } = null!;

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>
/// Table: wallets — the balance snapshot. Every change must be written in the same
/// transaction as its ledger entry (§4.4). New characters start at 0 (§0.2).
/// </summary>
public class Wallet : IVersioned, IHasId
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid CharacterId { get; set; }

    public Character? Character { get; set; }

    public string CurrencyCode { get; set; } = null!;

    public long Balance { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public long Version { get; set; } = 1;

    /// <summary>
    /// Applies a signed delta and returns the resulting balance for the ledger entry.
    /// Refuses to go negative — the DB CHECK is the backstop, not the mechanism.
    /// </summary>
    public long Apply(long amount)
    {
        if (amount == 0)
        {
            throw DomainException.Validation("金額不可為 0。");
        }

        var after = Balance + amount;

        if (after < 0)
        {
            throw DomainException.Conflict(
                ErrorCodes.InsufficientFunds, $"餘額 {Balance} 不足以支付 {-amount}。");
        }

        Balance = after;
        return after;
    }
}

/// <summary>
/// Table: ledger_transactions — the header grouping one economic change. reason_code is
/// mandatory: no admin money movement is anonymous (§6.11).
/// </summary>
public class LedgerTransaction : IHasId
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public LedgerTransactionType TransactionType { get; set; }

    public string? ReferenceType { get; set; }

    public Guid? ReferenceId { get; set; }

    public string ReasonCode { get; set; } = null!;

    public string? ReasonText { get; set; }

    public Guid? InitiatedBy { get; set; }

    public string? RequestId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<LedgerEntry> Entries { get; set; } = new List<LedgerEntry>();
}

/// <summary>Table: ledger_entries — append-only; UPDATE/DELETE blocked by trigger.</summary>
public class LedgerEntry : IHasId
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid TransactionId { get; set; }

    public LedgerTransaction? Transaction { get; set; }

    public Guid WalletId { get; set; }

    public Wallet? Wallet { get; set; }

    public long Amount { get; set; }

    public long BalanceAfter { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>Table: item_definitions — versioned; history is never rewritten (§6.5).</summary>
public class ItemDefinition : IHasId
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public string Code { get; set; } = null!;

    public int VersionNo { get; set; } = 1;

    public string DisplayName { get; set; } = null!;

    public string Description { get; set; } = string.Empty;

    public ItemCategory Category { get; set; }

    public string? ImageUrl { get; set; }

    public int StackLimit { get; set; } = 999;

    public bool IsConsumable { get; set; }

    /// <summary>jsonb — validated effect descriptors applied by the rules engine.</summary>
    public string EffectPayload { get; set; } = "{}";

    /// <summary>jsonb — who may use it, cooldowns, targeting rules.</summary>
    public string UsageRules { get; set; } = "{}";

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>Table: inventory_entries. Same item with the same expiry stacks into one row.</summary>
public class InventoryEntry : IVersioned, IHasId
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid CharacterId { get; set; }

    public Character? Character { get; set; }

    public Guid ItemDefinitionId { get; set; }

    public ItemDefinition? ItemDefinition { get; set; }

    public int Quantity { get; set; }

    public DateTimeOffset? ExpiresAt { get; set; }

    public DateTimeOffset AcquiredAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public long Version { get; set; } = 1;

    public bool IsAvailableAt(DateTimeOffset now) =>
        Quantity > 0 && (ExpiresAt is null || ExpiresAt > now);

    public int Apply(int delta)
    {
        if (delta == 0)
        {
            throw DomainException.Validation("數量變化不可為 0。");
        }

        var after = Quantity + delta;

        if (after < 0)
        {
            throw DomainException.Conflict(
                ErrorCodes.InsufficientItems, $"持有數量為 {Quantity}，不足以扣除 {-delta}。");
        }

        Quantity = after;
        return after;
    }
}

/// <summary>Table: inventory_transactions — append-only.</summary>
public class InventoryTransaction : IHasId
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid InventoryEntryId { get; set; }

    public InventoryTransactionType TransactionType { get; set; }

    public int QuantityDelta { get; set; }

    public int QuantityAfter { get; set; }

    /// <summary>jsonb — the item effect as it stood when applied.</summary>
    public string EffectSnapshot { get; set; } = "{}";

    public string? ReferenceType { get; set; }

    public Guid? ReferenceId { get; set; }

    public Guid? InitiatedBy { get; set; }

    public string? ReasonCode { get; set; }

    public string? ReasonText { get; set; }

    public string? RequestId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>Table: market_offers. The server is the only source of price (§6.5, §13.3).</summary>
public class MarketOffer : IVersioned, IHasId
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid ItemDefinitionId { get; set; }

    public ItemDefinition? ItemDefinition { get; set; }

    public string CurrencyCode { get; set; } = null!;

    public long UnitPrice { get; set; }

    /// <summary>NULL means unlimited stock.</summary>
    public int? StockTotal { get; set; }

    public int StockSold { get; set; }

    public int? PerCharacterLimit { get; set; }

    public DateTimeOffset? StartsAt { get; set; }

    public DateTimeOffset? EndsAt { get; set; }

    /// <summary>jsonb — allowlisted eligibility rules evaluated server-side.</summary>
    public string EligibilityRules { get; set; } = "{}";

    public bool IsActive { get; set; } = true;

    public Guid CreatedBy { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public long Version { get; set; } = 1;

    public int? StockRemaining => StockTotal is null ? null : StockTotal - StockSold;

    public bool IsOnSaleAt(DateTimeOffset now) =>
        IsActive
        && (StartsAt is null || StartsAt <= now)
        && (EndsAt is null || EndsAt > now);

    /// <summary>
    /// Validates eligibility and stock, then returns the price the server computed. The
    /// request body never carries a price (§13.3).
    /// </summary>
    public long EnsurePurchasable(
        Character character,
        int quantity,
        int alreadyPurchased,
        DateTimeOffset now)
    {
        character.EnsureCanAct();

        if (quantity <= 0)
        {
            throw DomainException.Validation("購買數量必須大於 0。");
        }

        if (!IsOnSaleAt(now))
        {
            throw DomainException.Conflict(ErrorCodes.ConflictState, "此商品目前不販售。");
        }

        if (StockRemaining is { } remaining && remaining < quantity)
        {
            throw DomainException.Conflict(ErrorCodes.SoldOut, $"庫存僅剩 {remaining}。");
        }

        if (PerCharacterLimit is not null && alreadyPurchased + quantity > PerCharacterLimit)
        {
            throw DomainException.Conflict(
                ErrorCodes.PurchaseLimitReached, $"每人限購 {PerCharacterLimit} 件。");
        }

        return UnitPrice * quantity;
    }
}

/// <summary>Table: purchases — the receipt with the price snapshot taken at the time.</summary>
public class Purchase : IHasId
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid CharacterId { get; set; }

    public Guid MarketOfferId { get; set; }

    public int Quantity { get; set; }

    public long UnitPrice { get; set; }

    public long TotalPrice { get; set; }

    public string CurrencyCode { get; set; } = null!;

    public Guid LedgerTransactionId { get; set; }

    public string IdempotencyKey { get; set; } = null!;

    public DateTimeOffset PurchasedAt { get; set; }
}
