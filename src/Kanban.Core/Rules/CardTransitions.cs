using Kanban.Core.Enums;

namespace Kanban.Core.Rules;

/// <summary>
/// Status changes a human may make from the board. InProgress belongs to the Runner:
/// a card enters it only by being claimed and leaves it only when the run finishes.
/// </summary>
public static class CardTransitions
{
    private static readonly HashSet<(CardStatus From, CardStatus To)> Permitted = new()
    {
        (CardStatus.New, CardStatus.Ready),
        (CardStatus.Review, CardStatus.Completed),
        (CardStatus.Review, CardStatus.Ready),
        (CardStatus.Ready, CardStatus.New),
        (CardStatus.Review, CardStatus.New),
        (CardStatus.Completed, CardStatus.New)
    };

    public static bool IsAllowed(CardStatus from, CardStatus to)
    {
        // Reordering inside a column is always fine, except that a card in
        // InProgress is being written to by the Runner and must not be touched.
        if (from == to)
        {
            return from != CardStatus.InProgress;
        }

        return Permitted.Contains((from, to));
    }

    public static bool RequiresNote(CardStatus from, CardStatus to) =>
        from == CardStatus.Review && to == CardStatus.Ready;
}