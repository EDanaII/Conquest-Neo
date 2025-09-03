using Conquest.Core;
using Conquest.Core.Models;

namespace Conquest.CLI;

public sealed class MapRenderer : IMapRenderer {
    public void Render(IGameIO io, GameState state) {
        var w = state.Config.Width;
        var h = state.Config.Height;

        // Top X axis (labels shown 1..W, internal indices 0..W-1)
        io.Write("   ");
        for (int x = 0; x < w; x++)
            io.Write(((x + 1) % 10 == 0) ? "0" : ((x + 1) % 10).ToString( ));
        io.WriteLine( );

        // Rows (internal 0..h-1, print labels as 1..h)
        for (int y = h - 1; y >= 0; y--) {
            io.Write((y + 1).ToString( ).PadLeft(2) + " ");
            for (int x = 0; x < w; x++) {
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
            io.WriteLine(" " + (y + 1).ToString( ).PadLeft(2));
        }

        // Bottom X axis (labels shown 1..W)
        io.Write("   ");
        for (int x = 0; x < w; x++)
            io.Write(((x + 1) % 10 == 0) ? "0" : ((x + 1) % 10).ToString( ));
        io.WriteLine( );
    }
}
