using Conquest.CLI;
using Conquest.Core;
using Conquest.Core.Model;

namespace Conquest.Tests;

public sealed class ClassicEndgameUiTests {
    private sealed class NoDrawRng : IRng {
        public int Next(int minInclusive, int maxExclusive) => throw new InvalidOperationException("Unexpected turn/result RNG");
        public void Reseed(int seed) => throw new NotSupportedException();
    }
    private sealed class Terminal(string keys) : IClassicTerminal {
        private readonly Queue<char> _keys = new(keys);
        public List<string[]> Screens { get; } = [];
        public int Clears { get; private set; }
        public int Remaining => _keys.Count;
        public int ReadKey() => _keys.TryDequeue(out char key) ? key : -1;
        public void Present(string[] screen) => Screens.Add(screen);
        public void Clear() => Clears++;
    }
    private static Game Initialized() {
        var game = new Game(new CRandRng(1), new GameState(new GameConfig { StartingMoney = 0, MaxTurns = 1, SafeExplore = true }));
        game.GenerateStars(); game.InitializeClassicPlayers(); game.InitializeClassicEnemy(); game.BeginClassicPlayerStartup();
        game.SelectClassicPlayerStartingStar((game.State.Players[0].Tf[1].Destination!.Value + 1) % game.State.Stars.Count);
        game.FinalizeClassicPlayerStartup();
        // A travelling enemy needs no new decision before this one-turn deadline.
        game.State.Players[0].Tf[1].ETA = 10;
        return new Game(new NoDrawRng(), game.State);
    }
    private static Terminal Run(Game game, string keys) {
        var terminal = new Terminal(keys); Assert.Equal(0, new ClassicGameLoop(game, terminal).Run()); return terminal;
    }

    [Fact]
    public void FinalTurnDisplaysOriginalSummaryAndConsumesOneAcknowledgementBeforeExiting() {
        var game = Initialized(); var terminal = Run(game, "gxg"); var last = terminal.Screens[^1];
        Assert.Equal(1, game.Turn); Assert.Equal(2, game.State.Result!.DisplayedTurn);
        Assert.Equal(ClassicEndReason.TurnLimit, game.State.Result.Reasons); Assert.Equal(1, terminal.Remaining);
        Assert.Equal("*** Game over ***", last[0].TrimEnd());
        Assert.Equal("Player: Population in transports: 35  IU's on colonies:  0  TOTAL: 35", last[1].TrimEnd());
        Assert.Equal("", last[2].TrimEnd());
        Assert.Equal("Enemy:  Population in transports: 35  IU's on colonies:  0  TOTAL: 35", last[3].TrimEnd());
        Assert.Equal("*** DRAWN ***", last[4].TrimEnd()); Assert.Equal(1, terminal.Clears);
        Assert.All(last, row => Assert.Equal(79, row.Length));
    }

    [Fact]
    public void ConfirmedQuitScoresImmediatelyWithoutEnemyOrdersMovementOrClockUpdates() {
        var game = Initialized(); game.State.Players[0].Tf[1].Transports = 10;
        var terminal = Run(game, "qyx"); Assert.Equal(0, game.Turn); Assert.Equal(1, game.State.ProductionYear);
        Assert.Equal(10, game.State.Players[0].Tf[1].ETA); Assert.Equal(ClassicEndReason.Quit, game.State.Result!.Reasons);
        Assert.Equal("*** PLAYER WINS - YOU HAVE SAVED THE GALAXY! ***", terminal.Screens[^1][4].TrimEnd());
        Assert.Contains(terminal.Screens, s => s[0].StartsWith("Quit game....[verify]")); Assert.Equal(2, terminal.Clears);
    }

    [Fact]
    public void DeclinedQuitCompletesTheOriginalTurnAndCanReachItsNaturalEnding() {
        var game = Initialized(); Run(game, "qnx"); Assert.Equal(1, game.Turn);
        Assert.Equal(ClassicEndReason.TurnLimit, game.State.Result!.Reasons); Assert.Equal(9, game.State.Players[0].Tf[1].ETA);
    }

    [Fact]
    public void EofAtQuitVerificationDoesNotPublishAnUnconfirmedOutcome() {
        var game = Initialized(); var terminal = Run(game, "q");
        Assert.Null(game.State.Result); Assert.Equal(0, game.Turn); Assert.StartsWith("Quit game", terminal.Screens[^1][0]);
    }

    [Fact]
    public void EofAtEndingAndNewLoopOnlyRedisplayRetainedResultWithoutReplayingTheTurn() {
        var game = Initialized(); Run(game, "g"); var result = game.State.Result!; Assert.NotNull(result);
        game = new Game(new NoDrawRng(), game.State); var terminal = Run(game, "gg");
        Assert.Same(result, game.State.Result); Assert.Equal(1, game.Turn); Assert.Equal(9, game.State.Players[0].Tf[1].ETA);
        Assert.Equal(1, terminal.Remaining); Assert.Single(terminal.Screens);
    }
}
