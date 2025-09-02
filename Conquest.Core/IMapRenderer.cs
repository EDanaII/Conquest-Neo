namespace Conquest.Core;

public interface IMapRenderer {
    void Render(IGameIO io, GameState state);
}
