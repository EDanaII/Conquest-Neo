using Conquest.CLI;
using Conquest.Core;
using Conquest.Core.Model;

namespace Conquest.Tests;

public sealed class EncounterProgressionTests {
    private sealed class Rng(params int[] draws) : IRng {
        private readonly Queue<int> _draws = new(draws);
        public List<(int, int)> Bounds { get; } = [];
        public int Next(int minInclusive, int maxExclusive) {
            Bounds.Add((minInclusive, maxExclusive)); int value = _draws.Dequeue();
            Assert.InRange(value, minInclusive, maxExclusive - 1); return value;
        }
        public void Reseed(int seed) => throw new NotSupportedException();
    }
    private sealed class Terminal(string input) : IClassicTerminal {
        private readonly Queue<char> _keys = new(input);
        public List<string[]> Screens { get; } = [];
        public int ReadKey() => _keys.TryDequeue(out char ch) ? ch : -1;
        public void Present(string[] screen) => Screens.Add(screen);
    }
    private static Game Scenario(IRng rng) {
        var state = new GameState(new GameConfig { NumStars = 2, SafeExplore = true }); state.Stars[1].X = 1;
        foreach (var s in state.Stars) {
            s.ExploredBy[1] = true; state.Board[s.X, s.Y].StarRef = s;
            s.Planets.Add(new Planet(s) { Number = 1, Capacity = 80, PlayerSeenCapacity = 80 });
        }
        foreach (var p in state.Players) { p.Weapons = 3; p.Range = 5; p.Velocity = 1; }
        state.FleetsSys.Place(1, 1, 0, scouts: 0).Cruisers = 1;
        state.FleetsSys.Place(0, 1, 1, scouts: 0).Cruisers = 1;
        return new Game(rng, state);
    }
    private static Terminal Run(Game game, string keys) {
        var terminal = new Terminal(keys); Assert.Equal(0, new ClassicGameLoop(game, terminal).Run()); return terminal;
    }

    [Fact]
    public void OrdinaryDThenGArrivalAutomaticallyPresentsBattleAndTurnsResumeAfterEnemyDestroyed() {
        var rng = new Rng(1, 1, 100, 1); var game = Scenario(rng);
        var terminal = Run(game, "dabgxgxg");
        Assert.Equal(2, game.Turn); Assert.False(game.State.ClassicTurnInProgress);
        Assert.Null(game.State.Players[0].Tf[1].Destination);
        Assert.Equal(1, game.State.Players[1].Tf[1].Cruisers);
        Assert.Contains(terminal.Screens, s => s[18].StartsWith("B?"));
        Assert.DoesNotContain(terminal.Screens, s => s[24].Contains("not implemented"));
        Assert.Equal(4, rng.Bounds.Count);
    }

    [Theory]
    [InlineData(100, 1, false, true)]
    [InlineData(1, 100, true, false)]
    [InlineData(100, 100, false, false)]
    public void EitherOrBothFleetsDestroyedCleanUpAndGAdvancesAgain(int enemyRoll, int playerRoll, bool enemyLives, bool playerLives) {
        var rng = new Rng(enemyRoll, playerRoll); var game = Scenario(rng); Run(game, "dabgxxg");
        Assert.Equal(2, game.Turn); Assert.Null(game.State.Battle);
        Assert.Equal(enemyLives, game.State.Players[0].Tf[1].Destination != null);
        Assert.Equal(playerLives, game.State.Players[1].Tf[1].Destination != null);
        Assert.Equal(" A ", new ClassicScreen().Cell(game.State, 0, 0)); // Departure star remains intact.
        Assert.Equal(playerLives ? " Ba" : " B ", new ClassicScreen().Cell(game.State, 1, 0));
    }

    [Fact]
    public void BothSidesCanRemainColocatedWithoutWarshipsAndFutureTurnsAndProductionProceed() {
        var rng = new Rng(1, 100, 1, 100); var game = Scenario(rng);
        game.State.Players[0].Tf[1].Scouts = game.State.Players[1].Tf[1].Scouts = 1;
        var terminal = Run(game, "dabgxxggg");
        Assert.Equal(4, game.Turn); Assert.False(game.HasPendingClassicCombat);
        Assert.All(game.State.Players, p => { Assert.Equal(1, p.Tf[1].Destination); Assert.Equal(1, p.Tf[1].Scouts); Assert.Equal(0, p.Tf[1].Cruisers); });
        Assert.Equal(4, rng.Bounds.Count);
        Assert.DoesNotContain(terminal.Screens, s => s[24].Contains("not implemented"));
    }

    [Fact]
    public void LaterFleetExchangeRepeatsUntilEitherSideLosesAndOReportsDoNotRoll() {
        var rng = new Rng(80, 80, 1, 1, 100, 1); var game = Scenario(rng);
        var terminal = Run(game, "dabgx?ohgxg");
        Assert.Equal(2, game.Turn); Assert.Equal(6, rng.Bounds.Count);
        Assert.Contains(terminal.Screens, s => s[0][49..].Contains("Colonies"));
        Assert.Contains(terminal.Screens, s => s[19].StartsWith("enemy     21(weap  3)sur:   80"));
    }

    [Fact]
    public void FleetRoundsUseTransportScoutDefenseAndEnemyThenPlayerTSCBRollOrder() {
        var rng = new Rng(100, 1, 1, 100, 1, 100, 100, 1, 1); var game = Scenario(rng);
        foreach (var p in game.State.Players) { var f = p.Tf[1]; f.Transports = f.Scouts = f.BattleStars = 1; }
        game.State.FleetsSys.SetCourse(1, 1, 1); game.BeginClassicTurn(); game.PrepareClassicCombat();
        Assert.Equal(Math.Exp(Math.Log(.8) * 141.0 / 144), game.State.Battle!.PlayerSurvival);
        game.AcknowledgeFleetBattle();
        Assert.Equal(new ShipLosses(1, 0, 0, 1), game.State.Battle!.EnemyLosses);
        Assert.Equal(new ShipLosses(0, 1, 1, 0), game.State.Battle.PlayerLosses);
        Assert.Equal(9, rng.Bounds.Count); Assert.Equal((1, 3), rng.Bounds[^1]);
    }

    [Fact]
    public void EnemyWithdrawalRejectionSamplesBeforePlayerOrdersAndSettlesOnlyOnce() {
        // Survive first round, enemy withdraws its scout: reject current B, accept A.
        var rng = new Rng(1, 1, 1, 2, 1, 100, 1); var game = Scenario(rng); game.State.Players[0].Tf[1].Scouts = 1;
        Run(game, "dabgx"); Assert.Equal(5, rng.Bounds.Count);
        Assert.Equal(FleetBattleStage.Orders, game.State.Battle!.Stage);
        int slot = game.State.Battle.EnemyWithdrawalFleet!.Value;
        Assert.Equal((0, 1, 0), (game.State.Players[0].Tf[slot].Destination, game.State.Players[0].Tf[slot].ETA, game.State.Players[0].Tf[slot].EtaOriginal));
        game = new Game(rng, game.State); Run(game, "gx");
        Assert.Equal(1, game.Turn); Assert.Null(game.State.Players[0].Tf[1].Destination);
        Assert.Equal(1, game.State.Players[0].Tf[slot].Scouts); Assert.Equal(7, rng.Bounds.Count);
        Run(game, "g"); Assert.Equal(2, game.Turn); Assert.Equal(0, game.State.Players[0].Tf[slot].ETA);
    }

    [Fact]
    public void EnemyWarshipsWithdrawWhenOriginalThresholdRequiresItAndPlayerSurvives() {
        var rng = new Rng(1, 1, 1, 1, 1, 1, 1); var game = Scenario(rng); game.State.Players[1].Tf[1].Cruisers = 5;
        game.State.Stars[1].Planets[0].Capacity = 20;
        var terminal = Run(game, "dabgxgxg");
        Assert.Equal(2, game.Turn); Assert.Equal(7, rng.Bounds.Count);
        Assert.Null(game.State.Players[0].Tf[1].Destination);
        Assert.Equal(1, game.State.Players[0].Tf[2].Cruisers);
        Assert.Equal(0, game.State.Players[0].Tf[2].ETA);
        Assert.DoesNotContain(terminal.Screens, s => s[24].Contains("not implemented"));
    }

    [Theory]
    [InlineData("a", true)]
    [InlineData("b", false)]
    [InlineData("z", false)]
    public void WTransfersWholeFleetAndInvalidOrSameStarDestinationRejoinsIt(string target, bool succeeds) {
        var rng = new Rng(1, 1); var game = Scenario(rng); Run(game, "dabgxw\n" + target);
        if (succeeds) {
            Assert.Equal(FleetBattleStage.FinalPause, game.State.Battle!.Stage);
            Assert.Null(game.State.Players[1].Tf[1].Destination);
            Assert.Equal((0, 1, 1), (game.State.Players[1].Tf[2].Destination, game.State.Players[1].Tf[2].ETA, game.State.Players[1].Tf[2].Withdrew));
            Run(game, "xg"); Assert.Equal(2, game.Turn);
        }
        else {
            Assert.Equal(FleetBattleStage.Orders, game.State.Battle!.Stage);
            Assert.Equal(1, game.State.Players[1].Tf[1].Cruisers); Assert.Null(game.State.Players[1].Tf[2].Destination);
        }
        Assert.Equal(2, rng.Bounds.Count);
    }

    [Fact]
    public void PartialWithdrawalCanResumeDestinationAfterEofAndRetainsBattleParent() {
        var rng = new Rng(1, 1, 1); var game = Scenario(rng); game.State.Players[1].Tf[1].Cruisers = 2;
        Run(game, "dabgxwc\n"); Assert.NotNull(game.State.Battle!.PlayerWithdrawalFleet); Assert.Equal(3, rng.Bounds.Count);
        game = new Game(rng, game.State); Run(game, "a");
        Assert.Null(game.State.Battle!.PlayerWithdrawalFleet); Assert.Equal(FleetBattleStage.Orders, game.State.Battle.Stage);
        Assert.Equal(1, game.State.Players[1].Tf[1].Cruisers); Assert.Equal(1, game.State.Players[1].Tf[2].Cruisers);
        Assert.Equal(1, game.State.Players[1].Tf[2].Withdrew); Assert.Equal(3, rng.Bounds.Count);
    }

    [Fact]
    public void EofAtInitialOrFinalPauseDoesNotReplayMovementOrRolls() {
        var rng = new Rng(100, 1); var game = Scenario(rng); Run(game, "dabg");
        Assert.Equal(FleetBattleStage.InitialPause, game.State.Battle!.Stage); Assert.Empty(rng.Bounds);
        Assert.Equal(0, game.State.Players[1].Tf[1].ETA); Assert.Equal(0, game.Turn);
        game = new Game(rng, game.State); Run(game, "x"); Assert.Equal(FleetBattleStage.FinalPause, game.State.Battle!.Stage);
        Assert.Equal(2, rng.Bounds.Count); Run(game, "xg"); Assert.Equal(2, game.Turn); Assert.Equal(2, rng.Bounds.Count);
    }

    [Fact]
    public void SharedStartResolvesBeforeMovementAndClearsOnlyAfterFinalAcknowledgement() {
        var game = new Game(new CRandRng(1), new GameState(new GameConfig { StartingMoney = 16, StartingTransports = 0, SafeExplore = true }));
        game.GenerateStars(); game.InitializeClassicPlayers(); game.InitializeClassicEnemy(); game.BeginClassicPlayerStartup();
        game.SelectClassicPlayerStartingStar(game.State.Players[0].Tf[1].Destination!.Value);
        game.PurchaseClassicStartupShips(StartupShip.Cruiser, 1); game.FinalizeClassicPlayerStartup();
        var rng = new Rng(100, 100, 100, 1); game = new Game(rng, game.State);
        Run(game, "x"); Assert.True(game.State.PendingSharedStartEncounter); Assert.Equal(0, game.Turn);
        Assert.Equal(FleetBattleStage.FinalPause, game.State.Battle!.Stage);
        Run(game, "xg"); Assert.False(game.State.PendingSharedStartEncounter); Assert.Equal(1, game.Turn);
        Assert.Equal(2, game.State.ProductionYear); Assert.Equal(4, rng.Bounds.Count);
    }

    [Fact]
    public void EnemyPlanetaryResponseAfterPlayerFleetLossRequiresPausesAndThenCompletesTurn() {
        var rng = new Rng(1, 100); var game = Scenario(rng);
        var p = game.State.Stars[1].Planets[0]; p.Owner = Ownership.Player; p.Inhabitants = p.IndustrialUnits = 4;
        p.EnemySeenDefense = 0; var terminal = Run(game, "dabgxxxxg");
        Assert.Equal(Ownership.Enemy, p.Owner); Assert.True(p.Conquered); Assert.Equal(2, game.Turn);
        Assert.Equal((4, 4, 80), (p.Inhabitants, p.IndustrialUnits, p.Capacity));
        Assert.Contains(terminal.Screens, s => s[17].StartsWith(">>>>>> Enemy Attacks B1 <<<<<<"));
        Assert.Equal(2, rng.Bounds.Count);
    }

    [Fact]
    public void FleetBattlePrecedesOptionalPlanetAttackAtSameStarAndDoesNotSkipLaterStars() {
        var rng = new Rng(100, 1); var game = Scenario(rng);
        game.State.Stars[1].Planets[0].Owner = Ownership.Enemy;
        var terminal = Run(game, "dabgxxgxg"); Assert.Equal(2, game.Turn);
        Assert.Equal(Ownership.Player, game.State.Stars[1].Planets[0].Owner);
        int fleet = terminal.Screens.FindIndex(s => s[17].Contains("Taskforce Battle"));
        int planet = terminal.Screens.FindIndex(s => s[18].StartsWith("P?")); Assert.True(fleet >= 0 && planet > fleet);
    }

    [Fact]
    public void MultiplePlayerFleetsMergeIntoFreshSlotBeforeFirstBattleRoll() {
        var rng = new Rng(100, 1, 1, 1); var game = Scenario(rng);
        game.State.FleetsSys.Place(1, 2, 1, scouts: 0).Cruisers = 2;
        Run(game, "dabg"); Assert.Equal(3, game.State.Battle!.PlayerFleet); Assert.Empty(rng.Bounds);
        Assert.Null(game.State.Players[1].Tf[1].Destination); Assert.Null(game.State.Players[1].Tf[2].Destination);
        Assert.Equal(3, game.State.Players[1].Tf[3].Cruisers);
        Run(game, "xxg"); Assert.Equal(2, game.Turn); Assert.Equal(" Bc", new ClassicScreen().Cell(game.State, 1, 0));
        Assert.Equal(4, rng.Bounds.Count);
    }

    [Fact]
    public void EnemySlotLimitReportsPendingBattleWithoutRepeatingResolvedLosses() {
        var rng = new Rng(1, 1); var game = Scenario(rng);
        for (int i = 2; i <= 26; i++) game.State.FleetsSys.Place(0, i, 0, scouts: 1);
        var terminal = Run(game, "dabgxgg");
        Assert.Equal(0, game.Turn); Assert.Equal(FleetBattleStage.Orders, game.State.Battle!.Stage);
        Assert.Equal(2, rng.Bounds.Count);
        Assert.Contains(terminal.Screens, s => s[24].Contains("Taskforce limit reached"));
        Assert.Equal(1, game.State.Players[0].Tf[1].Cruisers); Assert.Equal(1, game.State.Players[1].Tf[1].Cruisers);
    }

    [Fact]
    public void WSlotLimitDoesNotSplitOrConsumeTheNextPromptAndIsVisibleAtBPrompt() {
        var rng = new Rng(1, 1); var game = Scenario(rng);
        for (int i = 2; i <= 26; i++) game.State.FleetsSys.Place(1, i, 0, scouts: 1);
        var terminal = Run(game, "dabgxwh"); Assert.Equal(0, game.Turn); Assert.Equal(2, rng.Bounds.Count);
        Assert.Null(game.State.Battle!.PlayerWithdrawalFleet);
        Assert.Contains(terminal.Screens, s => s[24].Contains("Taskforce limit reached"));
        Assert.Contains(terminal.Screens, s => s[8][49..].Contains("Withdraw"));
    }

    [Fact]
    public void OutOfRangeWithdrawalRollsBackWholeFleetWithoutLosingShips() {
        var rng = new Rng(1, 1); var game = Scenario(rng); Run(game, "dabgx");
        game.State.Players[1].Range = 1;
        game.State.Board[0, 0].StarRef = null; game.State.Stars[0].X = 4;
        game.State.Board[4, 0].StarRef = game.State.Stars[0];
        var terminal = Run(game, "w\na");
        Assert.Equal(FleetBattleStage.Orders, game.State.Battle!.Stage); Assert.Null(game.State.Battle.PlayerWithdrawalFleet);
        Assert.Equal(1, game.State.Players[1].Tf[1].Destination); Assert.Equal(1, game.State.Players[1].Tf[1].Cruisers);
        Assert.Null(game.State.Players[1].Tf[2].Destination); Assert.Equal(2, rng.Bounds.Count);
        Assert.Contains(terminal.Screens, s => s[24].Contains("Maximum range"));
    }

    [Fact]
    public void MandatoryBattleRejectsLandingAndQuitKeysRatherThanReturningToMovementInput() {
        var rng = new Rng(1, 1); var game = Scenario(rng); var terminal = Run(game, "dabgxllqq");
        Assert.Equal(FleetBattleStage.Orders, game.State.Battle!.Stage); Assert.Equal(0, game.Turn);
        Assert.Equal(4, terminal.Screens.Count(s => s[18].StartsWith("B?") && s[24].StartsWith("!illegal command")));
        Assert.DoesNotContain(terminal.Screens, s => s[18].StartsWith(" Land Taskforce:") || s[0].StartsWith("Quit game"));
    }

    [Fact]
    public void EnemyDefendedSalvoKeepsCAndBOnlyCasualtyRulesAndEndsAfterLastWarshipDies() {
        var rng = new Rng(100, 1); var game = Scenario(rng); game.State.Players[1].Tf[1].Destination = null;
        var enemy = game.State.Players[0].Tf[1]; enemy.Transports = enemy.Scouts = 1;
        var p = game.State.Stars[1].Planets[0]; p.Owner = Ownership.Player; p.Inhabitants = p.IndustrialUnits = 4;
        p.MissileBases = 1; p.EnemySeenDefense = 0;
        Run(game, "gxxg"); Assert.Equal(2, game.Turn); Assert.Equal(Ownership.Player, p.Owner);
        Assert.Equal((1, 1, 0), (enemy.Transports, enemy.Scouts, enemy.Cruisers)); Assert.Equal(1, p.EnemySeenDefense);
        Assert.Equal(2, rng.Bounds.Count);
    }

    [Fact]
    public void NoReachableEnemyWithdrawalReportsOriginalNonterminationBoundaryWithoutRerollingOrBypassing() {
        var rng = new Rng(1, 1, 1); var game = Scenario(rng); game.State.Players[0].Tf[1].Scouts = 1;
        game.State.Players[0].Range = 0; var terminal = Run(game, "dabgxggw\n");
        Assert.Equal(3, rng.Bounds.Count); Assert.Equal(0, game.Turn); Assert.NotNull(game.State.Battle);
        Assert.Contains(terminal.Screens, s => s[24].Contains("no reachable star"));
        Assert.Null(game.State.Battle!.PlayerWithdrawalFleet); Assert.Equal(1, game.State.Players[1].Tf[1].Cruisers);
    }
}
