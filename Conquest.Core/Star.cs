namespace Conquest.Core.Model;

public class Star : BoardPoint {
    public string Name { get; set; } = ".";
    // Two teams: 0 = enemy, 1 = player (matches your ViewingTeam usage)
    public bool[] ExploredBy { get; } = new bool[2];
}
