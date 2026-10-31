using Conquest.Core.Model;

namespace Conquest.Core;

public partial class Game {
    private void IssueClassicEnemyOrders() {
        try {
            // enemy.c:inputmach visits live slots in order, not a frozen fleet list.
            for (int slot = 1; slot <= 26; slot++) {
                var fleet = State.Players[0].Tf[slot];
                if (fleet.Destination is not int star || fleet.ETA != 0) continue;
                double[] distances = EnemyReachableStars(star);
                SendEnemyScouts(fleet, distances);
                SendEnemyTransports(fleet, distances);
                MoveEnemyWarships(fleet, distances);
                State.FleetsSys.CleanupClassicFleet(0, slot);
            }
        }
        catch (InvalidOperationException ex) {
            State.EnemyProcessingFailure = "Enemy orders stopped: " + ex.Message + " State retained; unsafe orders will not be replayed.";
            throw new InvalidOperationException(State.EnemyProcessingFailure, ex);
        }
    }
    private double[] EnemyReachableStars(int origin) {
        var from = State.Stars[origin]; var result = new double[State.Stars.Count];
        for (int i = 0; i < result.Length; i++) {
            var to = State.Stars[i]; double distance = Math.Sqrt(Math.Pow(from.X - to.X, 2) + Math.Pow(from.Y - to.Y, 2));
            if (distance <= State.Players[0].Range) result[i] = distance;
        }
        return result;
    }
    private bool PlayerPresent(int star) => StationaryFleet(1, star) != 0
        || State.Stars[star].Planets.Any(p => p.Owner == Ownership.Player);
    private void EnemyDepart(int star) { if (PlayerPresent(star)) State.EnemyDepartures.Add(star); }
    private TaskForce NewEnemyFleet(int star) => State.Players[0].Tf[State.FleetsSys.AllocateCombatFleet(0, star)];
    private void SendEnemyFleet(TaskForce fleet, double[] distances, int destination) {
        if ((uint)destination >= (uint)State.Stars.Count)
            throw new InvalidOperationException("Original enemy destination is undefined (no candidate planet/star).");
        EnemyDepart(fleet.Destination!.Value);
        fleet.Destination = destination;
        fleet.ETA = (int)((distances[destination] - .01) / State.Players[0].Velocity) + 1;
        // C enemy orders leave x/y, xf/yf, origeta and withdrew unchanged.
    }
    private void SendEnemyScouts(TaskForce fleet, double[] distances) {
        if (fleet.Scouts <= 0) return;
        if (!distances.Any(d => d > 0))
            throw new InvalidOperationException("No reachable scout destination; original C would not terminate.");
        var unknown = Enumerable.Range(0, distances.Length).Where(i => distances[i] > 0 && !State.Stars[i].ExploredBy[0]).ToList();
        while (unknown.Count > 0 && fleet.Scouts > 0) {
            var child = NewEnemyFleet(fleet.Destination!.Value); child.Scouts = 1;
            int choice = _rng.Next(1, unknown.Count + 1) - 1;
            SendEnemyFleet(child, distances, unknown[choice]);
            unknown[choice] = unknown[^1]; unknown.RemoveAt(unknown.Count - 1); fleet.Scouts--;
        }
        while (fleet.Scouts > 0) {
            int target;
            do { target = _rng.Next(1, distances.Length + 1) - 1; } while (distances[target] <= 0);
            var child = NewEnemyFleet(fleet.Destination!.Value); child.Scouts = 1;
            SendEnemyFleet(child, distances, target); fleet.Scouts--;
        }
    }
    private int EnemyTransportScore(Planet planet, double distance) {
        int score;
        if (!planet.Star.ExploredBy[0]) score = 60;
        else {
            score = planet.EnemySeenOwner switch { Ownership.None => 40, Ownership.Enemy => 30, _ => 0 };
            if (planet.EnemySeenOwner != Ownership.Player && planet.Capacity - planet.Inhabitants > 40 - (Turn + 1) / 2) score += 40;
        }
        return score - (int)(2 * distance + .5);
    }
    private bool EnemyStarUnderdefended(int star) => State.Stars[star].Planets.Any(p => p.Owner == Ownership.Enemy
        && p.IndustrialUnits > 10 && 6 * p.AdvancedMissileBases + p.MissileBases < (int)(p.IndustrialUnits / 15.0 + .5));
    private void SendEnemyTransports(TaskForce fleet, double[] distances) {
        // The C recursion re-scores after each landing; a loop preserves that order.
        while (fleet.Transports > 0) {
            int bestStar = -1, secondStar = -1, top = -10000, second = -11000; Planet? best = null;
            int origin = fleet.Destination!.Value;
            for (int star = 0; star < State.Stars.Count; star++) {
                if (distances[star] <= 0 && star != origin) continue;
                foreach (var planet in State.Stars[star].Planets) {
                    int score = EnemyTransportScore(planet, distances[star]);
                    if (score <= second) continue;
                    if (score > top) {
                        second = top; secondStar = bestStar; top = score; bestStar = star; best = planet;
                    }
                    else { second = score; secondStar = star; }
                }
            }
            if (best == null) throw new InvalidOperationException("Original transport orders have no candidate planet.");
            if (bestStar == origin) {
                if (StationaryFleet(1, origin) != 0 || best.Owner == Ownership.Player) return;
                int landed = Math.Min(fleet.Transports, (best.Capacity - best.Inhabitants) / 3);
                if (landed <= 0) return;
                if (best.Inhabitants == 0) { best.Owner = Ownership.Enemy; best.EnemySeenOwner = Ownership.Enemy; }
                best.Inhabitants += landed; best.IndustrialUnits += landed; fleet.Transports -= landed;
                continue;
            }
            void Send(int transports, int target) {
                var child = NewEnemyFleet(origin); child.Transports = transports; fleet.Transports -= transports;
                if (fleet.Cruisers > 0 && !EnemyStarUnderdefended(origin)) { child.Cruisers = 1; fleet.Cruisers--; }
                SendEnemyFleet(child, distances, target);
            }
            // "Second star" in C is the second-best PLANET, possibly at the same star.
            if (fleet.Transports >= 10 && secondStar >= 0) { Send(fleet.Transports / 2, bestStar); bestStar = secondStar; }
            Send(fleet.Transports, bestStar);
        }
    }
    private int EnemyWarshipScore(Planet planet) {
        int score;
        if (!planet.Star.ExploredBy[0]) score = 600;
        else {
            score = planet.EnemySeenOwner switch {
                Ownership.None => 100,
                Ownership.Enemy when planet.Conquered => 1000,
                Ownership.Enemy => 6 * planet.AdvancedMissileBases + planet.MissileBases <= planet.IndustrialUnits / 15
                    && planet.IndustrialUnits >= Constants.Cost.MissileBase ? 300 : 0,
                _ => planet.Conquered ? 400 : 200
            };
            if (planet.EnemySeenOwner == Ownership.Enemy && planet.AdvancedMissileBases >= 4) score -= 250;
            if (planet.Capacity < 40 && planet.IndustrialUnits < 15) score -= 100;
        }
        return score + _rng.Next(1, 21);
    }
    private void MoveEnemyWarships(TaskForce fleet, double[] distances) {
        if (fleet.Cruisers + fleet.BattleStars <= 0) return;
        int origin = fleet.Destination!.Value, bestStar = Array.FindIndex(distances, d => d > 0), top = -1000;
        Planet? best = null;
        for (int star = 0; star < State.Stars.Count; star++) {
            if (distances[star] <= 0 && star != origin) continue;
            foreach (var planet in State.Stars[star].Planets) {
                int score = EnemyWarshipScore(planet) + (star == origin ? 250 : 0) - (StationaryFleet(0, star) > 0 ? 150 : 0);
                if (score > top) { top = score; bestStar = star; best = planet; }
            }
        }
        if (bestStar != origin) { SendEnemyFleet(fleet, distances, bestStar); return; }
        if (best is not { Owner: Ownership.Enemy, Conquered: true }) return;
        if (best.IndustrialUnits < 20) {
            BlastPlanet(best, Math.Min(WarshipForces(fleet, 0), 4 * best.Inhabitants));
            if (PlayerPresent(origin)) best.PlayerSeenCapacity = best.Capacity;
        }
        else if ((fleet.BattleStars > 3 || fleet.Cruisers > 3) && _rng.Next(1, 5) == 4 || fleet.BattleStars > 8) {
            WanderEnemyWarships(fleet, distances);
        }
    }
    private void WanderEnemyWarships(TaskForce fleet, double[] distances) {
        if (fleet.BattleStars <= 1 && fleet.Cruisers <= 1) return;
        // C intends the rnd(count)-th reachable star. Its scan starts at an
        // uninitialized dummy slot and increments past the selected entry.
        // Here index zero is real star A: select from valid stars in order.
        var reachable = Enumerable.Range(0, distances.Length).Where(i => distances[i] > 0).ToArray();
        if (reachable.Length == 0) return;
        int target = reachable[_rng.Next(1, reachable.Length + 1) - 1];
        var child = NewEnemyFleet(fleet.Destination!.Value);
        child.BattleStars = fleet.BattleStars / 2; fleet.BattleStars -= child.BattleStars;
        child.Cruisers = fleet.Cruisers / 2; fleet.Cruisers -= child.Cruisers;
        if (fleet.Transports > 3) { child.Transports = 2; fleet.Transports -= 2; }
        SendEnemyFleet(child, distances, target);
    }
}
