using Conquest.Core;

namespace Conquest.CLI;
class Program {
    static int Main(string[] args) {
        var io = new ConsoleIO( );
//        var rng = new SystemRng( );
        var rng = new CRandRng( );


        var config = GameConfig.FromArgs(args);
        if (config.Seed is int s) rng.Reseed(s);

        var state = new GameState(config);
        return new Game(io, rng, state).Run( );
    }
}
