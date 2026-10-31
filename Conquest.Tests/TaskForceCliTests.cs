using Conquest.Core;
using Conquest.Core.Model;
using Conquest.Tests.TestHelpers;

namespace Conquest.Tests;

public sealed class TaskForceCliTests {
    private static TestGame SmallGame() {
        var g = TestGame.New(new GameConfig { Width = 4, Height = 4, NumStars = 3, SafeExplore = true });
        var positions = new[] { (0, 0), (3, 0), (0, 3) };
        for (int i = 0; i < positions.Length; i++) {
            var star = g.State.Stars[i];
            (star.X, star.Y) = positions[i];
            g.State.Board[star.X, star.Y].StarRef = star;
            g.State.Board[star.X, star.Y].Star = i;
        }
        return g;
    }

    private static string MapCell(IEnumerable<string> lines, int x, int y) =>
        lines.SkipWhile(line => !line.StartsWith("tf map:", StringComparison.Ordinal)).Skip(1)
            .First(line => line.StartsWith($"{y,2} ", StringComparison.Ordinal))
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)[x + 1];

    [Theory]
    [InlineData("move", "A")]
    [InlineData("course", "A")]
    [InlineData("move", "B")]
    [InlineData("course", "B")]
    [InlineData("move", "V")]
    [InlineData("course", "V")]
    public void ClassicStartupFleet_IsListedAndMovesInTheCurrentGame(string command, string start) {
        var g = TestGame.New(seed: 1);
        Assert.Equal(0, g.RunCommands("newgame", start, "C2S2R", "tf list", "tf map"));
        var game = g.Shell.CurrentGame;
        var state = game.State;
        var fleet = state.Players[1].Tf[1];
        Assert.NotSame(g.Game, game);
        Assert.False(state.PendingSharedStartEncounter);
        string initialListing = Assert.Single(g.IO.OutputLines.Where(line => line.StartsWith("TF 1:")));
        Assert.Contains("transports=35", initialListing);
        Assert.Contains("scouts=2", initialListing);
        Assert.Contains("cruisers=1", initialListing);
        Assert.Contains("battlestars=0", initialListing);
        Assert.Contains("stationary", initialListing);
        Assert.Contains($"({start})", initialListing);
        Assert.DoesNotContain("->", initialListing);
        Assert.DoesNotContain("ETA", initialListing);
        Assert.Equal("1", MapCell(g.IO.OutputLines, fleet.X, fleet.Y));

        int target = start == "A" ? 1 : 0;
        var destination = state.Stars[target];
        int distance = (int)(Math.Sqrt(Math.Pow(destination.X - fleet.X, 2) + Math.Pow(destination.Y - fleet.Y, 2)) - 0.049 + 1);
        state.Players[1].Range = 20; // Fleet recognition is independent of range rejection.
        destination.ExploredBy[1] = true;
        int outputStart = g.IO.OutputLines.Count;
        Assert.Equal(0, g.RunCommands($"{command} 1 {target}", "tf list", "tick", "tf list"));
        Assert.Same(game, g.Shell.CurrentGame);
        Assert.Equal(target, fleet.Destination);
        Assert.Equal(distance - 1, fleet.ETA);
        Assert.Equal(distance, fleet.EtaOriginal);
        Assert.Equal(1, game.Turn);
        Assert.Equal((35, 2, 1, 0), (fleet.Transports, fleet.Scouts, fleet.Cruisers, fleet.BattleStars));
        Assert.Equal(2, state.Players[1].RangeResearchProgress);
        string output = string.Concat(g.IO.OutputLines.Skip(outputStart));
        Assert.Contains($"TF 1 course set to star {target}. ETA {distance}.", output);
        Assert.Contains("travelling -> star", output);
        Assert.DoesNotContain("not placed", output);
        Assert.DoesNotContain("has 0 ships", output);
    }

    [Theory]
    [InlineData("move", "transport")]
    [InlineData("course", "transport")]
    [InlineData("move", "scout")]
    [InlineData("course", "scout")]
    [InlineData("move", "cruiser")]
    [InlineData("course", "cruiser")]
    [InlineData("move", "battlestar")]
    [InlineData("course", "battlestar")]
    public void DebugPlacedFleet_WithAnyShipType_CanMoveFromOrigin(string command, string ship) {
        var g = SmallGame();
        var fleet = g.State.FleetsSys.Place(1, 1, 0, scouts: 0);
        switch (ship) {
            case "transport": fleet.Transports = 2; break;
            case "scout": fleet.Scouts = 2; break;
            case "cruiser": fleet.Cruisers = 2; break;
            case "battlestar": fleet.BattleStars = 2; break;
        }
        Assert.Equal(0, fleet.Destination);
        Assert.Equal(0, g.RunCommands("tf list", "tf map", $"{command} 1 1", "tick"));
        Assert.Equal((1, 0), (fleet.X, fleet.Y));
        Assert.Equal(1, fleet.Destination);
        Assert.Equal(2, fleet.ETA);
        Assert.Contains(g.IO.OutputLines, line => line.Contains("TF 1: (0,0)") && line.Contains("stationary"));
        Assert.Equal("1", MapCell(g.IO.OutputLines, 0, 0));
        Assert.DoesNotContain(g.IO.OutputLines, line => line.Contains("has 0 ships") || line.Contains("not placed"));
    }

    [Theory]
    [InlineData("move")]
    [InlineData("course")]
    public void ClassicFleetWithoutScouts_IsRecognizedAfterStartup(string command) {
        var g = TestGame.New(seed: 1);
        g.RunCommands("newgame", "B", "C14R", "tf list", $"{command} 1 0");
        var fleet = g.Shell.CurrentGame.State.Players[1].Tf[1];
        Assert.Equal((35, 0, 1), (fleet.Transports, fleet.Scouts, fleet.Cruisers));
        Assert.Equal(0, fleet.Destination);
        Assert.Equal(5, fleet.ETA);
        Assert.Contains(g.IO.OutputLines, line => line.StartsWith("TF 1:") && line.Contains("cruisers=1"));
        Assert.DoesNotContain(g.IO.OutputLines, line => line.Contains("has 0 ships"));
    }

    [Fact]
    public void ValidClassicDestination_IsPlacementEvidence_EvenWithoutSectorReference() {
        var g = SmallGame();
        var fleet = g.State.Players[1].Tf[1];
        fleet.Destination = 0;
        fleet.Cruisers = 1;
        // Exercise the destination branch independently of debug occupancy evidence.
        Assert.Null(g.State.Board[0, 0].TaskForces[1]);
        g.RunCommands("tf list", "move 1 1");
        Assert.Equal(1, fleet.Destination);
        Assert.Equal(3, fleet.ETA);
        Assert.Contains(g.IO.OutputLines, line => line.Contains("TF 1: (0,0)") && line.Contains("cruisers=1"));
    }

    [Theory]
    [InlineData("move")]
    [InlineData("course")]
    public void ArrivalAtStarZero_RemainsListedAndCanDepartAgain(string command) {
        var g = SmallGame();
        var fleet = g.State.FleetsSys.Place(1, 1, 1);
        g.RunCommands($"{command} 1 0", "tick 3", "tf list", "tf map");
        Assert.Equal((0, 0), (fleet.X, fleet.Y));
        Assert.Equal(0, fleet.Destination); // Stationary destination retains star zero.
        Assert.Same(fleet, g.State.Board[0, 0].TaskForces[1]);
        string listing = Assert.Single(g.IO.OutputLines.Where(line => line.StartsWith("TF 1:")));
        Assert.Contains("@ star 0 (A) stationary", listing);
        Assert.DoesNotContain("->", listing);
        Assert.Equal("1", MapCell(g.IO.OutputLines, 0, 0));
        g.RunCommands($"{command} 1 2");
        Assert.Equal(2, fleet.Destination);
        Assert.Equal(3, fleet.ETA);
    }

    [Fact]
    public void FleetMap_IncludesAllZeroAxesAndActualStarNames_WithoutPhantomFleets() {
        var g = SmallGame();
        g.State.Stars[1].Name = "Q";
        g.State.Stars[2].Name = "AA";
        g.RunCommands("tf map", "tf list");
        Assert.Equal("A", MapCell(g.IO.OutputLines, 0, 0));
        Assert.Equal("Q", MapCell(g.IO.OutputLines, 3, 0));
        Assert.Equal("AA", MapCell(g.IO.OutputLines, 0, 3));
        Assert.Equal(".", MapCell(g.IO.OutputLines, 3, 3));
        Assert.DoesNotContain(g.IO.OutputLines, line => line.StartsWith("TF "));
        Assert.Contains(g.IO.OutputLines, line => line.Contains("zero-based coordinates; 1=player team, 2=enemy team"));
        Assert.DoesNotContain(g.IO.OutputLines, line => line.StartsWith(" 4 "));

        var player = g.State.FleetsSys.Place(1, 26, 0, scouts: 0);
        var enemy = g.State.FleetsSys.Place(0, 1, 2);
        int outputStart = g.IO.OutputLines.Count;
        g.RunCommands("tf map", "tf list");
        var output = g.IO.OutputLines.Skip(outputStart).ToArray();
        Assert.Equal("1", MapCell(output, 0, 0));
        Assert.Equal("2", MapCell(output, 0, 3));
        Assert.Contains(output, line => line.StartsWith("TF 26:") && line.Contains("stationary"));
        Assert.Same(player, g.State.Board[0, 0].TaskForces[1]);
        Assert.Same(enemy, g.State.Board[0, 3].TaskForces[0]);
        Assert.Equal(0, player.Destination);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    public void CoordinatesAndShipsAlone_DoNotInventPlacement(int x, int y) {
        var g = SmallGame();
        var fleet = g.State.Players[1].Tf[1];
        (fleet.X, fleet.Y) = (x, y);
        fleet.Scouts = 2;
        g.RunCommands("tf list", "tf map", "move 1 1");
        Assert.Contains(g.IO.OutputLines, line => line.Contains("TF 1 is not placed"));
        Assert.DoesNotContain(g.IO.OutputLines, line => line.StartsWith("TF 1:"));
        Assert.Null(fleet.Destination);
        Assert.Equal(0, fleet.ETA);
    }

    [Fact]
    public void PlacedEmptyFleet_StillRejectsMovement_AndHelpExplainsInterchangeableIds() {
        var g = SmallGame();
        var fleet = g.State.FleetsSys.Place(1, 1, 0, scouts: 0);
        g.RunCommands("move 1 1", "course 1 2", "help", "move A B", "course 1 B");
        Assert.Equal(4, g.IO.OutputLines.Count(line => line.Contains("TF 1 has 0 ships")));
        Assert.Contains(g.IO.OutputLines, line => line.Contains("a=1, z=26"));
        Assert.Contains(g.IO.OutputLines, line => line.Contains("A=0, Z=25"));
        Assert.Contains(g.IO.OutputLines, line => line.Contains("Letters are case-insensitive"));
        Assert.Equal(0, fleet.Destination);
    }

    [Theory]
    [InlineData("a", "a", "b", 1, 0, 1)]
    [InlineData("A", "A", "B", 1, 0, 1)]
    [InlineData("z", "z", "a", 26, 25, 0)]
    [InlineData("Z", "Z", "A", 26, 25, 0)]
    [InlineData("1", "0", "25", 1, 0, 25)]
    [InlineData("26", "25", "0", 26, 25, 0)]
    [InlineData("a", "0", "Z", 1, 0, 25)]
    public void Identifiers_WorkForPlacementMovementAndInspection(
        string fleetId, string startId, string targetId, int slot, int start, int target) {
        foreach (string command in new[] { "move", "course" }) {
            var g = TestGame.New();
            g.Game.GenerateStars();
            g.RunCommands($"place {startId} {fleetId} 4", $"{command} {fleetId} {targetId}", "tf list", "tf map");
            var fleet = g.State.Players[1].Tf[slot];
            Assert.Equal((g.State.Stars[start].X, g.State.Stars[start].Y), (fleet.X, fleet.Y));
            Assert.Equal(4, fleet.Scouts);
            Assert.Equal(target, fleet.Destination);
            Assert.Contains(g.IO.OutputLines, line => line.StartsWith($"TF {slot}:") && line.Contains($"travelling -> star {target}"));
            Assert.Contains(g.IO.OutputLines, line => line.Contains("Letters and numbers are interchangeable"));
        }
    }

    [Theory]
    [InlineData("0")]
    [InlineData("27")]
    [InlineData("-1")]
    [InlineData("2147483648")]
    [InlineData("aa")]
    [InlineData("a1")]
    [InlineData("1.0")]
    [InlineData("@")]
    [InlineData("[")]
    [InlineData("é")]
    public void InvalidFleetIdentifiers_AreRejectedWithoutMutation(string identifier) {
        var g = SmallGame();
        g.RunCommands($"place A {identifier}", $"move {identifier} B", $"course {identifier} B", "help");
        Assert.Equal(3, g.IO.OutputLines.Count(line => line.Contains("Invalid task-force identifier")));
        Assert.All(g.State.Players[1].Tf.Skip(1), fleet => {
            Assert.Equal(0, fleet.Scouts);
            Assert.Null(fleet.Destination);
        });
        Assert.Contains(g.IO.OutputLines, line => line.Contains("Commands:"));
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("26")]
    [InlineData("2147483648")]
    [InlineData("AA")]
    [InlineData("A1")]
    [InlineData("1.0")]
    [InlineData("@")]
    [InlineData("[")]
    [InlineData("é")]
    [InlineData("D")]
    [InlineData("3")]
    public void InvalidOrUnavailableStars_AreRejectedWithoutMutation(string identifier) {
        var g = SmallGame();
        var fleet = g.State.FleetsSys.Place(1, 1, 0, scouts: 4);
        g.RunCommands($"place {identifier} a", $"move a {identifier}", $"course a {identifier}", "help");
        Assert.Equal(3, g.IO.OutputLines.Count(line => line.Contains("Invalid star identifier") || line.Contains("not available in the current galaxy")));
        Assert.Equal((0, 0, 4), (fleet.X, fleet.Y, fleet.Scouts));
        Assert.Equal(0, fleet.Destination);
        Assert.Equal(0, fleet.ETA);
        Assert.Contains(g.IO.OutputLines, line => line.Contains("Commands:"));
    }
}
