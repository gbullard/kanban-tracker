using Kanban.Core.Enums;
using Kanban.Core.Rules;
using Xunit;

namespace Kanban.Core.Tests;

public class CardTransitionsTests
{
    [Theory]
    [InlineData(CardStatus.New, CardStatus.Ready)]
    [InlineData(CardStatus.Review, CardStatus.Completed)]
    [InlineData(CardStatus.Review, CardStatus.Ready)]
    [InlineData(CardStatus.Ready, CardStatus.New)]
    [InlineData(CardStatus.Review, CardStatus.New)]
    [InlineData(CardStatus.Completed, CardStatus.New)]
    public void Permitted_transitions_are_allowed(CardStatus from, CardStatus to)
    {
        Assert.True(CardTransitions.IsAllowed(from, to));
    }

    [Theory]
    [InlineData(CardStatus.New)]
    [InlineData(CardStatus.Ready)]
    [InlineData(CardStatus.Review)]
    [InlineData(CardStatus.Completed)]
    public void Reordering_within_a_column_is_allowed(CardStatus status)
    {
        Assert.True(CardTransitions.IsAllowed(status, status));
    }

    [Fact]
    public void Reordering_within_InProgress_is_not_allowed()
    {
        // The Runner is actively writing to these cards. Nothing touches them.
        Assert.False(CardTransitions.IsAllowed(CardStatus.InProgress, CardStatus.InProgress));
    }

    [Theory]
    [InlineData(CardStatus.New)]
    [InlineData(CardStatus.Ready)]
    [InlineData(CardStatus.Review)]
    [InlineData(CardStatus.Completed)]
    public void Nothing_may_be_dragged_into_InProgress(CardStatus from)
    {
        Assert.False(CardTransitions.IsAllowed(from, CardStatus.InProgress));
    }

    [Theory]
    [InlineData(CardStatus.New)]
    [InlineData(CardStatus.Ready)]
    [InlineData(CardStatus.Review)]
    [InlineData(CardStatus.Completed)]
    public void Nothing_may_be_dragged_out_of_InProgress(CardStatus to)
    {
        Assert.False(CardTransitions.IsAllowed(CardStatus.InProgress, to));
    }

    [Theory]
    [InlineData(CardStatus.New, CardStatus.Review)]
    [InlineData(CardStatus.New, CardStatus.Completed)]
    [InlineData(CardStatus.Ready, CardStatus.Review)]
    [InlineData(CardStatus.Ready, CardStatus.Completed)]
    [InlineData(CardStatus.Completed, CardStatus.Ready)]
    [InlineData(CardStatus.Completed, CardStatus.Review)]
    public void Skipping_the_agent_is_not_allowed(CardStatus from, CardStatus to)
    {
        Assert.False(CardTransitions.IsAllowed(from, to));
    }

    [Fact]
    public void Sending_a_card_back_for_rework_requires_a_note()
    {
        Assert.True(CardTransitions.RequiresNote(CardStatus.Review, CardStatus.Ready));
    }

    [Theory]
    [InlineData(CardStatus.New, CardStatus.Ready)]
    [InlineData(CardStatus.Review, CardStatus.Completed)]
    [InlineData(CardStatus.Ready, CardStatus.New)]
    public void Other_transitions_do_not_require_a_note(CardStatus from, CardStatus to)
    {
        Assert.False(CardTransitions.RequiresNote(from, to));
    }
}