using Conquest.Core;
using Conquest.Core.Persistence;
using Conquest.Core.Random;
using Conquest.Core.Stars;
using Conquest.Tests.TestHelpers;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Xunit;

namespace Conquest.Tests;

public class StarGenTests {
    [Fact]
    public void StarGen_StructuralRulesHold_ForSeed12345( ) {
        var rng = new CRandRng( ); rng.Seed(12345);
        int W = 64, H = 64, spacing = 3, count = 20;
        var gen = new StarGenerator(W, H, spacing);
        var stars = gen.Generate(count, rng);

        Assert.Equal(count, stars.Count);

        var seen = new HashSet<(int, int)>( );
        foreach (var (x, y) in stars) {
            Assert.InRange(x, 0, W - 1);
            Assert.InRange(y, 0, H - 1);
            Assert.True(seen.Add((x, y)), $"Duplicate star at ({x},{y})");
        }

        for (int i = 0; i < stars.Count; i++)
            for (int j = i + 1; j < stars.Count; j++) {
                var (x1, y1) = stars[i];
                var (x2, y2) = stars[j];
                int dx = x1 - x2, dy = y1 - y2;
                int d2 = dx * dx + dy * dy;
                Assert.True(d2 >= spacing * spacing,
                    $"Stars too close: ({x1},{y1}) and ({x2},{y2})");
            }

        var rng2 = new CRandRng( ); rng2.Seed(12345);
        var stars2 = gen.Generate(count, rng2);
        Assert.Equal(stars, stars2);
    }

    /* TODO:
    [Fact]
    public void Config_InvalidDimensions_Throws( ) {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GameConfig(width: 0, height: 64, ...));
    }
    */

    [Fact]
    public void StarGen_CountZero_ReturnsEmpty( ) {
        var rng = new CRandRng( ); rng.Seed(42);
        var gen = new StarGenerator(16, 16, 3);
        var stars = gen.Generate(0, rng);
        Assert.Empty(stars);
    }

    [Fact]
    public void Router_InvokesHandler_CaseInsensitive( ) {
        var called = false;
        var h = new FakeHandler("genstars", (a, io) => called = true);
        var r = new CommandRouter(new[] { h });

        Assert.True(r.TryRun("GenStars", new[] { "--count", "5" }, new DummyIO( )));
        Assert.True(called);
    }

    [Fact]
    public void Fog_TogglesStateFlag( ) {
        var G = TestGame.New( ); // your factory: constructs GameState + fakes
        var s0 = G.State.FogEnabled;
        G.RunCommand("fog on");
        Assert.True(G.State.FogEnabled);
        G.RunCommand("fog off");
        Assert.False(G.State.FogEnabled);
    }

    /* TODO: Later -- this isn't important now
    [Fact]
    public void SaveLoad_RoundTrip_PreservesCoreState( ) {
        var g = TestGame.WithProgress( );
        var blob = SaveSystem.Save(g.State);
        var restored = SaveSystem.Load(blob);

        Assert.Equal(g.State.Config, restored.Config);
        Assert.Equal(g.State.Stars, restored.Stars);
        Assert.Equal(g.State.FogEnabled, restored.FogEnabled);
    }
    */

    [Fact]
    public void Rng_Next_RespectsBounds( ) {
        var r = new CRandRng( ); r.Reseed(123);
        for (int i = 0; i < 10_000; i++) {
            var v = r.Next(5, 17);
            Assert.InRange(v, 5, 16);
        }
    }

    [Fact]
    public void MapCommand_InvokesRenderer( ) {
        var io = new DummyIO(new[] { "map", "quit" });
        var rng = new CRandRng( ); rng.Seed(1);
        var state = new GameState(GameConfig.Default);
        var renderer = new SpyRenderer( );

        var game = new Game(io, rng, state, renderer);
        game.Run( );

        Assert.True(renderer.Called);
    }

    private sealed class SpyRenderer : IMapRenderer {
        public bool Called { get; private set; }
        public void Render(IGameIO io, GameState state) => Called = true;
    }

    [Theory]
    [InlineData(new[] { "--count", "7" }, "--count", 9, 7)]
    [InlineData(new[] { "--count=7" }, "--count", 9, 7)]
    [InlineData(new[] { "--count=NaN" }, "--count", 9, 9)]
    [InlineData(new string[0], "--count", 9, 9)]
    public void Args_GetInt_Works(string[] args, string key, int def, int expected) {
        Assert.Equal(expected, args.GetInt(key, def));
    }
    
    [Fact]
    public void Cli_Smoke_Sequence_Completes( ) {
        var io = new DummyIO(new[] { "seed 1", "genstars", "map", "quit" });
        var rng = new CRandRng( );
        var state = new GameState(GameConfig.Default);
        var renderer = new SpyRenderer( );
        var game = new Game(io, rng, state, renderer);

        var rc = game.Run( );
        Assert.Equal(0, rc);
        Assert.Equal(state.Config.NumStars, state.Stars.Count /* if index 0 reserved */);
    }

    public static TestGame WithProgress( ) {
        // Start from a deterministic seed so results are reproducible
        var G = TestGame.New(seed: 1);

        // Use the real game loop to mutate state (no string asserts needed)
        G.RunCommands("genstars", "fog on");

        // If you have a reserved [0] element, skip it; otherwise remove Skip(1)
        var first = G.State.Stars.Skip(1).FirstOrDefault( );
        if (first is not null) {
            // If your Star uses a different structure, tweak this line accordingly
            first.ExploredBy[1] = true;
        }

        return G;
    }
}
