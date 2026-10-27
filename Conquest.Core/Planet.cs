namespace Conquest.Core.Model;

public sealed class Planet {
    public Planet(Star star) {
        Star = star ?? throw new ArgumentNullException(nameof(star));
    }

    public Star Star { get; }
    public int Number { get; set; }
    public int Capacity { get; set; }
    public Ownership Owner { get; set; } = Ownership.None;
    public int Inhabitants { get; set; }
    public int IndustrialUnits { get; set; }
    public int MissileBases { get; set; }
    public int AdvancedMissileBases { get; set; }
    public bool Conquered { get; set; }
    public bool UnderAttack { get; set; }
    public int PlayerSeenCapacity { get; set; }
    public Ownership EnemySeenOwner { get; set; } = Ownership.None;
    public int EnemySeenDefense { get; set; } = 1;
}
