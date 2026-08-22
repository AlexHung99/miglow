using System.Data;
using GongWei.Application.Abstractions;
using GongWei.Domain.Characters;
using GongWei.Domain.Common;
using GongWei.Domain.Economy;
using Microsoft.EntityFrameworkCore;

namespace GongWei.Application.Economy;

public sealed record PurchaseResult(
    Guid PurchaseId,
    Guid ItemDefinitionId,
    int Quantity,
    long UnitPrice,
    long TotalPrice,
    string CurrencyCode,
    long BalanceAfter);

public sealed record AdjustmentResult(Guid TransactionId, long BalanceAfter, long AuditLogId);

public sealed record ItemGrantResult(Guid InventoryEntryId, int QuantityAfter, long AuditLogId);

/// <summary>
/// 宮市與管理端經濟操作. Purchases follow §6.5; admin adjustments follow §6.11 — no
/// amount threshold and no two-person approval, but a reason code and a real reason are
/// mandatory and everything commits with its audit row.
/// </summary>
public sealed class EconomyService(
    IGongWeiDb db,
    IClock clock,
    ICurrentUser currentUser,
    IAuditWriter audit,
    IOutboxWriter outbox)
{
    /// <summary>An adjustment reason must be a real sentence, not a placeholder (§6.11 step 2).</summary>
    private const int MinReasonTextLength = 5;

    // ------------------------------------------------------------------ player

    public async Task<PurchaseResult> PurchaseAsync(
        Guid characterId,
        Guid marketOfferId,
        int quantity,
        string idempotencyKey,
        CancellationToken ct = default)
    {
        var userId = currentUser.RequireUserId();
        var now = clock.UtcNow;

        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);

        // A replay that got past the middleware still lands on the receipt's unique index.
        var replay = await db.Purchases.FirstOrDefaultAsync(
            p => p.CharacterId == characterId && p.IdempotencyKey == idempotencyKey, ct);

        if (replay is not null)
        {
            var currentBalance = await db.Wallets
                .Where(w => w.CharacterId == characterId && w.CurrencyCode == replay.CurrencyCode)
                .Select(w => w.Balance)
                .FirstAsync(ct);

            await tx.CommitAsync(ct);

            return new PurchaseResult(
                replay.Id, Guid.Empty, replay.Quantity, replay.UnitPrice,
                replay.TotalPrice, replay.CurrencyCode, currentBalance);
        }

        // Fixed lock order: offer → wallet → inventory entry (§6.5).
        await db.LockRowAsync("market_offers", marketOfferId, ct);

        var offer = await db.MarketOffers
                        .Include(o => o.ItemDefinition)
                        .FirstOrDefaultAsync(o => o.Id == marketOfferId, ct)
                    ?? throw DomainException.NotFound("Market offer", marketOfferId);

        var character = await LoadOwnCharacterAsync(characterId, userId, ct);

        var alreadyPurchased = await db.Purchases
            .Where(p => p.CharacterId == characterId && p.MarketOfferId == marketOfferId)
            .SumAsync(p => (int?)p.Quantity, ct) ?? 0;

        // The server is the only source of price — the request never carries one (§13.3).
        var totalPrice = offer.EnsurePurchasable(character, quantity, alreadyPurchased, now);

        var wallet = await LockWalletAsync(characterId, offer.CurrencyCode, ct);
        var balanceAfter = wallet.Apply(-totalPrice);

        var ledgerTransaction = new LedgerTransaction
        {
            TransactionType = LedgerTransactionType.Purchase,
            ReferenceType = "market_offer",
            ReferenceId = offer.Id,
            ReasonCode = "market.purchase",
            InitiatedBy = userId,
            RequestId = currentUser.RequestId,
            CreatedAt = now
        };
        db.LedgerTransactions.Add(ledgerTransaction);

        db.LedgerEntries.Add(new LedgerEntry
        {
            TransactionId = ledgerTransaction.Id,
            WalletId = wallet.Id,
            Amount = -totalPrice,
            BalanceAfter = balanceAfter,
            CreatedAt = now
        });

        var entry = await UpsertInventoryEntryAsync(characterId, offer.ItemDefinitionId, now, ct);
        var quantityAfter = entry.Apply(quantity);

        db.InventoryTransactions.Add(new InventoryTransaction
        {
            InventoryEntryId = entry.Id,
            TransactionType = InventoryTransactionType.Purchase,
            QuantityDelta = quantity,
            QuantityAfter = quantityAfter,
            ReferenceType = "market_offer",
            ReferenceId = offer.Id,
            InitiatedBy = userId,
            ReasonCode = "market.purchase",
            RequestId = currentUser.RequestId,
            CreatedAt = now
        });

        offer.StockSold += quantity;

        var purchase = new Purchase
        {
            CharacterId = characterId,
            MarketOfferId = offer.Id,
            Quantity = quantity,
            UnitPrice = offer.UnitPrice,
            TotalPrice = totalPrice,
            CurrencyCode = offer.CurrencyCode,
            LedgerTransactionId = ledgerTransaction.Id,
            IdempotencyKey = idempotencyKey,
            PurchasedAt = now
        };
        db.Purchases.Add(purchase);

        audit.Write("market.purchase", "purchase", purchase.Id, after: new
        {
            purchase.CharacterId,
            purchase.MarketOfferId,
            purchase.Quantity,
            purchase.TotalPrice,
            balanceAfter
        });

        outbox.Enqueue("market.purchased", "purchase", purchase.Id, new
        {
            purchaseId = purchase.Id,
            characterId,
            itemDefinitionId = offer.ItemDefinitionId,
            quantity
        });

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return new PurchaseResult(
            purchase.Id, offer.ItemDefinitionId, quantity, offer.UnitPrice,
            totalPrice, offer.CurrencyCode, balanceAfter);
    }

    public async Task<InventoryEntry> UseItemAsync(
        Guid characterId,
        Guid inventoryEntryId,
        int quantity,
        CancellationToken ct = default)
    {
        var userId = currentUser.RequireUserId();
        var now = clock.UtcNow;

        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);

        await db.LockRowAsync("inventory_entries", inventoryEntryId, ct);

        var entry = await db.InventoryEntries
                        .Include(e => e.ItemDefinition)
                        .FirstOrDefaultAsync(e => e.Id == inventoryEntryId, ct)
                    ?? throw DomainException.NotFound("Inventory entry", inventoryEntryId);

        if (entry.CharacterId != characterId)
        {
            throw DomainException.NotFound("Inventory entry", inventoryEntryId);
        }

        var character = await LoadOwnCharacterAsync(characterId, userId, ct);
        character.EnsureCanAct();

        if (!entry.IsAvailableAt(now))
        {
            throw DomainException.Conflict(ErrorCodes.InsufficientItems, "此道具已過期或不足。");
        }

        if (entry.ItemDefinition is { IsConsumable: false })
        {
            throw DomainException.Validation("此道具不可消耗使用。");
        }

        var quantityAfter = entry.Apply(-quantity);

        db.InventoryTransactions.Add(new InventoryTransaction
        {
            InventoryEntryId = entry.Id,
            TransactionType = InventoryTransactionType.Use,
            QuantityDelta = -quantity,
            QuantityAfter = quantityAfter,
            EffectSnapshot = entry.ItemDefinition?.EffectPayload ?? "{}",
            InitiatedBy = userId,
            ReasonCode = "inventory.use",
            RequestId = currentUser.RequestId,
            CreatedAt = now
        });

        audit.Write("inventory.use", "inventory_entry", entry.Id,
            after: new { entry.ItemDefinitionId, quantity, quantityAfter });

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return entry;
    }

    // ------------------------------------------------------------------- admin

    /// <summary>
    /// §6.11. Any active non-Auditor admin role may do this: there is no amount threshold
    /// and no ApprovalRequest. What is mandatory is a reason code, a real reason, and an
    /// audit row committed in the same transaction as the ledger entry.
    /// </summary>
    public async Task<AdjustmentResult> AdjustCurrencyAsync(
        Guid characterId,
        string currencyCode,
        long amount,
        string reasonCode,
        string reasonText,
        CancellationToken ct = default)
    {
        RequireAdjustmentRole();
        EnsureReasonProvided(reasonCode, reasonText);

        var adminId = currentUser.RequireUserId();
        var now = clock.UtcNow;

        if (amount == 0)
        {
            throw DomainException.Validation("調整金額不可為 0。");
        }

        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);

        var wallet = await LockWalletAsync(characterId, currencyCode, ct);
        var balanceAfter = wallet.Apply(amount);

        var ledgerTransaction = new LedgerTransaction
        {
            TransactionType = LedgerTransactionType.AdminGrant,
            ReferenceType = "character",
            ReferenceId = characterId,
            ReasonCode = reasonCode,
            ReasonText = reasonText,
            InitiatedBy = adminId,
            RequestId = currentUser.RequestId,
            CreatedAt = now
        };
        db.LedgerTransactions.Add(ledgerTransaction);

        db.LedgerEntries.Add(new LedgerEntry
        {
            TransactionId = ledgerTransaction.Id,
            WalletId = wallet.Id,
            Amount = amount,
            BalanceAfter = balanceAfter,
            CreatedAt = now
        });

        var auditLog = audit.Write("economy.adjust", "character", characterId,
            before: new { balance = balanceAfter - amount },
            after: new { balance = balanceAfter, amount, currencyCode, transactionId = ledgerTransaction.Id },
            reason: $"{reasonCode}: {reasonText}");

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return new AdjustmentResult(ledgerTransaction.Id, balanceAfter, auditLog.Id);
    }

    /// <summary>Grants or removes items under the same no-threshold, reason-required rule.</summary>
    public async Task<ItemGrantResult> GrantItemAsync(
        Guid characterId,
        Guid itemDefinitionId,
        int quantity,
        string reasonCode,
        string reasonText,
        CancellationToken ct = default)
    {
        RequireAdjustmentRole();
        EnsureReasonProvided(reasonCode, reasonText);

        var adminId = currentUser.RequireUserId();
        var now = clock.UtcNow;

        if (quantity == 0)
        {
            throw DomainException.Validation("發放數量不可為 0。");
        }

        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);

        var entry = await UpsertInventoryEntryAsync(characterId, itemDefinitionId, now, ct);

        if (entry.Id != Guid.Empty)
        {
            await db.LockRowAsync("inventory_entries", entry.Id, ct);
        }

        var quantityAfter = entry.Apply(quantity);

        db.InventoryTransactions.Add(new InventoryTransaction
        {
            InventoryEntryId = entry.Id,
            TransactionType = InventoryTransactionType.AdminGrant,
            QuantityDelta = quantity,
            QuantityAfter = quantityAfter,
            ReferenceType = "character",
            ReferenceId = characterId,
            InitiatedBy = adminId,
            ReasonCode = reasonCode,
            ReasonText = reasonText,
            RequestId = currentUser.RequestId,
            CreatedAt = now
        });

        var auditLog = audit.Write("economy.item_grant", "character", characterId,
            after: new { itemDefinitionId, quantity, quantityAfter },
            reason: $"{reasonCode}: {reasonText}");

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return new ItemGrantResult(entry.Id, quantityAfter, auditLog.Id);
    }

    /// <summary>
    /// A correction is a new reversing transaction — the original entry is never edited,
    /// which the append-only trigger also enforces (§6.11 step 5).
    /// </summary>
    public async Task<AdjustmentResult> CorrectLedgerAsync(
        Guid originalTransactionId,
        long amount,
        string reasonCode,
        string reasonText,
        CancellationToken ct = default)
    {
        RequireAdjustmentRole();
        EnsureReasonProvided(reasonCode, reasonText);

        var adminId = currentUser.RequireUserId();
        var now = clock.UtcNow;

        if (amount == 0)
        {
            throw DomainException.Validation("補正金額不可為 0。");
        }

        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);

        var original = await db.LedgerTransactions
                           .Include(t => t.Entries)
                           .FirstOrDefaultAsync(t => t.Id == originalTransactionId, ct)
                       ?? throw DomainException.NotFound("Ledger transaction", originalTransactionId);

        var originalEntry = original.Entries.FirstOrDefault()
                            ?? throw DomainException.Validation("原交易沒有可補正的分錄。");

        await db.LockRowAsync("wallets", originalEntry.WalletId, ct);

        var wallet = await db.Wallets.FirstOrDefaultAsync(w => w.Id == originalEntry.WalletId, ct)
                     ?? throw DomainException.NotFound("Wallet", originalEntry.WalletId);

        var balanceAfter = wallet.Apply(amount);

        var correction = new LedgerTransaction
        {
            TransactionType = LedgerTransactionType.AdminCorrection,
            ReferenceType = "ledger_transaction",
            ReferenceId = original.Id,
            ReasonCode = reasonCode,
            ReasonText = reasonText,
            InitiatedBy = adminId,
            RequestId = currentUser.RequestId,
            CreatedAt = now
        };
        db.LedgerTransactions.Add(correction);

        db.LedgerEntries.Add(new LedgerEntry
        {
            TransactionId = correction.Id,
            WalletId = wallet.Id,
            Amount = amount,
            BalanceAfter = balanceAfter,
            CreatedAt = now
        });

        var auditLog = audit.Write("economy.ledger_correction", "ledger_transaction", original.Id,
            after: new { correctionTransactionId = correction.Id, amount, balanceAfter },
            reason: $"{reasonCode}: {reasonText}");

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return new AdjustmentResult(correction.Id, balanceAfter, auditLog.Id);
    }

    // ----------------------------------------------------------------- helpers

    /// <summary>Every active admin role except the read-only Auditor may adjust (§9.1).</summary>
    private void RequireAdjustmentRole() =>
        currentUser.RequireRole(
            AdminRole.CharacterReviewer,
            AdminRole.GameMaster,
            AdminRole.EconomyManager,
            AdminRole.Moderator,
            AdminRole.ContentEditor,
            AdminRole.CharacterManager,
            AdminRole.SystemConfigManager);

    private static void EnsureReasonProvided(string reasonCode, string reasonText)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(reasonCode))
        {
            errors["reasonCode"] = ["理由代碼必填"];
        }

        if ((reasonText ?? string.Empty).Trim().Length < MinReasonTextLength)
        {
            errors["reasonText"] = [$"理由說明至少需要 {MinReasonTextLength} 字"];
        }

        if (errors.Count > 0)
        {
            throw DomainException.FieldErrors(errors);
        }
    }

    private async Task<Wallet> LockWalletAsync(Guid characterId, string currencyCode, CancellationToken ct)
    {
        var wallet = await db.Wallets.FirstOrDefaultAsync(
            w => w.CharacterId == characterId && w.CurrencyCode == currencyCode, ct);

        if (wallet is null)
        {
            wallet = new Wallet
            {
                CharacterId = characterId,
                CurrencyCode = currencyCode,
                Balance = 0
            };
            db.Wallets.Add(wallet);
            return wallet;
        }

        await db.LockRowAsync("wallets", wallet.Id, ct);
        return wallet;
    }

    private async Task<InventoryEntry> UpsertInventoryEntryAsync(
        Guid characterId,
        Guid itemDefinitionId,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var entry = await db.InventoryEntries.FirstOrDefaultAsync(
            e => e.CharacterId == characterId
                 && e.ItemDefinitionId == itemDefinitionId
                 && e.ExpiresAt == null,
            ct);

        if (entry is not null)
        {
            return entry;
        }

        entry = new InventoryEntry
        {
            CharacterId = characterId,
            ItemDefinitionId = itemDefinitionId,
            Quantity = 0,
            AcquiredAt = now
        };

        db.InventoryEntries.Add(entry);
        return entry;
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
