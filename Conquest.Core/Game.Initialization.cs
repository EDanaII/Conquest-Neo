using Conquest.Core.Model;

namespace Conquest.Core;

public partial class Game {
    /// <summary>
    /// Establish the main.c player defaults and initconst() transports/advantage
    /// after GenerateStars(). This does not perform initmach() or init_player().
    /// </summary>
    public void InitializeClassicPlayers() {
        if (State.ClassicPlayerInitializationStarted || !State.HasGeneratedGalaxy
            || Turn != 0 || !HasFreshPlayers()
            || State.Stars.Any(s => s.ExploredBy.Any(seen => seen)
                || s.Planets.Any(p => p.Owner != Ownership.None || p.Inhabitants != 0
                    || p.IndustrialUnits != 0 || p.MissileBases != 0
                    || p.AdvancedMissileBases != 0 || p.Conquered || p.UnderAttack))
            || State.Board.Cast<Sector>().Any(cell => cell.Tf != -1
                || cell.TaskForces.Any(tf => tf != null))) {
            throw new InvalidOperationException("Classic player initialization requires a fresh generated game and can run only once.");
        }

        // State owns the guard so another Game facade cannot initialize it again.
        // A failed RNG call also requires a new state, rather than an implicit reset.
        State.ClassicPlayerInitializationStarted = true;
        for (int team = 0; team < State.Players.Length; team++) {
            var player = State.Players[team];
            player.Velocity = Constants.Init.Velocity;
            player.Range = Constants.Init.Range + (team == 0 ? 2 : 0);
            player.Weapons = Constants.Init.Weapons;
            player.PopulationGrowthRate = team == 0 ? 0.5 : 0.3;
            player.Tf[1].Transports = State.Config.StartingTransports;
        }

        var enemy = State.Players[0];
        switch (_rng.Next(1, 4)) {
            case 1:
                enemy.Weapons = _rng.Next(1, 5) + 2;
                break;
            case 2:
                enemy.Velocity = _rng.Next(1, 4);
                break;
            case 3:
                enemy.PopulationGrowthRate = (_rng.Next(1, 5) + 3) / 10.0;
                break;
        }
    }

    private bool HasFreshPlayers() => State.Players.All(player =>
        player.Velocity == 0 && player.Range == 0 && player.Weapons == 0
        && player.PopulationGrowthRate == 0 && player.VelocityResearchProgress == 0
        && player.RangeResearchProgress == 0 && player.WeaponsResearchProgress == 0
        && player.Tf.Skip(1).All(tf => tf != null && tf.X == 0 && tf.Y == 0
            && tf.Destination == null && tf.ETA == 0 && tf.EtaOriginal == 0
            && tf.Scouts == 0 && tf.Transports == 0 && tf.Cruisers == 0
            && tf.BattleStars == 0 && tf.Blasting == 0 && tf.Withdrew == 0));
}
