// File: Conquest.Core/Model/GameState.Visibility.cs
// Purpose: Visibility ("fog") support. Keep it minimal & faithful.

using System;

namespace Conquest.Core.Model {
    public partial class GameState {
        // Nullable so we can allocate lazily to the current Config size.
        public bool[,]? Visible { get; private set; }

        // Debug/cheat flag: if true, renderer shows all stars.
        public bool RevealStars { get; set; }

        public void EnsureVisibility( ) {
            var w = Config.Width;
            var h = Config.Height;

            if (Visible == null || Visible.GetLength(0) != w || Visible.GetLength(1) != h) {
                Visible = new bool[w, h];
            }
        }

        public void Discover(int x, int y) {
            EnsureVisibility( );
            if ((uint)x < (uint)Config.Width && (uint)y < (uint)Config.Height)
                Visible![x, y] = true;
        }

        public void DiscoverRadius(int x, int y, int radius = 0) {
            EnsureVisibility( );
            radius = Math.Max(0, radius);

            int x0 = Math.Max(0, x - radius);
            int x1 = Math.Min(Config.Width - 1, x + radius);
            int y0 = Math.Max(0, y - radius);
            int y1 = Math.Min(Config.Height - 1, y + radius);

            for (int yy = y0; yy <= y1; yy++)
                for (int xx = x0; xx <= x1; xx++)
                    Visible![xx, yy] = true;
        }

        public void RevealAll( ) {
            EnsureVisibility( );
            for (int y = 0; y < Config.Height; y++)
                for (int x = 0; x < Config.Width; x++)
                    Visible![x, y] = true;
        }
    }
}
