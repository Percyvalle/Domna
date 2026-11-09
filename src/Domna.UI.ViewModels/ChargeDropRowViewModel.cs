using System.Collections.ObjectModel;
using System.Globalization;
using Domna.Domain.Models;

namespace Domna.UI.ViewModels;

public sealed class ChargeDropRowViewModel
{
    internal ChargeDropRowViewModel(ChargeMaterial material, ChargeDrop? drop = null)
    {
        Material = material == ChargeMaterial.Coke ? "К" : "Р";
        for (int station = 1; station <= ChargeDrop.StationCount; station++)
        {
            int count = drop?[station] ?? 0;
            Portions.Add(count == 0 ? string.Empty : count.ToString(CultureInfo.CurrentCulture));
        }
    }

    public string Material { get; set; }

    public ObservableCollection<string> Portions { get; } = [];
}
