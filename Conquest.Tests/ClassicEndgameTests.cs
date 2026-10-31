using Conquest.Core;
using Conquest.Core.Model;

namespace Conquest.Tests;

public sealed class ClassicEndgameTests {
    private sealed class NoDrawRng : IRng {
        public int Next(int minInclusive, int maxExclusive) => throw new InvalidOperationException("Unexpected score RNG");
        public void Reseed(int seed) => throw new NotSupportedException();
    }
    private static Game Scenario(int player = 10, int enemy = 10, int maxTurns = 100) {
        var state = new GameState(new GameConfig { NumStars = 2, SafeExplore = true, MaxTurns = maxTurns });
        state.Stars[1].X = 4;
        foreach (var star in state.Stars) { star.ExploredBy[1] = true; state.Board[star.X, star.Y].StarRef = star; }
        foreach (var p in state.Players) { p.Weapons = 3; p.Velocity = 1; p.Range = 7; }
        state.FleetsSys.Place(1, 1, 0, scouts: 0).Transports = player;
        state.FleetsSys.Place(0, 1, 1, scouts: 0).Transports = enemy;
        return new Game(new NoDrawRng(), state);
    }
    private static void Advance(Game game, int turns) {
        for (int i = 0; i < turns; i++) {
            game.BeginClassicTurn(); game.PrepareClassicEconomy();
            while (game.State.Investment is { } session) {
                Assert.Null(game.InvestInClassicColony('R', session.Balance)); game.FinishClassicInvestmentLine();
            }
            game.CompleteClassicTurn();
        }
    }

    [Fact]
    public void ScoreCountsOnlyActiveTransportsAndOwnedIndustryIncludingConqueredColonies() {
        var game = Scenario(); var state = game.State;
        var travelling = state.FleetsSys.Place(1, 2, 0, scouts: 999); travelling.Transports = 5; travelling.Cruisers = 999;
        state.FleetsSys.SetCourse(1, 2, 1);
        state.Players[1].Tf[3].Transports = 999; // Unused slot retains counts after joins in C.
        var a = state.Stars[0]; var b = state.Stars[1];
        a.Planets.Add(new Planet(a) { Owner = Ownership.Player, Conquered = true, Inhabitants = 999, IndustrialUnits = 7 });
        b.Planets.Add(new Planet(b) { Owner = Ownership.Enemy, Inhabitants = 999, IndustrialUnits = 4 });
        b.Planets.Add(new Planet(b) { Owner = Ownership.None, Inhabitants = 999, IndustrialUnits = 999 });
        var result = game.CheckClassicGameOver(quit: true)!;
        Assert.Equal(new ClassicScore(15, 7), result.Player); Assert.Equal(22, result.Player.Total);
        Assert.Equal(new ClassicScore(10, 4), result.Enemy); Assert.Equal(14, result.Enemy.Total);
        Assert.Equal(ClassicWinner.Player, result.Winner); Assert.Equal(ClassicEndReason.Quit, result.Reasons);
        Assert.Equal((0, 1), (game.Turn, state.ProductionYear));
    }

    [Theory]
    [InlineData(0, 10, ClassicEndReason.PlayerEliminated, ClassicWinner.Enemy)]
    [InlineData(10, 0, ClassicEndReason.EnemyEliminated, ClassicWinner.Player)]
    [InlineData(0, 0, ClassicEndReason.PlayerEliminated | ClassicEndReason.EnemyEliminated, ClassicWinner.Draw)]
    public void ZeroEconomyEliminatesEvenWithSurvivingWarships(int player, int enemy, ClassicEndReason reason, ClassicWinner winner) {
        var game = Scenario(player, enemy);
        foreach (var p in game.State.Players) p.Tf[1].Cruisers = p.Tf[1].BattleStars = 100;
        var result = game.CheckClassicGameOver()!; Assert.Equal(reason, result.Reasons); Assert.Equal(winner, result.Winner);
    }

    [Fact]
    public void PopulationWithoutIndustryDoesNotPreventEliminationAndNegativeTotalsAreNotClamped() {
        var game = Scenario(player: 0); var star = game.State.Stars[0];
        star.Planets.Add(new Planet(star) { Owner = Ownership.Player, Inhabitants = 100, IndustrialUnits = 0 });
        Assert.True(game.CheckClassicGameOver()!.Reasons.HasFlag(ClassicEndReason.PlayerEliminated));
        game = Scenario(); star = game.State.Stars[0];
        star.Planets.Add(new Planet(star) { Owner = Ownership.Player, IndustrialUnits = -11 });
        Assert.Null(game.CheckClassicGameOver()); // C tests == 0, not <= 0.
        Assert.Equal(-1, game.CheckClassicGameOver(quit: true)!.Player.Total);
    }

    [Theory]
    [InlineData(8, 1, 38, 8.0, false)]
    [InlineData(8, 1, 39, 8.0, true)]
    [InlineData(15, 2, 39, 8.0, false)]
    [InlineData(16, 2, 39, 8.0, true)]
    [InlineData(8, 3, 39, 2.5, false)]
    [InlineData(9, 3, 39, 2.5, true)]
    public void OverwhelmStartsAtDisplayedFortyAndUsesIntegerDivision(int player, int enemy, int turns, double factor, bool ended) {
        var game = Scenario(player, enemy); game.State.Config.OverwhelmFactor = factor; Advance(game, turns);
        var result = game.CheckClassicGameOver(); Assert.Equal(ended, result != null);
        if (ended) { Assert.Equal(ClassicEndReason.EnemyOverwhelmed, result!.Reasons); Assert.Equal(ClassicWinner.Player, result.Winner); }
    }

    [Fact]
    public void BothOverwhelmFlagsCanBeSetWithSmallConfiguredFactorButScoreStillDecidesWinner() {
        var game = Scenario(); game.State.Config.OverwhelmFactor = 1; Advance(game, 39);
        var result = game.CheckClassicGameOver()!;
        Assert.Equal(ClassicEndReason.PlayerOverwhelmed | ClassicEndReason.EnemyOverwhelmed, result.Reasons);
        Assert.Equal(ClassicWinner.Draw, result.Winner);
    }

    [Theory]
    [InlineData(2, 3, 1.5, ClassicWinner.Draw)]
    [InlineData(2, 4, 1.5, ClassicWinner.Enemy)]
    [InlineData(4, 2, 1.5, ClassicWinner.Player)]
    [InlineData(10, 10, .5, ClassicWinner.Enemy)]
    [InlineData(0, 0, 1.5, ClassicWinner.Draw)]
    public void FinalWinnerUsesStrictThresholdAndEnemyFirstPrecedenceEvenOnQuit(int player, int enemy, double factor, ClassicWinner winner) {
        var game = Scenario(player, enemy); game.State.Config.VictoryMultiplier = factor;
        Assert.Equal(winner, game.CheckClassicGameOver(quit: true)!.Winner);
        Assert.Equal(0, game.Turn); Assert.Equal(1, game.State.ProductionYear);
    }

    [Fact]
    public void TurnLimitEndsOnlyAfterLastTurnAndLastProductionYearDoesNotInvest() {
        var game = Scenario(maxTurns: 4); var home = game.State.Stars[0];
        home.Planets.Add(new Planet(home) { Owner = Ownership.Player, Capacity = 20, Inhabitants = 3, IndustrialUnits = 3 });
        game.State.Players[1].PopulationGrowthRate = .3;
        Advance(game, 3); Assert.Null(game.CheckClassicGameOver()); Advance(game, 1);
        var result = game.CheckClassicGameOver()!;
        Assert.Equal(ClassicEndReason.TurnLimit, result.Reasons); Assert.Equal(5, result.DisplayedTurn);
        Assert.Equal(3, home.Planets[0].Inhabitants); Assert.Equal(3, home.Planets[0].IndustrialUnits);
        Assert.Throws<InvalidOperationException>(() => game.BeginClassicTurn());
    }

    [Fact]
    public void NoResultIsFinalizedBeforePendingInvestmentOrCombatFinishes() {
        var game = Scenario(); Advance(game, 3); var a = game.State.Stars[0];
        a.Planets.Add(new Planet(a) { Owner = Ownership.Player, Capacity = 20, Inhabitants = 3, IndustrialUnits = 3 });
        game.BeginClassicTurn(); game.PrepareClassicEconomy(); Assert.NotNull(game.State.Investment);
        Assert.Throws<InvalidOperationException>(() => game.CheckClassicGameOver()); Assert.Null(game.State.Result);
        Assert.Null(game.InvestInClassicColony('R', 3)); game.FinishClassicInvestmentLine(); game.CompleteClassicTurn();
        Assert.Null(game.CheckClassicGameOver());
        game = Scenario(); game.State.FleetsSys.Place(0, 1, 0, scouts: 0).Cruisers = 1;
        game.BeginClassicTurn(); game.PrepareClassicCombat(); Assert.NotNull(game.State.Battle);
        Assert.Throws<InvalidOperationException>(() => game.CheckClassicGameOver(quit: true)); Assert.Null(game.State.Result);
    }

    [Fact]
    public void RetainedResultSurvivesNewFacadeAndBlocksBothClassicAndDebugAdvancement() {
        var game = Scenario(); var result = game.CheckClassicGameOver(quit: true)!;
        game.State.Players[1].Tf[1].Transports = 100; game = new Game(new NoDrawRng(), game.State);
        Assert.Same(result, game.CheckClassicGameOver()); Assert.Equal(10, result.Player.Total);
        Assert.Throws<InvalidOperationException>(() => game.BeginClassicTurn());
        Assert.Throws<InvalidOperationException>(() => game.Tick());
        Assert.Throws<InvalidOperationException>(() => game.State.FleetsSys.Tick()); Assert.Equal(0, game.Turn);
    }

    [Fact]
    public void UndefinedCOverflowIsRejectedWithoutPublishingResult() {
        var game = Scenario(player: int.MaxValue); game.State.FleetsSys.Place(1, 2, 0, scouts: 0).Transports = 1;
        Assert.Contains("overflow", Assert.Throws<InvalidOperationException>(() => game.CheckClassicGameOver(quit: true)).Message);
        Assert.Null(game.State.Result);
        game = Scenario(); game.State.Config.VictoryMultiplier = double.PositiveInfinity;
        Assert.Contains("undefined", Assert.Throws<InvalidOperationException>(() => game.CheckClassicGameOver(quit: true)).Message);
        Assert.Null(game.State.Result);
    }

    [Fact]
    public void FourthYearGrowthCanRestoreLastIndustryAfterBombardmentBeforeEliminationIsChecked() {
        var game = Scenario(player: 0); game.State.Players[1].Tf[1].Cruisers = 1;
        game.State.Players[1].PopulationGrowthRate = .3; var star = game.State.Stars[0];
        var colony = new Planet(star) { Number = 1, Owner = Ownership.Player, Conquered = true,
            Capacity = 20, Inhabitants = 8, IndustrialUnits = 1 }; star.Planets.Add(colony);
        Advance(game, 3); Assert.Equal(1, game.BombardPlanet(1, 1, 1)); Assert.Equal(0, colony.IndustrialUnits);
        Assert.Null(game.State.Result); game.BeginClassicTurn();
        Assert.Throws<InvalidOperationException>(() => game.CheckClassicGameOver());
        game.PrepareClassicEconomy(); Assert.Equal((8, 1), (colony.Inhabitants, colony.IndustrialUnits));
        Assert.Null(game.InvestInClassicColony('R', 1)); game.FinishClassicInvestmentLine(); game.CompleteClassicTurn();
        Assert.Null(game.CheckClassicGameOver()); Assert.Equal(4, game.Turn);
    }
}
