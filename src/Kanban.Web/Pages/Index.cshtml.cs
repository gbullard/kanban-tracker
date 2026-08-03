using Kanban.Core;
using Kanban.Core.Entities;
using Kanban.Core.Enums;
using Kanban.Core.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Kanban.Web.Pages;

public record ColumnModel(CardStatus Status, string Heading, IReadOnlyList<Card> Cards);

public class IndexModel : PageModel
{
    private readonly BoardService _board;
    private readonly KanbanDbContext _db;

    public IndexModel(BoardService board, KanbanDbContext db)
    {
        _board = board;
        _db = db;
    }

    [BindProperty(SupportsGet = true)]
    public int? ProjectId { get; set; }

    public List<ColumnModel> Columns { get; private set; } = new();
    public List<Project> Projects { get; private set; } = new();

    private static readonly (CardStatus Status, string Heading)[] Layout =
    {
        (CardStatus.New, "New"),
        (CardStatus.Ready, "Ready"),
        (CardStatus.InProgress, "In Progress"),
        (CardStatus.Review, "Review"),
        (CardStatus.Completed, "Completed")
    };

    public async Task OnGetAsync(CancellationToken ct)
    {
        Projects = await _db.Projects.AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.Name)
            .ToListAsync(ct);

        var cards = await _board.GetBoardAsync(ProjectId, ct);

        Columns = Layout
            .Select(l => new ColumnModel(
                l.Status,
                l.Heading,
                cards.Where(c => c.Status == l.Status).OrderBy(c => c.Position).ToList()))
            .ToList();
    }
}