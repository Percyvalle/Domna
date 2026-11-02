using Domna.Domain.Parameters;

namespace Domna.UI.ViewModels;

/// <summary>
/// How one process parameter is named on screen.
/// </summary>
/// <remarks>
/// Two names, because the screen needs two. The input list carries the full
/// technological wording the operator recognises from the shop; the calculation
/// protocol, where the row is one line of a printed table, carries the short form.
/// </remarks>
/// <param name="Parameter">The parameter being named.</param>
/// <param name="Name">Full wording, for the input list.</param>
/// <param name="ShortName">Short wording, for the protocol table.</param>
/// <param name="Unit">Unit of measurement.</param>
public readonly record struct ParameterLabel(Parameter Parameter, string Name, string ShortName, string Unit);
