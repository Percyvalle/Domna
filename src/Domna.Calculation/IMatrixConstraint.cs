using Domna.Domain.Models;

namespace Domna.Calculation;

internal interface IMatrixConstraint
{
    int Dimensions { get; }

    bool IsMovable { get; }

    double[] Encode();

    ParameterSet Decode(ParameterSet parameters, ReadOnlySpan<double> coords);

    ChargeProgram BuildUpdatedProgram(ChargeProgram original, ReadOnlySpan<double> coords);
}
