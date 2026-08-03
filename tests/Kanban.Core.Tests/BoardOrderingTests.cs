using Kanban.Core.Rules;
using Xunit;

namespace Kanban.Core.Tests;

public class BoardOrderingTests
{
    [Fact]
    public void Renumber_assigns_contiguous_zero_based_positions_in_the_given_order()
    {
        var result = BoardOrdering.Renumber(new[] { 7, 3, 9 });

        Assert.Equal(new[]
        {
            new CardPosition(7, 0),
            new CardPosition(3, 1),
            new CardPosition(9, 2)
        }, result);
    }

    [Fact]
    public void Renumber_returns_empty_for_an_empty_column()
    {
        Assert.Empty(BoardOrdering.Renumber(Array.Empty<int>()));
    }

    [Fact]
    public void Renumber_rejects_duplicate_card_ids()
    {
        var ex = Assert.Throws<ArgumentException>(() => BoardOrdering.Renumber(new[] { 4, 5, 4 }));
        Assert.Contains("4", ex.Message);
    }

    [Fact]
    public void Renumber_rejects_null()
    {
        Assert.Throws<ArgumentNullException>(() => BoardOrdering.Renumber(null!));
    }
}