using Domna.Domain.Parameters;

namespace Domna.UI.ViewModels;

/// <summary>
/// How each process parameter is named and measured on screen.
/// </summary>
/// <remarks>
/// <para>
/// Presentation, not domain: the model knows the parameters, not the words the
/// operator reads. When the app gains a second language this table is what moves
/// behind a resource lookup — nothing in Domain or Calculation has to change.
/// </para>
/// <para>
/// The wording follows the approved dashboard design. Three different sets of
/// labels for these same fields exist across the earlier implementations
/// (see <c>docs/ui-design-inputs.md</c> §2.4); this is the one the screen was
/// designed against, and it is still awaiting a technologist's sign-off.
/// </para>
/// </remarks>
public static class ParameterLabels
{
    /// <summary>The wording the three station rows share, after their numbers.</summary>
    /// <remarks>
    /// The station rows lead with the numbers, not with the wording they share. The
    /// input list gives one line per parameter, so a name is read from its front —
    /// three rows starting "Содержание железорудного материала…" would be
    /// indistinguishable the moment the tail is trimmed.
    /// </remarks>
    private const string Stations = "содержание железорудного материала по станциям углового положения лотка";

    /// <summary>One label per parameter, in parameter-vector order.</summary>
    public static IReadOnlyList<ParameterLabel> All { get; } =
    [
        new(Parameter.BurdenLevel, "Фактический уровень засыпи (УЗ)", "Уровень засыпи", "м"),
        new(Parameter.OreLoadRate, "Рудная нагрузка (РН)", "Рудная нагрузка", "т/т"),
        new(Parameter.CokeSize, "Эквивалентная крупность кокса (ЭквК)", "Крупность кокса", "мм"),
        new(Parameter.SinterSize, "Эквивалентная крупность агломерата (ЭквАгл)", "Крупность агломерата", "мм"),
        new(Parameter.PelletsSize, "Эквивалентная крупность окатышей (ЭквОк)", "Крупность окатышей", "мм"),
        new(Parameter.PelletsRatio, "Доля окатышей в железорудной части шихты", "Доля окатышей", "доля"),
        new(Parameter.ChargeStations9To11, $"№№ 9-11 · {Stations}", "Станции 9-11", "%"),
        new(Parameter.ChargeStations6To8, $"№№ 6-8 · {Stations}", "Станции 6-8", "%"),
        new(Parameter.ChargeStations3To5, $"№№ 3-5 · {Stations}", "Станции 3-5", "%"),
        new(Parameter.HotBlastPressure, "Давление горячего дутья (ДавГорДут)", "Давление горячего дутья", "атм"),
        new(Parameter.TopGasPressure, "Давление колошникового газа (ДавКолГаз)", "Давление колошникового газа", "атм"),
    ];
}
