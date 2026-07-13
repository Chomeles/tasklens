namespace TaskLens.Core.Tests;

// ponytail: scaffold smoke test so `dotnet test` runs something; task 02 adds real model tests.
public class ScaffoldSmokeTest
{
    [Fact]
    public void CoreAssemblyIsReferenced()
    {
        Assert.Equal("TaskLens.Core", typeof(Core.AssemblyMarker).Assembly.GetName().Name);
    }
}
