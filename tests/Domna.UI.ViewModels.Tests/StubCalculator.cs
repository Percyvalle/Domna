using Domna.Calculation;
using Domna.Domain.Interfaces;
using Domna.Domain.Models;

namespace Domna.UI.ViewModels.Tests;

/// <summary>
/// The real calculator, except where a test needs an answer the real one will not
/// give — a non-finite profile, which the screen has to survive.
/// </summary>
internal sealed class StubCalculator : ITemperatureProfileCalculator
{
    private readonly TemperatureProfileCalculator _real = new();

    public TemperatureProfile? Override { get; set; }

    public TemperatureProfile Calculate(ParameterSet parameters) =>
        Override ?? _real.Calculate(parameters);
}
