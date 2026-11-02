namespace Domna.UI.ViewModels;

/// <summary>One computed temperature on the calculation protocol.</summary>
/// <param name="Name">The measuring position, spelled out.</param>
/// <param name="Value">The temperature, formatted with its unit.</param>
public readonly record struct ProtocolReading(string Name, string Value);
