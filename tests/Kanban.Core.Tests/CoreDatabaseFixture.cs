using Kanban.TestSupport;

namespace Kanban.Core.Tests;

public class CoreDatabaseFixture : DatabaseFixture
{
    public CoreDatabaseFixture() : base("Kanban_Core_Tests") { }
}