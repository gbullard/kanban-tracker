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

    public record MoveRequest(int CardId, string TargetStatus, int[] OrderedCardIds, string? Note, int? ProjectId);

    public async Task<IActionResult> OnPostMoveAsync([FromBody] MoveRequest request, CancellationToken ct)
    {
        if (!Enum.TryParse<CardStatus>(request.TargetStatus, out var target))
        {
            return BadRequest($"Unknown column '{request.TargetStatus}'.");
        }

        var result = await _board.MoveAsync(request.CardId, target, request.OrderedCardIds, request.Note, ct);
        if (!result.Success)
        {
            return BadRequest(result.Error!);
        }

        ProjectId = request.ProjectId;
        await LoadColumns(ct);
        return Partial("_Board", Columns);
    }

    public async Task OnGetAsync(CancellationToken ct)
    {
        Projects = await _db.Projects.AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.Name)
            .ToListAsync(ct);

        await LoadColumns(ct);
    }

    public async Task<IActionResult> OnGetBoardAsync(int? projectId, CancellationToken ct)
    {
        ProjectId = projectId;
        await LoadColumns(ct);
        return Partial("_Board", Columns);
    }

    private async Task LoadColumns(CancellationToken ct)
    {
        var cards = await _board.GetBoardAsync(ProjectId, ct);

        Columns = Layout
            .Select(l => new ColumnModel(
                l.Status,
                l.Heading,
                cards.Where(c => c.Status == l.Status).OrderBy(c => c.Position).ToList()))
            .ToList();
    }
}