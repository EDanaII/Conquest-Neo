using Conquest.CLI;
using Conquest.Core;
using Conquest.Core.Model;

namespace Conquest.Tests;

public sealed class ClassicLandingTests {
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
    private static Game Scenario(bool multiple = false) {
        var state = new GameState(new GameConfig { NumStars = 2, SafeExplore = true });
        state.Stars[1].X = 4;
        foreach (var star in state.Stars) { star.ExploredBy[1] = true; state.Board[star.X, star.Y].StarRef = star; }
        var home = state.Stars[0];
        home.Planets.Add(new Planet(home) { Number = 3, Capacity = 20, PlayerSeenCapacity = 20 });
        if (multiple) home.Planets.Add(new Planet(home) { Number = 6, Capacity = 30, PlayerSeenCapacity = 30 });
        var fleet = state.FleetsSys.Place(1, 1, 0, scouts: 2); fleet.Transports = 8; fleet.Cruisers = 1;
        state.Players[1].Range = 5; state.Players[1].Velocity = 1; state.Players[1].Weapons = 3;
        return new Game(new NoDrawRng(), state);
    }
    private static Terminal Run(Game game, string input) {
        var terminal = new Terminal(input);
        Assert.Equal(0, new ClassicGameLoop(game, terminal).Run());
        Assert.Equal(0, game.Turn);
        return terminal;
    }
    private static bool ShowsQuantity(string[] screen) => screen[20].StartsWith("     transports: ");

    [Fact]
    public void SinglePlanetAutoSelectionUsesOriginalPromptRowsAndImmediatelyUpdatesMapAndStarSummary() {
        var game = Scenario(); var terminal = Run(game, "la3\n");
        Assert.Contains(terminal.Screens, s => s[18].StartsWith(" Land Taskforce: "));
        Assert.Contains(terminal.Screens, s => s[18].StartsWith(" Land Taskforce:A") && s[19].StartsWith("         planet: 3") && ShowsQuantity(s));
        Assert.Contains((17, 20), terminal.Cursors);
        Assert.Equal("         planet: 3", terminal.Last[19][..31].TrimEnd());
        Assert.Equal("     transports: 3", terminal.Last[20][..31].TrimEnd());
        Assert.Equal("@Aa", terminal.Last[14].Substring(3, 3));
        Assert.Equal("--------- Star A ---------", terminal.Last[0][49..].TrimEnd());
        Assert.Equal("TFa   5t 2s 1c", terminal.Last[1][49..].TrimEnd());
        Assert.Equal(" 3:20( 3,/  3)", terminal.Last[2][49..].TrimEnd());
        Assert.Equal(Ownership.Player, game.State.Stars[0].Planets[0].Owner);
    }

    [Fact]
    public void MultiplePlanetsConsumeOnePlanetKeyThenAQuantityLine() {
        var game = Scenario(multiple: true); var terminal = Run(game, "la62\n");
        Assert.Contains((17, 19), terminal.Cursors);
        Assert.Equal(Ownership.None, game.State.Stars[0].Planets[0].Owner);
        Assert.Equal(Ownership.Player, game.State.Stars[0].Planets[1].Owner);
        Assert.Equal(6, game.State.Players[1].Tf[1].Transports);
        Assert.Equal(" 3:20 No colony", terminal.Last[2][49..].TrimEnd());
        Assert.Equal(" 6:30( 2,/  2)", terminal.Last[3][49..].TrimEnd());
    }

    [Fact]
    public void LandingUpdatesFleetListingAndOwnedColonyReportWithoutProduction() {
        var game = Scenario(); var planet = game.State.Stars[0].Planets[0];
        planet.Owner = Ownership.Player; planet.Inhabitants = 4; planet.IndustrialUnits = 11;
        var terminal = Run(game, "la2\nt\nc");
        Assert.Contains(terminal.Screens, s => s[0][49..].TrimEnd() == "TFa:A( 1, 1) 6t 2s 1c");
        Assert.Equal("Colonies:", terminal.Last[0][49..].TrimEnd());
        Assert.Equal("A3:20( 6,/ 13)", terminal.Last[1][49..].TrimEnd());
        Assert.Equal((6, 13), (planet.Inhabitants, planet.IndustrialUnits));
    }

    [Fact]
    public void LandingLastTransportRemovesFleetAndKeepsColonyVisibleInReportsAndMap() {
        var game = Scenario(); var fleet = game.State.Players[1].Tf[1]; fleet.Scouts = fleet.Cruisers = 0;
        var terminal = Run(game, "la\nc");
        Assert.Null(fleet.Destination); Assert.Equal(0, fleet.Transports);
        Assert.Equal("@A ", terminal.Last[14].Substring(3, 3));
        Assert.Contains(terminal.Screens, s => s[0][49..].StartsWith("--------- Star A") && s[1][49..].TrimEnd() == " 3:20( 8,/  8)");
        Assert.Equal("A3:20( 8,/  8)", terminal.Last[1][49..].TrimEnd());
        terminal = Run(game, "t\n");
        Assert.All(terminal.Last.Take(17), row => Assert.Equal("", row[49..].TrimEnd()));
        Assert.Equal("@A ", terminal.Last[14].Substring(3, 3));
    }

    [Fact]
    public void EmptyLandingFleetDoesNotEraseAnotherFleetAtTheColony() {
        var game = Scenario(); var fleet = game.State.Players[1].Tf[1]; fleet.Scouts = fleet.Cruisers = 0;
        game.State.FleetsSys.Place(1, 2, 0, scouts: 1);
        var terminal = Run(game, "la0\n");
        Assert.Null(fleet.Destination);
        Assert.Equal("@Ab", terminal.Last[14].Substring(3, 3));
        Assert.Equal("TFb      1s", terminal.Last[1][49..].TrimEnd());
    }

    [Theory]
    [InlineData("", 8)]
    [InlineData("0", 8)]
    [InlineData("0X", 8)]
    [InlineData("0 2", 8)]
    [InlineData("X", 1)]
    [InlineData("-3", 1)]
    [InlineData("?", 1)]
    [InlineData("  S", 1)]
    [InlineData("2T9S", 2)]
    [InlineData(" 2 99", 2)]
    [InlineData("3", 3)]
    [InlineData("0002", 2)]
    public void TransportLineUsesOriginalFirstTokenValueAndIgnoresItsSymbolAndLaterTokens(string line, int expected) {
        var game = Scenario(); Run(game, "la" + line + "\n");
        var planet = game.State.Stars[0].Planets[0];
        Assert.Equal((expected, expected), (planet.Inhabitants, planet.IndustrialUnits));
        Assert.Equal(8 - expected, game.State.Players[1].Tf[1].Transports);
    }

    [Fact]
    public void TransportLineSupportsBackspaceAndCRLFWithoutAddingCommands() {
        var game = Scenario(); var terminal = Run(game, "la9\b2\r\n");
        Assert.Equal(2, game.State.Stars[0].Planets[0].Inhabitants);
        Assert.Equal("", terminal.Last[24].TrimEnd());
        Assert.Equal("     transports: 2", terminal.Last[20][..31].TrimEnd());
    }

    [Fact]
    public void TwentyFiveCharacterLineCompletesWithoutEnterAndNextKeyRemainsACommand() {
        var game = Scenario(); var terminal = Run(game, "la" + new string('0', 25) + "h");
        Assert.Equal(8, game.State.Stars[0].Planets[0].Inhabitants);
        Assert.Equal(" B - Blast Planet", terminal.Last[0][49..].TrimEnd());
        Assert.Equal("", terminal.Last[24].TrimEnd());
    }

    [Theory]
    [InlineData("l1", "  !illegal tf")]
    [InlineData("lz", "  !nonexistent tf")]
    [InlineData("l\n", "  !illegal tf")]
    [InlineData("l\u001b", "  !illegal tf")]
    public void InvalidFleetSelectionReturnsWithoutPlanetOrQuantityPrompts(string input, string error) {
        var game = Scenario(); var terminal = Run(game, input);
        Assert.Equal(error, terminal.Last[24].TrimEnd());
        Assert.DoesNotContain(terminal.Screens, s => s[19].StartsWith("         planet:") || ShowsQuantity(s));
        Assert.Equal(Ownership.None, game.State.Stars[0].Planets[0].Owner);
        Assert.Equal(8, game.State.Players[1].Tf[1].Transports);
    }

    [Theory]
    [InlineData('9', 9)]
    [InlineData('X', 40)]
    [InlineData('\n', -38)]
    [InlineData('\u001b', -21)]
    public void InvalidPlanetSelectionEchoesOriginalCharacterMinusZeroAndReturnsWithoutQuantityPrompt(char key, int echoed) {
        var game = Scenario(multiple: true); var terminal = Run(game, "la" + key);
        Assert.Equal(" !Not a habitable planet", terminal.Last[24].TrimEnd());
        Assert.Equal("         planet: " + echoed, terminal.Last[19][..31].TrimEnd());
        Assert.DoesNotContain(terminal.Screens, ShowsQuantity);
        Assert.All(game.State.Stars[0].Planets, p => Assert.Equal(Ownership.None, p.Owner));
    }

    [Theory]
    [InlineData(false, "l")]
    [InlineData(false, "la")]
    [InlineData(false, "la2")]
    [InlineData(true, "la")]
    [InlineData(true, "la6")]
    [InlineData(true, "la62")]
    public void EndOfInputAbortsEveryLandingStageWithoutResourceOrOwnershipChanges(bool multiple, string input) {
        var game = Scenario(multiple); Run(game, input);
        Assert.Equal(8, game.State.Players[1].Tf[1].Transports);
        Assert.Equal(0, game.State.Players[1].Tf[1].Destination);
        Assert.All(game.State.Stars[0].Planets, p => {
            Assert.Equal(Ownership.None, p.Owner); Assert.Equal(0, p.Inhabitants); Assert.Equal(0, p.IndustrialUnits);
        });
    }

    [Fact]
    public void NoPlanetsAndEnemyFleetPathsRefreshStarThenRejectWithoutAnyCombatOrFurtherInput() {
        var game = Scenario(); game.State.Stars[0].Planets.Clear();
        game.State.FleetsSys.Place(0, 1, 0, scouts: 1);
        var terminal = Run(game, "la");
        Assert.Equal("  !no planets at this star", terminal.Last[24].TrimEnd());
        Assert.Contains(terminal.Last, row => row[49..].StartsWith("  no useable planets"));
        var star = game.State.Stars[0]; star.Planets.Add(new Planet(star) { Number = 3, Capacity = 20, PlayerSeenCapacity = 20 });
        terminal = Run(game, "la");
        Assert.Equal("  !enemy ships present", terminal.Last[24].TrimEnd());
        Assert.DoesNotContain(terminal.Screens, s => s[19].StartsWith("         planet:") || ShowsQuantity(s));
        Assert.Equal(8, game.State.Players[1].Tf[1].Transports);
        Assert.Equal(1, game.State.Players[0].Tf[1].Scouts);
    }

    [Theory]
    [InlineData(Ownership.Enemy, false)]
    [InlineData(Ownership.Player, true)]
    public void EnemyAndConqueredPlanetsAreRejectedAfterSelectionBeforeTransportInput(Ownership owner, bool conquered) {
        var game = Scenario(); var planet = game.State.Stars[0].Planets[0]; planet.Owner = owner; planet.Conquered = conquered;
        var terminal = Run(game, "la");
        Assert.Equal("  !Enemy infested planet  !!", terminal.Last[24].TrimEnd());
        Assert.Equal("         planet: 3", terminal.Last[19][..31].TrimEnd());
        Assert.DoesNotContain(terminal.Screens, ShowsQuantity);
        Assert.Equal(owner, planet.Owner); Assert.Equal(0, planet.Inhabitants); Assert.Equal(8, game.State.Players[1].Tf[1].Transports);
    }

    [Theory]
    [InlineData(20, 8, "9", "  !only  8 transports in tf")]
    [InlineData(5, 8, "", "  !only room for  5 transports")]
    [InlineData(0, 8, "1", "  !only room for  0 transports")]
    [InlineData(20, 0, "", "  !illegal transports")]
    [InlineData(20, 0, "1", "  !only  0 transports in tf")]
    [InlineData(20, 8, "999999999999", "  !illegal transports")]
    public void QuantityAndCapacityErrorsDoNotConsumeShipsOrCreateAColony(int capacity, int transports, string line, string error) {
        var game = Scenario(); var planet = game.State.Stars[0].Planets[0]; planet.Capacity = capacity;
        game.State.Players[1].Tf[1].Transports = transports;
        var terminal = Run(game, "la" + line + "\n");
        Assert.Equal(error, terminal.Last[24].TrimEnd()); Assert.Contains(terminal.Screens, ShowsQuantity);
        Assert.Equal(Ownership.None, planet.Owner); Assert.Equal((0, 0), (planet.Inhabitants, planet.IndustrialUnits));
        Assert.Equal(transports, game.State.Players[1].Tf[1].Transports);
    }

    [Fact]
    public void FailedQuantityCanBeRetriedAndPendingTravelIsNotCancelledByLanding() {
        var game = Scenario(); var terminal = Run(game, "la9\nla2\n");
        Assert.Contains(terminal.Screens, s => s[24].StartsWith("  !only  8 transports in tf"));
        Assert.Equal(2, game.State.Stars[0].Planets[0].Inhabitants);
        Assert.Equal(6, game.State.Players[1].Tf[1].Transports);
        game.State.FleetsSys.SetCourse(1, 1, 1);
        terminal = Run(game, "la");
        Assert.Equal("  !tf is not in normal space", terminal.Last[24].TrimEnd());
        Assert.DoesNotContain(terminal.Screens, ShowsQuantity);
        Assert.Equal(1, game.State.Players[1].Tf[1].Destination); Assert.Equal(4, game.State.Players[1].Tf[1].ETA);
        Assert.Equal(2, game.State.Stars[0].Planets[0].Inhabitants);
    }

    [Fact]
    public void ColonyPersistsAfterFleetDepartureAndTheFirstInvestmentCycle() {
        var game = Scenario(); game.State.Players[1].PopulationGrowthRate = 0.3;
        var terminal = new Terminal("la3\ndabgggg4R\nc");
        new ClassicGameLoop(game, terminal).Run();
        Assert.Equal(4, game.Turn);
        Assert.Equal("@A ", terminal.Last[14].Substring(3, 3));
        Assert.Equal(" Ba", terminal.Last[14].Substring(15, 3));
        Assert.Equal("A3:20( 4,/  4)", terminal.Last[1][49..].TrimEnd());
        Assert.Equal((4, 4), (game.State.Stars[0].Planets[0].Inhabitants, game.State.Stars[0].Planets[0].IndustrialUnits));
    }
}
