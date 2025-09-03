// File: Conquest.Tests/TestHelpers/SpyRenderer.cs
// Test double for IMapRenderer with call tracking.
// Namespace matches your 'using Conquest.Tests.TestHelpers;' in tests.

using System.Collections.Generic;
using Conquest.Core;
using Conquest.Core.Model;

namespace Conquest.Tests.TestHelpers {
    public sealed class SpyRenderer : IMapRenderer {
        /// <summary>Total times Render(...) was called.</summary>
        public int CallCount { get; private set; }

        /// <summary>The most recent GameState passed to Render(...).</summary>
        public GameState? LastState { get; private set; }

        /// <summary>All GameState instances seen, in call order (optional, handy for deeper tests).</summary>
        public List<GameState> States { get; } = new List<GameState>( );

        public void Render(IGameIO io, GameState state) {
            CallCount++;
            LastState = state;
            States.Add(state);

            // No output required for this spy; if you want, you can emit a tiny marker:
            // io.WriteLine("[SpyRenderer] Render invoked");
        }
    }
}
