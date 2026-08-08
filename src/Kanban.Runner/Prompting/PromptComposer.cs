using System.Text;
using Kanban.Core.Entities;

namespace Kanban.Runner.Prompting;

/// <summary>
/// Builds the entire brief the agent receives. The agent gets no other context, so
/// everything it needs — including the history of a reworked card — is assembled here.
/// </summary>
public static class PromptComposer
{
    public static string Compose(
        Card card,
        Project project,
        IReadOnlyList<CardNote> notes,
        string branchName)
    {
        var sb = new StringBuilder();

        sb.AppendLine("# Task");
        sb.AppendLine();
        sb.AppendLine(card.Title);

        if (!string.IsNullOrWhiteSpace(card.Description))
        {
            sb.AppendLine();
            sb.AppendLine(card.Description.Trim());
        }

        if (notes.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("# Notes");
            sb.AppendLine();
            sb.AppendLine("Earlier passes and human feedback on this task, oldest first.");

            foreach (var note in notes.OrderBy(n => n.CreatedUtc).ThenBy(n => n.Id))
            {
                sb.AppendLine();
                sb.AppendLine($"## {note.Author} — {note.CreatedUtc:yyyy-MM-dd HH:mm} UTC");
                sb.AppendLine();
                sb.AppendLine(note.Body.Trim());
            }
        }

        sb.AppendLine();
        sb.AppendLine("# Working directory");
        sb.AppendLine();
        sb.AppendLine($"Project: {project.Name}");
        sb.AppendLine($"Path: {project.Path}");
        sb.AppendLine($"You are already on branch {branchName}, checked out for you.");

        sb.AppendLine();
        sb.AppendLine("# Rules");
        sb.AppendLine();
        sb.AppendLine("- Do not run any git command. Do not commit, branch, stash, merge, or switch branches.");
        sb.AppendLine("  Your changes are committed for you when you finish.");
        sb.AppendLine("- Do not modify anything outside the working directory above.");
        sb.AppendLine("- Work until the task is done, then write the result file described below.");
        sb.AppendLine("  Writing that file is how you report back. If you skip it, your work is recorded as failed.");

        sb.AppendLine();
        sb.AppendLine("# Required output: .kanban/result.json");
        sb.AppendLine();
        sb.AppendLine("Create the .kanban directory in the working directory if it does not exist,");
        sb.AppendLine("then write .kanban/result.json containing exactly this shape:");
        sb.AppendLine();
        sb.AppendLine("```json");
        sb.AppendLine("{");
        sb.AppendLine("  \"status\": \"completed\",");
        sb.AppendLine("  \"summary\": \"2-5 sentences describing what you changed and why.\"");
        sb.AppendLine("}");
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("If you could not complete the task, write this instead:");
        sb.AppendLine();
        sb.AppendLine("```json");
        sb.AppendLine("{");
        sb.AppendLine("  \"status\": \"blocked\",");
        sb.AppendLine("  \"summary\": \"What you tried and how far you got.\",");
        sb.AppendLine("  \"blockedReason\": \"The specific thing preventing completion.\"");
        sb.AppendLine("}");
        sb.AppendLine("```");

        return sb.ToString();
    }
}