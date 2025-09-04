using Conquest.Core;
using Conquest.Core.Model;
using Conquest.Tests.TestHelpers;
using System;
using Xunit;

namespace Conquest.Tests;

public sealed class FleetBasicsTests {
    private static (Game game, GameState state, DummyIO io) NewGame( ) {
        var io = new DummyIO(Array.Empty<string>( )); // no commands; we’ll drive via APIs
        var rng = new CRandRng( );
        var state = new GameState(GameConfig.Default);
        var renderer = new SpyRenderer( );
        var game = new Game(io, rng, state, renderer);
        return (game, state, io);
    }

    [Fact]
    public void Place_Puts_TF_At_Star_And_Updates_State( ) {
        // Drive via CLI sequence (constructor takes the scripted input)
        var io = new DummyIO(new string[] { "seed 1", "genstars", "place 1", "quit" });
        var rng = new CRandRng( );
        var state = new GameState(GameConfig.Default);
        var renderer = new SpyRenderer( );
        var game = new Game(io, rng, state, renderer);

        game.Run( );

        var tf = state.Players[1].Tf[1];
        var star = state.Stars[1];

        Assert.Equal(star.X, tf.X);
        Assert.Equal(star.Y, tf.Y);
        Assert.True(tf.Scouts > 0);
        Assert.True(tf.Destination is null); // placed leaves no course set
    }

    [Fact]
    public void Move_Computes_ETA_And_Arrives_On_Time( ) {
        var io = new DummyIO(new string[] { "seed 1", "genstars", "place 1", "move 1 2", "quit" });
        var rng = new CRandRng( );
        var state = new GameState(GameConfig.Default);
        var renderer = new SpyRenderer( );
        var game = new Game(io, rng, state, renderer);

        game.Run( );

        var tf = state.Players[1].Tf[1];
        var s1 = state.Stars[1];
        var s2 = state.Stars[2];
        var manhattan = Math.Abs(s1.X - s2.X) + Math.Abs(s1.Y - s2.Y);

        Assert.True(tf.ETA > 0);
        Assert.Equal(manhattan, tf.EtaOriginal);

        game.Tick(manhattan);

        Assert.Equal((s2.X, s2.Y), (tf.X, tf.Y));
        Assert.Equal(0, tf.ETA);
        Assert.True(tf.Destination is null);
    }

    [Fact]
    public void Tick_Increments_Turn( ) {
        var (game, _, _) = NewGame( );
        var t0 = game.Turn;
        game.Tick(3);
        Assert.Equal(t0 + 3, game.Turn);
    }

    [Fact]
    public void Move_Guard_Blocks_Unplaced_TF( ) {
        // Attempt to move without placing first; guard should prevent motion
        var io = new DummyIO(new string[] { "seed 1", "genstars", "move 1 2", "quit" });
        var rng = new CRandRng( );
        var state = new GameState(GameConfig.Default);
        var renderer = new SpyRenderer( );
        var game = new Game(io, rng, state, renderer);

        game.Run( );

        var tf = state.Players[1].Tf[1];
        Assert.True(tf.Destination is null);   // no valid course
        Assert.Equal((0, 0), (tf.X, tf.Y)); // still unplaced
    }
}
