<p align="center">
  <img src="assets/logo.svg" alt="Alembic" width="360">
</p>

# Alembic

**A medium-agnostic planning and rewrite engine for .NET.**

Alembic takes a tree of operations you wrote and finds an **equivalent tree that is better** —
cheaper, or expressed in a form that can actually be executed. You bring your own op types, your own
rules, and your own cost model; Alembic provides the search: it matches rules against the tree,
generates equivalent alternatives, costs them, and drives the rewrite to a result.

It attaches **no meaning** to your ops. They may model a query, a data pipeline, a tensor or image
graph, a build graph, an expression language — anything expressible as a tree of operations. It is
not a query engine and assumes no query language.

Alembic is a medium-agnostic port of the core of [Apache Calcite](https://calcite.apache.org/)'s
planner (itself in the Volcano → Cascades lineage). It keeps the optimizer *ideas* — equivalence
classes, physical *traits*, *conventions*, pattern-matching *rules*, *converters*, and cost-based
search — and throws away everything specific to relational algebra and SQL, so the same machinery
applies to any domain.

## What it gives you

- **An op model.** Ops with a small, generic contract (`IOp`): ordered `Inputs`, a physical trait
  set, and a structural-identity contract (`DeepEquals`/`DeepHashCode`) the planner deduplicates on.
  The planner rewrites by producing new ops and sharing the subtrees it doesn't touch. `AbstractOp` is
  a convenience base; you can also implement `IOp` directly.
- **Output types.** Each op has an `OutputType` — an opaque, user-defined descriptor of *what it
  produces*. The planner uses it as the equivalence invariant (two ops can only be interchangeable if
  their outputs match) but attaches no meaning to it; ops that don't need one default to the trivial
  `Void`. A downstream layer can make it as rich as a relational row type.
- **Traits and conventions.** Interned, low-overhead physical properties carried by each op
  (sortedness, distribution, …). A **convention** is the coarsest trait — the family an op belongs to,
  e.g. a logical form versus a particular execution backend (CPU, GPU, an interpreter).
- **Rules and converters.** Strongly-typed pattern matching over subtrees. **Converter rules** lower
  an op from one trait to another (logical → physical, unsorted → sorted); the planner inserts trait
  **enforcers** only where they pay off.
- **Two planners over one model.** A **heuristic** planner (`HepPlanner`) that applies a program of
  rules to a fixed point — deterministic rewriting, canonicalization, straightforward lowering — and a
  **cost-based** planner (`VolcanoPlanner`) that searches equivalence classes and extracts the cheapest
  plan that satisfies a required output form. The *same* rules work under both.
- **A cost model.** A small, opaque cost (`IOpCost`): the engine only compares and adds costs, with no
  built-in units (and deliberately no row count — it is not a database). Each op states its own cost via
  `ComputeSelfCost`; the planner accumulates and minimizes.

## Example

Lower a logical plan to the cheapest physical one with the cost-based planner — choosing backends and
inserting conversions automatically:

```csharp
var planner = new VolcanoPlanner();
planner.AddRule(new LowerToCpu());     // logical → CPU
planner.AddRule(new LowerToGpu());     // logical → GPU
planner.AddRule(new DownloadRule());   // GPU → CPU enforcer
planner.AddRule(new UploadRule());     // CPU → GPU enforcer

planner.SetRoot(root);                                   // register the plan
planner.SetRoot(planner.ChangeTraits(root, cpuTraits));  // require the result on the CPU
var best = planner.FindBestPlan();
```

Asked for a CPU result over a pipeline of GPU-favorable ops with a CPU-only step in the middle, the
planner produces a plan that crosses **CPU → GPU → CPU → GPU → CPU**, inserting uploads and downloads
exactly where a run of GPU work outweighs the transfer cost — and *declines* to cross for a lone op
when the round-trip costs more than staying put.

For deterministic rewriting (no cost trade-offs), use the heuristic planner with a rule program:

```csharp
var planner = new HepPlanner(HepProgram.Builder().AddRuleCollection(rules).Build());
planner.SetRoot(root);
var plan = planner.FindBestPlan();
```

## Status

Early and evolving, but substantial. The current state provides the op model (with output types,
interned trait sets, conventions, and converters), a strongly-typed rule/operand matcher, **both**
planners — heuristic (HEP) and cost-based (Volcano) — with trait enforcement and a cost model, and a
metadata-query layer for cumulative cost, memory, and parallelism.

Future work includes a top-down (Cascades) search driver for the cost-based planner — the iterative
search already finds the optimal plan; top-down makes it efficient at scale — and a statistics/metadata
framework for deriving costs.

## License

Apache 2.0. See [LICENSE](LICENSE).
