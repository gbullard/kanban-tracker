using Kanban.Core.Enums;

namespace Kanban.Core.Entities;

public class RunLogLine
{
    public long Id { get; set; }
    public int RunId { get; set; }
    public Run? Run { get; set; }

    /// <summary>Monotonic within a run. Preserves the interleaving of stdout and stderr.</summary>
    public int Seq { get; set; }

    public LogStream Stream { get; set; }
    public string Text { get; set; } = string.Empty;
    public DateTime LoggedUtc { get; set; } = DateTime.UtcNow;
}