// File: Conquest.Core/Game.StarGeneration.cs
// Purpose: 0-based star placement with no overlaps.

using System;
using System.Collections.Generic;
using Conquest.Core.Model;

namespace Conquest.Core {
	public partial class Game {
        /// <summary>
        /// Places exactly state.Config.NumStars stars in [0..Width) x [0..Height), no overlaps.
        /// Assumes state.Stars list exists and Board is allocated [Width,Height].
        /// </summary>
        private void GenerateStars(IRng rng, Conquest.Core.Model.GameState state) {
            var cfg = state.Config;

            if (cfg.Width <= 0 || cfg.Height <= 0)
                throw new InvalidOperationException("Board dimensions must be positive.");

            var capacity = (long)cfg.Width * cfg.Height;
            if (cfg.NumStars < 0 || cfg.NumStars > capacity)
                throw new InvalidOperationException("NumStars must be between 0 and Width*Height.");

            // Ensure Stars list has exactly NumStars elements.
            if (state.Stars.Count != cfg.NumStars) {
                state.Stars.Clear( );
                for (int i = 0; i < cfg.NumStars; i++)
                    state.Stars.Add(new Star( ));
            }

            // Clear board star refs / marks.
            for (int y = 0; y < cfg.Height; y++)
                for (int x = 0; x < cfg.Width; x++) {
                    var cell = state.Board[x, y];
                    cell.StarRef = null;
                    cell.Star = -1; // legacy index marker
                }

            var used = new HashSet<(int x, int y)>( );

            for (int i = 0; i < cfg.NumStars; i++) {
                int x, y;
                do {
                    x = rng.Next(0, cfg.Width);   // 0..Width-1
                    y = rng.Next(0, cfg.Height);  // 0..Height-1
                } while (!used.Add((x, y)));

                var s = state.Stars[i];
                s.X = x; s.Y = y;

                var cell = state.Board[x, y];
                cell.StarRef = s;
                cell.Star = i; // keep legacy/indexy field in sync
            }
        }
    }
}
