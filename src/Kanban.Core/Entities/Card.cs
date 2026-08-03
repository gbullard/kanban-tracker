using Kanban.Core.Enums;

namespace Kanban.Core.Entities;

public class Card
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public Project? Project { get; set; }

    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }

    public CardStatus Status { get; set; } = CardStatus.New;
    public int Position { get; set; }

    /// <summary>Set by the Runner when a card arrives in Review. Null at all other times.</summary>
    public RunOutcome? Outcome { get; set; }

    /// <summary>The card's git branch, e.g. "card/12-add-login". Null until the first run starts.</summary>
    public string? BranchName { get; set; }

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;

    public List<CardNote> Notes { get; set; } = new();
    public List<Run> Runs { get; set; } = new();
}