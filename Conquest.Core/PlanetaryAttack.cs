namespace Conquest.Core.Model;

public sealed class PlanetaryAttack {
    internal PlanetaryAttack(int starIndex) { StarIndex = starIndex; }
    public int StarIndex { get; }
    public bool AwaitingAcknowledgement { get; internal set; }
}

public sealed record PlanetarySalvoResult(int FleetIndex, int PlanetNumber, int AttackerForces, int DefenderForces,
    double? AttackerSurvival, double? DefenderSurvival, int CruisersLost, int BattleStarsLost,
    int MissileBasesLost, int AdvancedBasesLost, int LossPasses, bool Captured);
