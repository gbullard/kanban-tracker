using Kanban.Core.Enums;

namespace Kanban.Core.Entities;

public class Run
{
    public int Id { get; set; }
    public int CardId { get; set; }
    public Card? Card { get; set; }

    public DateTime StartedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? EndedUtc { get; set; }

    public int? ExitCode { get; set; }
    public RunOutcome? Outcome { get; set; }
    public string? FailureReason { get; set; }
    public string? Summary { get; set; }

    public string BranchName { get; set; } = string.Empty;

    /// <summary>HEAD at the moment this run started. The diff is measured against it.</summary>
    public string BaseCommitSha { get; set; } = string.Empty;

    public int? FilesChanged { get; set; }
    public int? Insertions { get; set; }
    public int? Deletions { get; set; }

    public List<RunLogLine> LogLines { get; set; } = new();
}