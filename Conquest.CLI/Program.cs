using Conquest.Core;
using Conquest.Core.Model;
using System.Security.Cryptography;

namespace Conquest.CLI;

class Program {
    static int Main(string[] args) {
        var io = new ConsoleIO( );
        var config = GameConfig.FromArgs(args);
        // Keep Core's deterministic generator, but choose a fresh launch seed
        // unless the player explicitly requests a reproducible sequence.
        var rng = new CRandRng(config.Seed ?? RandomNumberGenerator.GetInt32(1, int.MaxValue));

        var state = new GameState(config);
        var renderer = new MapRenderer( ); // CLI implementation of IMapRenderer

        var game = new Game(rng, state);
        var shell = new GameShell(game, io, rng, renderer);

        if (args.Contains("--debug-shell")) return shell.Run();
        if (config.NumStars is < 1 or > 26) {
            io.WriteLine("Classic mode requires 1-26 stars.");
            return 1;
        }
        game.GenerateStars();
        game.InitializeClassicPlayers();
        game.InitializeClassicEnemy();
        var terminal = new ClassicConsoleTerminal();
        if (!new ClassicStartupFlow(io, terminal).Run(game)) return 0;
        return new ClassicGameLoop(game, terminal).Run();
    }
}
