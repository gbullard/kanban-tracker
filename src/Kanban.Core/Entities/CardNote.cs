using Kanban.Core.Enums;

namespace Kanban.Core.Entities;

public class CardNote
{
    public int Id { get; set; }
    public int CardId { get; set; }
    public Card? Card { get; set; }

    public NoteAuthor Author { get; set; }
    public string Body { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}