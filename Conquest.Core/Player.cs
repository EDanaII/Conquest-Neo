// Conquest.Core/Models/Player.cs
namespace Conquest.Core.Model;

public class Player {
    public int Velocity { get; set; }
    public int Range { get; set; }
    public int Weapons { get; set; }
    public double PopulationGrowthRate { get; set; }
    public int VelocityResearchProgress { get; set; }
    public int RangeResearchProgress { get; set; }
    public int WeaponsResearchProgress { get; set; }

    // 1..26 task forces (slot 0 unused for convenience)
    public TaskForce[] Tf { get; } = new TaskForce[27];
    public Player( ) {
        for (int i = 1; i < Tf.Length; i++)
            Tf[i] = new TaskForce { Name = ((char)('A' + i - 1)).ToString( ) };
    }
}
