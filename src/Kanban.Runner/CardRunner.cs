using Kanban.Core;
using Kanban.Core.Entities;
using Kanban.Core.Enums;
using Kanban.Runner.Agents;
using Kanban.Runner.Classification;
using Kanban.Runner.Contracts;
using Kanban.Runner.Git;
using Kanban.Runner.Options;
using Kanban.Runner.Prompting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Kanban.Runner;

/// <summary>
/// Takes one claimed card from InProgress to Review. This class is the only writer of a
/// card's status while a run is in flight, and the agent it launches never touches the
/// database or git.
/// </summary>
public class CardRunner
{
    private const string ResultDirectory = ".kanban";
    private const string ResultFileName = "result.json";

    private readonly KanbanDbContext _db;
    private readonly IGitOperations _git;
    private readonly IAgentProcess _agent;
    private readonly RunnerOptions _options;
    private readonly ILogger<CardRunner> _log;

    public CardRunner(
        KanbanDbContext db,
        IGitOperations git,
        IAgentProcess agent,
        IOptions<RunnerOptions> options,
        ILogger<CardRunner> log)
    {
        _db = db;
        _git = git;
        _agent = agent;
        _options = options.Value;
        _log = log;
    }

    public async Task ExecuteAsync(int cardId, CancellationToken ct)
    {
        var card = await _db.Cards
            .Include(c => c.Project)
            .Include(c => c.Notes)
            .FirstOrDefaultAsync(c => c.Id == cardId, ct);

        if (card?.Project is null)
        {
            _log.LogError("Card {CardId} vanished or has no project. Nothing to run.", cardId);
            return;
        }

        var project = card.Project;
        var branch = card.BranchName ?? BranchNaming.ForCard(card.Id, card.Title);

        var run = new Run
        {
            CardId = card.Id,
            BranchName = branch,
            BaseCommitSha = string.Empty
        };
        _db.Runs.Add(run);
        await _db.SaveChangesAsync(ct);

        _log.LogInformation("Card {CardId} run {RunId} starting on branch {Branch}.", card.Id, run.Id, branch);

        var gitFailure = await PrepareRepositoryAsync(project.Path, branch, run, ct);

        AgentExecution execution = new(0, false);
        ResultFileRead result = ResultFileRead.Missing();
        var commitProduced = false;

        if (gitFailure is null)
        {
            card.BranchName = branch;
            await _db.SaveChangesAsync(ct);

            var prompt = PromptComposer.Compose(
                card,
                project,
                card.Notes.OrderBy(n => n.CreatedUtc).ThenBy(n => n.Id).ToList(),
                branch);

            var seq = 0;
            execution = await _agent.RunAsync(
                new AgentRequest(project.Path, prompt),
                async (stream, text) =>
                {
                    _db.RunLogLines.Add(new RunLogLine
                    {
                        RunId = run.Id,
                        Seq = Interlocked.Increment(ref seq),
                        Stream = stream,
                        Text = text
                    });
                    await _db.SaveChangesAsync(ct);
                },
                ct);

            result = ReadAndDeleteResultFile(project.Path);
            commitProduced = await _git.CommitAllAsync(project.Path, $"card {card.Id}: {card.Title}", ct);

            if (commitProduced)
            {
                var stat = await _git.GetDiffStatAsync(project.Path, run.BaseCommitSha, ct);
                run.FilesChanged = stat.FilesChanged;
                run.Insertions = stat.Insertions;
                run.Deletions = stat.Deletions;
            }
        }

        var classification = RunClassifier.Classify(new RunFacts(
            gitFailure,
            execution.TimedOut,
            _options.AgentTimeoutMinutes,
            execution.ExitCode,
            result,
            commitProduced));

        run.EndedUtc = DateTime.UtcNow;
        run.ExitCode = execution.ExitCode;
        run.Outcome = classification.Outcome;
        run.FailureReason = classification.FailureReason;
        run.Summary = classification.Summary;

        if (!string.IsNullOrWhiteSpace(classification.Summary))
        {
            _db.CardNotes.Add(new CardNote
            {
                CardId = card.Id,
                Author = NoteAuthor.Agent,
                Body = classification.Summary
            });
        }

        card.Status = CardStatus.Review;
        card.Outcome = classification.Outcome;
        card.UpdatedUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        _log.LogInformation(
            "Card {CardId} run {RunId} finished: {Outcome}. {Reason}",
            card.Id, run.Id, classification.Outcome, classification.FailureReason ?? string.Empty);
    }

    /// <summary>Returns null on success, or a human-readable reason the run cannot start.</summary>
    private async Task<string?> PrepareRepositoryAsync(string path, string branch, Run run, CancellationToken ct)
    {
        if (!Directory.Exists(path))
        {
            return $"project directory '{path}' does not exist";
        }

        if (!_git.IsRepository(path))
        {
            return $"'{path}' is not a git repository";
        }

        try
        {
            await _git.EnsureExcludedAsync(path, ResultDirectory + "/", ct);

            if (!await _git.IsWorkingTreeCleanAsync(path, ct))
            {
                return "working tree not clean — commit or stash your changes before queueing a card";
            }

            await _git.CheckoutBranchAsync(path, branch, ct);

            run.BaseCommitSha = await _git.GetHeadShaAsync(path, ct);
            await _db.SaveChangesAsync(ct);

            return null;
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Git preparation failed for {Path}.", path);
            return $"git preparation failed: {ex.Message}";
        }
    }

    private ResultFileRead ReadAndDeleteResultFile(string projectPath)
    {
        var file = Path.Combine(projectPath, ResultDirectory, ResultFileName);

        if (!File.Exists(file))
        {
            return ResultFileRead.Missing();
        }

        string content;
        try
        {
            content = File.ReadAllText(file);
        }
        catch (IOException ex)
        {
            _log.LogWarning(ex, "Could not read {File}.", file);
            return ResultFileRead.Malformed();
        }

        try { File.Delete(file); } catch { /* the next run overwrites it anyway */ }

        return ResultFileParser.Parse(content);
    }
}