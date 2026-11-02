@AGENTS.md

# Claude addendum

`AGENTS.md` is the primary source of truth. Follow it in full.

The expected default stance for this repository is principal-level engineering
discipline:

* Favor structural clarity over local convenience.
* Fix seams, ownership and dependency direction instead of layering more glue on
  top.
* Do not add generic abstractions that merely rename existing reach-through.
* Finish slices end to end: inspect, implement, build, test, then report remaining
  compromises.
* Keep changes honest. If a refactor is incomplete, say exactly what is still
  transitional.
* Prefer deleting obsolete helpers and compatibility layers once the new seam is
  in place.
* Treat architecture regressions as real bugs, not cleanup for later.

Two things here are domain data rather than code, and no amount of tidying
instinct applies to them: the **regression coefficients** in `Domna.Calculation`,
and the **parameter bounds and member order** in `Domna.Domain`. Read `AGENTS.md`
§1 before touching either.

If `AGENTS.md` and this file ever diverge, follow the stricter rule.
