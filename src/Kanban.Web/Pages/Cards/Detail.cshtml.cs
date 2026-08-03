using Kanban.Core;
using Kanban.Core.Entities;
using Kanban.Core.Enums;
using Kanban.Core.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Kanban.Web.Pages.Cards;

public class DetailModel : PageModel
{
    private readonly KanbanDbContext _db;
    private readonly BoardService _board;

    public DetailModel(KanbanDbContext db, BoardService board)
    {
        _db = db;
        _board = board;
    }

    [BindProperty(SupportsGet = true)] public int Id { get; set; }
    [BindProperty] public string NoteBody { get; set; } = string.Empty;

    public Card Card { get; private set; } = null!;
    public string? Error { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var card = await _db.Cards.AsNoTracking()
            .Include(c => c.Project)
            .Include(c => c.Notes)
            .FirstOrDefaultAsync(c => c.Id == Id, ct);

        if (card is null) return NotFound();

        card.Notes = card.Notes.OrderBy(n => n.CreatedUtc).ThenBy(n => n.Id).ToList();
        Card = card;
        return Page();
    }

    public async Task<IActionResult> OnPostNoteAsync(CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(NoteBody))
        {
            _db.CardNotes.Add(new CardNote
            {
                CardId = Id,
                Author = NoteAuthor.User,
                Body = NoteBody.Trim()
            });
            await _db.SaveChangesAsync(ct);
        }

        return RedirectToPage(new { id = Id });
    }

    public async Task<IActionResult> OnPostSendBackAsync(CancellationToken ct)
    {
        // MoveAsync wants the target column's full ordering. The card goes to the bottom
        // of Ready, behind anything already queued.
        var readyIds = await _db.Cards.AsNoTracking()
            .Where(c => c.Status == CardStatus.Ready)
            .OrderBy(c => c.Position)
            .Select(c => c.Id)
            .ToListAsync(ct);

        readyIds.Add(Id);

        var result = await _board.MoveAsync(Id, CardStatus.Ready, readyIds, NoteBody, ct);
        if (!result.Success)
        {
            Error = result.Error;
            return await OnGetAsync(ct);
        }

        return RedirectToPage("/Index");
    }
}