using Conquest.Core;
using Conquest.Core.Model;
using System.Text.Json;
using Xunit.Abstractions;

namespace Conquest.Tests;

public sealed class ClassicV1AuditTests(ITestOutputHelper output) {
    private sealed class AuditBudgetExceeded : Exception { }
    private sealed class BudgetRng(int seed, int budget = 1_000_000) : IRng {
        private readonly CRandRng _rng = new(seed);
        public int Calls { get; private set; }
        public int Next(int minInclusive, int maxExclusive) {
            if (++Calls > budget) throw new AuditBudgetExceeded();
            return _rng.Next(minInclusive, maxExclusive);
        }
        public void Reseed(int seed) => throw new NotSupportedException();
    }
    public static IEnumerable<object[]> Seeds => Enumerable.Range(1, 32).Select(seed => new object[] { seed });

    [Theory]
    [MemberData(nameof(Seeds))]
    public void OrdinarySeededClassicGameReachesLegitimateEndingWithoutGuardOrBudgetFailure(int seed) {
        string Play() {
            var rng = new BudgetRng(seed); var game = new Game(rng, new GameState(new GameConfig { MaxTurns = 100 }));
            game.GenerateStars(); game.InitializeClassicPlayers(); game.InitializeClassicEnemy(); game.BeginClassicPlayerStartup();
            int enemy = game.State.Players[0].Tf[1].Destination!.Value;
            int start = Enumerable.Range(0, game.State.Stars.Count).First(i => i != enemy && game.State.Stars[i].Planets.Any(p => p.Capacity >= 10));
            game.SelectClassicPlayerStartingStar(start); game.PurchaseClassicStartupShips(StartupShip.Cruiser, 1);
            game.PurchaseClassicStartupShips(StartupShip.Scout, 2); game.InvestClassicStartupResearch(ResearchField.Range, 2);
            game.FinalizeClassicPlayerStartup();
            var home = game.State.Stars[start].Planets.First(p => p.Capacity >= 10);
            game.State.FleetsSys.LandPlayerTransports(1, home.Number, 10);
            int battles = 0, attacks = 0, cycles = 0;
            void Combat() {
                int interactions = 0;
                while (game.HasPendingClassicCombat) {
                    Assert.True(++interactions < 10_000, "Combat failed to reach another phase.");
                    if (game.State.Battle is { Stage: FleetBattleStage.InitialPause }) { battles++; game.AcknowledgeFleetBattle(); }
                    else if (game.State.Battle is { Stage: FleetBattleStage.Orders }) game.ContinueFleetBattle();
                    else if (game.State.Battle != null) game.AcknowledgeFleetBattle();
                    else if (game.State.EnemyAttack != null) { attacks++; game.AcknowledgeEnemyAttack(); }
                    else if (game.State.Attack is { AwaitingAcknowledgement: true }) game.AcknowledgePlayerAttack();
                    else game.EndPlayerAttack();
                }
            }
            while (game.State.Result == null) {
                Assert.True(game.Turn < 100); game.BeginClassicTurn(); game.PrepareClassicCombat(); Combat();
                int year = game.State.ProductionYear; game.PrepareClassicEconomy();
                if (year == 4 && game.State.ProductionYear == 0) cycles++;
                while (game.State.Investment is { } investment) {
                    if (!investment.Planet.Conquered) Assert.Null(game.InvestInClassicColony('-'));
                    Assert.Null(game.InvestInClassicColony('W', investment.Balance)); game.FinishClassicInvestmentLine();
                }
                game.PrepareClassicCombat(afterInvestment: true); Combat(); game.CompleteClassicTurn(); game.CheckClassicGameOver();
            }
            var result = game.State.Result!;
            output.WriteLine($"seed={seed}; turns={game.Turn}; winner={result.Winner}; reason={result.Reasons}; P={result.Player.Total}; E={result.Enemy.Total}; battles={battles}; attack-pauses={attacks}; cycles={cycles}; RNG={rng.Calls}");
            Assert.False(game.State.ClassicTurnInProgress); Assert.False(game.HasPendingClassicCombat);
            Assert.NotEqual(ClassicEndReason.None, result.Reasons);
            return JsonSerializer.Serialize(new { result, battles, attacks, cycles, rng.Calls, Players = game.State.Players,
                Planets = game.State.Stars.SelectMany(s => s.Planets).Select(p => new { p.Owner, p.Capacity, p.Inhabitants, p.IndustrialUnits, p.MissileBases, p.AdvancedMissileBases }) });
        }
        Assert.Equal(Play(), Play());
    }

    [Theory]
    [InlineData(4, 4, 1)]
    [InlineData(4, 4, 7)]
    [InlineData(4, 4, 41)]
    [InlineData(2, 2, 1)]
    [InlineData(1, 8, 1)]
    [InlineData(8, 1, 1)]
    public void DenseSmallBoardGeneratesAllRequestedStarsWithinBoundedDraws(int width, int height, int seed) {
        string Generate() {
            var rng = new BudgetRng(seed, 10_000);
            var game = new Game(rng, new GameState(new GameConfig { Width = width, Height = height, NumStars = width * height }));
            game.GenerateStars(); Assert.Equal(width * height, game.State.Stars.Select(s => (s.X, s.Y)).Distinct().Count());
            Assert.All(game.State.Stars, s => {
                Assert.InRange(s.X, 0, width - 1); Assert.InRange(s.Y, 0, height - 1);
                Assert.Same(s, game.State.Board[s.X, s.Y].StarRef);
            });
            return JsonSerializer.Serialize(game.State.Stars.Select(s => new { s.X, s.Y,
                Planets = s.Planets.Select(p => new { p.Number, p.Capacity }) }));
        }
        Assert.Equal(Generate(), Generate());
    }

    [Fact]
    public void CapturingLastEnemyColonyCompletesTurnAndProducesPlayerVictory() {
        var state = new GameState(new GameConfig { NumStars = 2, SafeExplore = true });
        state.Stars[1].X = 1;
        foreach (var star in state.Stars) {
            star.ExploredBy[1] = true; state.Board[star.X, star.Y].StarRef = star;
        }
        foreach (var player in state.Players) { player.Weapons = 3; player.Range = 5; player.Velocity = 1; }
        var target = new Planet(state.Stars[1]) { Number = 1, Owner = Ownership.Enemy, Capacity = 20,
            Inhabitants = 10, IndustrialUnits = 10 };
        state.Stars[1].Planets.Add(target);
        var fleet = state.FleetsSys.Place(1, 1, 0, scouts: 0); fleet.Cruisers = 1; fleet.Transports = 10;
        var rng = new BudgetRng(1); var game = new Game(rng, state);
        state.FleetsSys.SetCourse(1, 1, 1);
        game.BeginClassicTurn(); game.PrepareClassicCombat();
        Assert.NotNull(state.Attack); Assert.Null(state.Result);
        Assert.True(game.FirePlayerSalvo(1, 1).Captured);
        game.AcknowledgePlayerAttack(); game.PrepareClassicEconomy(); game.CompleteClassicTurn();
        var result = game.CheckClassicGameOver()!;
        Assert.Equal(Ownership.Player, target.Owner); Assert.True(target.Conquered);
        Assert.Equal(ClassicEndReason.EnemyEliminated, result.Reasons);
        Assert.Equal(ClassicWinner.Player, result.Winner); Assert.Equal(20, result.Player.Total);
        Assert.Equal(0, result.Enemy.Total); Assert.Equal(1, game.Turn); Assert.Equal(0, rng.Calls);
        Assert.Throws<InvalidOperationException>(() => game.BeginClassicTurn());
    }
}
