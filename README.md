# Domna

Desktop application that models the thermal state of a blast furnace top. Enter
eleven process parameters — charge program, granulometry, pressures — and it
predicts the radial gas-temperature profile across the furnace top: centre, ore
ridge, periphery, and the ratios between them.

C# / .NET 10 / WPF. No server, no database, no network.

## Build and run

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download) on Windows.

```
dotnet build Domna.sln -c Debug --no-incremental
dotnet test  Domna.sln
dotnet run   --project src/Domna.App
```

Build output for every project is centralized under `Build/<Project>/{bin,obj}` and is
not committed.

## Portable release

One self-contained `.exe` for a machine that has no .NET installed — copy it to a flash
drive, copy it off, double-click:

```
dotnet publish src/Domna.App -c Release -p:PublishProfile=Portable
```

The result is a single `Build/publish/Domna.App.exe` (~67 MB, win-x64). It carries the
.NET 10 runtime and WPF inside itself, writes nothing next to itself, and needs no
installer or admin rights. On first launch it unpacks to the user's temp directory, so
that start takes a few seconds; later ones are immediate.

It is deliberately **not** trimmed: WPF does not support IL trimming, and globalization
data stays in because the operator's decimal comma depends on it (`AGENTS.md` §1.5).

Requires Windows 10 version 1607 or newer (64-bit) — .NET 10 does not run on Windows 7
or 8.x. Settings for the profile live in
`src/Domna.App/Properties/PublishProfiles/Portable.pubxml`.

For a machine that already has the .NET 10 Desktop Runtime, a small framework-dependent
build is enough:

```
dotnet publish src/Domna.App -c Release -r win-x64 --self-contained false
```

## Layout

```
src/Domna.Domain/          parameters, bounds, the temperature profile — the shared vocabulary
src/Domna.Calculation/     the fitted regression model. Pure functions, no I/O
src/Domna.UI.ViewModels/   MVVM presentation logic, testable without a UI thread
src/Domna.App/             the WPF host: theme, screens, hand-drawn charts
tests/                     xUnit suites for the vocabulary, the model and the view models
```

## Documentation

* [`docs/domain-model.md`](docs/domain-model.md) — what is modelled, from what,
  and where the model stops being trustworthy. Read this before changing anything
  numeric.
* [`AGENTS.md`](AGENTS.md) — conventions, layering rules, working rules.

## The screen

One dashboard, three columns. Eleven process parameters on the left; the three
temperature ratios, the radial profile, the furnace-top map and the optimizer in
the middle; the session's saved calculations on the right.

Recalculation happens on the **Рассчитать** button, not on every keystroke: the
screen keeps the last result and says out loud when the input has moved past it,
so a number on screen always belongs to one complete parameter set. A value
outside the fitted region is computed, flagged red, and the result is labelled an
extrapolation — see `docs/domain-model.md`.

Saving a calculation makes it the baseline: every delta on the screen is read
against it, and the optimizer drives the ratio gaps to it. History lives in memory
only and is lost when the app closes — **В Excel** writes it out first, one row per
saved calculation with its eleven inputs, the three temperatures, the three ratios
and whether the row is an extrapolation.

That file is a `.csv` written in the operator's own culture, delimiter included,
because Excel reads a cell with the decimal separator of the machine it runs on. It
carries a UTF-8 byte-order mark so the Cyrillic headings survive the trip.

## Status

The screen is complete against the approved dashboard design, on real numbers
throughout. Still open:

* **numeric input** commits on focus loss and accepts either decimal separator; it
  does not yet constrain keystrokes or show a per-field message;
* **the optimizer** uses four independent differential-evolution searches with
  Nelder-Mead refinement of multiple candidates. It returns the best point found,
  but does not prove global optimality or that a nonzero residual is unavoidable;
* **the open questions** in `docs/domain-model.md` and `docs/ui-design-inputs.md`
  are unresolved: the true bound set, the provenance of the fit, and sign-off on
  the parameter wording the screen uses.
