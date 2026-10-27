using System.Text;
using Conquest.CLI;
using Conquest.Core;
using Conquest.Core.Model;
using Conquest.Tests.TestHelpers;

namespace Conquest.Tests;

public sealed class ClassicStartupCliTests {
    private sealed class ScriptedIO : IGameIO {
        private readonly Queue<string> _lines;
        private readonly StringBuilder _output = new();
        private int _reads;
        public ScriptedIO(params string[] lines) { _lines = new(lines); }
        public string Output => _output.ToString();
        public void Enqueue(params string[] lines) {
            foreach (string line in lines) _lines.Enqueue(line);
        }
        public string? ReadLine() {
            Assert.True(++_reads < 100, "Input loop did not terminate at EOF.");
            return _lines.TryDequeue(out var line) ? line : null;
        }
        public void Write(string text) => _output.Append(text);
        public void WriteLine(string text = "") => _output.AppendLine(text);
    }

    private sealed class ScriptedRng : IRng {
        private readonly Queue<(int Min, int Max, int Value)> _draws = new();
        public int Calls { get; private set; }
        public void Add(int min, int max, int value) => _draws.Enqueue((min, max, value));
        public int Next(int minInclusive, int maxExclusive) {
            Assert.NotEmpty(_draws);
            var draw = _draws.Dequeue();
            Assert.Equal((draw.Min, draw.Max), (minInclusive, maxExclusive));
            Assert.InRange(draw.Value, minInclusive, maxExclusive - 1);
            Calls++;
            return draw.Value;
        }
        public void Reseed(int seed) => throw new NotSupportedException();
        public void AssertExhausted() => Assert.Empty(_draws);
    }

    private static void AddNewGame(ScriptedRng rng, bool enemyAtLast = false) {
        var positions = new[] { (0, 0), (7, 0), (14, 14) };
        for (int i = 0; i < positions.Length; i++) {
            rng.Add(0, 15, positions[i].Item1);
            rng.Add(0, 15, positions[i].Item2);
            rng.Add(1, 5, i == 0 ? 3 : 2);
            if (i == 0) {
                rng.Add(1, 3, 1);
                rng.Add(1, 5, 4);
                rng.Add(1, 5, 3); // Capacity 50.
            }
        }
        rng.Add(1, 4, 2);
        rng.Add(1, 4, 3);
        rng.Add(1, 11, 1);
        rng.Add(1, 6, 1);
        rng.Add(1, 6, 1);
        rng.Add(1, 6, enemyAtLast ? 5 : 1);
    }

    private static (GameShell Shell, Game Original, ScriptedRng Rng, ScriptedIO IO, SpyRenderer Renderer) NewShell(
        string[] input, int money = 30, int capacity = 0, bool fog = false) {
        var rng = new ScriptedRng();
        var state = new GameState(new GameConfig { NumStars = 3, StartingMoney = money, Capacity = capacity }) {
            FogEnabled = fog
        };
        var game = new Game(rng, state);
        var io = new ScriptedIO(input);
        var renderer = new SpyRenderer();
        return (new GameShell(game, io, rng, renderer), game, rng, io, renderer);
    }

    [Theory]
    [InlineData("C2S2R")]
    [InlineData("c2s2r")]
    [InlineData("C 2S 2R")]
    [InlineData("C S S R R")]
    public void CombinedPurchases_UseCoreOperations_AndReturnToDebugShell(string purchases) {
        var (shell, original, rng, io, renderer) = NewShell(new[] { "newgame", " b ", purchases, "map", "tick", "help", "map", "quit" });
        AddNewGame(rng);
        Assert.Equal(0, shell.Run());
        var game = shell.CurrentGame;
        Assert.NotSame(original, game);
        Assert.NotSame(original.State, game.State);
        var fleet = game.State.Players[1].Tf[1];
        Assert.Equal(1, fleet.Cruisers);
        Assert.Equal(2, fleet.Scouts);
        Assert.Equal(0, fleet.BattleStars);
        Assert.Equal(35, fleet.Transports);
        Assert.Equal(1, game.State.PlayerStartingStar);
        Assert.Equal(2, game.State.Players[1].RangeResearchProgress);
        Assert.Equal(PlayerStartupStage.Completed, game.State.PlayerStartupStage);
        Assert.Equal(0, game.State.PlayerStartupBalance);
        Assert.Equal(1, game.Turn);
        Assert.Same(game.State, renderer.LastState);
        Assert.Equal(2, renderer.CallCount);
        Assert.Same(renderer.States[0], renderer.States[1]);
        Assert.Contains("Startup money remaining: 30", io.Output);
        Assert.Contains("Startup money remaining: 0", io.Output);
        Assert.Contains("Classic startup complete at star B", io.Output);
        Assert.Contains("Fleet 1: transports=35 cruisers=1 scouts=2 battlestars=0", io.Output);
        Assert.Contains("Range: 5 (research 2)", io.Output);
        Assert.Contains("Advanced 1 tick(s). Turn=1", io.Output);
        Assert.Contains("Commands: newgame", io.Output);
        Assert.Equal(18, rng.Calls);
        rng.AssertExhausted();
    }

    [Theory]
    [InlineData("W", 30, 3, 30, 1, 0, 5, 0)]
    [InlineData("V", 30, 3, 0, 1, 30, 5, 0)]
    [InlineData("R", 30, 3, 0, 1, 0, 6, 10)]
    public void ResearchTokens_ShowLevelsAndProgress(string token, int money,
        int weapons, int weaponsProgress, int velocity, int velocityProgress, int range, int rangeProgress) {
        var (shell, _, rng, io, _) = NewShell(new[] { "newgame", "C", $"{money}{token}", "quit" }, money: money);
        AddNewGame(rng);
        Assert.Equal(0, shell.Run());
        var player = shell.CurrentGame.State.Players[1];
        Assert.Equal((weapons, weaponsProgress), (player.Weapons, player.WeaponsResearchProgress));
        Assert.Equal((velocity, velocityProgress), (player.Velocity, player.VelocityResearchProgress));
        Assert.Equal((range, rangeProgress), (player.Range, player.RangeResearchProgress));
        Assert.Contains($"Weapons: {weapons} (research {weaponsProgress})", io.Output);
        Assert.Contains($"Velocity: {velocity} (research {velocityProgress})", io.Output);
        Assert.Contains($"Range: {range} (research {rangeProgress})", io.Output);
        rng.AssertExhausted();
    }

    [Fact]
    public void BattlestarToken_UsesCorePrice70() {
        var (shell, _, rng, _, _) = NewShell(new[] { "newgame", "B", "B", "quit" }, money: 70);
        AddNewGame(rng);
        Assert.Equal(0, shell.Run());
        Assert.Equal(1, shell.CurrentGame.State.Players[1].Tf[1].BattleStars);
        Assert.Equal(0, shell.CurrentGame.State.PlayerStartupBalance);
        rng.AssertExhausted();
    }

    [Fact]
    public void InvalidChoicesAndPurchases_DoNotEndSessionOrSpendMoney() {
        var (shell, _, rng, io, _) = NewShell(new[] {
            "newgame", "0", "Z", "B", "", "H", "2C X -1S 2147483647B 999999999999999999R", "5S", "quit"
        });
        AddNewGame(rng);
        Assert.Equal(0, shell.Run());
        var fleet = shell.CurrentGame.State.Players[1].Tf[1];
        Assert.Equal(5, fleet.Scouts);
        Assert.Equal(0, fleet.Cruisers);
        Assert.Equal(0, fleet.BattleStars);
        Assert.Equal(0, shell.CurrentGame.State.Players[1].RangeResearchProgress);
        Assert.Contains("Invalid starting star", io.Output);
        Assert.Contains("Insufficient funds for 2C", io.Output);
        Assert.Contains("Invalid purchase token: X", io.Output);
        Assert.Contains("Invalid purchase: -1S", io.Output);
        Assert.Contains("quantity: too large", io.Output);
        Assert.Contains("Invalid purchase quantity.", io.Output);
        Assert.Equal(PlayerStartupStage.Completed, shell.CurrentGame.State.PlayerStartupStage);
        rng.AssertExhausted();
    }

    [Theory]
    [InlineData("2 S")]
    [InlineData("16")]
    public void MalformedQuantity_IsReportedAndNextLineStillWorks(string malformed) {
        var (shell, _, rng, io, _) = NewShell(new[] { "newgame", "B", malformed, "2S16R", "2R", "quit" });
        AddNewGame(rng);
        Assert.Equal(0, shell.Run());
        Assert.Contains("Invalid purchase", io.Output);
        Assert.Equal(2, shell.CurrentGame.State.Players[1].Tf[1].Scouts);
        Assert.Equal(18, shell.CurrentGame.State.Players[1].RangeResearchProgress);
        Assert.Equal(0, shell.CurrentGame.State.PlayerStartupBalance);
        Assert.Contains("Startup money remaining: 2", io.Output);
    }

    [Fact]
    public void EveryTokenOnTheLine_IsProcessedEvenAfterBudgetExhaustion() {
        var (shell, _, rng, io, _) = NewShell(new[] { "newgame", "B", "C2S2R1S", "quit" });
        AddNewGame(rng);
        Assert.Equal(0, shell.Run());
        Assert.Equal(2, shell.CurrentGame.State.Players[1].Tf[1].Scouts);
        Assert.Contains("Insufficient funds for 1S", io.Output);
    }

    [Fact]
    public void SelectionMap_ShowsLettersUnderFog_WithoutLeakingEnemyLocationOrDiscoveringStars() {
        string? firstMap = null;
        foreach (bool enemyAtLast in new[] { false, true }) {
            var (shell, _, rng, io, _) = NewShell(new[] { "newgame", "B", "5S", "quit" }, fog: true);
            AddNewGame(rng, enemyAtLast);
            Assert.Equal(0, shell.Run());
            var output = io.Output;
            int start = output.IndexOf("Starting-star map", StringComparison.Ordinal);
            int end = output.IndexOf("Start at star (", start, StringComparison.Ordinal);
            string map = output[start..end];
            Assert.Contains(" 0   A", map);
            Assert.Contains("  B", map);
            Assert.Contains("  C", map);
            Assert.DoesNotContain("enemy", map, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("?", map);
            if (firstMap != null) Assert.Equal(firstMap, map);
            firstMap = map;
            Assert.True(shell.CurrentGame.State.FogEnabled);
            Assert.Null(shell.CurrentGame.State.Visible);
            Assert.False(shell.CurrentGame.State.RevealStars);
            for (int i = 0; i < 3; i++) Assert.Equal(i == 1, shell.CurrentGame.State.Stars[i].ExploredBy[1]);
        }
    }

    [Fact]
    public void AutomaticCapacitySelection_UsesCoreSampling_WithoutReadingAChoice() {
        var (shell, _, rng, io, _) = NewShell(new[] { "newgame", "5S", "tick", "map", "quit" }, capacity: 50);
        AddNewGame(rng);
        rng.Add(1, 4, 2);
        rng.Add(1, 4, 1);
        Assert.Equal(0, shell.Run());
        Assert.DoesNotContain("Start at star (", io.Output);
        Assert.Contains("Starting star: A", io.Output);
        Assert.Contains("Combat is not implemented", io.Output);
        Assert.Contains("before advancement", io.Output);
        Assert.True(shell.CurrentGame.State.PendingSharedStartEncounter);
        Assert.Equal(0, shell.CurrentGame.Turn);
        Assert.Equal(20, rng.Calls);
        rng.AssertExhausted();
    }

    [Fact]
    public void ZeroMoney_FinalizesWithoutReadingAPurchase() {
        var (shell, _, rng, io, renderer) = NewShell(new[] { "newgame", "B", "map", "quit" }, money: 0);
        AddNewGame(rng);
        Assert.Equal(0, shell.Run());
        Assert.DoesNotContain("Purchases:", io.Output);
        Assert.Equal(0, shell.CurrentGame.State.Players[1].Tf[1].Scouts);
        Assert.Equal(1, renderer.CallCount);
        Assert.Equal(PlayerStartupStage.Completed, shell.CurrentGame.State.PlayerStartupStage);
    }

    [Fact]
    public void Newgame_CreatesFreshStateEachTime_AndDebugCommandsRemainAvailable() {
        var (shell, original, rng, io, renderer) = NewShell(new[] {
            "newgame", "B", "5S", "map", "genstars", "place 2 26 4", "tf list",
            "newgame", "C", "30R", "map", "quit"
        });
        original.State.Stars[0].X = 10;
        original.State.Stars[0].Y = 10;
        original.State.FleetsSys.Place(1, 1, 0, 99);
        original.Tick(3);
        AddNewGame(rng);
        AddNewGame(rng);
        Assert.Equal(0, shell.Run());
        Assert.Equal(2, renderer.CallCount);
        var first = renderer.States[0];
        var second = renderer.States[1];
        Assert.NotSame(first, second);
        Assert.NotSame(original.State, first);
        Assert.Equal(99, original.State.Players[1].Tf[1].Scouts);
        Assert.Equal(3, original.Turn);
        Assert.Equal(5, first.Players[1].Tf[1].Scouts);
        Assert.Equal(4, first.Players[1].Tf[26].Scouts);
        Assert.Equal(0, second.Players[1].Tf[1].Scouts);
        Assert.Equal(0, second.Players[1].Tf[26].Scouts);
        Assert.Equal(6, second.Players[1].Range);
        Assert.Equal(10, second.Players[1].RangeResearchProgress);
        Assert.Equal(0, shell.CurrentGame.Turn);
        Assert.Contains("Cannot regenerate the galaxy", io.Output);
        Assert.Contains("Placed TF 26", io.Output);
        Assert.Contains("TF 26:", io.Output);
        Assert.Equal(36, rng.Calls);
        rng.AssertExhausted();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EofDuringStartup_ExitsWithoutFinalizingOrReplacingCurrentGame(bool duringPurchases) {
        var (shell, original, rng, io, _) = NewShell(duringPurchases ? new[] { "newgame", "B", "C" } : new[] { "newgame" });
        AddNewGame(rng);
        Assert.Equal(0, shell.Run());
        Assert.Same(original, shell.CurrentGame);
        Assert.DoesNotContain("Classic startup complete", io.Output);
        rng.AssertExhausted();
    }

    [Fact]
    public void QuitDuringStartup_ExitsWithoutTreatingRemainingBudgetAsSpent() {
        var (shell, original, rng, io, _) = NewShell(new[] { "newgame", "B", "quit" });
        AddNewGame(rng);
        Assert.Equal(0, shell.Run());
        Assert.Same(original, shell.CurrentGame);
        Assert.DoesNotContain("Classic startup complete", io.Output);
        Assert.Contains("Startup money remaining: 30", io.Output);
    }

    public static IEnumerable<object[]> StarLetters() {
        for (int i = 0; i < 26; i++) yield return new object[] { ((char)('A' + i)).ToString(), i };
        yield return new object[] { "q", 16 };
    }

    [Theory]
    [MemberData(nameof(StarLetters))]
    public void EveryStarLetter_IsSelectable_IncludingTheShellQuitAlias(string letter, int index) {
        var rng = new CRandRng(1);
        var game = new Game(rng, new GameState());
        var io = new ScriptedIO("newgame", letter, "5S", "quit");
        var shell = new GameShell(game, io, rng, new SpyRenderer());
        Assert.Equal(0, shell.Run());
        Assert.Equal(index, shell.CurrentGame.State.PlayerStartingStar);
        Assert.Equal(PlayerStartupStage.Completed, shell.CurrentGame.State.PlayerStartupStage);
        Assert.Contains($"Classic startup complete at star {letter.ToUpperInvariant()}", io.Output);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(27)]
    public void InvalidClassicStarCount_DoesNotConsumeRngOrReplaceGame(int count) {
        var (shell, original, rng, io, _) = NewShell(new[] { "newgame", "help", "quit" });
        original.State.Config.NumStars = count;
        Assert.Equal(0, shell.Run());
        Assert.Same(original, shell.CurrentGame);
        Assert.Equal(0, rng.Calls);
        Assert.Contains("Classic startup requires 1-26 stars", io.Output);
        Assert.Contains("Commands: newgame", io.Output);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void AbortedStartup_LeavesPreviousGameUsable(bool purchasing, bool quit) {
        var input = new List<string> { "newgame" };
        if (purchasing) input.AddRange(new[] { "B", "C" });
        if (quit) input.Add("quit");
        var (shell, original, rng, io, renderer) = NewShell(input.ToArray());
        original.State.Stars[0].X = 1;
        original.State.Stars[0].Y = 1;
        original.State.Stars[1].X = 2;
        original.State.Stars[1].Y = 1;
        original.State.FleetsSys.Place(1, 1, 0, 9);
        AddNewGame(rng);
        Assert.Equal(0, shell.Run());
        Assert.Same(original, shell.CurrentGame);
        Assert.Equal(9, original.State.Players[1].Tf[1].Scouts);
        Assert.Equal(PlayerStartupStage.NotStarted, original.State.PlayerStartupStage);
        Assert.DoesNotContain("Classic startup complete", io.Output);

        // Reuse the same shell after quit/EOF with new input; no reset is necessary.
        io.Enqueue("map", "move 1 1", "tick", "tf list", "quit");
        Assert.Equal(0, shell.Run());
        Assert.Same(original, shell.CurrentGame);
        Assert.Same(original.State, renderer.LastState);
        Assert.Equal(1, original.Turn);
        Assert.Equal((2, 1), (original.State.Players[1].Tf[1].X, original.State.Players[1].Tf[1].Y));
        Assert.Equal(9, original.State.Players[1].Tf[1].Scouts);
        Assert.Contains("Advanced 1 tick(s). Turn=1", io.Output);
        rng.AssertExhausted();
    }

    [Fact]
    public void SeedCommands_ReproduceNewgameAndPreserveTheExpectedRngStream() {
        var rng = new CRandRng(9);
        var io = new ScriptedIO("seed 123", "newgame", "B", "5S", "map",
            "seed 123", "newgame", "B", "5S", "map", "quit");
        var renderer = new SpyRenderer();
        var shell = new GameShell(new Game(rng, new GameState()), io, rng, renderer);
        Assert.Equal(0, shell.Run());
        Assert.Equal(2, renderer.CallCount);
        var first = renderer.States[0];
        var second = renderer.States[1];
        Assert.NotSame(first, second);
        Assert.Equal(first.Stars.Select(s => (s.X, s.Y)), second.Stars.Select(s => (s.X, s.Y)));
        Assert.Equal(first.Stars.SelectMany(s => s.Planets).Select(p => (p.Number, p.Capacity)),
            second.Stars.SelectMany(s => s.Planets).Select(p => (p.Number, p.Capacity)));
        Assert.Equal(first.Players[0].Tf[1].Destination, second.Players[0].Tf[1].Destination);
        Assert.Equal(first.EnemyResearchField, second.EnemyResearchField);

        var referenceRng = new CRandRng(123);
        var reference = new Game(referenceRng, new GameState());
        reference.GenerateStars();
        reference.InitializeClassicPlayers();
        reference.InitializeClassicEnemy();
        Assert.Equal(reference.State.Stars.Select(s => (s.X, s.Y)), second.Stars.Select(s => (s.X, s.Y)));
        Assert.Equal(referenceRng.Next(1, 1001), rng.Next(1, 1001));
    }
}
