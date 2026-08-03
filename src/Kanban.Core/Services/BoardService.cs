using Kanban.Core.Entities;
using Kanban.Core.Enums;
using Kanban.Core.Rules;
using Microsoft.EntityFrameworkCore;

namespace Kanban.Core.Services;

public record MoveResult(bool Success, string? Error)
{
    public static MoveResult Ok() => new(true, null);
    public static MoveResult Fail(string error) => new(false, error);
}

public class BoardService
{
    private readonly KanbanDbContext _db;

    public BoardService(KanbanDbContext db) => _db = db;

    public async Task<IReadOnlyList<Card>> GetBoardAsync(int? projectId, CancellationToken ct)
    {
        var query = _db.Cards.AsNoTracking().Include(c => c.Project).AsQueryable();

        if (projectId is not null)
        {
            query = query.Where(c => c.ProjectId == projectId);
        }

        return await query
            .OrderBy(c => c.Status)
            .ThenBy(c => c.Position)
            .ToListAsync(ct);
    }

    /// <summary>
    /// Applies a drag. <paramref name="orderedCardIds"/> is the complete ordered contents of the
    /// target column after the drop, including the moved card.
    /// </summary>
    public async Task<MoveResult> MoveAsync(
        int cardId,
        CardStatus targetStatus,
        IReadOnlyList<int> orderedCardIds,
        string? note,
        CancellationToken ct)
    {
        var card = await _db.Cards.FirstOrDefaultAsync(c => c.Id == cardId, ct);
        if (card is null)
        {
            return MoveResult.Fail($"Card {cardId} no longer exists.");
        }

        if (!CardTransitions.IsAllowed(card.Status, targetStatus))
        {
            return MoveResult.Fail($"Moving a card from {card.Status} to {targetStatus} is not allowed.");
        }

        if (CardTransitions.RequiresNote(card.Status, targetStatus) && string.IsNullOrWhiteSpace(note))
        {
            return MoveResult.Fail("Sending a card back for rework requires a note.");
        }

        if (!orderedCardIds.Contains(cardId))
        {
            return MoveResult.Fail("The column ordering did not include the moved card.");
        }

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        if (!string.IsNullOrWhiteSpace(note))
        {
            _db.CardNotes.Add(new CardNote
            {
                CardId = card.Id,
                Author = NoteAuthor.User,
                Body = note.Trim()
            });
        }

        var previousStatus = card.Status;
        card.Status = targetStatus;
        card.UpdatedUtc = DateTime.UtcNow;

        // The Failed/Succeeded badge describes the last run's fate in Review.
        // Once the card leaves Review it is meaningless, so clear it.
        if (previousStatus == CardStatus.Review && targetStatus != CardStatus.Review)
        {
            card.Outcome = null;
        }

        var positions = BoardOrdering.Renumber(orderedCardIds);
        var affected = await _db.Cards
            .Where(c => orderedCardIds.Contains(c.Id))
            .ToListAsync(ct);

        foreach (var target in affected)
        {
            target.Position = positions.First(p => p.CardId == target.Id).Position;
        }

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return MoveResult.Ok();
    }
}