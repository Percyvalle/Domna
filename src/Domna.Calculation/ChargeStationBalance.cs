using Domna.Domain.Models;
using Domna.Domain.Optimization;
using Domna.Domain.Parameters;

namespace Domna.Calculation;

internal static class ChargeStationBalance
{
    public static Parameter[] MovableParameters(OptimizationRequest request)
    {
        Parameter[] free = [.. request.FreeParameters.Distinct()];
        Parameter[] stations = [.. free.Where(IsStation)];
        double total = stations.Sum(parameter => request.Start[parameter]);
        double minimum = stations.Sum(parameter => ParameterCatalog.RangeOf(parameter).Minimum);
        double maximum = stations.Sum(parameter => ParameterCatalog.RangeOf(parameter).Maximum);

        // A lone zone has nowhere to transfer material. An infeasible station total
        // likewise cannot be repaired without changing the operator's starting sum.
        return stations.Length < 2 || total < minimum || total > maximum
            ? [.. free.Where(parameter => !IsStation(parameter))]
            : free;
    }

    public static ParameterSet Restore(ParameterSet candidate, ParameterSet start, Parameter[] free)
    {
        double difference = 0.0;
        double capacity = 0.0;
        foreach (Parameter parameter in free)
        {
            if (IsStation(parameter))
            {
                difference += start[parameter] - candidate[parameter];
            }
        }

        bool increase = difference > 0.0;
        foreach (Parameter parameter in free)
        {
            if (IsStation(parameter))
            {
                capacity += Capacity(candidate, parameter, increase);
            }
        }

        // Distribute the correction in proportion to the remaining room in each
        // zone. Every balanced point is unchanged, so the whole feasible region
        // remains searchable, including its boundaries.
        foreach (Parameter parameter in free)
        {
            if (!IsStation(parameter) || capacity <= 0.0)
            {
                continue;
            }

            double room = Capacity(candidate, parameter, increase);
            ParameterRange range = ParameterCatalog.RangeOf(parameter);
            double value = Math.Clamp(candidate[parameter] + (difference * (room / capacity)), range.Minimum, range.Maximum);
            difference -= value - candidate[parameter];
            capacity -= room;
            candidate = candidate.With(parameter, value);
        }

        return candidate;
    }

    private static bool IsStation(Parameter parameter) => parameter is
        Parameter.ChargeStations9To11 or Parameter.ChargeStations6To8 or Parameter.ChargeStations3To5;

    private static double Capacity(ParameterSet parameters, Parameter parameter, bool increase)
    {
        ParameterRange range = ParameterCatalog.RangeOf(parameter);
        return increase ? range.Maximum - parameters[parameter] : parameters[parameter] - range.Minimum;
    }
}
