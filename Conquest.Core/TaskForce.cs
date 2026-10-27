namespace Conquest.Core.Model;

public class TaskForce : BoardPoint {
    public string Name { get; set; } = ""; // used in your Sector.ToString()

    public int BattleStars { get; set; }
    public int Blasting { get; set; }
    public int Cruisers { get; set; }
    // Classic startup retains the current star's zero-based ID with ETA == 0,
    // matching C dest. Debug placement/arrival still clear this field to null.
    public int? Destination { get; set; } = null;
    public int ETA { get; set; }
    public int EtaOriginal { get; set; }
    public int Scouts { get; set; }
    public int Transports { get; set; }
    public int Withdrew { get; set; }
}
