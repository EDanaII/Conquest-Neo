namespace Conquest.Core.Model;

public enum FleetBattleStage { InitialPause, Orders, FinalPause }

public sealed class FleetBattle {
    internal FleetBattle(int star, int enemy, int player) { StarIndex = star; EnemyFleet = enemy; PlayerFleet = player; }
    public int StarIndex { get; }
    public int EnemyFleet { get; }
    public int PlayerFleet { get; }
    public FleetBattleStage Stage { get; internal set; }
    public int EnemyForces { get; internal set; }
    public int PlayerForces { get; internal set; }
    public double EnemySurvival { get; internal set; }
    public double PlayerSurvival { get; internal set; }
    public ShipLosses EnemyLosses { get; internal set; } = new(0, 0, 0, 0);
    public ShipLosses PlayerLosses { get; internal set; } = new(0, 0, 0, 0);
    public int? EnemyWithdrawalFleet { get; internal set; }
    public int? PlayerWithdrawalFleet { get; internal set; }
    internal bool FirstRound { get; set; } = true;
    internal bool WithdrawalPrepared { get; set; }
}

public sealed record ShipLosses(int Transports, int Scouts, int Cruisers, int BattleStars);

public sealed class EnemyPlanetAttack {
    internal EnemyPlanetAttack(int star, int fleet, Planet planet) { StarIndex = star; FleetIndex = fleet; Planet = planet; }
    public int StarIndex { get; }
    public int FleetIndex { get; }
    public Planet Planet { get; internal set; }
    public PlanetarySalvoResult? Salvo { get; internal set; }
    internal HashSet<int> FiredAt { get; } = [];
}
