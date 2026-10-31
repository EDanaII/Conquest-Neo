using Conquest.Core.Model;

namespace Conquest.Core;

public partial class Game {
    // combat.c:battle: finish all three operations at a star before visiting the next.
    private void ContinueClassicCombat() {
        if (HasPendingClassicCombat) return;
        while (State.CombatStar < State.Stars.Count) {
            int star = State.CombatStar;
            if (State.CombatStep == 0) {
                int enemy = StationaryFleet(0, star), player = StationaryFleet(1, star);
                if (enemy > 0 && player > 0 && (HasWarships(0, star) || HasWarships(1, star))) {
                    var players = StationaryFleets(1, star).ToArray();
                    if (players.Length > 1) {
                        player = State.FleetsSys.AllocateCombatFleet(1, star);
                        foreach (int child in players) State.FleetsSys.MergeCombatFleet(1, player, child, star);
                    }
                    State.CombatStep = 1;
                    State.Battle = new FleetBattle(star, enemy, player); RefreshBattleForces(); return;
                }
                State.CombatStep = 1;
            }
            if (State.CombatStep == 1) {
                if (HasWarships(0, star)) {
                    int fleet = StationaryFleet(0, star);
                    if (EnemyAttackTarget(star, fleet) is { } planet) {
                        State.EnemyAttack = new EnemyPlanetAttack(star, fleet, planet); return;
                    }
                }
                State.CombatStep = 2;
            }
            if (State.CombatStep == 2) {
                State.CombatStep = 3;
                if (CanAttackPlanet(star)) { State.Attack = new PlanetaryAttack(star); return; }
            }
            State.CombatStar++; State.CombatStep = 0;
        }
        if (State.StartupCombatInProgress) {
            State.PendingSharedStartEncounter = false; State.StartupCombatInProgress = false;
        }
    }
    private IEnumerable<int> StationaryFleets(int team, int star) => Enumerable.Range(1, 26)
        .Where(i => State.Players[team].Tf[i].Destination == star && State.Players[team].Tf[i].ETA == 0);
    private int StationaryFleet(int team, int star) => StationaryFleets(team, star).FirstOrDefault();

    public bool FleetBattleContinues {
        get {
            if (State.Battle is not { } battle) return false;
            var e = State.Players[0].Tf[battle.EnemyFleet]; var p = State.Players[1].Tf[battle.PlayerFleet];
            return e.Destination != null && p.Destination != null
                && WarshipForces(e, 0) + WarshipForces(p, 1) > 0;
        }
    }
    public void RefreshBattleForces() {
        var battle = State.Battle ?? throw new InvalidOperationException("No fleet battle is active.");
        State.FleetsSys.CleanupCombatFleets();
        var e = State.Players[0].Tf[battle.EnemyFleet]; var p = State.Players[1].Tf[battle.PlayerFleet];
        battle.EnemyForces = e.Destination == null ? 0 : WarshipForces(e, 0);
        battle.PlayerForces = p.Destination == null ? 0 : WarshipForces(p, 1);
        if (!FleetBattleContinues) return;
        battle.EnemySurvival = Math.Exp(Math.Log(.8) * Math.Min(14, (double)battle.PlayerForces / (battle.EnemyForces + e.Transports + 2 * e.Scouts)));
        battle.PlayerSurvival = Math.Exp(Math.Log(.8) * Math.Min(14, (double)battle.EnemyForces / (battle.PlayerForces + p.Transports + 2 * p.Scouts)));
    }
    public void AcknowledgeFleetBattle() {
        var battle = State.Battle ?? throw new InvalidOperationException("No fleet battle is active.");
        if (battle.Stage == FleetBattleStage.Orders) throw new InvalidOperationException("Use G or W at B?.");
        if (battle.Stage == FleetBattleStage.InitialPause) { FireFleetRound(); return; }
        State.FleetsSys.CleanupCombatFleets(); ApplyClassicRevolt(battle.StarIndex);
        State.Battle = null; ContinueClassicCombat();
    }
    private static ShipLosses Counts(TaskForce f) => new(f.Transports, f.Scouts, f.Cruisers, f.BattleStars);
    private static ShipLosses Difference(ShipLosses a, TaskForce b) => new(a.Transports - b.Transports,
        a.Scouts - b.Scouts, a.Cruisers - b.Cruisers, a.BattleStars - b.BattleStars);
    private void FleetLossPass(TaskForce fleet, double save) {
        fleet.Transports = CombatSurvivors(fleet.Transports, save); fleet.Scouts = CombatSurvivors(fleet.Scouts, save);
        fleet.Cruisers = CombatSurvivors(fleet.Cruisers, save); fleet.BattleStars = CombatSurvivors(fleet.BattleStars, save);
    }
    private void FireFleetRound() {
        var b = State.Battle!;
        if (!FleetBattleContinues) { b.Stage = FleetBattleStage.FinalPause; return; }
        var enemy = State.Players[0].Tf[b.EnemyFleet]; var player = State.Players[1].Tf[b.PlayerFleet];
        var oldE = Counts(enemy); var oldP = Counts(player);
        do {
            FleetLossPass(enemy, b.EnemySurvival); FleetLossPass(player, b.PlayerSurvival);
        } while (!b.FirstRound && oldE == Counts(enemy) && oldP == Counts(player));
        b.FirstRound = false; b.EnemyLosses = Difference(oldE, enemy); b.PlayerLosses = Difference(oldP, player);
        RefreshBattleForces();
        b.Stage = FleetBattleContinues ? FleetBattleStage.Orders : FleetBattleStage.FinalPause;
        if (b.Stage == FleetBattleStage.Orders) PrepareEnemyWithdrawal();
    }

    // Integral combat withdrawal only; this does not generate strategic enemy orders.
    public void PrepareEnemyWithdrawal() {
        var b = State.Battle ?? throw new InvalidOperationException("No fleet battle is active.");
        if (b.Stage != FleetBattleStage.Orders || b.WithdrawalPrepared) return;
        var enemy = State.Players[0].Tf[b.EnemyFleet]; var player = State.Players[1].Tf[b.PlayerFleet];
        bool threatened = player.Cruisers + player.BattleStars > 0;
        int size = 0; var owner = Ownership.None;
        foreach (var planet in State.Stars[b.StarIndex].Planets)
            if (planet.Capacity > size) { size = planet.Capacity; owner = planet.Owner; }
        bool flee = b.EnemySurvival < .70 && size < 30 || b.EnemySurvival < .50 && owner == Ownership.Player
            || b.EnemySurvival < .30 && size < 60 || b.EnemySurvival < .20;
        bool ships = threatened && (enemy.Transports + enemy.Scouts > 0 || flee && enemy.Cruisers + enemy.BattleStars > 0);
        double Distance(int star) {
            var from = State.Stars[b.StarIndex]; var to = State.Stars[star];
            return Math.Sqrt(Math.Pow(from.X - to.X, 2) + Math.Pow(from.Y - to.Y, 2));
        }
        if (ships && (State.Players[0].Velocity < 1 || !Enumerable.Range(0, State.Stars.Count).Any(s => Distance(s) > 0 && Distance(s) <= State.Players[0].Range)))
            throw new InvalidOperationException("Enemy withdrawal has no reachable star; original C would not terminate. Battle remains pending.");
        int slot = State.FleetsSys.AllocateCombatFleet(0, b.StarIndex);
        var child = State.Players[0].Tf[slot];
        if (threatened) {
            child.Transports = enemy.Transports; child.Scouts = enemy.Scouts;
            if (flee) { child.Cruisers = enemy.Cruisers; child.BattleStars = enemy.BattleStars; }
        }
        if (ships) {
            int target; double distance;
            do { target = _rng.Next(1, State.Stars.Count + 1) - 1; distance = Distance(target); }
            while (distance <= 0 || distance > State.Players[0].Range);
            child.Destination = target; child.ETA = (int)((distance - .01) / State.Players[0].Velocity) + 1;
            // C leaves origeta == 0 and x/y at departure for this enemy fleet.
            b.EnemyWithdrawalFleet = slot;
        }
        else child.Destination = null;
        b.WithdrawalPrepared = true;
    }
    private void SettleEnemyWithdrawal() {
        var b = State.Battle!;
        if (b.EnemyWithdrawalFleet is int slot) {
            var original = State.Players[0].Tf[b.EnemyFleet]; var child = State.Players[0].Tf[slot];
            original.Transports -= child.Transports; original.Scouts -= child.Scouts;
            original.Cruisers -= child.Cruisers; original.BattleStars -= child.BattleStars;
        }
        b.EnemyWithdrawalFleet = null; b.WithdrawalPrepared = false; RefreshBattleForces();
    }
    public void ContinueFleetBattle() {
        var b = State.Battle ?? throw new InvalidOperationException("No fleet battle is active.");
        if (b.Stage != FleetBattleStage.Orders || b.PlayerWithdrawalFleet != null)
            throw new InvalidOperationException("Finish the pending battle prompt first.");
        PrepareEnemyWithdrawal(); SettleEnemyWithdrawal(); FireFleetRound();
    }
    public string? BeginBattleWithdrawal(IEnumerable<(char Ship, int Quantity)> ships) {
        var b = State.Battle ?? throw new InvalidOperationException("No fleet battle is active.");
        if (b.Stage != FleetBattleStage.Orders || b.PlayerWithdrawalFleet != null)
            throw new InvalidOperationException("Finish the pending battle prompt first.");
        PrepareEnemyWithdrawal();
        var result = State.FleetsSys.SplitPlayerFleet(b.PlayerFleet, ships);
        if (State.Players[1].Tf[result.Slot].Destination != null) b.PlayerWithdrawalFleet = result.Slot;
        return result.Error;
    }
    public void FinishBattleWithdrawal(int? target) {
        var b = State.Battle ?? throw new InvalidOperationException("No fleet battle is active.");
        int slot = b.PlayerWithdrawalFleet ?? throw new InvalidOperationException("No withdrawal destination is pending.");
        var child = State.Players[1].Tf[slot];
        string? error = null;
        try {
            if (target is not int star || (uint)star >= (uint)State.Stars.Count) throw new InvalidOperationException("  !illegal star");
            State.FleetsSys.SetCourse(1, slot, star);
            if (child.ETA == 0) throw new InvalidOperationException("Tf remains at star.");
            child.Withdrew = 1;
        }
        catch (InvalidOperationException ex) {
            error = ex.Message;
            State.FleetsSys.MergeCombatFleet(1, b.PlayerFleet, slot, b.StarIndex);
        }
        b.PlayerWithdrawalFleet = null; RefreshBattleForces();
        if (!FleetBattleContinues) { SettleEnemyWithdrawal(); b.Stage = FleetBattleStage.FinalPause; }
        if (error != null) throw new InvalidOperationException(error);
    }

    private Planet? EnemyAttackTarget(int star, int fleet) {
        var tf = State.Players[0].Tf[fleet]; int force = tf.Cruisers + 6 * tf.BattleStars;
        if (tf.Destination == null || force <= 0) return null;
        double best = 1000; Planet? target = null;
        foreach (var p in State.Stars[star].Planets.Where(p => p.Owner == Ownership.Player)) {
            double score = ((double)p.EnemySeenDefense / force - (p.Capacity > 30 ? 2 : 1.5)) * p.Capacity;
            if (score < best) { best = score; target = p; }
        }
        return best < 0 ? target : null;
    }
    public void AcknowledgeEnemyAttack() {
        var attack = State.EnemyAttack ?? throw new InvalidOperationException("No enemy planetary attack is active.");
        if (attack.Salvo == null) {
            attack.Salvo = FirePlanetSalvo(0, attack.FleetIndex, attack.Planet, attack.FiredAt.Add(attack.Planet.Number));
            attack.Planet.EnemySeenDefense = attack.Planet.MissileBases + 6 * attack.Planet.AdvancedMissileBases;
        }
        else if (EnemyAttackTarget(attack.StarIndex, attack.FleetIndex) is { } planet) {
            attack.Planet = planet; attack.Salvo = null;
        }
        else {
            ApplyClassicRevolt(attack.StarIndex); State.EnemyAttack = null; State.CombatStep = 2; ContinueClassicCombat();
        }
    }
}
