using Domna.Domain.Models;

namespace Domna.Domain.Interfaces;

/// <summary>Derives regression inputs from a charge program.</summary>
public interface IChargeProgramCalculator
{
    /// <summary>Calculates the station shares, or returns null if a material or group has no portions.</summary>
    /// <param name="program">The program to calculate.</param>
    /// <returns>The three ore shares in percent when all three are defined.</returns>
    ChargeStationShares? Calculate(ChargeProgram program);
}
