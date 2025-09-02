using Conquest.Tests.TestHelpers;
using Xunit;

namespace Conquest.Tests;

public sealed class MapCommandTests {
    [Fact]
    public void Map_Invokes_Renderer( ) {
        var G = TestGame.New(seed: 1, scriptedInput: new[] { "map" });
        var rc = G.RunCommands( ); // will append "quit" automatically

        Assert.Equal(0, rc);
        Assert.True(G.Renderer.CallCount >= 1);
        Assert.Same(G.State, G.Renderer.LastState);
    }
}
