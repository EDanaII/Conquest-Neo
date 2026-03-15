using Conquest.Tests.TestHelpers;
using Xunit;

namespace Conquest.Tests;

public sealed class FogTests {
    [Fact]
    public void Fog_On_Off_TogglesFlag( ) {
        var g1 = TestGame.New(seed: 1, scriptedInput: new[] { "fog on", "quit" });
        Assert.False(g1.State.FogEnabled);
        g1.Shell.Run( );
        Assert.True(g1.State.FogEnabled);

        var g2 = TestGame.New(seed: 1, scriptedInput: new[] { "fog off", "quit" });
        g2.State.FogEnabled = true;
        g2.Shell.Run( );
        Assert.False(g2.State.FogEnabled);
    }
}
