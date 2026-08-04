using Kanban.Runner.Prompting;
using Xunit;

namespace Kanban.Runner.Tests;

public class BranchNamingTests
{
    [Fact]
    public void Lowercases_and_hyphenates_the_title()
    {
        Assert.Equal("card/12-add-user-login", BranchNaming.ForCard(12, "Add User Login"));
    }

    [Fact]
    public void Strips_characters_git_refuses_in_a_ref_name()
    {
        // git rejects ~ ^ : ? * [ \ and whitespace in ref names.
        Assert.Equal("card/3-fix-the-bug", BranchNaming.ForCard(3, "Fix: the ~bug~?!"));
    }

    [Fact]
    public void Collapses_runs_of_separators_and_trims_them()
    {
        Assert.Equal("card/8-a-b", BranchNaming.ForCard(8, "  --a  ///  b--  "));
    }

    [Fact]
    public void Truncates_a_long_title_without_leaving_a_trailing_hyphen()
    {
        var branch = BranchNaming.ForCard(1, new string('x', 20) + " " + new string('y', 200));

        Assert.True(branch.Length <= 60, $"branch was {branch.Length} chars: {branch}");
        Assert.DoesNotContain("--", branch);
        Assert.False(branch.EndsWith('-'));
    }

    [Fact]
    public void Falls_back_to_the_id_when_the_title_slugifies_to_nothing()
    {
        Assert.Equal("card/5-task", BranchNaming.ForCard(5, "~~~"));
    }
}