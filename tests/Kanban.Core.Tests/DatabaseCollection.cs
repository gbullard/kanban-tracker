using Xunit;

namespace Kanban.Core.Tests;

// xUnit resolves [CollectionDefinition] per assembly, so this cannot live in Kanban.TestSupport.
[CollectionDefinition("database")]
public class DatabaseCollection : ICollectionFixture<CoreDatabaseFixture> { }