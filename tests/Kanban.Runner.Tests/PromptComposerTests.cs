using Kanban.Core.Entities;
using Kanban.Core.Enums;
using Kanban.Runner.Prompting;
using Xunit;

namespace Kanban.Runner.Tests;

public class PromptComposerTests
{
    private static readonly Project Project = new()
    {
        Id = 1,
        Name = "Demo",
        Path = @"C:\Repos\demo"
    };

    private static Card Card() => new()
    {
        Id = 12,
        ProjectId = 1,
        Title = "Add user login",
        Description = "Use forms auth."
    };

    [Fact]
    public void Includes_the_title_description_project_path_and_branch()
    {
        var prompt = PromptComposer.Compose(Card(), Project, Array.Empty<CardNote>(), "card/12-add-user-login");

        Assert.Contains("Add user login", prompt);
        Assert.Contains("Use forms auth.", prompt);
        Assert.Contains(@"C:\Repos\demo", prompt);
        Assert.Contains("card/12-add-user-login", prompt);
    }

    [Fact]
    public void Includes_notes_in_creation_order_labelled_by_author()
    {
        var notes = new[]
        {
            new CardNote { Author = NoteAuthor.Agent, Body = "First pass done.",  CreatedUtc = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc) },
            new CardNote { Author = NoteAuthor.User,  Body = "Also add logging.", CreatedUtc = new DateTime(2026, 1, 1, 11, 0, 0, DateTimeKind.Utc) }
        };

        var prompt = PromptComposer.Compose(Card(), Project, notes, "card/12-x");

        var firstIndex = prompt.IndexOf("First pass done.", StringComparison.Ordinal);
        var secondIndex = prompt.IndexOf("Also add logging.", StringComparison.Ordinal);

        Assert.True(firstIndex >= 0 && secondIndex > firstIndex, "notes must appear in creation order");
        Assert.Contains("Agent", prompt);
        Assert.Contains("User", prompt);
    }

    [Fact]
    public void Omits_the_notes_section_entirely_when_there_are_none()
    {
        var prompt = PromptComposer.Compose(Card(), Project, Array.Empty<CardNote>(), "card/12-x");

        Assert.DoesNotContain("# Notes", prompt);
    }

    [Fact]
    public void Handles_a_card_with_no_description()
    {
        var card = Card();
        card.Description = null;

        var prompt = PromptComposer.Compose(card, Project, Array.Empty<CardNote>(), "card/12-x");

        Assert.Contains("Add user login", prompt);
        Assert.DoesNotContain("(null)", prompt);
    }

    [Fact]
    public void States_the_result_contract_and_forbids_git()
    {
        var prompt = PromptComposer.Compose(Card(), Project, Array.Empty<CardNote>(), "card/12-x");

        Assert.Contains(".kanban/result.json", prompt);
        Assert.Contains("\"status\"", prompt);
        Assert.Contains("\"summary\"", prompt);
        Assert.Contains("blocked", prompt);
        Assert.Contains("Do not run any git command", prompt);
    }
}