using System;
using Conquest.Core.Model;

namespace Conquest.Core {
    /// <summary>
    /// Classic fleet lifecycle and movement over the existing 0-based board.
    /// Uses Player.Tf[1..26] and TaskForce.Destination/ETA fields already in your model.
    /// </summary>
    public sealed class FleetSystem {
        private readonly GameState _state;

        public FleetSystem(GameState state) { _state = state ?? throw new ArgumentNullException(nameof(state)); }

        internal int FirstFreePlayerSlot() => Array.FindIndex(_state.Players[1].Tf, 1, f => f.Destination == null);
        internal void CleanupClassicFleet(int team, int index) { RemoveEmpty(_state.Players[team].Tf[index]); RebuildFleetReferences(); }
        internal void CleanupCombatFleets() {
            foreach (var player in _state.Players) foreach (var fleet in player.Tf.Skip(1)) RemoveEmpty(fleet);
            RebuildFleetReferences();
        }
        internal int AllocateCombatFleet(int team, int star) {
            int slot = Array.FindIndex(_state.Players[team].Tf, 1, f => f.Destination == null);
            if (slot < 1) throw new InvalidOperationException("! Taskforce limit reached!");
            var f = _state.Players[team].Tf[slot]; var s = _state.Stars[star];
            f.Transports = f.Scouts = f.Cruisers = f.BattleStars = 0;
            f.X = f.DepartureX = s.X; f.Y = f.DepartureY = s.Y;
            f.Destination = star; f.ETA = f.EtaOriginal = f.Blasting = 0;
            return slot;
        }
        internal void MergeCombatFleet(int team, int parent, int child, int star) {
            _state.Players[team].Tf[parent].Destination = star;
            MergeTaskForces(_state.Players[team].Tf[parent], _state.Players[team].Tf[child]);
            CleanupCombatFleets();
        }

        // taskforce.c:get_tf, shared by N and the production workspace.
        internal int AllocatePlayerFleetAtStar(int starIndex) {
            int slot = FirstFreePlayerSlot();
            if (slot < 1) throw new InvalidOperationException("Classic investment needs a free task-force slot; join fleets before continuing.");
            InitializePlayerSlot(slot, starIndex);
            return slot;
        }
        private void InitializePlayerSlot(int slot, int starIndex) {
            var fleet = _state.Players[1].Tf[slot]; var star = _state.Stars[starIndex];
            fleet.Transports = fleet.Scouts = fleet.Cruisers = fleet.BattleStars = 0;
            fleet.X = fleet.DepartureX = star.X; fleet.Y = fleet.DepartureY = star.Y;
            fleet.Destination = starIndex; fleet.ETA = fleet.EtaOriginal = fleet.Blasting = 0;
            // C get_tf deliberately leaves the old withdrew flag untouched.
        }
        internal void CompleteProductionFleet(int slot, int starIndex) {
            var fleet = _state.Players[1].Tf[slot];
            foreach (var other in _state.Players[1].Tf.Skip(1))
                if (!ReferenceEquals(fleet, other) && other.Destination == starIndex && other.ETA == 0)
                    MergeTaskForces(fleet, other);
            RemoveEmpty(fleet); RebuildFleetReferences();
        }

        // command.c:land validates the fleet before displaying its star report.
        public int ValidatePlayerLandingFleet(int fleetIndex) {
            if (fleetIndex is < 1 or > 26) throw new InvalidOperationException("  !illegal tf");
            var fleet = _state.Players[1].Tf[fleetIndex];
            if (fleet.Destination is not int star || (uint)star >= (uint)_state.Stars.Count)
                throw new InvalidOperationException("  !nonexistent tf");
            if (fleet.ETA != 0) throw new InvalidOperationException("  !tf is not in normal space");
            // Unlike D/J/N, C land() does not reject blasting or withdrawal flags.
            return star;
        }

        public IReadOnlyList<Planet> PlayerLandingPlanets(int fleetIndex) {
            int star = ValidatePlayerLandingFleet(fleetIndex);
            var planets = _state.Stars[star].Planets;
            if (planets.Count == 0) throw new InvalidOperationException("  !no planets at this star");
            // C uses tf_stars: stationary destinations, not enemy display coordinates.
            if (_state.Players[0].Tf.Skip(1).Any(f => f.Destination == star && f.ETA == 0))
                throw new InvalidOperationException("  !enemy ships present");
            return planets;
        }

        public Planet ValidatePlayerLandingPlanet(int fleetIndex, int planetNumber) {
            var planet = PlayerLandingPlanets(fleetIndex).FirstOrDefault(p => p.Number == planetNumber);
            if (planet == null) throw new InvalidOperationException(" !Not a habitable planet");
            if (planet.Owner == Ownership.Enemy || planet.Owner == Ownership.Player && planet.Conquered)
                throw new InvalidOperationException("  !Enemy infested planet  !!");
            return planet;
        }

        /// <summary>Classic L: immediate population/IU gain and transport consumption, without a turn.</summary>
        public void LandPlayerTransports(int fleetIndex, int planetNumber, int transports) {
            var planet = ValidatePlayerLandingPlanet(fleetIndex, planetNumber);
            var fleet = _state.Players[1].Tf[fleetIndex];
            int room = planet.Capacity - planet.Inhabitants;
            if (transports == 0) transports = fleet.Transports;
            if (transports < 1) throw new InvalidOperationException("  !illegal transports");
            if (transports > fleet.Transports)
                throw new InvalidOperationException($"  !only {fleet.Transports,2} transports in tf");
            if (transports > room)
                throw new InvalidOperationException($"  !only room for {room,2} transports");

            planet.Owner = Ownership.Player;
            planet.Inhabitants += transports;
            planet.IndustrialUnits += transports;
            fleet.Transports -= transports;
            RemoveEmpty(fleet);
            RebuildFleetReferences();
        }

        // taskforce.c:make_tf/get_tf. Used before the UI asks for ships so a
        // full fleet table returns immediately, without consuming another line.
        public int ValidatePlayerSplit(int sourceIndex) {
            if (sourceIndex is < 1 or > 26)
                throw new InvalidOperationException("  !illegal tf");
            var source = _state.Players[1].Tf[sourceIndex];
            if (source.ETA != 0 || source.Destination is not int star || (uint)star >= (uint)_state.Stars.Count)
                throw new InvalidOperationException("  !illegal tf");
            if (source.Blasting != 0) throw new InvalidOperationException(" !Tf is blasting a planet");
            for (int i = 1; i < _state.Players[1].Tf.Length; i++)
                if (_state.Players[1].Tf[i].Destination == null) return i;
            return 0;
        }

        /// <summary>Classic N: allocate first free slot, transfer tokens in order, remove empty fleets.</summary>
        public (int Slot, string? Error) SplitPlayerFleet(int sourceIndex, IEnumerable<(char Ship, int Quantity)> ships) {
            ArgumentNullException.ThrowIfNull(ships);
            var orders = ships.ToArray();
            int slot = ValidatePlayerSplit(sourceIndex);
            if (slot == 0) throw new InvalidOperationException("! Taskforce limit reached!");
            var source = _state.Players[1].Tf[sourceIndex];
            var child = _state.Players[1].Tf[slot];
            InitializePlayerSlot(slot, source.Destination!.Value);
            // get_tf does not reset withdrew: retain that slot's original behavior.
            string? error = null;
            if (orders.Length == 0) {
                child.Transports = source.Transports; child.Scouts = source.Scouts;
                child.Cruisers = source.Cruisers; child.BattleStars = source.BattleStars;
                source.Transports = source.Scouts = source.Cruisers = source.BattleStars = 0;
            }
            else {
                foreach (var (ship, quantity) in orders) {
                    if (ship is not ('T' or 'S' or 'C' or 'B')) { error = $"  ! Illegal field {ship}"; continue; }
                    // C integer overflow has no defined result. Reject unrepresentable
                    // parsed quantities without transferring ships for that token.
                    if (quantity < 0) { error = "  !invalid ship quantity"; continue; }
                    switch (ship) {
                        case 'T': {
                            int count = Math.Min(quantity, source.Transports);
                            source.Transports -= count; child.Transports += count; break;
                        }
                        case 'S': {
                            int count = Math.Min(quantity, source.Scouts);
                            source.Scouts -= count; child.Scouts += count; break;
                        }
                        case 'C': {
                            int count = Math.Min(quantity, source.Cruisers);
                            source.Cruisers -= count; child.Cruisers += count; break;
                        }
                        case 'B': {
                            int count = Math.Min(quantity, source.BattleStars);
                            source.BattleStars -= count; child.BattleStars += count; break;
                        }
                    }
                }
            }
            RemoveEmpty(source); RemoveEmpty(child);
            RebuildFleetReferences();
            return (slot, error);
        }

        private static void RemoveEmpty(TaskForce fleet) {
            if (fleet.Transports == 0 && fleet.Scouts == 0 && fleet.Cruisers == 0 && fleet.BattleStars == 0)
                fleet.Destination = null;
        }

        /// <summary>
        /// Classic J, including the original X-only location check. The C Y
        /// comparison is self-comparison; do not silently introduce a new rule.
        /// </summary>
        public string? JoinPlayerFleets(int parentIndex, IEnumerable<int> childIndices) {
            ArgumentNullException.ThrowIfNull(childIndices);
            char parentLetter = (char)('A' + parentIndex - 1);
            if (parentIndex is < 1 or > 26) throw new InvalidOperationException($"  ! illegal tf {parentLetter}");
            var parent = _state.Players[1].Tf[parentIndex];
            if (parent.ETA > 0) throw new InvalidOperationException($"  !tf{parentLetter} is not in normal space");
            if (parent.Destination == null) throw new InvalidOperationException("  !nonexistent tf");
            if (parent.Blasting != 0) throw new InvalidOperationException("  !Tf is blasting a planet");
            string? error = null;
            foreach (int index in childIndices) {
                char letter = (char)('A' + index - 1);
                if (index is < 1 or > 26) { error = $"  !illegal tf {letter}"; continue; }
                if (index == parentIndex) { error = $"!Duplicate tf {letter}"; continue; }
                var child = _state.Players[1].Tf[index];
                if (child.Destination == null) { error = $"!Nonexistant TF{letter}"; continue; }
                if (child.X != parent.X) { error = $"  !tf{letter} bad location"; continue; }
                if (child.ETA != 0) { error = $"  !tf{letter} is not in normal space"; continue; }
                if (child.Blasting != 0) { error = $" !Tf{letter} is blasting a planet"; continue; }
                MergeTaskForces(parent, child);
            }
            RebuildFleetReferences();
            return error;
        }

        private static void MergeTaskForces(TaskForce parent, TaskForce child) {
            parent.Transports += child.Transports; parent.Scouts += child.Scouts;
            parent.Cruisers += child.Cruisers; parent.BattleStars += child.BattleStars;
            // joinsilent marks the child unused but retains its other fields.
            child.Destination = null;
        }

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
                if (ReferenceEquals(oldCell.TaskForces[team], tf)) oldCell.TaskForces[team] = null!;
                if (oldCell.Tf == tfIndex) oldCell.Tf = -1;
            }

            tf.X = star.X;
            tf.Y = star.Y;
            tf.DepartureX = star.X;
            tf.DepartureY = star.Y;
            tf.Destination = starIndex;
            tf.ETA = 0;
            tf.EtaOriginal = 0;
            tf.Scouts = scouts;

            var cell = _state.Board[tf.X, tf.Y];
            cell.TaskForces[team] = tf;
            cell.Tf = tfIndex;

            return tf;
        }

        /// <summary>
        /// Player orders: OriginalSource/command.c send_tf and movement.c set_des.
        /// </summary>
        public void SetCourse(int team, int tfIndex, int starIndex) {
            if (team is not (0 or 1)) throw new ArgumentOutOfRangeException(nameof(team));
            if (tfIndex < 1 || tfIndex >= _state.Players[team].Tf.Length) throw new ArgumentOutOfRangeException(nameof(tfIndex));
            if (starIndex < 0 || starIndex >= _state.Stars.Count) throw new ArgumentOutOfRangeException(nameof(starIndex));

            var tf = _state.Players[team].Tf[tfIndex];
            var dest = _state.Stars[starIndex];
            if (team != 1)
                throw new InvalidOperationException("SetCourse accepts player orders; enemy order selection is not implemented.");
            if (tf.Destination is not int currentStar || (uint)currentStar >= (uint)_state.Stars.Count)
                throw new InvalidOperationException("Task force is not placed.");
            if (tf.ETA != 0 && (tf.ETA != tf.EtaOriginal || tf.Withdrew != 0))
                throw new InvalidOperationException("Task force is not in normal space; its course cannot change after movement begins.");
            if (tf.Blasting != 0)
                throw new InvalidOperationException("Task force is blasting a planet.");
            if (tf.Transports == 0 && tf.Scouts == 0 && tf.Cruisers == 0 && tf.BattleStars == 0)
                throw new InvalidOperationException("Task force has no ships.");
            var player = _state.Players[team];
            // Debug games have not initialized technology; use the original starting values.
            int velocity = player.Velocity == 0 && !_state.ClassicPlayerInitializationCompleted ? Constants.Init.Velocity : player.Velocity;
            int range = player.Range == 0 && !_state.ClassicPlayerInitializationCompleted ? Constants.Init.Range : player.Range;
            if (velocity < 1 || range < 0)
                throw new InvalidOperationException("Movement requires positive velocity and nonnegative range.");
            int departureStar = _state.Stars.FindIndex(s => s.X == tf.X && s.Y == tf.Y);
            if (departureStar < 0)
                throw new InvalidOperationException("Task force must depart from a star.");

            // Reissuing before the first move cancels the old order even if the new
            // order fails its range check. Coordinates still identify the departure star.
            PreparePlayerCourse(tfIndex);
            var origin = _state.Stars[tf.Destination.Value];
            int dx = dest.X - origin.X, dy = dest.Y - origin.Y;
            double distance = Math.Sqrt(dx * dx + dy * dy);
            int roundedDistance = (int)(distance - 0.049 + 1);
            if (roundedDistance > range && (tf.Transports != 0 || tf.Cruisers != 0 || tf.BattleStars != 0))
                throw new InvalidOperationException($"Maximum range is {range}.");
            if (distance < 0.5) return;

            tf.Destination = starIndex;
            tf.EtaOriginal = tf.ETA = ((roundedDistance - 1) / velocity) + 1;
            tf.DepartureX = tf.X;
            tf.DepartureY = tf.Y;
        }

        /// <summary>
        /// command.c:send_tf / movement.c:set_des: cancel an eligible pending
        /// order before reading its replacement. No time or RNG advances.
        /// </summary>
        public bool PreparePlayerCourse(int tfIndex) {
            if (tfIndex < 1 || tfIndex >= _state.Players[1].Tf.Length)
                throw new ArgumentOutOfRangeException(nameof(tfIndex));
            var tf = _state.Players[1].Tf[tfIndex];
            if (tf.Destination is not int currentStar || (uint)currentStar >= (uint)_state.Stars.Count)
                throw new InvalidOperationException(" !nonexistent tf");
            if (tf.ETA != 0 && (tf.ETA != tf.EtaOriginal || tf.Withdrew != 0))
                throw new InvalidOperationException(" !Tf is not in normal space");
            if (tf.Blasting != 0) throw new InvalidOperationException(" !Tf is blasting a planet");
            bool cancelled = tf.ETA != 0;
            if (cancelled) {
                int origin = _state.Stars.FindIndex(s => s.X == tf.X && s.Y == tf.Y);
                if (origin < 0) throw new InvalidOperationException("Task force must depart from a star.");
                tf.Destination = origin;
                tf.ETA = 0;
            }
            tf.Withdrew = 0;
            return cancelled;
        }

        /// <summary>
        /// Advance one movement phase. Orders themselves do not advance time.
        /// </summary>
        public void Tick(IRng? rng = null) {
            _state.EnsureStartupAllowsAdvancement();
            if (rng == null && !_state.Config.SafeExplore && _state.Players[1].Tf.Skip(1).Any(tf =>
                tf.ETA == 1 && tf.Destination is int star && !_state.Stars[star].ExploredBy[1]))
                throw new InvalidOperationException("Unexplored arrivals require the game RNG. Advance with Game.Tick().");
            _state.HasAdvancedTurns = true;
            for (int team = 0; team <= 1; team++) {
                var p = _state.Players[team];
                for (int i = 1; i < p.Tf.Length; i++) {
                    var tf = p.Tf[i];
                    if (tf.Destination is not int destId || tf.ETA <= 0) continue;

                    var dest = _state.Stars[destId];
                    tf.ETA--;
                    // C only interpolates the player's coordinates.
                    if (team == 1) {
                        double ratio = 1.0 - (double)tf.ETA / tf.EtaOriginal;
                        tf.X = tf.DepartureX + ClassicRound(ratio * (dest.X - tf.DepartureX));
                        tf.Y = tf.DepartureY + ClassicRound(ratio * (dest.Y - tf.DepartureY));
                    }
                    if (tf.ETA == 0) {
                        if (team == 1) {
                            if (!dest.ExploredBy[1] && !_state.Config.SafeExplore)
                                Explore(tf, rng!);
                            if (tf.Destination != null) {
                                dest.ExploredBy[1] = true;
                                foreach (var planet in dest.Planets)
                                    planet.PlayerSeenCapacity = planet.Capacity;
                            }
                        }
                        else {
                            dest.ExploredBy[0] = true;
                            foreach (var planet in dest.Planets)
                                planet.EnemySeenOwner = planet.Owner;
                            // movement.c keeps the ARRIVING slot as parent, absorbing
                            // the first stationary fleet. Slot identity affects later AI.
                            var parent = p.Tf.Skip(1).FirstOrDefault(other => !ReferenceEquals(other, tf)
                                && other.Destination == destId && other.ETA == 0);
                            if (parent != null) {
                                MergeTaskForces(tf, parent);
                            }
                            if (_state.Players[1].Tf.Skip(1).Any(f => f.Destination == destId && f.ETA == 0)
                                || dest.Planets.Any(p => p.Owner == Ownership.Player)) _state.EnemyArrivals.Add(destId);
                        }
                    }
                }
            }

            // Rebuild after all fleets move so crossing/colocated fleets cannot
            // clear one another's references. The legacy cell can hold one per team.
            foreach (var fleet in _state.Players[1].Tf.Skip(1))
                if (fleet.Destination != null) fleet.Blasting = 0;
            RebuildFleetReferences();
        }

        private void RebuildFleetReferences() {
            foreach (var cell in _state.Board) {
                cell.TaskForces[0] = cell.TaskForces[1] = null!;
                cell.Tf = -1;
            }
            for (int team = 0; team < 2; team++)
                for (int i = 1; i < _state.Players[team].Tf.Length; i++) {
                    var tf = _state.Players[team].Tf[i];
                    if (tf.Destination == null) continue;
                    var cell = _state.Board[tf.X, tf.Y];
                    cell.TaskForces[team] ??= tf;
                    if (team == 1 && cell.Tf == -1) cell.Tf = i;
                }
        }

        // utils.c round: retain the floating-point addition before truncation.
        private static int ClassicRound(double value) => (int)(value < 0 ? value - 0.5 : value + 0.5);

        // movement.c fleet_explore/lose: later nonempty ship types overwrite
        // the shared probability. Preserve its >100 cap AFTER division by 100.
        private static void Explore(TaskForce tf, IRng rng) {
            double probability = 0;
            if (tf.Transports != 0) probability = (10.0 + rng.Next(1, 6) * tf.Transports) / 100.0;
            if (tf.Scouts != 0) probability = (70.0 + rng.Next(1, 11) * tf.Scouts) / 100.0;
            if (tf.Cruisers != 0) probability = (90.0 + rng.Next(1, 11) * tf.Cruisers) / 100.0;
            if (tf.BattleStars != 0) probability = (97.0 + rng.Next(1, 4) * tf.BattleStars) / 100.0;
            if (probability > 100) probability = 100;
            int Survivors(int ships) {
                int remaining = ships;
                for (int i = 0; i < ships; i++)
                    if (rng.Next(1, 101) / 100.0 > probability) remaining--;
                return remaining;
            }
            tf.Transports = Survivors(tf.Transports);
            tf.Scouts = Survivors(tf.Scouts);
            tf.Cruisers = Survivors(tf.Cruisers);
            tf.BattleStars = Survivors(tf.BattleStars);
            if (tf.Transports == 0 && tf.Scouts == 0 && tf.Cruisers == 0 && tf.BattleStars == 0)
                tf.Destination = null;
        }
    }
}
