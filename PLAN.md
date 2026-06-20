# Alembic — Implementation Plan & Handoff

This document is the working plan for Alembic. It captures the current state, the design
decisions that are already settled (do **not** re-open these without reason), the project
conventions, and the prioritized list of remaining work. A fresh session should be able to
continue from here without re-deriving the design.

---

## 1. What Alembic is

A **language-agnostic planning engine** for .NET, shipped as a pure library. It plans operations
over a tree of nodes. The consumer supplies the node types and the rules; Alembic supplies the
machinery that matches rules against the tree, applies them, and drives the rewrite. It attaches
**no meaning** to nodes — they may model a query, a pipeline, a build graph, an expression
language, anything. It is the planning core on its own, with no relational algebra.

The design distills ideas from the planner of Apache Calcite (immutable nodes, traits,
conventions, rules, planner) but strips the relational parts. **Do not mention Calcite anywhere in
the source code** (comments or XML docs). The README may mention it as a derivation; the code may
not.

---

## 2. Current state (what is built and working)

- Target framework `net8.0` (the chosen floor; the machine SDK is .NET 10, `global.json` rolls
  forward).
- `dotnet test src/Alembic.Tests/Alembic.Tests.csproj` → **2/2 passing**.
- `dotnet build Alembic.sln` → clean (0 warnings, 0 errors).
- The dist pipeline works: `Alembic.dist.msbuildproj` produces `dist/nuget/*.nupkg` (+ `.snupkg`)
  and a published `dist/tests/...` bundle. **Run dist/msbuild via PowerShell, not Git Bash** —
  Git Bash mangles `/p:` switches (use `-p:` or the PowerShell tool).

### Implemented types

```
src/Alembic/
  Algebra/
    INode.cs            interface: Traits, Children, Copy, DeepEquals, DeepHashCode
                        + DIMs: Convention, IsLeaf, WithChild
    NodeBase.cs         abstract base: one `Signature` override drives DeepEquals/DeepHashCode,
                        caches the hash (the AbstractRelNode-style pattern)
  Plan/
    IPlanner.cs         SetRoot(node), FindBestPlan()
    Traits/
      ITrait.cs         Def; DIM Satisfies (defaults to equality)
      ITraitDef.cs      Name, Default (non-generic, for the registry)
      TraitDef.cs       abstract TraitDef<TTrait> : ITraitDef
      Convention.cs     a trait; equal by name; Convention.None sentinel
      ConventionTraitDef.cs  singleton def; always registered first (ordinal 0)
      TraitContext.cs   owns ordinals + the TraitSet intern cache; exposes Empty
      TraitSet.cs       interned, positional ImmutableArray<ITrait>; Get/Replace/Convention
    Rules/
      IRule.cs          Operand, OnMatch; DIM Matches (defaults true)
      Operand.cs        node-type + optional predicate + child operands; Operand.Of<T>(...)
      RuleCall.cs       bound nodes (pre-order), Node<T>(i), Transform(equiv), Result
      IConverterRule.cs mixin interface: supply In/Out/Operand/Convert, OnMatch provided via DIM
    Hep/
      HepMatchOrder.cs  Arbitrary | BottomUp | TopDown | DepthFirst
      HepProgram.cs     rules + match order + match limit; Builder()
      HepProgramBuilder.cs  fluent: AddRule / AddMatchOrder / AddMatchLimit / Build
      HepPlanner.cs     the planner (see note below)

src/Alembic.Tests/
  Fixtures.cs           toy Logical/Physical conventions, 4 node types, 2 converter rules
  LoweringTests.cs      bottom-up logical->physical lowering; trait-set interning
```

### How the current HEP planner works (important)

`HepPlanner` currently rewrites the **immutable tree directly** — it does NOT build the shared
vertex DAG. Per pass it walks the tree in match order (bottom-up rewrites children first, then
applies rules to the rebuilt parent), and re-passes until `DeepEquals` reports a fixed point (or
the pass limit, default 1024). When a child changes, the parent is rebuilt with
`node.Copy(node.Traits, newChildren)` (mechanical spine reconstruction, not rule-driven creation).
`ApplyRules` applies at most one transform per node per pass; cascades are handled by re-passing.

This is correct but unoptimized. The shared-DAG dedup is the first roadmap item; the identity
machinery it needs (`DeepEquals`/`DeepHashCode`) is already in place.

---

## 3. Settled design decisions (do not re-open without reason)

1. **`INode` is an interface, not a base class.** This lets a consumer implement it directly on a
   domain type they own (single-inheritance / record constraints make a base class hostile to
   that). `NodeBase` is an optional convenience base.
2. **Nodes are immutable.** Rewriting produces new nodes via `Copy(traits, children)`; untouched
   subtrees are shared by reference. This is what makes structural sharing, interning, and
   copy-on-write sound.
3. **Identity is split two ways.** A node's own `Equals`/`GetHashCode` stay as reference identity.
   Structural equivalence (what the planner dedups on) is the separate `DeepEquals` / `DeepHashCode`
   contract, derived on `NodeBase` from a single `Signature` member (the node's own attributes,
   excluding children) plus the children. `NodeBase` caches the hash (`_hash`, `== 0` sentinel).
4. **`Children` (not `Inputs`).** The engine needs to navigate and rebuild the tree generically;
   `Children` is the honest, non-dataflow name. This is engine navigation, not rule-driven creation.
5. **Traits are interned and positional.** `TraitSet` is an `ImmutableArray<ITrait>` indexed by an
   ordinal the `TraitContext` assigns each `ITraitDef`. Equal sets are canonicalized to one shared
   instance, so a node's traits cost one pointer and common sets are singletons.
6. **Conventions are first-class traits.** Lowering between conventions is done by `IConverterRule`,
   realized as a **mixin interface** (supply `In`/`Out`/`Operand`/`Convert`; `OnMatch` is a DIM) —
   not a base class.
7. **Default interface methods (DIMs) for contracts; concrete classes for state.** DIMs carry
   derived/optional behavior (`Satisfies`, `Matches`, `IsLeaf`, `WithChild`, converter `OnMatch`).
   Anything stateful (planner, caches, interning) is a concrete class. No abstract base classes in
   the public API except `NodeBase`/`TraitDef<T>`.
8. **Node types are hand-written classes, not records.** Records were considered for ergonomics, but
   `NodeBase` caches the digest in a field and a record's `with` copy would carry a stale hash to a
   structurally different node. Caching-on-the-node + records are mutually exclusive; we chose
   caching. (If a payload-wrapping option is added later for foreign types, revisit.)
9. **HEP first, Volcano later.** Automatic trait enforcement (inserting converters to satisfy a
   required output convention) is a Volcano feature and is deferred. HEP does lowering via explicit
   converter rules and program order.
10. **Namespaces:** `Alembic.Algebra` (node model), `Alembic.Plan` (planner core), `Alembic.Plan.Traits`,
    `Alembic.Plan.Rules`, `Alembic.Plan.Hep`. `Alembic.Plan.Volcano` is reserved for the cost-based
    planner.

---

## 4. Project conventions (must follow)

- **No "Calcite" in source** (comments/docs). README may reference it as a derivation only.
- **`<summary>` XML docs are multi-line** — content on its own line(s) under `<summary>`, never
  inline.
- **Wrap at 140 columns** (`.editorconfig` `max_line_length = 140`).
- **One public type per file.**
- C# style (from `.editorconfig` + GeoDesk house style): file-scoped namespaces, Allman braces, a
  blank line just inside class braces (before first / after last member), `using`s **outside** the
  namespace, `_camelCase` `readonly` fields (often without explicit `private`), expression-bodied
  **properties** yes / expression-bodied **methods** no (use `{ return ...; }`), `Nullable` enable,
  `ImplicitUsings` disabled (explicit `using`s), `GenerateDocumentationFile` true.
- **`D:\geodesk-net` is the reference** for .NET infra and house style (build props, GitVersion,
  pipeline, dist projects). Alembic mirrors it, minus the IKVM-SDK / Java / submodule / GOL-tool
  specifics. `IKVM.Core.MSBuild` (referenced in `Directory.Build.props`) provides the
  `PackageProjectReference` / `PublishProjectReference` targets the dist projects use — keep it.
- Library `Alembic.csproj` currently suppresses `CS1591` (`NoWarn`) while XML docs are still being
  completed.

---

## 5. Remaining work (roadmap, roughly prioritized)

### Near-term (engine correctness/usefulness)
1. **Shared-DAG HEP planner.** Introduce `HepVertex` (wraps an `INode`, has an id, caches the
   structural hash). Build a DAG from the root bottom-up, dedup equivalent subtrees via a
   `Dictionary<INode, HepVertex>` keyed on `DeepHashCode`/`DeepEquals` (plain dictionary, not
   `ConditionalWeakTable`). Replace a matched vertex's contents on transform and propagate to
   parents (copy-on-write up the spine). This is the main perf/architecture step.
2. **`HepInstruction` model.** Replace the flat rule list with ordered instructions:
   `RuleInstance`, `RuleCollection`, `MatchLimit`, `Subprogram`, per-instruction match order. Build
   them via the program builder. Model as a sealed hierarchy / records + switch in the planner.
3. **General (non-converter) rewrite rules + matcher coverage.** Add tests/examples that exercise
   multi-level operand patterns, predicates, and rules that rewrite within a single convention
   (e.g., a "push filter into source" style rule). Confirm the operand matcher handles arity and
   nesting correctly.
4. **`RuleCall` multiple equivalents.** Today `Transform` records a single `Result`. Decide whether
   a rule may register several equivalents (needed once cost selection exists) and adapt.
5. **`RuleSet`** type — a named collection of rules, addable to a program in one call.

### Mid-term (richer trait/convention model)
6. **A second trait dimension** (e.g., an ordering or a "collation") to prove the generic trait
   system beyond convention, including a trait with a real `Satisfies` partial order (not just
   equality).
7. **Trait conversion hooks** on `TraitDef` (canConvert / convert) and an optional built-in
   converter node concept.

### Larger (cost + Volcano)
8. **Cost model.** `ICost` / `ICostModel` (deferred so far). Needed to choose among equivalents and
   for Volcano.
9. **Volcano (cost-based) planner** under `Alembic.Plan.Volcano`: equivalence classes
   (set / subset by trait), cost-based dynamic programming, and **automatic trait enforcement**
   (insert converters to satisfy a required output convention). This is the big one and depends on
   the cost model and the DAG work.

### Cross-cutting / polish
10. **Foreign-domain support (optional).** A payload-wrapping node option for third-party types that
    cannot implement `INode` directly (the "opaque payload" path discussed in design). Decide if/when
    this is worth it.
11. **Complete XML docs** and remove the `CS1591` suppression in `Alembic.csproj`.
12. **More tests:** match-limit/termination, deep trees, trait interning edge cases, top-down order.
13. **Naming decision still open:** `IConverterRule.In` / `Out` vs `From` / `To`.

---

## 6. Build & test

```pwsh
# from D:\alembic
dotnet test src/Alembic.Tests/Alembic.Tests.csproj   # run the unit tests
dotnet build Alembic.sln                              # full solution incl. dist (no-op) projects

# Full dist build (NuGet + published tests) — use PowerShell, NOT Git Bash:
dotnet msbuild Alembic.dist.msbuildproj -p:Configuration=Release -p:Version=1.0.0-dev
# outputs: dist/nuget/*.nupkg (+ .snupkg), dist/tests/Alembic.Tests/net8.0/...
```

CI is `.github/workflows/Alembic.yml` (build via the dist meta-project → test matrix → release with
GitVersion + NuGet trusted publishing). GitVersion config is `GitVersion.yml`
(`main` = `pre`, `develop` = `dev`).

---

## 7. Notes / gotchas

- The repo is git-initialized; the scaffold is **not yet committed** (as of this writing). Build
  output (`bin`/`obj`/`dist`) is gitignored.
- `D:\geodesk-net` is the user's other .NET project and the canonical reference for house setup.
- The user (Jerome Haltom) maintains `IKVM.Core.MSBuild`; reusing it here is intentional.
