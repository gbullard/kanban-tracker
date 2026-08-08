namespace Kanban.Runner.Git;

public record DiffStat(int FilesChanged, int Insertions, int Deletions)
{
    public static readonly DiffStat Empty = new(0, 0, 0);
}