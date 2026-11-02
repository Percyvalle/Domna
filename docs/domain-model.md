# The domain model

What the app computes, from what, and where it stops being trustworthy. This
document owns the domain; `AGENTS.md` owns the conventions.

## What is being modelled

Gas rising through a blast furnace does not leave the top evenly. Its temperature
across the radius — hot on the axis, cold where the charge piles up at the ore
ridge, somewhere between at the wall — is the visible trace of how the gas flow is
distributed, and the gas flow is what the charge program controls.

The operator reads three temperatures and, more importantly, the **ratios between
them**. A ratio describes the *shape* of the flow, and the shape is what the
operator can steer; the absolute level mostly follows the heat balance. This is
why the model fits two ratios directly rather than three temperatures.

## The eleven parameters

Order is the model's parameter vector. `Parameter` in `Domna.Domain` is declared
in exactly this order, and `ParameterCatalog` carries the bounds.

| # | `Parameter` | Russian | Unit | Min | Max | Default |
| --- | --- | --- | --- | --- | --- | --- |
| 0 | `BurdenLevel` | Уровень засыпи (УЗ) | m | 1.25 | 3.10 | 1.857 |
| 1 | `OreLoadRate` | Рудная нагрузка (РН) | t/t | 3.72 | 4.57 | 3.880 |
| 2 | `CokeSize` | Эквивалентная крупность кокса (ЭквК) | mm | 50.00 | 57.70 | 53.88 |
| 3 | `SinterSize` | Эквивалентная крупность агломерата (ЭквАгл) | mm | 9.74 | 11.69 | 10.31 |
| 4 | `PelletsSize` | Эквивалентная крупность окатышей (ЭквОк) | mm | 12.68 | 14.61 | 13.54 |
| 5 | `PelletsRatio` | Доля окатышей в железорудной части | fraction | 0.238 | 0.505 | 0.342 |
| 6 | `ChargeStations9To11` | Содержание материала по станциям 9-11 | % | 59.70 | 100.00 | 75.00 |
| 7 | `ChargeStations6To8` | Содержание материала по станциям 6-8 | % | 43.75 | 58.82 | 50.00 |
| 8 | `ChargeStations3To5` | Содержание материала по станциям 3-5 | % | 38.83 | 42.55 | 40.00 |
| 9 | `HotBlastPressure` | Давление горячего дутья (ДавГорДут) | atm | 2.45 | 2.80 | 2.661 |
| 10 | `TopGasPressure` | Давление колошникового газа (ДавКолГаз) | atm | 1.37 | 1.48 | 1.399 |

"Stations 3-5 / 6-8 / 9-11" are the distributor positions the charge is dropped
at, grouped into peripheral, intermediate and central rings.

## What the model computes

Three quantities are fitted directly, each a polynomial in the parameters above:

* `T_center` — gas temperature on the axis, °C
* `T_center / T_oreRidge` — the centre-to-ridge ratio
* `T_periphery / T_center` — the periphery-to-centre ratio

The remaining values are derived, never fitted:

```
T_oreRidge  = T_center / (T_center / T_oreRidge)
T_periphery = T_center * (T_periphery / T_center)
```

`TemperatureProfile` therefore stores only the three temperatures and computes any
ratio on demand, which is what makes a profile incapable of holding a temperature
and a ratio that contradict each other.

### The shape of the polynomials

Each expansion is a sum of an intercept and 22-23 terms over 25 monomials:

* cubes of single parameters — `BurdenLevel³`, `PelletsRatio³`, `TopGasPressure³`, …
* a parameter squared times a station reading — `BurdenLevel² · Stations6To8`
* a size times a station reading, or times its square — `CokeSize · Stations3To5²`

There are no linear terms and no free pairwise products beyond these: the model is
a fit over a narrow operating region, not a physical derivation. Outside that
region a cubic term diverges fast, which is exactly why the bounds matter.

**The 70 coefficients are measured data.** They live in
`Domna.Calculation/TemperatureProfileCalculator.cs` at full `double` precision, in
the fitted term order, one term per line. They were transferred from the reference
C++ implementation mechanically and verified as an exact multiset match, and the
computed profile at the catalogue defaults is pinned by a test:

```
Center     272.59362591618179 °C
OreRidge   139.41992678663084 °C
Periphery  170.40870586815851 °C
```

## The inverse problem

The forward direction — parameters to profile — is the model. The inverse
direction is a search over it: given a wanted profile, which parameters produce
it. `IParameterOptimizer` in `Domna.Domain` states the question and
`DifferentialEvolutionOptimizer` in `Domna.Calculation` answers it.

Four properties are load-bearing:

* **The objective is written on the two fitted ratios**, `T_center/T_oreRidge` and
  `T_periphery/T_center`, each as a relative miss so neither dominates through its
  scale. Those are the quantities the regression fits directly and the ones that
  describe the shape of the gas flow; the absolute level follows the heat balance
  and is not something the charge program sets.
* **Movable parameters stay within the fitted bounds.** The search box is
  `ParameterCatalog`. A target that is unreachable inside that box yields the
  best point found plus its residual.
  A nonzero residual does not establish unreachability or global optimality.
  Fixed extrapolated inputs and infeasible station totals remain pinned as
  described below; they are not silently pulled into the fitted region.
* **It is deterministic.** Each independent population is drawn from a SplitMix64
  stream with its own fixed seed, written out in `SearchSequence` rather than
  taken from `System.Random`, whose sequence is only stable within one runtime version. The
  same request returns the same answer on every machine.
* **The sum of the three charge-station values is conserved.** The sum of
  `ChargeStations9To11`, `ChargeStations6To8` and `ChargeStations3To5` stays at
  its value immediately before the search, within floating-point precision;
  it is not normalised to 100%. A decrease of two percentage points in one zone
  requires a combined increase of two points in the other free zones. Fixed
  stations stay fixed. With only one free station, that station cannot move.
  Every evaluated candidate, including the final simplex polish, respects this
  balance and the catalogue bounds. If an extrapolated starting sum of the free
  stations cannot fit within their combined bounds, the station values remain
  pinned; other free parameters can still move.

Only the parameters the operator marks free are moved; the rest stay pinned,
because in the shop most of the eleven are not the operator's to change on the
day. The search performs four independent DE/rand/1/bin rounds, each initialized
uniformly over the complete normalized box and retaining the starting point.
Each round runs 90 generations with the requested F and CR settings. It does not
shrink subsequent populations around a previous round's winner.

Up to three promising candidates per round are retained with at least 0.1
separation in the maximum coordinate distance in normalized space. Candidates are
collected throughout the round, before the population can collapse into a single
basin. Each retained candidate receives two bounded Nelder-Mead passes; rebuilding
the simplex helps recover from flattening caused by clamping at a boundary. The
best point from all evaluations is retained, including when the search is cancelled;
cancellation during local refinement is reported as cancellation too. Progress
reports follow each complete round, including its local refinements.

Independent starts and multiple local refinements reduce sensitivity to local
minima; they do not prove that the returned point is a global optimum. A known
reachable-target regression test and a corpus of 541 reachable targets with varied
starts protect against the previously observed missed exact solutions. The
residual describes agreement with the fitted ratios, not uncertainty in the fit
or agreement of the three absolute temperatures. No technological acceptance
threshold is inferred from numerical convergence.

## Where the model stops

* **Outside the bounds there is no model.** The app still computes and displays a
  result, because refusing to would hide information the operator may legitimately
  want, but it flags every out-of-range input and marks the result as
  extrapolation. Do not silently clamp, and do not widen a bound.
* **Nothing here is a heat balance.** The model cannot explain *why* a profile
  changes; it interpolates a fit. It has no notion of blast temperature, moisture,
  coke quality beyond granulometry, or furnace geometry.
* **It says nothing about time.** Every parameter is a steady-state value; there is
  no dynamics, no lag, no transient.

## Open questions

Not resolved silently in passing. If a task depends on one of these, state the
assumption in your report.

1. **Two conflicting bound sets exist in the reference material.** The predecessor
   projects carry different limits for the same parameters — for example
   `BurdenLevel` as 1.25-3.10 in one and 1.25-2.46 in the other, `SinterSize` as
   9.74-11.69 versus 9.94-10.67. `ParameterCatalog` uses the wider set, which is
   also the only one carrying default values. Which set is the true fitted region
   needs confirming with whoever produced the fit; the narrower one may be the
   region actually covered by the training data.
2. **The provenance of the fit is undocumented.** The training dataset, the furnace
   it was measured on, the period, the goodness of fit and the residual spread are
   all unknown here. Without them the app cannot tell an operator how much to
   trust a number, and a confidence interval cannot be shown even in principle.
3. **Physical ordering of the three temperatures is an observation, not a rule.**
   At the catalogue defaults the ore ridge is coldest and the centre hottest. Tests
   assert that at the defaults only, because nothing establishes it holds
   everywhere in the fitted region.
4. **The optimizer has no notion of an unreachable target.** It returns its best
   point and the residual it reached; nothing distinguishes "matched exactly" from
   "this is as close as the fitted region allows" except the size of that residual,
   and no threshold for it has been agreed with a technologist.
