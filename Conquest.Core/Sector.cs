// Conquest.Core/Models/Sector.cs
namespace Conquest.Core.Models;

public class Sector {
    public Sector( ) {
        ViewingTeam = 1;                 // 0 = enemy, 1 = player
        TaskForces = new TaskForce[2];   // per-team view (may contain nulls)
    }

    // === Legacy/index-based fields (match old C-style logic) ===
    public int Status { get; set; }
    public int Star { get; set; }   // index into GameState.Stars (0 = none)
    public int Tf { get; set; }   // index into GameState.Players[team].Tf (0 = none)

    // === Rich/object-based fields for your newer code ===
    public Star? StarRef { get; set; }            // object reference to the star in this sector
    public TaskForce[] TaskForces { get; set; }   // per-team TF reference (viewer-facing)

    public int ViewingTeam { get; set; }

    public override string ToString( ) {
        var star = StarRef;
        var mark = star == null ? " " : (star.ExploredBy[ViewingTeam] ? " " : "?");
        var starName = star?.Name ?? ".";
        var tfName = TaskForces[ViewingTeam]?.Name ?? " ";
        return $"{mark}{starName}{tfName}";
    }
}
