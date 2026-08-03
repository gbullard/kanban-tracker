using Kanban.Core;
using Kanban.Core.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Kanban.Web.Pages.Projects;

public class IndexModel : PageModel
{
    private readonly KanbanDbContext _db;

    public IndexModel(KanbanDbContext db) => _db = db;

    public List<Project> Projects { get; private set; } = new();

    [BindProperty] public string Name { get; set; } = string.Empty;
    [BindProperty] public string Path { get; set; } = string.Empty;

    public string? Error { get; private set; }

    public async Task OnGetAsync(CancellationToken ct) => await LoadAsync(ct);

    private async Task LoadAsync(CancellationToken ct) =>
        Projects = await _db.Projects.AsNoTracking().OrderBy(p => p.Name).ToListAsync(ct);

    public async Task<IActionResult> OnPostAddAsync(CancellationToken ct)
    {
        Name = Name?.Trim() ?? string.Empty;
        Path = Path?.Trim() ?? string.Empty;

        if (Name.Length == 0 || Path.Length == 0)
        {
            Error = "Name and path are both required.";
        }
        else if (!Directory.Exists(Path))
        {
            Error = $"'{Path}' does not exist on this machine.";
        }
        else if (!Directory.Exists(System.IO.Path.Combine(Path, ".git")))
        {
            Error = $"'{Path}' is not a git repository. The agent works on a branch per card, so it must be one.";
        }
        else if (await _db.Projects.AnyAsync(p => p.Name == Name, ct))
        {
            Error = $"A project named '{Name}' already exists.";
        }

        if (Error is not null)
        {
            await LoadAsync(ct);
            return Page();
        }

        _db.Projects.Add(new Project { Name = Name, Path = Path });
        await _db.SaveChangesAsync(ct);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostToggleAsync(int id, CancellationToken ct)
    {
        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (project is not null)
        {
            project.IsActive = !project.IsActive;
            await _db.SaveChangesAsync(ct);
        }
        return RedirectToPage();
    }
}