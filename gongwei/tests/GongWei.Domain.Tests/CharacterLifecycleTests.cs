using FluentAssertions;
using GongWei.Domain.Characters;
using GongWei.Domain.Common;
using Xunit;

namespace GongWei.Domain.Tests;

public class ApplicationLifecycleTests
{
    [Theory]
    [InlineData(ApplicationStatus.Draft, ApplicationStatus.Submitted)]
    [InlineData(ApplicationStatus.Draft, ApplicationStatus.Cancelled)]
    [InlineData(ApplicationStatus.Submitted, ApplicationStatus.NeedsRevision)]
    [InlineData(ApplicationStatus.Submitted, ApplicationStatus.Approved)]
    [InlineData(ApplicationStatus.Submitted, ApplicationStatus.Rejected)]
    [InlineData(ApplicationStatus.NeedsRevision, ApplicationStatus.Submitted)]
    public void Allows_the_transitions_the_spec_lists(ApplicationStatus from, ApplicationStatus to) =>
        ApplicationLifecycle.CanTransition(from, to).Should().BeTrue();

    [Theory]
    [InlineData(ApplicationStatus.Approved, ApplicationStatus.Submitted)]
    [InlineData(ApplicationStatus.Rejected, ApplicationStatus.Submitted)]
    [InlineData(ApplicationStatus.Cancelled, ApplicationStatus.Draft)]
    [InlineData(ApplicationStatus.Draft, ApplicationStatus.Approved)]
    public void Rejects_everything_else(ApplicationStatus from, ApplicationStatus to) =>
        ApplicationLifecycle.CanTransition(from, to).Should().BeFalse();

    [Fact]
    public void Only_draft_submitted_and_needs_revision_hold_the_open_slot()
    {
        // This is what the ux_ca_user_open partial index enforces in the database.
        ApplicationLifecycle.IsOpen(ApplicationStatus.Draft).Should().BeTrue();
        ApplicationLifecycle.IsOpen(ApplicationStatus.Submitted).Should().BeTrue();
        ApplicationLifecycle.IsOpen(ApplicationStatus.NeedsRevision).Should().BeTrue();

        ApplicationLifecycle.IsOpen(ApplicationStatus.Approved).Should().BeFalse();
        ApplicationLifecycle.IsOpen(ApplicationStatus.Rejected).Should().BeFalse();
        ApplicationLifecycle.IsOpen(ApplicationStatus.Cancelled).Should().BeFalse();
    }
}

public class CharacterLifecycleTests
{
    [Fact]
    public void Death_is_terminal_apart_from_archiving()
    {
        CharacterLifecycle.CanTransition(CharacterStatus.Dead, CharacterStatus.Archived).Should().BeTrue();

        foreach (var target in Enum.GetValues<CharacterStatus>().Where(s => s != CharacterStatus.Archived))
        {
            CharacterLifecycle.CanTransition(CharacterStatus.Dead, target).Should().BeFalse(
                "a dead character must never return to {0}", target);
        }
    }

    [Fact]
    public void Taking_leave_grants_no_protection_from_death()
    {
        // Spec §5.2 is explicit that Paused is not a shield.
        CharacterLifecycle.CanTransition(CharacterStatus.Paused, CharacterStatus.Dead).Should().BeTrue();
    }

    [Fact]
    public void Waiting_birth_cannot_jump_straight_to_paused()
    {
        CharacterLifecycle.CanTransition(CharacterStatus.WaitingBirth, CharacterStatus.Paused)
            .Should().BeFalse();
    }

    [Theory]
    [InlineData(CharacterStatus.WaitingBirth, true)]
    [InlineData(CharacterStatus.Active, true)]
    [InlineData(CharacterStatus.Paused, true)]
    [InlineData(CharacterStatus.Suspended, true)]
    [InlineData(CharacterStatus.Dead, false)]
    [InlineData(CharacterStatus.Archived, false)]
    public void Only_living_states_occupy_the_one_character_slot(CharacterStatus status, bool expected) =>
        CharacterLifecycle.OccupiesCurrentSlot(status).Should().Be(expected);

    [Theory]
    [InlineData(CharacterRole.Consort, CharacterStatus.Active)]
    [InlineData(CharacterRole.Prince, CharacterStatus.WaitingBirth)]
    [InlineData(CharacterRole.Princess, CharacterStatus.WaitingBirth)]
    public void Heirs_start_in_the_wait_pool_and_consorts_start_active(
        CharacterRole role,
        CharacterStatus expected) =>
        CharacterLifecycle.InitialStatusFor(role).Should().Be(expected);

    [Fact]
    public void Restoring_from_suspension_prefers_the_saved_previous_status()
    {
        CharacterLifecycle.ResolveRestoreTarget(CharacterStatus.Paused, null)
            .Should().Be(CharacterStatus.Paused);
    }

    [Fact]
    public void Restoring_without_any_target_is_refused()
    {
        var act = () => CharacterLifecycle.ResolveRestoreTarget(null, null);

        act.Should().Throw<DomainException>()
            .Which.Code.Should().Be(ErrorCodes.CharacterStateInvalid);
    }

    [Fact]
    public void Restoring_to_a_dead_state_is_refused()
    {
        var act = () => CharacterLifecycle.ResolveRestoreTarget(null, CharacterStatus.Dead);

        act.Should().Throw<DomainException>();
    }
}
