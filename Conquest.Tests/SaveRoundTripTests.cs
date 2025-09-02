using Conquest.Core;
using Conquest.Core.Persistence;
using Conquest.Tests.TestHelpers;
using Xunit;

namespace Conquest.Tests;

public sealed class SaveRoundTripTests {
    [Fact]
    public void SaveLoad_RoundTrip_PreservesCoreState( ) {
        var g = TestGame.WithProgress( );

        var json = SaveSystem.Save(g.State);
        var restored = SaveSystem.Load(json);

        // Config fields (don’t rely on reference equality)
        Assert.Equal(g.State.Config.Width, restored.Config.Width);
        Assert.Equal(g.State.Config.Height, restored.Config.Height);
        Assert.Equal(g.State.Config.NumStars, restored.Config.NumStars);
        Assert.Equal(g.State.Config.MaxTurns, restored.Config.MaxTurns);
        Assert.Equal(g.State.Config.VictoryMultiplier, restored.Config.VictoryMultiplier);
        Assert.Equal(g.State.Config.OverwhelmFactor, restored.Config.OverwhelmFactor);
        Assert.Equal(g.State.Config.Capacity, restored.Config.Capacity);
        Assert.Equal(g.State.Config.Seed, restored.Config.Seed);

        // Global flags
        Assert.Equal(g.State.FogEnabled, restored.FogEnabled);

        // Stars: count & key fields
        Assert.Equal(g.State.Stars.Count, restored.Stars.Count);
        for (int i = 0; i < g.State.Stars.Count; i++) {
            var a = g.State.Stars[i];
            var b = restored.Stars[i];
            Assert.Equal(a.Name, b.Name);
            Assert.Equal(a.X, b.X);
            Assert.Equal(a.Y, b.Y);
            Assert.Equal(a.ExploredBy[1], b.ExploredBy[1]);
        }
    }
}
