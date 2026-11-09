namespace Domna.Domain.Models;

/// <summary>The sequence of drops that makes up a charge program.</summary>
public sealed class ChargeProgram
{
    /// <summary>Initializes a new instance of the <see cref="ChargeProgram"/> class.</summary>
    /// <param name="drops">The successive material drops.</param>
    public ChargeProgram(IEnumerable<ChargeDrop> drops)
    {
        ArgumentNullException.ThrowIfNull(drops);
        Drops = Array.AsReadOnly(drops.ToArray());
    }

    /// <summary>The drops in loading order.</summary>
    public IReadOnlyList<ChargeDrop> Drops { get; }
}
