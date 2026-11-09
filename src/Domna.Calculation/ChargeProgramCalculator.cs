using Domna.Domain.Interfaces;
using Domna.Domain.Models;

namespace Domna.Calculation;

public sealed class ChargeProgramCalculator : IChargeProgramCalculator
{
    /// <inheritdoc />
    public ChargeStationShares? Calculate(ChargeProgram program)
    {
        ArgumentNullException.ThrowIfNull(program);

        // Bucket zero still contributes to the total used to normalize each material.
        Span<double> coke = stackalloc double[4];
        Span<double> ore = stackalloc double[4];
        coke.Clear();
        ore.Clear();

        foreach (ChargeDrop drop in program.Drops)
        {
            Span<double> counts = drop.Material == ChargeMaterial.Coke ? coke : ore;
            for (int station = 1; station <= ChargeDrop.StationCount; station++)
            {
                counts[station / 3] += drop[station];
            }
        }

        double allCoke = coke[0] + coke[1] + coke[2] + coke[3];
        double allOre = ore[0] + ore[1] + ore[2] + ore[3];
        if (allCoke <= 0 || allOre <= 0)
        {
            return null;
        }

        Span<double> shares = stackalloc double[3];
        for (int group = 1; group <= shares.Length; group++)
        {
            double cokeFraction = coke[group] / allCoke;
            double oreFraction = ore[group] / allOre;
            double combined = cokeFraction + oreFraction;
            if (combined <= 0)
            {
                return null;
            }

            shares[group - 1] = oreFraction / combined * 100;
        }

        return new ChargeStationShares(shares[2], shares[1], shares[0]);
    }
}
