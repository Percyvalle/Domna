# Domna — Agent & Contributor Guide

This is the working guide for Claude Code and contributors. It covers conventions,
layering rules and repository shape. The domain itself — what the eleven parameters
mean, where the model came from, what it is allowed to say — lives in
[`docs/domain-model.md`](docs/domain-model.md). **If this file and the domain
document disagree about the domain, the domain document wins.**

> **Domna** models the thermal state of a blast furnace top. The operator enters
> eleven process parameters (charge program, granulometry, pressures) and the app
> predicts the radial gas-temperature profile — centre, ore ridge, periphery — and
> the ratios between them, from a fitted polynomial regression.
> Stack: **C# / .NET 10 / WPF**, MVVM via CommunityToolkit.Mvvm, xUnit. Desktop
> only: no server, no database, no network, no telemetry.

---

## 1. Red lines

Breaking these does not produce a code smell — it produces a plausible-looking
number that is wrong, which an operator may act on.

1. **The regression coefficients are measured data.** Never round, re-derive,
   rename, reorder or "clean up" a coefficient, and never invent one. They are
   carried at full `double` precision in the fitted term order.
2. **`Parameter` member order is the model's parameter vector.** Inserting,
   removing or reordering a member silently re-maps every coefficient onto the
   wrong monomial. The enum is domain data, not a listing.
3. **Parameter bounds are the fitted region, not UI preferences.** Never widen a
   bound to make an input "work". Outside the region the regression says nothing;
   the app computes anyway and flags the result as extrapolation.
4. **The calculation layer is pure.** No I/O, no clock, no randomness, no DI, no
   async, no UI types. Same input, same output, always — the UI recalculates on
   every keystroke and the optimizer will call it millions of times.
5. **Culture is explicit at every boundary.** Display uses the current culture (a
   Russian operator expects a decimal comma); parsing, storage and export use
   `CultureInfo.InvariantCulture`. Never call a `Parse`/`ToString` overload that
   picks a culture implicitly. The one exception is `HistoryTable`, the spreadsheet
   export: Excel parses a cell with the decimal separator of the machine it runs
   on, so an invariant file lands in a Russian Excel as one column of text. It
   takes the culture as a parameter and the caller passes the operator's — explicit,
   which is what this rule is actually about.

---

## 2. Repository shape

The layout is fixed. A new top-level directory is a deliberate decision, not a
side effect of a task.

```
Domna/
├─ AGENTS.md                  this file
├─ CLAUDE.md                  pointer to this file
├─ README.md                  what it is, how to build and run
├─ Domna.sln
├─ Directory.Build.props      shared properties + analyzers for every project
├─ .editorconfig              analyzer rule configuration (the only place for it)
├─ Build/                     all build output, `Build/<Project>/{bin,obj}` — generated, gitignored
├─ docs/
│  ├─ domain-model.md         the parameters, the model, its limits and open questions
│  ├─ ui-design-inputs.md     what must be gathered before the interface can be designed
│  └─ _local.*.md             drafts and plans, never committed (see §6)
├─ src/
│  ├─ Domna.Domain/           vocabulary: parameters, bounds, profile, interfaces.
│  │                          Dependency-free leaf — references nothing, ever
│  ├─ Domna.Calculation/      the fitted model. Pure functions over Domain types
│  ├─ Domna.UI.ViewModels/    MVVM presentation logic. No WPF types
│  └─ Domna.App/              WPF host: App, MainWindow, Themes
└─ tests/
   ├─ Domna.Domain.Tests/            vocabulary invariants
   └─ Domna.Calculation.Tests/       the model, pinned to reference values
```

### Where new code goes

| What | Where |
| --- | --- |
| A parameter, a bound, a unit of domain vocabulary | `Domna.Domain/` |
| A formula, a coefficient, the optimizer | `Domna.Calculation/` |
| Screen state, formatting, commands | `Domna.UI.ViewModels/` |
| XAML, styles, converters, window code-behind | `Domna.App/` |
| A draft, a plan, an investigation | `docs/_local.*.md` |

**Banned folders:** `Helpers/`, `Utils/`, `Misc/`, `Shared/`, `Common/`, `Core/`.
They are dumping grounds — code inside them loses its owner. If a function has
nowhere to live, the concept has not been named yet: name it.

---

## 3. Dependency & layering rules (enforced)

```
Domna.App ──► Domna.UI.ViewModels ──► Domna.Domain
    └───────► Domna.Calculation ────► Domna.Domain
```

1. `Domna.Domain` references nothing — no package, no project. It is the shared
   vocabulary and it must stay loadable by anything.
2. `Domna.Calculation` references `Domna.Domain` and nothing else. Every public
   method is deterministic and unit-tested.
3. `Domna.UI.ViewModels` never references WPF (`PresentationFramework`,
   `System.Windows.*`). It is testable without a UI thread, and that is the point.
4. The view models depend on `ITemperatureProfileCalculator`, never on the
   concrete calculator. `Domna.App` is the only place that picks an implementation.
5. No calculation in a view model, and no domain rule in XAML or code-behind. A
   view model reads state and formats it; the arithmetic lives in `Domna.Calculation`.
6. Nothing references `Domna.App`.

---

## 4. Code conventions

* File-scoped namespaces; `Nullable` and `ImplicitUsings` enabled; **one public
  type per file**, file name = type name.
* Idiomatic .NET 10: `record` / `readonly record struct` for values, `required`
  members, collection expressions, pattern matching. No `Manager`/`Helper` class
  names — name a type after its responsibility.
* Domain values are immutable. `ParameterSet.With(...)` returns a new set; nothing
  mutates a parameter vector in place.
* **XML documentation is required on the `Domna.Domain` public surface** and only
  there (`src/Domna.Domain/.editorconfig` enforces it). Elsewhere a comment states
  a *reason*; it never restates the code.
* A file past ~300 lines or a method past ~40 is a reason to split. The regression
  expansions in `Domna.Calculation` are the one deliberate exception: they are one
  term per line so they stay diffable against the source they were ported from.
* `double` throughout — this is process physics, not money. Never compare two
  computed doubles with `==` except to test a divide-by-zero guard.
* XAML carries no geometry or colour of its own: spacing, brushes and text styles
  come from `Domna.App/Themes/Theme.xaml`. A screen arranges shared styles; it
  does not fork their dimensions.

### Analyzers (keep the build at zero)

StyleCop.Analyzers + Roslynator + the .NET analyzers run on every project via
`Directory.Build.props`. `TreatWarningsAsErrors` is `false`, so the discipline is
yours: **leave the solution at zero warnings.** Don't silence a rule with
`#pragma` or `NoWarn` — write code that satisfies it. Deliberate rule exceptions
live in an `.editorconfig` with a justification comment, and there are three: the
root one, `src/Domna.Domain/.editorconfig` (docs required there), and
`tests/.editorconfig` (test naming).

Verify with a full rebuild — an incremental build hides warnings in projects you
did not touch:

```
dotnet build Domna.sln -c Debug --no-incremental
```

---

## 5. Tests

A change to the model or the vocabulary is not done without them.

* **The model is pinned to reference values.** `Domna.Calculation.Tests` asserts
  the profile at the catalogue defaults against numbers computed independently of
  this implementation. If a coefficient, a monomial or a term order drifts, that
  test is the only thing that will notice — never update its expected values to
  match new output. A failure there means the port broke, or the fit itself
  changed and you can say exactly why.
* **The fitted region is exercised at its corners**, not only at the defaults.
* **The vocabulary defends itself:** parameter indices contiguous from zero, every
  parameter has a range, every default sits inside its range.
* View models are tested without a UI thread; if a test needs a `Dispatcher`,
  logic has leaked into the view.

Run the suite with `dotnet test Domna.sln`.

---

## 6. Working rules

1. **Definition of done:** a clean `--no-incremental` build with zero warnings, a
   green `dotnet test`, and `docs/domain-model.md` updated if the domain changed.
2. One feature per commit. Never change behaviour and structure in the same
   commit — refactor first, then add the feature.
3. **Commit format:** `[TYPE] summary`, TYPE ∈ `FEATURE` / `FIX` / `REFACT` /
   `DOC`, summary ≤ 70 chars, imperative mood.
   Example: `[FIX] flag extrapolated results in the profile panel`.
4. **Never add AI attribution** to commits — no `Co-Authored-By`, no
   "Generated with".
5. Drafts, plans and notes use the `_local.` prefix (`docs/_local.optimizer-port.md`).
   They are gitignored but stay on disk across sessions.
6. Leave no litter in the tree: temp scripts, dumps, `*.bak` and experiments go to
   the scratchpad or a `_local.*` file.
7. The open questions in `docs/domain-model.md` are not resolved silently in
   passing. If a task depends on one, state the assumption in your report.

---

## 7. Engineering stance

* Favor structural clarity over local convenience. Fix seams and dependency
  direction instead of layering more glue on top.
* Don't add abstractions that merely rename existing reach-through.
* Finish slices end to end: inspect → implement → build → test → report remaining
  compromises. Say plainly when a refactor is still transitional.
* Delete obsolete helpers and compatibility layers once the new seam is in place.
* Treat architecture regressions as real bugs, not cleanup for later.
