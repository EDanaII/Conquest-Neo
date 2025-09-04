namespace Conquest.Core.Model;

public class TaskForce : BoardPoint {
    public string Name { get; set; } = ""; // used in your Sector.ToString()

    public int BattleStars { get; set; }
    public int Blasting { get; set; }
    public int Cruisers { get; set; }
    public int? Destination { get; set; } = null; // null = no course
    public int ETA { get; set; }
    public int EtaOriginal { get; set; }
    public int Scouts { get; set; }
    public int Transports { get; set; }
    public int Withdrew { get; set; }
}
