using Conquest.CLI;
using Conquest.Core;
using Conquest.Core.Model;
using Conquest.Tests.TestHelpers;
using System;
using Xunit;

namespace Conquest.Tests;

public sealed class FleetBasicsTests {
    [Theory]
    [InlineData("move", -1)]
    [InlineData("move", 0)]
    [InlineData("move", 27)]
    [InlineData("move", 99)]
    [InlineData("course", -1)]
    [InlineData("course", 0)]
    [InlineData("course", 27)]
    [InlineData("course", 99)]
    public void Course_InvalidSlot_ReportsError_PreservesFleets_AndContinues(string command, int slot) {
        var g = TestGame.New();
        g.State.Stars[0].X = 2;
        g.State.Stars[0].Y = 3;
        g.State.Stars[1].X = 5;
        g.State.Stars[1].Y = 7;
        // Preserve an existing course as well as stationary fleets on both teams.
        g.State.FleetsSys.Place(0, 1, 0);
        g.State.FleetsSys.Place(1, 1, 0);
        g.State.FleetsSys.Place(1, 26, 1);
        g.State.FleetsSys.SetCourse(1, 1, 1);
        g.Game.Tick();
        var fleetsBefore = System.Text.Json.JsonSerializer.Serialize(g.State.Players);
        var turnBefore = g.Game.Turn;

        var rc = g.RunCommands($"{command} {slot} 0", "map");

        Assert.Equal(0, rc);
        Assert.Contains(g.IO.OutputLines, line => line.Contains($"Illegal fleet slot {slot}."));
        Assert.Equal(fleetsBefore, System.Text.Json.JsonSerializer.Serialize(g.State.Players));
        Assert.Equal(turnBefore, g.Game.Turn);
        Assert.Equal(1, g.Renderer.CallCount);
    }

    [Theory]
    [InlineData("move", 1)]
    [InlineData("move", 26)]
    [InlineData("course", 1)]
    [InlineData("course", 26)]
    public void Course_BoundarySlot_SetsCourse_AfterInvalidCommand(string command, int slot) {
        var g = TestGame.New();
        g.State.Stars[0].X = 2;
        g.State.Stars[0].Y = 3;
        g.State.Stars[1].X = 5;
        g.State.Stars[1].Y = 7;
        var tf = g.State.FleetsSys.Place(1, slot, 0);

        var rc = g.RunCommands($"{command} 99 1", $"{command} {slot} 1");

        Assert.Equal(0, rc);
        Assert.Equal(1, tf.Destination);
        Assert.Equal(7, tf.ETA);
        Assert.Equal(7, tf.EtaOriginal);
        Assert.Equal((2, 3), (tf.X, tf.Y));
        Assert.Equal(3, tf.Scouts);
        Assert.Equal(0, g.Game.Turn);
        Assert.Contains(g.IO.OutputLines, line => line.Contains($"TF {slot} course set to star 1."));
    }

    private static (Game game, GameState state, DummyIO io) NewGame( ) {
        var io = new DummyIO(Array.Empty<string>( )); // no commands; drive via APIs
        var rng = new CRandRng( );
        var state = new GameState(GameConfig.Default);
        var game = new Game(rng, state);

        return (game, state, io);
    }

    private static (GameShell shell, Game game, GameState state, DummyIO io, SpyRenderer renderer) NewShell(params string[] commands) {
        var io = new DummyIO(commands);
        var rng = new CRandRng( );
        var state = new GameState(GameConfig.Default);
        var renderer = new SpyRenderer( );
        var game = new Game(rng, state);
        var shell = new GameShell(game, io, rng, renderer);

        return (shell, game, state, io, renderer);
    }

    [Fact]
    public void Place_Puts_TF_At_Star_And_Updates_State( ) {
        var (shell, _, state, _, _) = NewShell("seed 1", "genstars", "place 1", "quit");
        var rc = shell.Run( );

        var tf = state.Players[1].Tf[1];
        var star = state.Stars[1];

        Assert.Equal(star.X, tf.X);
        Assert.Equal(star.Y, tf.Y);
        Assert.True(tf.Scouts > 0);
        Assert.True(tf.Destination is null);
    }

    [Fact]
    public void Move_Computes_ETA_And_Arrives_On_Time( ) {
        var (shell, game, state, _, _) = NewShell("seed 1", "genstars", "place 1", "move 1 2", "quit");
        var rc = shell.Run( );

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
        var (shell, _, state, io, _) = NewShell("seed 1", "genstars", "move 1 2", "quit");
        var rc = shell.Run( );

        var tf = state.Players[1].Tf[1];
        Assert.True(tf.Destination is null);
        Assert.Equal((0, 0), (tf.X, tf.Y));
    }
}
