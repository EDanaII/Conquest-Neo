using Conquest.CLI;
using Conquest.Core;
using Conquest.Core.Model;

namespace Conquest.Tests;

public sealed class ClassicInteractionTests {
    private sealed class Rng(int value = 1) : IRng {
        public int Calls { get; private set; }
        public int Next(int minInclusive, int maxExclusive) { Calls++; return Math.Clamp(value, minInclusive, maxExclusive - 1); }
        public void Reseed(int seed) => throw new NotSupportedException();
    }
    private sealed class Terminal(string keys) : IClassicTerminal {
        private readonly Queue<char> _keys = new(keys);
        public List<string[]> Screens { get; } = [];
        public List<(int Column, int Row)> Cursors { get; } = [];
        public int Clears { get; private set; }
        public int ReadKey() => _keys.TryDequeue(out char key) ? key : -1;
        public void Present(string[] screen) => Screens.Add(screen);
        public void MoveCursor(int column, int row) => Cursors.Add((column, row));
        public void Clear() => Clears++;
        public string[] Last => Screens[^1];
    }
    private static Game Scenario(IRng? rng = null) {
        var state = new GameState(new GameConfig { NumStars = 3, SafeExplore = true });
        var positions = new[] { (0, 0), (3, 0), (0, 4) };
        for (int i = 0; i < positions.Length; i++) {
            var s = state.Stars[i];
            (s.X, s.Y) = positions[i]; s.ExploredBy[1] = true;
            state.Board[s.X, s.Y].StarRef = s;
        }
        state.Players[1].Velocity = 1; state.Players[1].Range = 5; state.Players[1].Weapons = 3;
        state.FleetsSys.Place(1, 1, 0, scouts: 2);
        return new Game(rng ?? new Rng(), state);
    }
    private static Terminal Run(Game game, string keys) {
        var terminal = new Terminal(keys);
        Assert.Equal(0, new ClassicGameLoop(game, terminal).Run());
        return terminal;
    }
    private static bool InputHas(string[] screen, string text) => screen.Skip(19).Take(5).Any(row => row[..31].TrimEnd() == text);

    [Fact]
    public void DestinationAndReplacementUseOriginalPromptsWithoutAdvancingTime() {
        var rng = new Rng(); var game = Scenario(rng);
        var terminal = Run(game, "dabdac");
        var fleet = game.State.Players[1].Tf[1];
        Assert.Equal(2, fleet.Destination); Assert.Equal(4, fleet.ETA);
        Assert.Equal(0, game.Turn); Assert.Equal(0, rng.Calls);
        Assert.Contains(terminal.Screens, s => InputHas(s, "       distance:   3.0") && InputHas(s, "eta in  3 turns."));
        Assert.True(InputHas(terminal.Last, "(Cancelling previous orders)"));
        Assert.True(InputHas(terminal.Last, "        to star: C"));
        Assert.True(InputHas(terminal.Last, "       distance:   4.0"));
        Assert.True(InputHas(terminal.Last, "eta in  4 turns."));
        Assert.Contains((17, 19), terminal.Cursors);
        Assert.Contains((17, 20), terminal.Cursors);
    }

    [Theory]
    [InlineData("daz", "  !illegal star")]
    [InlineData("da\n", "  !illegal star")]
    [InlineData("daa", "Tf remains at star.")]
    public void InvalidOrCurrentStarReplacementCancelsBeforeReadingDestination(string replacement, string expected) {
        var game = Scenario();
        var terminal = Run(game, "dab" + replacement);
        var fleet = game.State.Players[1].Tf[1];
        Assert.Equal(0, fleet.Destination); Assert.Equal(0, fleet.ETA);
        Assert.Equal(3, fleet.EtaOriginal); Assert.Equal(0, game.Turn);
        Assert.True(terminal.Last[24].TrimEnd() == expected || InputHas(terminal.Last, expected));
    }

    [Fact]
    public void FailedRangeReplacementLeavesStationaryAndReportsDistanceAndOriginalError() {
        var game = Scenario();
        game.State.Players[1].Range = 3;
        game.State.Players[1].Tf[1].Transports = 1;
        var terminal = Run(game, "dabdac");
        Assert.Equal(0, game.State.Players[1].Tf[1].ETA);
        Assert.Equal(0, game.State.Players[1].Tf[1].Destination);
        Assert.True(InputHas(terminal.Last, "       distance:   4.0"));
        Assert.Equal("  !maximum range is  3", terminal.Last[24].TrimEnd());
    }

    [Theory]
    [InlineData("d1", " !illegal tf")]
    [InlineData("dz", " !nonexistent tf")]
    [InlineData("dabgda", " !Tf is not in normal space")]
    public void InvalidFleetAndInFlightOrdersReturnToCommandPrompt(string keys, string expected) {
        var game = Scenario(); var terminal = Run(game, keys);
        Assert.Equal(expected, terminal.Last[24].TrimEnd());
        Assert.Equal('?', terminal.Last[18][0]);
        Assert.Equal(keys.Contains('g') ? 1 : 0, game.Turn);
    }

    [Fact]
    public void BlastingFleetIsRejectedBeforeDestinationPrompt() {
        var game = Scenario(); game.State.Players[1].Tf[1].Blasting = 1;
        var terminal = Run(game, "da");
        Assert.Equal(" !Tf is blasting a planet", terminal.Last[24].TrimEnd());
        Assert.DoesNotContain(terminal.Screens, s => InputHas(s, "        to star: "));
    }

    [Fact]
    public void WithdrawnPendingFleetCannotChangeCourseAndRejectedOrderDoesNotConsumeStarKey() {
        var game = Scenario(); game.State.FleetsSys.SetCourse(1, 1, 1);
        game.State.Players[1].Tf[1].Withdrew = 1;
        var terminal = Run(game, "dah");
        Assert.Contains(terminal.Screens, s => s[24].StartsWith(" !Tf is not in normal space"));
        Assert.Equal(" B - Blast Planet", terminal.Last[0][49..].TrimEnd());
        Assert.Equal(1, game.State.Players[1].Tf[1].Destination);
        Assert.Equal(3, game.State.Players[1].Tf[1].ETA);
        Assert.Equal(1, game.State.Players[1].Tf[1].Withdrew);
        Assert.Equal(0, game.Turn);
    }

    [Fact]
    public void EndOfInputAfterReplacementPromptLeavesPreparedOrderCancelled() {
        var game = Scenario(); Run(game, "dabda");
        Assert.Equal(0, game.State.Players[1].Tf[1].Destination);
        Assert.Equal(0, game.State.Players[1].Tf[1].ETA);
        Assert.Equal(0, game.Turn);
    }

    [Fact]
    public void StarSelectionUsesSecondRowTokenPrefixesAndBackspace() {
        var game = Scenario();
        var terminal = Run(game, "s\b2z\ba 3b\r\n");
        Assert.Contains(terminal.Screens, s => s[18].StartsWith(" Star summary:") && s[19].StartsWith(":2A 3B"));
        Assert.Equal("--------- Star A ---------", terminal.Last[0][49..].TrimEnd());
        Assert.Contains(terminal.Last, row => row[49..].StartsWith("--------- Star B"));
        Assert.DoesNotContain(terminal.Last, row => row[49..].StartsWith("--------- Star C"));
        Assert.Equal("", terminal.Last[24].TrimEnd()); // CRLF is one Enter, not a command.
        Assert.Contains((6, 19), terminal.Cursors);
    }

    [Fact]
    public void FleetSummaryUsesOriginalOneRowAndTravelSuffix() {
        var game = Scenario(); var terminal = Run(game, "dabt2a\n");
        Assert.Equal("TFa: ( 1, 1)    2s       B3", terminal.Last[0][49..].TrimEnd());
        Assert.All(terminal.Last.Skip(1).Take(16), row => Assert.Equal("", row[49..].TrimEnd()));
        Assert.Equal(0, game.Turn);
    }

    [Fact]
    public void TwentyFiveCharactersFinishLineWithoutEnterAndNextKeyIsCommand() {
        var game = Scenario(); var terminal = Run(game, "t" + new string('Z', 25) + "h");
        Assert.Equal(" B - Blast Planet", terminal.Last[0][49..].TrimEnd());
        Assert.Equal(" T - TaskForce summary", terminal.Last[12][49..].TrimEnd());
        Assert.Contains(terminal.Screens, s => s[18].StartsWith(" Tf summary :" + new string('Z', 25)));
        Assert.Contains((38, 18), terminal.Cursors);
        Assert.Equal(0, game.Turn);
    }

    [Fact]
    public void RedrawClearsReportsAndForcesConsoleRefreshWithoutAdvancing() {
        var game = Scenario(); var terminal = Run(game, "hm");
        Assert.Equal(1, terminal.Clears);
        Assert.All(terminal.Last.Take(17), row => Assert.Equal("", row[49..].TrimEnd()));
        Assert.Equal(0, game.Turn);
    }

    [Theory]
    [InlineData("qY", 0)]
    [InlineData("qn", 1)]
    [InlineData("q", 0)]
    public void QuitHasClearScreenVerificationAndDecliningEndsOrderPhase(string keys, int turns) {
        var game = Scenario(); game.State.FleetsSys.SetCourse(1, 1, 1);
        var terminal = Run(game, keys);
        Assert.Equal(turns, game.Turn);
        Assert.Equal(3 - turns, game.State.Players[1].Tf[1].ETA);
        Assert.Contains(terminal.Screens, s => s[0].TrimEnd() == "Quit game....[verify]" && s.Skip(1).All(row => string.IsNullOrWhiteSpace(row)));
        Assert.Contains((0, 1), terminal.Cursors);
    }

    [Fact]
    public void ResearchRequirementsReuseCoreTablesAndCapFieldsAtMaximum() {
        var game = Scenario(); var terminal = Run(game, "rvw\n");
        Assert.Equal("V: 1 res:   0 need:  40", terminal.Last[18][54..].TrimEnd());
        Assert.Equal("R: 5 res:   0 need:  20", terminal.Last[19][54..].TrimEnd());
        Assert.Equal("W: 3 res:   0 need:  50", terminal.Last[20][54..].TrimEnd());
        Assert.Equal(0, game.Turn);
        var player = game.State.Players[1];
        player.Velocity = 12; player.Range = 15; player.Weapons = 10;
        terminal = Run(game, "r\n");
        Assert.Equal("V:12", terminal.Last[18][54..].TrimEnd());
        Assert.Equal("R:15", terminal.Last[19][54..].TrimEnd());
        Assert.Equal("W:10", terminal.Last[20][54..].TrimEnd());
    }

    [Fact]
    public void UnknownArrivalPausesReportsExplorationAndRefreshesMapAndTurn() {
        var game = Scenario(); game.State.Stars[1].ExploredBy[1] = false;
        var terminal = Run(game, "dabggg.t\n");
        Assert.Equal(3, game.Turn);
        Assert.Contains(terminal.Screens, s => s[18].StartsWith("Task force a exploring B.") && s[24].StartsWith("Press any key to continue") && s[0][49..].StartsWith("--------- Star B"));
        Assert.Equal("TFa:B( 4, 1)    2s", terminal.Last[0][49..].TrimEnd());
        Assert.Equal(" Ba", terminal.Last[14].Substring(12, 3));
        Assert.Equal("Turn:                4", terminal.Last[18].Substring(31, 22));
        Assert.Equal("P Arrive:            B", terminal.Last[20].Substring(31, 22).TrimEnd());
    }

    [Fact]
    public void DestroyedExplorationReportsLossAndRestoresMapWithoutArrivalOrReveal() {
        var rng = new Rng(100); var game = Scenario(rng);
        game.State.Config.SafeExplore = false;
        game.State.Players[1].Tf[1].Scouts = 1;
        game.State.Stars[1].ExploredBy[1] = false;
        var terminal = Run(game, "dabggg.");
        Assert.Equal(2, rng.Calls);
        Assert.Null(game.State.Players[1].Tf[1].Destination);
        Assert.False(game.State.Stars[1].ExploredBy[1]);
        Assert.Contains(terminal.Screens, s => s[19].StartsWith("  1s destroyed."));
        Assert.Equal("?B ", terminal.Last[14].Substring(12, 3));
        Assert.Equal("", terminal.Last[20].Substring(31, 22).TrimEnd());
    }

    [Fact]
    public void ColonyReportPaginatesWithoutLosingRows() {
        var game = Scenario(); var star = game.State.Stars[0];
        for (int i = 0; i < 18; i++) star.Planets.Add(new Planet(star) { Number = 1, Owner = Ownership.Player, PlayerSeenCapacity = 20, Inhabitants = i, IndustrialUnits = 100 });
        var terminal = Run(game, "c.");
        var page = Assert.Single(terminal.Screens.Where(s => s[24].StartsWith("Press any key to continue")));
        Assert.Equal("Colonies:", page[0][49..].TrimEnd());
        Assert.Equal("A1:20(15,/100)", page[16][49..].TrimEnd());
        Assert.Equal("A1:20(16,/100)", terminal.Last[0][49..].TrimEnd());
        Assert.Equal("A1:20(17,/100)", terminal.Last[1][49..].TrimEnd());
    }

    [Fact]
    public void StarPaginationKeepsNormalStarBlocksTogetherAndConsumesAcknowledgement() {
        var game = Scenario();
        for (int i = 0; i < 3; i++) {
            var star = game.State.Stars[i];
            for (int p = 1; p <= 5; p++) star.Planets.Add(new Planet(star) { Number = p, PlayerSeenCapacity = 20 });
        }
        var terminal = Run(game, "s\ng");
        var page = Assert.Single(terminal.Screens.Where(s => s[24].StartsWith("Press any key to continue")));
        Assert.Equal("--------- Star A ---------", page[0][49..].TrimEnd());
        Assert.Contains(page, row => row[49..].StartsWith("--------- Star B"));
        Assert.DoesNotContain(page, row => row[49..].StartsWith("--------- Star C"));
        Assert.Equal("--------- Star C ---------", terminal.Last[0][49..].TrimEnd());
        Assert.Equal(0, game.Turn); // G acknowledges the page; it is not a movement order.
    }

    [Theory]
    [InlineData("g ")]
    [InlineData(" g")]
    public void GoAndSpaceAdvanceOnePhaseEachAndKnownArrivalsDoNotPause(string commands) {
        var game = Scenario(); game.State.Players[1].Velocity = 2;
        var terminal = Run(game, "dab" + commands + "ta\n");
        Assert.Equal(2, game.Turn);
        Assert.Equal(0, game.State.Players[1].Tf[1].ETA);
        Assert.Equal("TFa:B( 4, 1)    2s", terminal.Last[0][49..].TrimEnd());
        Assert.DoesNotContain(terminal.Screens, s => s[24].StartsWith("Press any key to continue"));
    }

    [Theory]
    [InlineData("\n", "  !illegal command")]
    [InlineData("\u001b", "  !illegal command")]
    [InlineData("e", "  !illegal command")]
    [InlineData("?", "")]
    public void UnsupportedAndInvalidKeysPreserveStateAndOriginalCommandMeanings(string keys, string expected) {
        var game = Scenario(); var terminal = Run(game, keys);
        Assert.Equal(expected, terminal.Last[24].TrimEnd());
        Assert.Equal(0, game.Turn); Assert.Equal(0, game.State.Players[1].Tf[1].Destination);
    }
}
