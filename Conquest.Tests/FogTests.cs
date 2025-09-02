using Conquest.Tests.TestHelpers;
using Xunit;

namespace Conquest.Tests;

public sealed class FogTests {
    [Fact]
    public void Fog_On_Off_TogglesFlag( ) {
        var G = TestGame.New(seed: 1);
        Assert.False(G.State.FogEnabled);

        G.RunCommand("fog on");
        Assert.True(G.State.FogEnabled);

        G.RunCommand("fog off");
        Assert.False(G.State.FogEnabled);
    }
}
