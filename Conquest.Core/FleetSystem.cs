using System;
using Conquest.Core.Model;

namespace Conquest.Core {
    /// <summary>
    /// Minimal fleet placement and movement over the existing 0-based board.
    /// Uses Player.Tf[1..26] and TaskForce.Destination/ETA fields already in your model.
    /// </summary>
    public sealed class FleetSystem {
        private readonly GameState _state;

        public FleetSystem(GameState state) { _state = state ?? throw new ArgumentNullException(nameof(state)); }

        /// <summary>
        /// Place (or re-place) a task force at a given star for a team (0 or 1).
        /// </summary>
        public TaskForce Place(int team, int tfIndex, int starIndex, int scouts = 3) {
            if (team is not (0 or 1)) throw new ArgumentOutOfRangeException(nameof(team));
            if (tfIndex < 1 || tfIndex >= _state.Players[team].Tf.Length) throw new ArgumentOutOfRangeException(nameof(tfIndex));
            if (starIndex < 0 || starIndex >= _state.Stars.Count) throw new ArgumentOutOfRangeException(nameof(starIndex));

            var player = _state.Players[team];
            var tf = player.Tf[tfIndex];
            var star = _state.Stars[starIndex];

            // Clear previous board cell if any
            if ((uint)tf.X < (uint)_state.Config.Width && (uint)tf.Y < (uint)_state.Config.Height) {
                var oldCell = _state.Board[tf.X, tf.Y];
                if (oldCell.TaskForces.Length > team) oldCell.TaskForces[team] = null!;
                if (oldCell.Tf == tfIndex) oldCell.Tf = -1;
            }

            tf.X = star.X;
            tf.Y = star.Y;
            tf.Destination = null;
            tf.ETA = 0;
            tf.EtaOriginal = 0;
            tf.Scouts = scouts;

            var cell = _state.Board[tf.X, tf.Y];
            cell.TaskForces[team] = tf;
            cell.Tf = tfIndex;

            return tf;
        }

        /// <summary>
        /// Plot a course from the TF's current position to a star (Manhattan movement).
        /// </summary>
        public void SetCourse(int team, int tfIndex, int starIndex) {
            if (team is not (0 or 1)) throw new ArgumentOutOfRangeException(nameof(team));
            if (tfIndex < 1 || tfIndex >= _state.Players[team].Tf.Length) throw new ArgumentOutOfRangeException(nameof(tfIndex));
            if (starIndex < 0 || starIndex >= _state.Stars.Count) throw new ArgumentOutOfRangeException(nameof(starIndex));

            var tf = _state.Players[team].Tf[tfIndex];
            var dest = _state.Stars[starIndex];

            var dist = Math.Abs(dest.X - tf.X) + Math.Abs(dest.Y - tf.Y);
            tf.Destination = starIndex;
            tf.EtaOriginal = tf.ETA = dist;
        }

        /// <summary>
        /// Advance all task forces one step toward their destinations; update board occupancy.
        /// </summary>
        public void Tick( ) {
            for (int team = 0; team <= 1; team++) {
                var p = _state.Players[team];
                for (int i = 1; i < p.Tf.Length; i++) {
                    var tf = p.Tf[i];
                    if (tf.Destination is not int destId || tf.ETA <= 0) continue;

                    // Clear old cell occupancy before moving
                    if ((uint)tf.X < (uint)_state.Config.Width && (uint)tf.Y < (uint)_state.Config.Height) {
                        var oldCell = _state.Board[tf.X, tf.Y];
                        if (oldCell.TaskForces.Length > team) oldCell.TaskForces[team] = null!;
                        if (oldCell.Tf == i) oldCell.Tf = -1;
                    }

                    var dest = _state.Stars[destId];

                    if (tf.X != dest.X) tf.X += Math.Sign(dest.X - tf.X);
                    else if (tf.Y != dest.Y) tf.Y += Math.Sign(dest.Y - tf.Y);

                    tf.ETA--;

                    // Occupy new cell
                    var cell = _state.Board[tf.X, tf.Y];
                    cell.TaskForces[team] = tf;
                    cell.Tf = i;

                    // Arrival
                    if (tf.ETA <= 0) {
                        tf.ETA = 0;
                        tf.Destination = null;
                    }
                }
            }

            // (Optional) TODO: detect overlaps at same (x,y) between teams and mark an encounter flag.
        }
    }
}
