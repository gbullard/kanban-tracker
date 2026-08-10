using Kanban.Runner.Tests.Fakes;
using Xunit;

namespace Kanban.Runner.Tests;

// xUnit resolves [CollectionDefinition] per assembly, so this cannot live in Kanban.TestSupport.
[CollectionDefinition("database")]
public class DatabaseCollection : ICollectionFixture<RunnerDatabaseFixture> { }