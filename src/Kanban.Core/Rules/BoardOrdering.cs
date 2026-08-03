namespace Kanban.Core.Rules;

public readonly record struct CardPosition(int CardId, int Position);

/// <summary>
/// The board sends the complete ordered list of card ids for a column after every drop,
/// and the server rewrites positions as 0..n-1. This is deliberately not a fractional
/// index scheme: it is idempotent, cannot drift, and is trivial to verify.
/// </summary>
public static class BoardOrdering
{
    public static IReadOnlyList<CardPosition> Renumber(IReadOnlyList<int> orderedCardIds)
    {
        ArgumentNullException.ThrowIfNull(orderedCardIds);

        var duplicate = orderedCardIds
            .GroupBy(id => id)
            .FirstOrDefault(g => g.Count() > 1);

        if (duplicate is not null)
        {
            throw new ArgumentException(
                $"Duplicate card id {duplicate.Key} in column ordering.",
                nameof(orderedCardIds));
        }

        return orderedCardIds
            .Select((id, index) => new CardPosition(id, index))
            .ToList();
    }
}