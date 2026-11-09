namespace Domna.Domain.Models;

/// <summary>The ore shares of the three station groups, in percent.</summary>
/// <param name="Stations9To11">The share at stations 9 through 11.</param>
/// <param name="Stations6To8">The share at stations 6 through 8.</param>
/// <param name="Stations3To5">The share at stations 3 through 5.</param>
public readonly record struct ChargeStationShares(double Stations9To11, double Stations6To8, double Stations3To5);
