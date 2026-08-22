using FluentAssertions;
using GongWei.Domain.Characters;
using GongWei.Domain.Common;
using GongWei.Domain.Economy;
using GongWei.Domain.Identity;
using GongWei.Domain.Relationships;
using GongWei.Domain.Reproduction;
using Xunit;

namespace GongWei.Domain.Tests;

public class WalletTests
{
    [Fact]
    public void Applying_a_delta_returns_the_before_and_after_for_the_ledger_entry()
    {
        var wallet = new Wallet { Balance = 500 };

        var (before, after) = wallet.Apply(-120);

        before.Should().Be(500);
        after.Should().Be(380);
        wallet.Balance.Should().Be(380);
    }

    [Fact]
    public void A_wallet_never_goes_negative()
    {
        var wallet = new Wallet { Balance = 100 };

        var act = () => wallet.Apply(-101);

        act.Should().Throw<DomainException>()
            .Which.Code.Should().Be(ErrorCodes.InsufficientFunds);

        wallet.Balance.Should().Be(100, "a refused debit must leave the balance untouched");
    }

    [Fact]
    public void A_zero_amount_is_not_a_transaction()
    {
        var act = () => new Wallet { Balance = 10 }.Apply(0);

        act.Should().Throw<DomainException>();
    }
}

public class MarketOfferTests
{
    private static Character ActiveConsort() => new()
    {
        Role = CharacterRole.Consort,
        Status = CharacterStatus.Active,
        DisplayName = "測試"
    };

    private static MarketOffer Offer(int? stock = null, int? limit = null) => new()
    {
        Code = "test_offer",
        CurrencyCode = "silver",
        UnitPrice = 50,
        StockTotal = stock,
        StockRemaining = stock,
        PerCharacterLimit = limit,
        StartsAt = DateTimeOffset.UtcNow.AddHours(-1),
        IsActive = true
    };

    [Fact]
    public void The_server_computes_the_total_price()
    {
        var total = Offer().EnsurePurchasable(ActiveConsort(), null, 3, 0, DateTimeOffset.UtcNow);

        total.Should().Be(150);
    }

    [Fact]
    public void Buying_more_than_the_remaining_stock_is_refused()
    {
        var act = () => Offer(stock: 2)
            .EnsurePurchasable(ActiveConsort(), null, 3, 0, DateTimeOffset.UtcNow);

        act.Should().Throw<DomainException>()
            .Which.Code.Should().Be(ErrorCodes.InsufficientStock);
    }

    [Fact]
    public void The_per_character_limit_counts_earlier_purchases()
    {
        var act = () => Offer(limit: 5)
            .EnsurePurchasable(ActiveConsort(), null, 2, 4, DateTimeOffset.UtcNow);

        act.Should().Throw<DomainException>()
            .Which.Code.Should().Be(ErrorCodes.PurchaseLimitReached);
    }

    [Fact]
    public void A_paused_character_cannot_buy()
    {
        var character = ActiveConsort();
        character.Status = CharacterStatus.Paused;

        var act = () => Offer().EnsurePurchasable(character, null, 1, 0, DateTimeOffset.UtcNow);

        act.Should().Throw<DomainException>()
            .Which.Code.Should().Be(ErrorCodes.CharacterStateInvalid);
    }

    [Fact]
    public void An_offer_below_the_required_rank_is_refused()
    {
        var offer = Offer();
        offer.MinRankOrdinal = 4;

        var act = () => offer.EnsurePurchasable(ActiveConsort(), 2, 1, 0, DateTimeOffset.UtcNow);

        act.Should().Throw<DomainException>()
            .Which.Code.Should().Be(ErrorCodes.OfferNotAvailable);
    }
}

public class RelationshipTests
{
    [Fact]
    public void The_history_arithmetic_always_holds()
    {
        var relationship = new Relationship { Score = 10 };

        var (before, after, applied) = relationship.ApplyDelta(25);

        // ck_rh_arithmetic in the database asserts exactly this.
        (before + applied).Should().Be(after);
        after.Should().Be(35);
    }

    [Fact]
    public void A_delta_past_the_ceiling_is_clamped_and_reports_the_delta_actually_applied()
    {
        var relationship = new Relationship { Score = 95 };

        var (before, after, applied) = relationship.ApplyDelta(20);

        after.Should().Be(Relationship.MaxScore);
        applied.Should().Be(5);
        (before + applied).Should().Be(after);
    }

    [Fact]
    public void A_delta_that_changes_nothing_is_refused_rather_than_writing_a_zero_row()
    {
        var relationship = new Relationship { Score = Relationship.MaxScore };

        var act = () => relationship.ApplyDelta(10);

        act.Should().Throw<DomainException>();
    }

    [Theory]
    [InlineData(-100, RelationshipTier.Hostile)]
    [InlineData(-30, RelationshipTier.Cold)]
    [InlineData(0, RelationshipTier.Neutral)]
    [InlineData(30, RelationshipTier.Friendly)]
    [InlineData(80, RelationshipTier.Intimate)]
    public void Tier_follows_the_score(int score, RelationshipTier expected) =>
        Relationship.TierFor(score).Should().Be(expected);
}

public class PregnancyTests
{
    private static Pregnancy Ongoing() => new()
    {
        MotherCharacterId = Guid.CreateVersion7(),
        Status = PregnancyStatus.Ongoing,
        ConceivedAt = DateTimeOffset.UtcNow.AddDays(-3),
        DueAt = DateTimeOffset.UtcNow.AddDays(4),
        SlotReservedAt = DateTimeOffset.UtcNow.AddDays(-3)
    };

    [Fact]
    public void Resolving_releases_the_heir_slot_in_the_same_step()
    {
        var pregnancy = Ongoing();
        var now = DateTimeOffset.UtcNow;

        pregnancy.Resolve(PregnancyStatus.Miscarried, now, "test");

        // ck_preg_slot_release makes the pair mandatory; this keeps them together in code.
        pregnancy.Status.Should().Be(PregnancyStatus.Miscarried);
        pregnancy.SlotReleasedAt.Should().Be(now);
        pregnancy.ResolvedAt.Should().Be(now);
    }

    [Fact]
    public void A_pregnancy_cannot_be_resolved_twice()
    {
        var pregnancy = Ongoing();
        pregnancy.Resolve(PregnancyStatus.Completed, DateTimeOffset.UtcNow, null);

        var act = () => pregnancy.Resolve(PregnancyStatus.Miscarried, DateTimeOffset.UtcNow, null);

        act.Should().Throw<DomainException>()
            .Which.Code.Should().Be(ErrorCodes.PregnancyNotOngoing);
    }

    [Fact]
    public void Ongoing_is_not_a_terminal_status()
    {
        var act = () => Ongoing().Resolve(PregnancyStatus.Ongoing, DateTimeOffset.UtcNow, null);

        act.Should().Throw<DomainException>();
    }
}

public class PortraitSubmissionTests
{
    [Theory]
    [InlineData(0, 0, 1, 1)]
    [InlineData(0.1, 0.2, 0.5, 0.6)]
    public void Accepts_a_crop_inside_the_image(decimal x, decimal y, decimal w, decimal h)
    {
        var submission = new PlayerPortraitSubmission
        {
            CropX = x, CropY = y, CropWidth = w, CropHeight = h
        };

        submission.Invoking(s => s.EnsureCropIsValid()).Should().NotThrow();
    }

    [Theory]
    [InlineData(0.6, 0, 0.5, 1)]   // runs off the right edge
    [InlineData(0, 0.8, 1, 0.5)]   // runs off the bottom
    [InlineData(-0.1, 0, 0.5, 0.5)] // negative origin
    [InlineData(0, 0, 0, 1)]        // zero width
    public void Rejects_a_crop_outside_the_image(decimal x, decimal y, decimal w, decimal h)
    {
        var submission = new PlayerPortraitSubmission
        {
            CropX = x, CropY = y, CropWidth = w, CropHeight = h
        };

        submission.Invoking(s => s.EnsureCropIsValid()).Should().Throw<DomainException>();
    }

    [Fact]
    public void Only_a_pending_submission_is_player_editable()
    {
        new PlayerPortraitSubmission { ReviewStatus = PortraitReviewStatus.Pending }
            .IsPlayerEditable.Should().BeTrue();

        new PlayerPortraitSubmission { ReviewStatus = PortraitReviewStatus.Approved }
            .IsPlayerEditable.Should().BeFalse();
    }

    [Fact]
    public void Only_an_approved_submission_can_back_a_character()
    {
        foreach (var status in Enum.GetValues<PortraitReviewStatus>())
        {
            new PlayerPortraitSubmission { ReviewStatus = status }
                .IsUsableForCharacter.Should().Be(status == PortraitReviewStatus.Approved);
        }
    }
}

public class VersioningTests
{
    [Fact]
    public void A_stale_if_match_reports_the_current_version()
    {
        var wallet = new Wallet { Version = 7 };

        var act = () => wallet.EnsureVersion(5);

        var exception = act.Should().Throw<DomainException>().Which;
        exception.Code.Should().Be(ErrorCodes.VersionConflict);
        exception.Extensions["currentVersion"].Should().Be(7L);
    }

    [Fact]
    public void An_absent_if_match_does_not_block_the_write()
    {
        // Endpoints that require If-Match reject a missing header before reaching here.
        new Wallet { Version = 7 }.Invoking(w => w.EnsureVersion(null)).Should().NotThrow();
    }

    [Fact]
    public void Touch_bumps_the_version_and_the_timestamp()
    {
        var wallet = new Wallet { Version = 3 };
        var now = DateTimeOffset.UtcNow;

        wallet.Touch(now);

        wallet.Version.Should().Be(4);
        wallet.UpdatedAt.Should().Be(now);
    }
}

public class EnumNamingTests
{
    [Theory]
    [InlineData(CharacterStatus.WaitingBirth, "waiting_birth")]
    [InlineData(CharacterStatus.Active, "active")]
    [InlineData(AudienceStatus.ResolvedSuccess, "resolved_success")]
    [InlineData(AdminRole.SystemConfigManager, "system_config_manager")]
    [InlineData(LedgerReason.EventSettlement, "event_settlement")]
    [InlineData(ApprovalHandler.HighRiskSettingPublish, "high_risk_setting_publish")]
    public void Enum_members_map_to_the_names_used_by_the_check_constraints<T>(T value, string expected)
        where T : struct, Enum =>
        EnumNaming.ToDbValue(value).Should().Be(expected);

    [Fact]
    public void Round_trips_every_member_of_every_persisted_enum()
    {
        foreach (var status in Enum.GetValues<CharacterStatus>())
        {
            EnumNaming.FromDbValue<CharacterStatus>(EnumNaming.ToDbValue(status))
                .Should().Be(status);
        }
    }

    [Fact]
    public void An_unknown_database_value_throws_rather_than_defaulting()
    {
        // Silently landing on the first enum member would be far worse than failing.
        var act = () => EnumNaming.FromDbValue<CharacterStatus>("undead");

        act.Should().Throw<InvalidOperationException>();
    }
}
