namespace Domna.Domain.Models;

/// <summary>One drop of coke or ore, with a portion count for each distributor station.</summary>
public sealed class ChargeDrop
{
    /// <summary>The number of distributor stations, numbered 1 through 11.</summary>
    public const int StationCount = 11;

    private readonly int[] _portions;

    /// <summary>Initializes a new instance of the <see cref="ChargeDrop"/> class.</summary>
    /// <param name="material">The material being distributed.</param>
    /// <param name="portions">The nonnegative portion counts, one per station.</param>
    /// <exception cref="ArgumentException">The counts are negative or the station count is wrong.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The material is unknown.</exception>
    public ChargeDrop(ChargeMaterial material, IEnumerable<int> portions)
    {
        ArgumentNullException.ThrowIfNull(portions);
        if (material is not (ChargeMaterial.Coke or ChargeMaterial.Ore))
        {
            throw new ArgumentOutOfRangeException(nameof(material));
        }

        _portions = [.. portions];
        if (_portions.Length != StationCount || _portions.Any(count => count < 0))
        {
            throw new ArgumentException("A drop needs one nonnegative portion count per station.", nameof(portions));
        }

        Material = material;
    }

    /// <summary>The material being distributed.</summary>
    public ChargeMaterial Material { get; }

    /// <summary>The number of portions at the given one-based station number.</summary>
    public int this[int station] => _portions[station - 1];
}
