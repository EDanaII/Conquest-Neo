using Conquest.CLI;
using Conquest.Core;
using Conquest.Core.Model;

namespace Conquest.Tests;

public sealed class ClassicFleetCommandTests {
    private sealed class NoDrawRng : IRng {
        public int Next(int minInclusive, int maxExclusive) => throw new InvalidOperationException("Unexpected RNG draw");
        public void Reseed(int seed) => throw new InvalidOperationException("Unexpected reseed");
    }
    private sealed class Terminal(string input) : IClassicTerminal {
        private readonly Queue<char> _keys = new(input);
        public List<string[]> Screens { get; } = [];
        public List<(int, int)> Cursors { get; } = [];
        public int ReadKey() => _keys.TryDequeue(out char key) ? key : -1;
        public void Present(string[] screen) => Screens.Add(screen);
        public void MoveCursor(int column, int row) => Cursors.Add((column, row));
        public string[] Last => Screens[^1];
    }
    private static Game Scenario() {
        var state = new GameState(new GameConfig { NumStars = 3, SafeExplore = true });
        var positions = new[] { (0, 0), (0, 4), (4, 0) };
        for (int i = 0; i < positions.Length; i++) {
            var star = state.Stars[i]; (star.X, star.Y) = positions[i]; star.ExploredBy[1] = true;
            state.Board[star.X, star.Y].StarRef = star;
        }
        state.Players[1].Velocity = 1; state.Players[1].Range = 5; state.Players[1].Weapons = 3;
        var source = state.FleetsSys.Place(1, 1, 0, scouts: 6);
        source.Transports = 8; source.Cruisers = 3; source.BattleStars = 2;
        return new Game(new NoDrawRng(), state);
    }
    private static Terminal Run(Game game, string input) {
        var terminal = new Terminal(input);
        Assert.Equal(0, new ClassicGameLoop(game, terminal).Run());
        Assert.Equal(0, game.Turn);
        return terminal;
    }
    private static (int, int, int, int) Ships(TaskForce f) => (f.Transports, f.Scouts, f.Cruisers, f.BattleStars);

    [Fact]
    public void SplitUsesSingleFleetKeyThenShipLineAndRefreshesSharedMapAndStarReport() {
        var game = Scenario(); var terminal = Run(game, "na2s3tcb\n");
        Assert.Contains(terminal.Screens, s => s[18].StartsWith(" New tf- from tf: "));
        Assert.Contains(terminal.Screens, s => s[18].StartsWith(" New tf- from tf: A") && s[19].StartsWith("           ships: 2S3TCB"));
        Assert.Contains((18, 19), terminal.Cursors);
        Assert.Contains((24, 19), terminal.Cursors);
        Assert.Equal((5, 4, 2, 1), Ships(game.State.Players[1].Tf[1]));
        Assert.Equal((3, 2, 1, 1), Ships(game.State.Players[1].Tf[2]));
        Assert.Equal(" A*", terminal.Last[14].Substring(3, 3));
        Assert.Equal("--------- Star A ---------", terminal.Last[0][49..].TrimEnd());
        Assert.Equal("TFa   5t 4s 2c 1b", terminal.Last[1][49..].TrimEnd());
        Assert.Equal("TFb   3t 2s 1c 1b", terminal.Last[2][49..].TrimEnd());
        Assert.Equal("", terminal.Last[19][..31].TrimEnd());
    }

    [Fact]
    public void JoinReadsWholeTokenLineAndUpdatesBothReportsAndFleetGlyph() {
        var game = Scenario();
        var terminal = Run(game, "na2s3tcb\nt\nj2a3b\nt\n");
        Assert.Contains(terminal.Screens, s => s[18].StartsWith(" Join tfs:2A3B"));
        Assert.Equal((8, 6, 3, 2), Ships(game.State.Players[1].Tf[1]));
        Assert.Null(game.State.Players[1].Tf[2].Destination);
        Assert.Contains(terminal.Screens, s => s[0][49..].StartsWith("--------- Star A") && s[1][49..].StartsWith("TFa   8t 6s 3c 2b"));
        Assert.Contains(terminal.Screens, s => s[0][49..].StartsWith("TFa:A( 1, 1) 5t 4s 2c 1b") && s[1][49..].StartsWith("TFb:A( 1, 1) 3t 2s 1c 1b"));
        Assert.Equal("TFa:A( 1, 1) 8t 6s 3c 2b", terminal.Last[0][49..].TrimEnd());
        Assert.Equal("", terminal.Last[1][49..].TrimEnd());
        Assert.Equal(" Aa", terminal.Last[14].Substring(3, 3));
    }

    [Theory]
    [InlineData("")]
    [InlineData("2")]
    [InlineData("2 S")]
    public void BlankOrFirstBlankTokenMovesWholeFleetRatherThanCancelling(string ships) {
        var game = Scenario(); var terminal = Run(game, "na" + ships + "\n");
        Assert.Null(game.State.Players[1].Tf[1].Destination);
        Assert.Equal((8, 6, 3, 2), Ships(game.State.Players[1].Tf[2]));
        Assert.Equal(" Ab", terminal.Last[14].Substring(3, 3));
        Assert.Equal("TFb   8t 6s 3c 2b", terminal.Last[1][49..].TrimEnd());
    }

    [Theory]
    [InlineData("0S", "")]
    [InlineData("X", "  ! Illegal field X")]
    [InlineData("999999999999S", "  !invalid ship quantity")]
    public void ZeroInvalidAndOverflowQuantitiesLeaveNoEmptyActiveChild(string input, string error) {
        var game = Scenario(); var terminal = Run(game, "na" + input + "\n");
        Assert.Equal(error, terminal.Last[24].TrimEnd());
        Assert.Equal((8, 6, 3, 2), Ships(game.State.Players[1].Tf[1]));
        Assert.Null(game.State.Players[1].Tf[2].Destination);
        Assert.Equal(" Aa", terminal.Last[14].Substring(3, 3));
    }

    [Fact]
    public void SplitErrorsDoNotDiscardValidTokensAndBackspaceEditsShipCounts() {
        var game = Scenario(); var terminal = Run(game, "na-2S9\b3TX\r\n");
        Assert.Equal("  ! Illegal field X", terminal.Last[24].TrimEnd());
        Assert.Equal((3, 2, 0, 0), Ships(game.State.Players[1].Tf[2]));
        Assert.Equal(" A*", terminal.Last[14].Substring(3, 3));
    }

    [Theory]
    [InlineData("n1", "  !illegal tf")]
    [InlineData("nz", "  !illegal tf")]
    [InlineData("n\n", "  !illegal tf")]
    [InlineData("n\u001b", "  !illegal tf")]
    public void InvalidNewFleetSelectionReturnsWithoutShipPrompt(string input, string error) {
        var game = Scenario(); var terminal = Run(game, input);
        Assert.Equal(error, terminal.Last[24].TrimEnd());
        Assert.DoesNotContain(terminal.Screens, s => s[19].Contains("ships:"));
        Assert.Null(game.State.Players[1].Tf[2].Destination);
    }

    [Theory]
    [InlineData("n")]
    [InlineData("na")]
    [InlineData("na2S")]
    [InlineData("j")]
    [InlineData("ja")]
    public void EofAtNewAndJoinPromptsExitsWithoutReservingOrChangingFleets(string input) {
        var game = Scenario(); Run(game, input);
        Assert.Equal((8, 6, 3, 2), Ships(game.State.Players[1].Tf[1]));
        Assert.Equal(0, game.State.Players[1].Tf[1].Destination);
        Assert.Null(game.State.Players[1].Tf[2].Destination);
    }

    [Fact]
    public void SlotLimitReportsBeforeReadingShipsAndNextKeyIsACommand() {
        var game = Scenario();
        for (int i = 2; i <= 26; i++) game.State.FleetsSys.Place(1, i, 0);
        var terminal = Run(game, "nah");
        Assert.Contains(terminal.Screens, s => s[24].StartsWith("! Taskforce limit reached!") && s[0][49..].StartsWith("--------- Star A"));
        Assert.DoesNotContain(terminal.Screens, s => s[19].Contains("ships:"));
        Assert.Equal(" B - Blast Planet", terminal.Last[0][49..].TrimEnd());
        Assert.Equal("", terminal.Last[24].TrimEnd());
    }

    [Theory]
    [InlineData("", "  ! illegal tf")]
    [InlineData("Z", "  !nonexistent tf")]
    [InlineData("A A", "!Duplicate tf A")]
    [InlineData("A Z", "!Nonexistant TFZ")]
    [InlineData("A ?", "  !illegal tf ?")]
    public void InvalidJoinInputUsesOriginalErrorsWithoutRemovingSource(string input, string error) {
        var game = Scenario(); var terminal = Run(game, "j" + input + "\n");
        Assert.Equal(error, terminal.Last[24].TrimEnd());
        Assert.Equal((8, 6, 3, 2), Ships(game.State.Players[1].Tf[1]));
        Assert.Equal(0, game.State.Players[1].Tf[1].Destination);
    }

    [Fact]
    public void JoinContinuesAfterBadChildAndDuplicateNeverAddsShipsTwice() {
        var game = Scenario(); game.State.FleetsSys.Place(1, 2, 0, scouts: 2);
        game.State.FleetsSys.Place(1, 3, 0, scouts: 3);
        var terminal = Run(game, "ja?b b c\n");
        Assert.Equal((8, 11, 3, 2), Ships(game.State.Players[1].Tf[1]));
        Assert.Null(game.State.Players[1].Tf[2].Destination); Assert.Null(game.State.Players[1].Tf[3].Destination);
        Assert.Equal("!Nonexistant TFB", terminal.Last[24].TrimEnd());
        Assert.Equal("TFa   8t11s 3c 2b", terminal.Last[1][49..].TrimEnd());
    }

    [Fact]
    public void SameXDifferentYJoinPreservesClassicDefectAndRestoresRemoteStar() {
        var game = Scenario(); game.State.FleetsSys.Place(1, 2, 1, scouts: 2);
        var terminal = Run(game, "jab\n");
        Assert.Null(game.State.Players[1].Tf[2].Destination);
        Assert.Equal(" Aa", terminal.Last[14].Substring(3, 3));
        Assert.Equal(" B ", terminal.Last[10].Substring(3, 3));
        Assert.Equal("TFa   8t 8s 3c 2b", terminal.Last[1][49..].TrimEnd());
    }

    [Fact]
    public void DifferentXChildRemainsVisibleAndJoinReportsBadLocation() {
        var game = Scenario(); game.State.FleetsSys.Place(1, 2, 2, scouts: 2);
        var terminal = Run(game, "jab\n");
        Assert.Equal("  !tfB bad location", terminal.Last[24].TrimEnd());
        Assert.Equal(" Cb", terminal.Last[14].Substring(15, 3));
        Assert.Equal(2, game.State.Players[1].Tf[2].Destination);
    }

    [Fact]
    public void PendingOrderRejectsNewAndJoinWithoutReplacingItOrRequestingShips() {
        var game = Scenario(); game.State.FleetsSys.SetCourse(1, 1, 2);
        var terminal = Run(game, "najab\n");
        Assert.Contains(terminal.Screens, s => s[24].StartsWith("  !illegal tf"));
        Assert.DoesNotContain(terminal.Screens, s => s[19].Contains("ships:"));
        Assert.Equal("  !tfA is not in normal space", terminal.Last[24].TrimEnd());
        Assert.Equal(2, game.State.Players[1].Tf[1].Destination);
        Assert.Equal(4, game.State.Players[1].Tf[1].ETA);
    }

    [Fact]
    public void SplitScoutCanBeOrderedAndMovedUsingExistingMovementAndArrivals() {
        var game = Scenario(); var terminal = new Terminal("na2s\ndbcggggt\n");
        new ClassicGameLoop(game, terminal).Run();
        Assert.Equal(4, game.Turn);
        Assert.Equal(2, game.State.Players[1].Tf[2].Destination);
        Assert.Equal(0, game.State.Players[1].Tf[2].ETA);
        Assert.Equal(4, game.State.Players[1].Tf[1].Scouts);
        Assert.Contains(terminal.Screens, s => s[0][49..].StartsWith("TFa:A") && s[1][49..].StartsWith("TFb:C"));
        Assert.Equal(" Cb", terminal.Last[14].Substring(15, 3));
    }
}
