namespace AppFlow.Tests.Adapters;

using AppFlow.Sources;
using FluentAssertions;

public class WinGetAdapterTests
{
    [Fact]
    public void IsAvailable_DoesNotThrowWhenWinGetIsMissing()
    {
        var adapter = new WinGetAdapter();
        var action = () => _ = adapter.IsAvailable;
        action.Should().NotThrow();
    }

    [Fact]
    public void ParseTabularOutput_ParsesAlignedWinGetColumns()
    {
        const string output = """
            Name                         Id                         Version  Source
            ------------------------------------------------------------------------
            Visual Studio Code           Microsoft.VisualStudioCode 1.95.0   winget
            VLC media player             VideoLAN.VLC               3.0.21   winget
            """;

        var rows = WinGetAdapter.ParseTabularOutput(output);

        rows.Should().HaveCount(2);
        rows[0]["Name"].Should().Be("Visual Studio Code");
        rows[0]["Id"].Should().Be("Microsoft.VisualStudioCode");
        rows[1]["Id"].Should().Be("VideoLAN.VLC");
    }
}
