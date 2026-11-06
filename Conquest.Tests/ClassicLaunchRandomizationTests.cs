using System.Diagnostics;
using Conquest.CLI;
using Conquest.Core;
using Conquest.Core.Model;

namespace Conquest.Tests;

public sealed class ClassicLaunchRandomizationTests {
    private static async Task<string> Launch(string input, params string[] args) {
        var info = new ProcessStartInfo("dotnet") {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true
        };
        info.ArgumentList.Add(typeof(GameShell).Assembly.Location);
        foreach (string arg in args) info.ArgumentList.Add(arg);
        using var process = Process.Start(info)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        try {
            await process.StandardInput.WriteAsync(input);
            process.StandardInput.Close();
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
            Assert.Equal(0, process.ExitCode); Assert.Equal("", await error);
            return await output;
        }
        finally { if (!process.HasExited) process.Kill(entireProcessTree: true); }
    }

    private static string[][] SelectionMaps(string output) {
        var lines = output.Split(Environment.NewLine);
        return Enumerable.Range(0, lines.Length).Where(i => lines[i].StartsWith("Starting-star map"))
            .Select(i => lines.Skip(i + 1).Take(17).ToArray()).ToArray();
    }
    private static string Signature(IEnumerable<string> map) => string.Join("\n", map);

    [Fact]
    public async Task FreshUnseededLaunchesDoNotAllReuseOneGalaxy() {
        var outputs = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Launch("quit\n")));
        // Independent random seeds can coincide; reject a constant launch path
        // across four full galaxies rather than asserting universal uniqueness.
        Assert.True(outputs.Select(o => Signature(Assert.Single(SelectionMaps(o)))).Distinct().Count() > 1,
            "Every unseeded CLI process generated the same galaxy.");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(0)]
    [InlineData(-123)]
    public async Task ExplicitSeedLaunchesMatchEachOtherAndExistingCoreSequence(int seed) {
        string first = await Launch("quit\n", "--seed", seed.ToString());
        Assert.Equal(first, await Launch("quit\n", "--seed", seed.ToString()));
        var game = new Game(new CRandRng(seed), new GameState());
        game.GenerateStars(); game.InitializeClassicPlayers(); game.InitializeClassicEnemy();
        Assert.Equal(new ClassicScreen().Render(game).Take(17), Assert.Single(SelectionMaps(first)));
    }

    [Fact]
    public async Task SameProcessNewgameAdvancesStreamAndExplicitSeedReplaysWholeSequence() {
        const string input = "newgame\nB\n5S\nnewgame\nB\n5S\nquit\n";
        var unseeded = SelectionMaps(await Launch(input, "--debug-shell"));
        Assert.Equal(2, unseeded.Length); Assert.NotEqual(Signature(unseeded[0]), Signature(unseeded[1]));
        string seeded = await Launch(input, "--debug-shell", "--seed", "123");
        Assert.Equal(seeded, await Launch(input, "--debug-shell", "--seed", "123"));
        var seededMaps = SelectionMaps(seeded);
        Assert.Equal(2, seededMaps.Length); Assert.NotEqual(Signature(seededMaps[0]), Signature(seededMaps[1]));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LaunchKeepsSameGalaxyFromSelectionThroughPurchasesAndGameplay(bool explicitSeed) {
        // An unseeded B can share the enemy's starting star. Supply combat
        // acknowledgements/continue keys before checking the gameplay frames.
        string output = await Launch("B\nC2S2R\n" + new string('g', 100) + "qyx", explicitSeed ? ["--seed", "1"] : []);
        var lines = output.Split(Environment.NewLine);
        var selection = Assert.Single(SelectionMaps(output));
        // All complete 15x15 frames have the same coordinate/letter cells;
        // visibility and the starting fleet glyph legitimately change.
        var maps = Enumerable.Range(0, lines.Length).Where(i => lines[i].StartsWith("15|"))
            .Select(i => lines.Skip(i).Take(17).ToArray()).ToArray();
        Assert.True(maps.Length > 2);
        string Coordinates(string[] map) => string.Concat(Enumerable.Range(0, 15).SelectMany(row =>
            Enumerable.Range(0, 15).Select(x => map[row][4 + 3 * x])));
        Assert.All(maps, m => Assert.Equal(Coordinates(selection), Coordinates(m)));
        Assert.Contains("******* Initialization *******", output);
        Assert.Contains("---------- Movement ----------", output);
    }

    [Fact]
    public async Task UnseededDenseBoardStillGeneratesAllStarsAndReachesSelection() {
        var map = Assert.Single(SelectionMaps(await Launch("quit\n", "--width", "4", "--height", "4", "--stars", "16")));
        var letters = Enumerable.Range(0, 4).SelectMany(y => Enumerable.Range(0, 4).Select(x => map[14 - y][4 + 3 * x])).ToArray();
        Assert.Equal(16, letters.Distinct().Count()); Assert.All(letters, letter => Assert.InRange(letter, 'A', 'P'));
    }
}
