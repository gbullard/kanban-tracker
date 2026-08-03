using Kanban.Core;
using Kanban.Core.Entities;
using Kanban.Core.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Kanban.Web.Pages.Cards;

public class EditModel : PageModel
{
    private readonly KanbanDbContext _db;

    public EditModel(KanbanDbContext db) => _db = db;

    [BindProperty(SupportsGet = true)] public int? Id { get; set; }
    [BindProperty] public int ProjectId { get; set; }
    [BindProperty] public string Title { get; set; } = string.Empty;
    [BindProperty] public string? Description { get; set; }

    public List<Project> Projects { get; private set; } = new();
    public string? Error { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        await LoadProjectsAsync(ct);

        if (Projects.Count == 0)
        {
            Error = "Add a project first — a card must belong to one.";
            return Page();
        }

        if (Id is null)
        {
            ProjectId = Projects[0].Id;
            return Page();
        }

        var card = await _db.Cards.AsNoTracking().FirstOrDefaultAsync(c => c.Id == Id, ct);
        if (card is null) return NotFound();

        ProjectId = card.ProjectId;
        Title = card.Title;
        Description = card.Description;
        return Page();
    }

    private async Task LoadProjectsAsync(CancellationToken ct) =>
        Projects = await _db.Projects.AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.Name)
            .ToListAsync(ct);

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        Title = Title?.Trim() ?? string.Empty;

        if (Title.Length == 0)
        {
            Error = "A title is required.";
            await LoadProjectsAsync(ct);
            return Page();
        }

        if (Id is null)
        {
            // New cards go to the top of New, so everything below shifts down one.
            var existing = await _db.Cards.Where(c => c.Status == CardStatus.New).ToListAsync(ct);
            foreach (var c in existing) c.Position += 1;

            _db.Cards.Add(new Card
            {
                ProjectId = ProjectId,
                Title = Title,
                Description = Description,
                Status = CardStatus.New,
                Position = 0
            });
        }
        else
        {
            var card = await _db.Cards.FirstOrDefaultAsync(c => c.Id == Id, ct);
            if (card is null) return NotFound();

            card.ProjectId = ProjectId;
            card.Title = Title;
            card.Description = Description;
            card.UpdatedUtc = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
        return RedirectToPage("/Index");
    }
}