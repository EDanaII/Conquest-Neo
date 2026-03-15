using Conquest.Core.Model;
using System;

namespace Conquest.Core {
    public partial class Game {
        private readonly IRng _rng;

        public Game(IRng rng, GameState state) {
            _rng = rng ?? throw new ArgumentNullException(nameof(rng));
            State = state ?? throw new ArgumentNullException(nameof(state));
        }

        public GameState State { get; }

        public void GenerateStars( ) {
            GenerateStars(_rng, State);
        }
    }
}