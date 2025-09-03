using System.Text;
using Conquest.Core.Model;

namespace Conquest.Core {
    public sealed class MapRenderer : IMapRenderer {
        public void Render(IGameIO io, GameState state) {
            var cfg = state.Config;
            state.EnsureVisibility( );

            io.WriteLine($"Map {cfg.Width}x{cfg.Height} (reveal={(state.RevealStars ? "on" : "off")})");

            var sb = new StringBuilder(cfg.Width);
            for (int y = 0; y < cfg.Height; y++) {
                sb.Clear( );
                for (int x = 0; x < cfg.Width; x++) {
                    var cell = state.Board[x, y];
                    bool showStar = state.RevealStars || state.Visible![x, y];
                    char ch = (showStar && cell.Star >= 0) ? '*' : '.';
                    sb.Append(ch);
                }
                io.WriteLine(sb.ToString( ));
            }
        }
    }
}
