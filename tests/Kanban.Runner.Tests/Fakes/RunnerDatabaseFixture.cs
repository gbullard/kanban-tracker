using Kanban.TestSupport;

namespace Kanban.Runner.Tests.Fakes;

public class RunnerDatabaseFixture : DatabaseFixture
{
    public RunnerDatabaseFixture() : base("Kanban_Runner_Tests") { }
}