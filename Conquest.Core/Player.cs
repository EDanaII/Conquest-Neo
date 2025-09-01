// Conquest.Core/Models/Player.cs
namespace Conquest.Core.Models;

public class Player {
    // 1..26 task forces (slot 0 unused for convenience)
    public TaskForce[] Tf { get; } = new TaskForce[27];
    public Player( ) {
        for (int i = 1; i < Tf.Length; i++)
            Tf[i] = new TaskForce { Name = ((char)('A' + i - 1)).ToString( ) };
    }
}
