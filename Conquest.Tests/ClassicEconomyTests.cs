using Conquest.CLI;
using Conquest.Core;
using Conquest.Core.Model;

namespace Conquest.Tests;

public sealed class ClassicEconomyTests {
    private sealed class NoDrawRng : IRng {
        public int Next(int minInclusive, int maxExclusive) => throw new InvalidOperationException("Unexpected economic RNG draw");
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
    private static Game Scenario(int population = 3, int iu = 3, double rate = 0.3) {
        var state = new GameState(new GameConfig { NumStars = 2, SafeExplore = true }); state.Stars[1].X = 4;
        foreach (var star in state.Stars) { star.ExploredBy[1] = true; state.Board[star.X, star.Y].StarRef = star; }
        var home = state.Stars[0];
        home.Planets.Add(new Planet(home) { Number = 3, Capacity = 20, PlayerSeenCapacity = 20,
            Owner = Ownership.Player, Inhabitants = population, IndustrialUnits = iu });
        var player = state.Players[1]; player.Velocity = 1; player.Range = 5; player.Weapons = 3; player.PopulationGrowthRate = rate;
        var fleet = state.FleetsSys.Place(1, 1, 0, scouts: 2); fleet.Transports = 8; fleet.Cruisers = 1;
        return new Game(new NoDrawRng(), state);
    }
    private static Terminal Run(Game game, string input) {
        var terminal = new Terminal(input); Assert.Equal(0, new ClassicGameLoop(game, terminal).Run()); return terminal;
    }

    [Fact]
    public void FourthTurnShowsOriginalInvestmentBudgetAndCalendarAndOnlyCompletesAfterSpending() {
        var game = Scenario(); var terminal = Run(game, "ggg");
        Assert.Equal(3, game.Turn); Assert.Equal(4, game.State.ProductionYear);
        Assert.Equal(3, game.State.Stars[0].Planets[0].Inhabitants);
        terminal = Run(game, "g4R\n");
        Assert.Contains(terminal.Screens, s => s[17].StartsWith("$$$$$$$$$ Investment $$$$$$$$$") && s[18].StartsWith("A3>  4?")
            && s[18].Substring(31, 22) == "Turn:                4" && s[19].Substring(31, 22) == "Prod yr:             0");
        Assert.Contains((7, 18), terminal.Cursors);
        Assert.Equal(4, game.Turn); Assert.Equal(1, game.State.ProductionYear); Assert.Null(game.State.Investment);
        Assert.Equal("Prod yr:             1", terminal.Last[19].Substring(31, 22));
        Assert.Equal("@Ab", terminal.Last[14].Substring(3, 3)); // Research-only inv_player also merges into its new slot.
    }

    [Fact]
    public void LandingThenSpaceAdvancementGrowsColonyAndPreservesResourceAndDisplayAccounting() {
        var game = Scenario(0, 0); game.State.Stars[0].Planets[0].Owner = Ownership.None;
        var terminal = Run(game, "la3\n    4R\nc");
        Assert.Equal(4, game.Turn); Assert.Equal(4, game.State.Players[1].RangeResearchProgress);
        Assert.Equal(5, game.State.Players[1].Tf[2].Transports); Assert.Null(game.State.Players[1].Tf[1].Destination);
        Assert.Equal("A3:20( 4,/  4)", terminal.Last[1][49..].TrimEnd());
        Assert.Equal("@Ab", terminal.Last[14].Substring(3, 3));
    }

    [Fact]
    public void PurchasesAreImmediateAndAllTokensAfterBudgetExhaustionStillRun() {
        var game = Scenario(20, 20, rate: 0);
        var terminal = Run(game, "gggg2S8RZ\nt\n");
        Assert.Equal(4, game.Turn);
        Assert.Contains(terminal.Screens, s => s[24].StartsWith(" !Illegal field Z"));
        Assert.Equal("TFb:A( 1, 1) 8t 4s 1c", terminal.Last[0][49..].TrimEnd());
        Assert.Equal(20, game.State.Stars[0].Planets[0].IndustrialUnits);
        Assert.Equal(8, game.State.Players[1].RangeResearchProgress);
    }

    [Fact]
    public void InfrastructureChangesIUWithoutReducingItByTheCashSpent() {
        var game = Scenario(10, 10, rate: 0); var terminal = Run(game, "gggg2I4R\nc");
        Assert.Equal("A3:20(10,/ 12)", terminal.Last[1][49..].TrimEnd()); Assert.Equal(4, game.Turn);
    }

    [Fact]
    public void MaximumTransportMobilizationAbandonsColonyThenRemainingBudgetBuildsAScout() {
        var game = Scenario(6, 12, rate: 0); var planet = game.State.Stars[0].Planets[0]; planet.MissileBases = 2; planet.AdvancedMissileBases = 1;
        var terminal = Run(game, "gggg*TS\nt\n");
        Assert.Equal(4, game.Turn); Assert.Equal(Ownership.None, planet.Owner); Assert.Equal(0, planet.IndustrialUnits);
        Assert.Equal((0, 0), (planet.MissileBases, planet.AdvancedMissileBases));
        Assert.Equal(" Ab", terminal.Last[14].Substring(3, 3));
        Assert.Equal("TFb:A( 1, 1)14t 3s 1c", terminal.Last[0][49..].TrimEnd());
    }

    [Fact]
    public void BlankOrIllegalGDoesNotSkipInvestmentAndEOFCanResumeWithoutRepeatedGrowth() {
        var game = Scenario(); var first = Run(game, "gggg\nG\n");
        Assert.Equal(3, game.Turn); Assert.Equal(0, game.State.ProductionYear);
        Assert.Equal(4, game.State.Stars[0].Planets[0].Inhabitants); Assert.Equal(4, game.State.Investment!.Balance);
        Assert.Equal(" !Illegal field G", first.Last[24].TrimEnd());
        Assert.Throws<InvalidOperationException>(() => game.Tick());
        var second = Run(new Game(new NoDrawRng(), game.State), "4R\n");
        Assert.Equal(4, game.Turn); Assert.Equal(4, game.State.Stars[0].Planets[0].Inhabitants); Assert.Null(game.State.Investment);
        Assert.Equal(" 3:20( 4,/  4)", second.Last[2][49..].TrimEnd());
    }

    [Fact]
    public void InvestmentHelpIncludesAllOriginalCommandsAndDoesNotConsumeBudget() {
        var game = Scenario(); var terminal = Run(game, "ggggH\n4R\n");
        Assert.Contains(terminal.Screens, s => s[0][49..].TrimEnd() == " A - Bld Adv. Missle Base 35"
            && s[17][49..].StartsWith(">S - Star summary") && s[18].StartsWith("A3>  4?"));
        Assert.Equal(4, game.Turn); Assert.Equal(4, game.State.Players[1].RangeResearchProgress);
    }

    [Theory]
    [InlineData(">\nm")]
    [InlineData(">\nsa\n")]
    [InlineData(">\nc")]
    [InlineData(">\nr\n")]
    public void OriginalReportSubpromptReadsASeparateKeyAndReturnsToTheUnchangedBudget(string commands) {
        var game = Scenario(); var terminal = Run(game, "gggg" + commands + "4R\n");
        Assert.Contains(terminal.Screens, s => s[18].StartsWith(">?"));
        Assert.Equal(4, game.Turn); Assert.Equal(4, game.State.Players[1].RangeResearchProgress);
    }

    [Fact]
    public void InlineCharactersAfterGreaterThanAreStillInvestmentTokensAfterTheSeparateReportKey() {
        var game = Scenario(20, 20, rate: 0); Run(game, "gggg>C\nm4R\n");
        Assert.Equal(4, game.Turn); Assert.Equal(2, game.State.Players[1].Tf[2].Cruisers);
        Assert.Equal(4, game.State.Players[1].RangeResearchProgress);
    }

    [Fact]
    public void InvalidReportSubcommandCannotJoinOrAdvanceAndBudgetStillMustBeSpent() {
        var game = Scenario(); var terminal = Run(game, "gggg>\nj4R\n");
        Assert.Contains(terminal.Screens, s => s[24].StartsWith(" !Only M,S,C,R allowed") && s[18].StartsWith("A3>  4?"));
        Assert.Equal(4, game.Turn); Assert.Equal(2, game.State.Players[1].Tf[2].Scouts);
    }

    [Fact]
    public void ZeroBudgetRequiresAnInputLineAndRejectedAffordableMinimumDoesNotCreateFreeShips() {
        var game = Scenario(0, 0, rate: 0); var terminal = Run(game, "gggg");
        Assert.Equal(3, game.Turn); Assert.NotNull(game.State.Investment); Assert.Equal(0, game.State.Investment.Balance);
        Assert.StartsWith("A3>  0?", terminal.Last[18]);
        Run(game, "*S\n");
        Assert.Equal(4, game.Turn); Assert.Equal(2, game.State.Players[1].Tf[2].Scouts);
    }

    [Fact]
    public void MultipleColoniesUseTheirOwnBudgetsAndOriginalStoredOrder() {
        var game = Scenario(); var a = game.State.Stars[0]; a.Planets[0].Number = 6;
        a.Planets.Add(new Planet(a) { Number = 2, Capacity = 20, PlayerSeenCapacity = 20, Owner = Ownership.Player, Inhabitants = 5, IndustrialUnits = 5 });
        var b = game.State.Stars[1]; b.Planets.Add(new Planet(b) { Number = 1, Capacity = 30, PlayerSeenCapacity = 30, Owner = Ownership.Player, Inhabitants = 9, IndustrialUnits = 9 });
        var terminal = Run(game, "gggg4R\n7R\n12R\nc");
        Assert.Contains(terminal.Screens, s => s[18].StartsWith("A6>  4?"));
        Assert.Contains(terminal.Screens, s => s[18].StartsWith("A2>  7?"));
        Assert.Contains(terminal.Screens, s => s[18].StartsWith("B1> 12?"));
        Assert.Equal("A6:20( 4,/  4)", terminal.Last[1][49..].TrimEnd());
        Assert.Equal("A2:20( 7,/  7)", terminal.Last[2][49..].TrimEnd());
        Assert.Equal("B1:30(12,/ 12)", terminal.Last[3][49..].TrimEnd());
        Assert.Equal(4, game.Turn);
    }

    [Fact]
    public void EnemyColonyProductionBoundaryIsExplicitAndDoesNotSilentlyAdvanceTheFourthTurn() {
        var game = Scenario(); var remote = game.State.Stars[1];
        remote.Planets.Add(new Planet(remote) { Owner = Ownership.Enemy, Capacity = 20, Inhabitants = 5, IndustrialUnits = 5 });
        var terminal = Run(game, "gggg");
        Assert.Equal(3, game.Turn); Assert.False(game.State.ClassicTurnInProgress);
        Assert.Contains("Enemy investment requires initialized enemy research", terminal.Last[24]);
        Assert.Equal(3, game.State.Stars[0].Planets[0].Inhabitants); Assert.Equal(5, remote.Planets[0].Inhabitants);
    }
}
