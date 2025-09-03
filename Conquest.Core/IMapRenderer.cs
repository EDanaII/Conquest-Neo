// File: Conquest.Core/Rendering/IMapRenderer.cs

using Conquest.Core.Model;

namespace Conquest.Core {
    /// <summary>
    /// Renders the current map/state to an IGameIO.
    /// Keep it stateless; all inputs come via parameters.
    /// </summary>
    public interface IMapRenderer {
        void Render(IGameIO io, GameState state);
    }
}
