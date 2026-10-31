using Conquest.Core;
using Conquest.Core.Model;
using System.Text.Json;

namespace Conquest.Tests;

public sealed class ClassicPlaythroughTests {
    private sealed class CountingRng(int seed) : IRng {
        private readonly CRandRng _inner = new(seed);
        public int Calls { get; private set; }
        public int Next(int minInclusive, int maxExclusive) { Calls++; return _inner.Next(minInclusive, maxExclusive); }
        public void Reseed(int seed) => throw new NotSupportedException();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(17)]
    public void GeneratedGameDevelopsFightsAndReachesRepeatableOutcomeWithoutBypassingAnyGuard(int seed) {
        static string Play(int seed) {
            var rng = new CountingRng(seed);
            var game = new Game(rng, new GameState(new GameConfig { MaxTurns = 60 }));
            game.GenerateStars(); game.InitializeClassicPlayers(); game.InitializeClassicEnemy(); game.BeginClassicPlayerStartup();
            int enemy = game.State.Players[0].Tf[1].Destination!.Value; var enemyStar = game.State.Stars[enemy];
            var candidates = Enumerable.Range(0, game.State.Stars.Count).Where(i => i != enemy
                && game.State.Stars[i].Planets.Any(p => p.Capacity >= 10)
                && Math.Sqrt(Math.Pow(game.State.Stars[i].X - enemyStar.X, 2) + Math.Pow(game.State.Stars[i].Y - enemyStar.Y, 2)) <= 5).ToArray();
            Assert.NotEmpty(candidates); int start = candidates[0];
            game.SelectClassicPlayerStartingStar(start);
            Assert.True(game.PurchaseClassicStartupShips(StartupShip.Cruiser, 1));
            Assert.True(game.PurchaseClassicStartupShips(StartupShip.Scout, 2));
            Assert.True(game.InvestClassicStartupResearch(ResearchField.Range, 2)); game.FinalizeClassicPlayerStartup();
            var planet = game.State.Stars[start].Planets.First(p => p.Capacity >= 10);
            game.State.FleetsSys.LandPlayerTransports(1, planet.Number, 10);
            game.State.FleetsSys.SetCourse(1, 1, enemy); // A fixed human attack order, not an enemy-AI replacement.
            int battles = 0, salvos = 0, productionCycles = 0;
            void Combat() {
                int prompts = 0;
                while (game.HasPendingClassicCombat) {
                    Assert.True(++prompts < 1000);
                    if (game.State.Battle is { Stage: FleetBattleStage.InitialPause }) { battles++; game.AcknowledgeFleetBattle(); }
                    else if (game.State.Battle is { Stage: FleetBattleStage.Orders }) game.ContinueFleetBattle();
                    else if (game.State.Battle != null) game.AcknowledgeFleetBattle();
                    else if (game.State.EnemyAttack != null) { salvos++; game.AcknowledgeEnemyAttack(); }
                    else if (game.State.Attack is { AwaitingAcknowledgement: true }) game.AcknowledgePlayerAttack();
                    else game.EndPlayerAttack(); // Human breaks off optional planetary attacks.
                }
            }
            while (game.State.Result == null) {
                Assert.True(game.Turn < 60); game.BeginClassicTurn(); game.PrepareClassicCombat(); Combat();
                int year = game.State.ProductionYear; game.PrepareClassicEconomy();
                if (year == 4 && game.State.ProductionYear == 0) productionCycles++;
                while (game.State.Investment is { } session) {
                    if (!session.Planet.Conquered) Assert.Null(game.InvestInClassicColony('-'));
                    Assert.Null(game.InvestInClassicColony('W', session.Balance)); game.FinishClassicInvestmentLine();
                }
                game.PrepareClassicCombat(afterInvestment: true); Combat(); game.CompleteClassicTurn();
                int calls = rng.Calls; game.CheckClassicGameOver(); Assert.Equal(calls, rng.Calls);
            }
            Assert.True(battles + salvos > 0, $"Seed {seed} did not exercise conflict.");
            Assert.True(productionCycles > 0, $"Seed {seed} ended before development.");
            Assert.True(game.Turn > 4); Assert.NotEqual(ClassicEndReason.None, game.State.Result!.Reasons);
            Assert.Throws<InvalidOperationException>(() => game.BeginClassicTurn());
            return JsonSerializer.Serialize(new { game.State.Result, battles, salvos, productionCycles, rng.Calls,
                Players = game.State.Players, Colonies = game.State.Stars.SelectMany(s => s.Planets)
                    .Select(p => new { p.Owner, p.Capacity, p.Inhabitants, p.IndustrialUnits, p.MissileBases, p.AdvancedMissileBases }) });
        }
        Assert.Equal(Play(seed), Play(seed));
    }
}
