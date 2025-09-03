using Conquest.Core;
using Conquest.Core.Model;
using Conquest.Core.Random;

namespace Conquest.CLI;

class Program {
    static int Main(string[] args) {
        var io = new ConsoleIO( );
        var rng = new CRandRng( ); // parity RNG in Core.Random

        var config = GameConfig.FromArgs(args);
        // Accept int OR uint, depending on how GameConfig.Seed is defined
        if (config.Seed is int si) {
            rng.Seed(si); // C-parity path (used by StarGenerator)
        }

        var state = new GameState(config);
        var renderer = new MapRenderer( ); // CLI implementation of IMapRenderer

        return new Game(io, rng, state, renderer).Run( );
    }
}
