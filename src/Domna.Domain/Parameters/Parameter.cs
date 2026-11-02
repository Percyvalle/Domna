namespace Domna.Domain.Parameters;

/// <summary>
/// The eleven process parameters the thermal model takes as input.
/// </summary>
/// <remarks>
/// The member order is the order of the regression's parameter vector, not an
/// arbitrary listing: <see cref="Models.ParameterSet"/> and the coefficient expansions
/// index by it. Reordering, inserting or removing a member silently changes what
/// the model computes — treat this enum as fixed domain data.
/// </remarks>
public enum Parameter
{
    /// <summary>Уровень засыпи (УЗ), m.</summary>
    BurdenLevel = 0,

    /// <summary>Рудная нагрузка (РН), t of ore per t of coke.</summary>
    OreLoadRate = 1,

    /// <summary>Эквивалентная по поверхности крупность кокса (ЭквК), mm.</summary>
    CokeSize = 2,

    /// <summary>Эквивалентная по поверхности крупность агломерата (ЭквАгл), mm.</summary>
    SinterSize = 3,

    /// <summary>Эквивалентная по поверхности крупность окатышей (ЭквОк), mm.</summary>
    PelletsSize = 4,

    /// <summary>Содержание окатышей в железорудной части шихты (ДоляОк), fraction 0..1.</summary>
    PelletsRatio = 5,

    /// <summary>Содержание материала по станциям 9-11, %.</summary>
    ChargeStations9To11 = 6,

    /// <summary>Содержание материала по станциям 6-8, %.</summary>
    ChargeStations6To8 = 7,

    /// <summary>Содержание материала по станциям 3-5, %.</summary>
    ChargeStations3To5 = 8,

    /// <summary>Давление горячего дутья (ДавГорДут), atm.</summary>
    HotBlastPressure = 9,

    /// <summary>Давление колошникового газа (ДавКолГаз), atm.</summary>
    TopGasPressure = 10,
}
