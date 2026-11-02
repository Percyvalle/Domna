namespace Domna.UI.ViewModels;

/// <summary>One line of the calculation protocol's parameter table.</summary>
/// <param name="Name">Short parameter name.</param>
/// <param name="Value">The value as entered, formatted to the parameter's precision.</param>
/// <param name="Unit">Unit of measurement.</param>
public readonly record struct ProtocolRow(string Name, string Value, string Unit);
