// File: Conquest.Tests/StarGenTests.cs
// Purpose: Lock 0-based invariants with count, bounds, uniqueness, CLI smoke, and a light property test.

using Conquest.Core;
using Conquest.Core.Model;
using Conquest.Core.Random;
using Conquest.Tests.TestHelpers;
using System;
using System.Linq;
using Xunit;

namespace Conquest.Tests {
    public class StarGenTests {
        private static Game MakeGame(out GameState state) {
            var io = new DummyIO(new[] { "seed 1", "genstars", "map", "quit" });
            var rng = new CRandRng( );
            state = new GameState(GameConfig.Default);
            var renderer = new SpyRenderer( );
            var game = new Game(io, rng, state, renderer);
            return game;
        }

        [Fact]
        public void 
            Cli_Smoke_Sequence_Completes( ) {
            var game = MakeGame(out var state);

            var rc = game.Run( );
            Assert.Equal(0, rc);

            // Count equals NumStars (no reserved index 0).
            Assert.Equal(state.Config.NumStars, state.Stars.Count);

            // Bounds
            var cfg = state.Config;
            Assert.All(state.Stars, s => {
                Assert.InRange(s.X, 0, cfg.Width - 1);
                Assert.InRange(s.Y, 0, cfg.Height - 1);
            });

            // Uniqueness
            var unique = state.Stars.Select(s => (s.X, s.Y)).Distinct( ).Count( );
            Assert.Equal(state.Stars.Count, unique);
        }

        [Fact]
        public void Stars_Count_Equals_NumStars( ) {
            var game = MakeGame(out var state);
            game.Run( ); // executes seed/genstars/map/quit

            Assert.Equal(state.Config.NumStars, state.Stars.Count);
        }

        [Fact]
        public void Stars_AreWithin_Bounds( ) {
            var game = MakeGame(out var state);
            game.Run( );

            var cfg = state.Config;
            Assert.All(state.Stars, s => {
                Assert.True(0 <= s.X && s.X < cfg.Width, $"X out of range: {s.X}");
                Assert.True(0 <= s.Y && s.Y < cfg.Height, $"Y out of range: {s.Y}");
            });
        }

        [Fact]
        public void Stars_Are_Unique( ) {
            var game = MakeGame(out var state);
            game.Run( );

            var positions = state.Stars.Select(s => (s.X, s.Y)).ToArray( );
            var distinct = positions.Distinct( ).Count( );
            Assert.Equal(positions.Length, distinct);
        }

        [Fact]
        public void StarGen_Property_Light_Fuzz( ) {
            // Run multiple seeds to catch flakiness. Keep it light for CI speed.
            const int runs = 100;

            for (int seed = 1; seed <= runs; seed++) {
                var io = new DummyIO(new[] { $"seed {seed}", "genstars", "quit" });
                var rng = new CRandRng( );
                var state = new GameState(GameConfig.Default);
                var renderer = new SpyRenderer( );
                var game = new Game(io, rng, state, renderer);

                var rc = game.Run( );
                Assert.Equal(0, rc);

                var cfg = state.Config;

                // Count
                Assert.Equal(cfg.NumStars, state.Stars.Count);

                // Bounds
                Assert.All(state.Stars, s => {
                    Assert.InRange(s.X, 0, cfg.Width - 1);
                    Assert.InRange(s.Y, 0, cfg.Height - 1);
                });

                // Unique
                var uniq = state.Stars.Select(s => (s.X, s.Y)).Distinct( ).Count( );
                Assert.Equal(state.Stars.Count, uniq);
            }
        }
    }
}
