namespace Domna.Domain.Models;

/// <summary>
/// A radial measuring position on the furnace top, from the axis outwards.
/// </summary>
public enum TemperaturePosition
{
    /// <summary>Gas temperature on the furnace axis (центр).</summary>
    Center = 0,

    /// <summary>Gas temperature at the ore ridge, where the charge piles up (рудный гребень).</summary>
    OreRidge = 1,

    /// <summary>Gas temperature at the periphery, next to the wall (периферия).</summary>
    Periphery = 2,
}
