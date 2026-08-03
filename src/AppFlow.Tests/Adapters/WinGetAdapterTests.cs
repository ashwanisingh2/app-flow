namespace AppFlow.Tests.Adapters;

using AppFlow.Sources;
using FluentAssertions;
using Xunit;

public class WinGetAdapterTests
{
    [Fact]
    public void IsAvailable_ShouldNotThrow()
    {
        var adapter = new WinGetAdapter();
        
        // Just testing that it doesn't crash when probing
        var isAvailable = adapter.IsAvailable;
        
        // It could be true or false depending on the system, but shouldn't crash.
        isAvailable.Should().Be(isAvailable);
    }
}
