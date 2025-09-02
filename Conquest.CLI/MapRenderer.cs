using Conquest.Core;
using Conquest.Core.Models;

namespace Conquest.CLI;

public sealed class MapRenderer : IMapRenderer {
    public void Render(IGameIO io, GameState state) {
        var w = state.Config.Width;
        var h = state.Config.Height;

        // Top Y axis
        io.Write("   "); for (int x = 1; x <= w; x++) io.Write(x % 10 == 0 ? "0" : (x % 10).ToString( )); io.WriteLine( );

        for (int y = h; y >= 1; y--) {
            io.Write(y.ToString( ).PadLeft(2) + " ");
            for (int x = 1; x <= w; x++) {
                var cell = state.Board[x, y];
                // show star first letter if explored by player; '?' if not; '.' if no star
                if (cell.StarRef is Star s) {
                    var seen = !state.FogEnabled || s.ExploredBy[1]; // team 1 = player
                    io.Write(seen ? s.Name[0].ToString( ) : "?");
                }
                else {
                    io.Write(".");
                }
            }
            io.WriteLine(" " + y.ToString( ).PadLeft(2));
        }

        // Bottom X axis
        io.Write("   "); for (int x = 1; x <= w; x++) io.Write(x % 10 == 0 ? "0" : (x % 10).ToString( )); io.WriteLine( );
    }
}
