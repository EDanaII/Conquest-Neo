using Conquest.Core;

namespace Conquest.Tests.TestHelpers;

/// <summary>Minimal spy to verify the map renderer was invoked.</summary>
public sealed class SpyRenderer : IMapRenderer {
    public int CallCount { get; private set; }
    public GameState? LastState { get; private set; }

    public void Render(IGameIO io, GameState state) {
        CallCount++;
        LastState = state;
        // no text checks here; DummyIO already records it if needed
    }
}
