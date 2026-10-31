namespace Conquest.Core.Model;

/// <summary>Core-owned inv_player local variables, retained while the CLI waits for input.</summary>
public sealed class ColonyInvestment {
    internal ColonyInvestment(int starIndex, Planet planet, int fleetIndex) {
        StarIndex = starIndex; Planet = planet; FleetIndex = fleetIndex; Balance = planet.IndustrialUnits;
    }
    public int StarIndex { get; }
    public Planet Planet { get; }
    public int FleetIndex { get; }
    public int Balance { get; internal set; }
}
