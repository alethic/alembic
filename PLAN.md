# Alembic — Implementation Plan & Handoff

This document is the working plan for Alembic. It captures the current state, the design
decisions that are already settled (do **not** re-open these without reason), the project
conventions, and the prioritized list of remaining work. A fresh session should be able to
continue from here without re-deriving the design.

For the conceptual model — what traits mean, logical vs. physical plans, how the planners and cost
work — see [THEORY.md](THEORY.md). This file is *state and plan*; THEORY.md is *the durable mental
model*.

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

**Design philosophy (important):** the goal is to give library *users* the same capabilities Calcite
gives — so **replicate Calcite's design surface** (conventions that register rules, a cluster, rule
calls subclassed per planner, single-child node bases, visitors, etc.), adapting only where Calcite is
relational-algebra-specific or where our generic-over-`TNode` engine requires a different shape. Do
**not** omit a Calcite design element merely because the current milestone doesn't need it yet — these
are features for downstream users.

> **Node model (executive decision):** the engine operates on **`INode` directly**, monomorphically,
> exactly as Calcite operates on `RelNode`. An earlier generic-over-store design (`INodeStore<TNode>`)
> was explored and **reverted** — `INode` *is* the node model; there is no store abstraction.

---

## 2. Current state (what is built and working)

- Target framework `net8.0` (the chosen floor; the machine SDK is .NET 10, `global.json` rolls
  forward).
- `dotnet test src/Alembic.Tests/Alembic.Tests.csproj` → **41/41 passing**.
- `dotnet build Alembic.sln` → clean (0 warnings, 0 errors).
- The dist pipeline works: `Alembic.dist.msbuildproj` produces `dist/nuget/*.nupkg` (+ `.snupkg`)
  and a published `dist/tests/...` bundle. **Run dist/msbuild via PowerShell, not Git Bash** —
  Git Bash mangles `/p:` switches (use `-p:` or the PowerShell tool).

### Implemented types

```
src/Alembic/
  Algebra/
    INode.cs            interface (THE node model): Traits, Children, Copy, DeepEquals, DeepHashCode
                        + DIMs: Convention, IsLeaf, WithChild, ComputeSelfCost (RelNode.computeSelfCost), GetDigest
    AbstractNode.cs     abstract base: Explain(INodeWriter) lists terms; drives DeepEquals/DeepHashCode; keeps a digest
    INodeWriter.cs      collects a node's explain terms (attrs + inputs) — the RelWriter analog
    INodeDigest.cs      a node's structural digest (RelDigest analog); AbstractNode keeps an inner one (cached
                        hash, Done() renders the string); NodeDigest is the standalone default impl
    NodePlan.cs         INode.ToPlanString() — renders a plan as an indented tree (type/traits/attrs,
                        inputs nested) from the same Explain terms; for display/debugging
    SingleNode.cs       single-child AbstractNode (the SingleRel analog); lists its input as a term
    BiNode.cs           two-child AbstractNode with Left/Right (the BiRel analog); lists both inputs
    Convert/            (the rel.convert analog)
      IConverter.cs     a node that converts one trait of its input (Converter analog): InputTraits/TraitDef/Input
      ConverterImpl.cs  abstract single-input converter base (ConverterImpl analog)
  Plan/                 (traits live here, flat, mirroring Calcite's plan package)
    IPlanner.cs         IPlanner: AddTraitDef / TraitDefs / EmptyTraitSet / CostFactory / AddRule /
                        SetRoot / ChangeTraits / FindBestPlan — the RelOptPlanner analog
    CannotPlanException.cs  thrown by FindBestPlan when the requested output traits can't be met
    AbstractPlanner.cs  AbstractPlanner: shared trait-def registry + EmptyTraitSet + rules + cost factory
                        — the AbstractRelOptPlanner analog
    Cluster.cs          Cluster: session environment (wraps the planner) — the RelOptCluster analog
    ICost.cs            ICost: IsInfinite / IsLessThanOrEqual / IsLessThan / Plus (the RelOptCost analog)
    ICostFactory.cs     ICostFactory: MakeCost(cpu,io) + zero/infinite/huge/tiny (RelOptCostFactory analog)
    Cost.cs             concrete scalar cost + factory (the RelOptCostImpl analog)
    ITrait.cs           Def; DIM Satisfies; DIM Register(planner) — the RelTrait.register analog
    IMultipleTrait.cs   a trait a node can have several of at once (RelMultipleTrait analog)
    CompositeTrait.cs   a trait that bundles several same-dimension traits (RelCompositeTrait analog)
    INodeImplementor.cs marker for a convention's plan-implementation callback (RelImplementor analog)
    ITraitDef.cs        Name, Default (registered on the planner)
    TraitDef.cs         abstract TraitDef<TTrait> : ITraitDef
    IConvention.cs      the Convention interface: Name, Interface (getInterface), CanConvertConvention,
                        UseAbstractConvertersForConversion, Enforce — the Convention (interface) analog
    Convention.cs       the default IConvention impl (Convention.Impl analog): equal by name, unsealed,
                        Convention.None sentinel, overrides ITrait.Register (Convention.register)
    ConventionTraitDef.cs  singleton def; the always-present convention dimension
    TraitSet.cs         interned, ordered ImmutableArray<ITrait>; CreateEmpty / Plus / Replace (single or
                        a list → CompositeTrait) / Get / GetList / Convention / Satisfies; linear FindIndex;
                        inner intern Cache (Calcite's RelTraitSet)
    IPlannerListener.cs planning-event listener + nested events (RelOptListener analog)
    Rules/
      IRule.cs          IRule: Operand + OnMatch — every rule has an operand (Calcite's RelOptRule)
      RuleCall.cs       RuleCall: abstract base — operand-bound Nodes + Node(i) (the rel(i) accessor), Transform
      Operand.cs        Operand: predicate + child operands; Of<TNode>(...)
      OperandMatcher.cs static matcher; Match returns the operand-bound nodes (or Matches: bool)
      IConverterRule.cs mixin: Source/Target (any ITrait, not just a convention); Convert(node) returns
                        null to decline; operand matches the Source trait, OnMatch = Convert (DIMs)
      ConverterRule.cs  abstract base: holds Source/Target, leaves Convert abstract
    Hep/
      HepMatchOrder.cs  Arbitrary | BottomUp | TopDown | DepthFirst
      HepInstruction.cs HepInstruction + nested instruction/state types + PrepareContext
      HepState.cs       base for an instruction's mutable per-run state
      HepProgram.cs     HepProgram : HepInstruction — ordered instructions; prepare() → State; Builder()
      HepProgramBuilder.cs  fluent: AddRuleInstance / AddRuleCollection / AddRuleClass /
                        AddRuleByDescription / AddConverters / AddCommonRelSubExprInstruction /
                        AddMatchOrder / AddMatchLimit / AddSubprogram / AddGroupBegin / AddGroupEnd
      HepNodeVertex.cs  vertex wrapping a current node in the shared DAG (the HepRelVertex analog)
      HepPlanner.cs     HepPlanner : AbstractPlanner — DirectedGraph DAG, GC, fired-rules cache (see note)
      HepRuleCall.cs    HepRuleCall : RuleCall — the HEP rule-call
    Volcano/            the cost-based planner
      NodeSet.cs        NodeSet: an equivalence set, partitioned into subsets (the RelSet analog)
      NodeSubset.cs     NodeSubset: members sharing a trait set; tracks Best/BestCost (the RelSubset analog)
      VolcanoCost.cs    VolcanoCost: cpu/io cost (compare on cpu) + factory; the planner default
      VolcanoPlanner.cs VolcanoPlanner : AbstractPlanner — register/fire/propagate/extract; SetTopDownOpt (see §4)
      VolcanoRuleCall.cs  VolcanoRuleCall : RuleCall — Rule + OnMatch + Transform (registers an equivalent)
      VolcanoRuleMatch.cs  a queued match (rule + bound nodes), deduplicated (the VolcanoRuleMatch analog)
      DeferringRuleCall.cs  fires a rule by deferring each match to the queue (the DeferringRuleCall analog)
      RuleQueue.cs      RuleQueue: pending matches, duplicate-dropping
      IRuleDriver.cs / IterativeRuleDriver.cs  the RuleDriver abstraction + the exhaustive iterative driver
      AbstractConverter.cs  ConverterImpl-derived infinite-cost enforcer demanding its input in target traits
                            (kept in the Volcano namespace; inherits the convert-package base)
      ExpandConversionRule.cs  turns an AbstractConverter into real converters (auto-registered)

src/Alembic.Tests/                 (one top-level type per file)
  Holistic/                end-to-end tests that drive a planner over a near-real implementation (a test
                           language) to produce a plan; namespace Alembic.Tests.Holistic. Each prints
                           its result via INode.ToPlanString() to the test output.
    RelationalLoweringTests.cs   the relational lowering suite (HEP: lower, simplify, push down, enforce)
    ExpressionLoweringTests.cs   the arithmetic-expression lowering suite (lower, fold, fuse, enforce)
    VolcanoPlanningTests.cs   cost-based selection + lowering, an unsatisfiable case, non-convention trait
                             enforcement (sort), SetTopDownOpt, and listener events
    ExpressionVolcanoTests.cs   the binary-expression (BiNode) language lowered cost-based
    ImagePlanningTests.cs   the image-processor language: cost-based CPU/GPU selection, crossing and back
  (engine-feature / API tests, plain Alembic.Tests namespace:)
  ClusterTests.cs   Cluster.TraitSet / TraitSetOf
  ConventionInterfaceTests.cs   getInterface enforcement (a node must implement its convention's interface)
  CompositeTraitTests.cs + SortKey.cs + SortKeyTraitDef.cs   multi-valued dimensions via CompositeTrait
  OperandMatchingTests.cs + TagFilterOverSource.cs   operand rule + nested operand matching
  TraitDimensionTests.cs + Sortedness.cs + SortednessTraitDef.cs   a second trait dimension with
                           a real Satisfies partial order
  TraitPlannerTests.cs + MarkSorted.cs   a new trait dimension read/written by a rule, preserved
                           through planning
  Languages/Relational/    the toy relational language (namespace Alembic.Tests.Languages.Relational),
                           nodes and rules in separate namespaces/dirs, logical/physical models in
                           separate namespaces. Logical/ and Physical/ nodes (Source/Filter/Parameter;
                           filters extend SingleNode; plus PhysicalFilteredSource — a push-down
                           realization — and PhysicalSort, a sortedness enforcer), Rules/ (converters +
                           RemoveTrueFilter/MergeFilters simplifications + PushFilterIntoSource +
                           SortEnforcer, a converter over the non-convention sortedness trait).
                           RelationalPhysical overrides Convention.Register to contribute its converters.
  Languages/Image/         an image-processing language exercising cost-based convention choice. One
                           node class per operation, convention in the trait; Logical/CPU/GPU conventions
                           (GPU ops cheap, CPU dear). Load and Inpaint are CPU-only (IImageOperation.
                           SupportsGpu=false), so the planner must transfer around them; Blur/Grayscale/
                           Threshold run on either. Download/Upload transfer enforcers (ConverterImpl)
                           with a transfer cost; Rules/ (LowerToCpu/LowerToGpu + DownloadRule/UploadRule).
                           ImagePlanningTests drives plans that cross CPU↔GPU and back (CPU→GPU→CPU,
                           CPU→GPU→CPU→GPU→CPU), keep GPU output transfer-free, and decline to cross when
                           a lone op isn't worth the transfer.
  Languages/Expression/    the toy arithmetic-expression language (a binary expression tree). Logical/
                           (Literal, Variable, Add/Multiply as BiNode) and Physical/ (counterparts plus
                           PhysicalFma — a 3-child fused multiply-add) models. Rules/ (converters +
                           FoldAdd/FoldMultiply constant folding + FuseMultiplyAdd, a physical fusion
                           push-down). ExpressionPhysical contributes its converters via Convention.Register.
```

Both languages have leaf/parameter nodes, simplification rules that collapse static operations
(filter merging / true-filter removal; constant folding), and a push-down rule (filter-into-source;
multiply-add fusion). They are two independent proofs that the same engine drives a relational-shaped
model and a binary-expression-shaped one.

### How the HEP planner works (important)

`HepPlanner` rewrites the **immutable tree directly** — it does NOT build the shared vertex DAG. Per
pass it walks the tree in match order (bottom-up rewrites children first, then applies rules to the
rebuilt parent), and re-passes until `INode.DeepEquals` reports a fixed point (or the pass limit,
default 1024). When a child changes (detected by `ReferenceEquals`), the parent is rebuilt with
`node.Copy(node.Traits, children)` (mechanical spine reconstruction, not rule-driven creation).
`ApplyRules` matches each rule's operand (`OperandMatcher.Matches(rule.Operand, node)`) then calls
`OnMatch`, applying at most one transform per node per pass; cascades are handled by re-passing.

**Required output traits.** `ChangeTraits(root, toTraits)` records the traits the final plan must
carry (only the root's request is remembered, as in the heuristic planner it models). After the
rewrite reaches a fixed point, `FindBestPlan` verifies that **every** node satisfies the request
(`TraitSet.Satisfies`) and throws `CannotPlanException` otherwise. Because conversions rewrite nodes
in place rather than wrapping them in converter nodes, a complete plan is convention-uniform, so a
single surviving node that falls short means no rule chain could finish the lowering. (This is the
completeness guarantee the tests previously asserted by hand with an `AssertAllPhysical` walk.)

This is correct but unoptimized. The shared-DAG dedup is the first roadmap item; the identity
machinery it needs (`DeepEquals`/`DeepHashCode`) is already in place.

### How the Volcano planner works (important)

`VolcanoPlanner` keeps the shared graph HEP does not. **Registration** (`SetRoot` → `RegisterImpl`)
replaces each node's children with their `NodeSubset` (via `OnRegister`), dedups by digest, places the
node in a `NodeSet`, costs it (`PropagateCostImprovements`), and **fires rules** — a `DeferringRuleCall`
defers each operand match as a `VolcanoRuleMatch` added to the `RuleQueue`. `FindBestPlan` hands the
queue to the `IRuleDriver` (`IterativeRuleDriver`), which pops each match and applies it; the match's
`Transform` registers the equivalent into the matched node's set. Each subset remembers its cheapest
member; `BuildCheapestPlan` walks subsets→best to rebuild the concrete tree (throwing
`CannotPlanException` if a needed subset is empty). `SetTopDownOpt(true)` is reserved for a future
Cascades driver and currently throws.

**Lowering / convention enforcement.** `OnRegister` makes every node require its inputs in *its own*
convention (the "a consumer asks for the result in a particular convention" case), which propagates a
lowering down the tree. The root has no consumer, so `ChangeTraits` records the requested traits and
`EnsureRootConverters` puts an `AbstractConverter` into the root set; `ExpandConversionRule` (auto-
registered) turns it into real converters via `ChangeTraitsUsingConverters`.

**Operand matching over subsets.** Because a registered node's children are subsets, the matcher
descends into each subset's members (and binds a subset directly for an "any" operand), enumerating all
bindings. Rules read the bound nodes via `RuleCall.Node(i)`, never `node.Children` — which is why the
same rule works under both planners.

---

## 3. Settled design decisions (do not re-open without reason)

1. **`INode` is an interface, not a base class.** This lets a consumer implement it directly on a
   domain type they own (single-inheritance / record constraints make a base class hostile to
   that). `AbstractNode` is an optional convenience base.
2. **Nodes are immutable.** Rewriting produces new nodes via `Copy(traits, children)`; untouched
   subtrees are shared by reference. This is what makes structural sharing, interning, and
   copy-on-write sound.
3. **Identity is split two ways.** A node's own `Equals`/`GetHashCode` stay as reference identity.
   Structural equivalence (what the planner dedups on) is the separate `DeepEquals` / `DeepHashCode`
   contract, derived on `AbstractNode` from the node's **explain terms** — the named attributes and
   inputs a node lists in `Explain(INodeWriter)`, exactly as Calcite derives its digest from
   `explainTerms` (`RelWriter`). Inputs are terms too (`SingleNode`/`BiNode` add them; custom-arity
   nodes add their own), so the digest is one uniform list. Each node **keeps one `INodeDigest`** (a
   nested `InnerNodeDigest`, the `InnerRelDigest` analog) that caches its hash and renders the digest
   string via the writer's `Done`. (There is **no `Signature` property** — an earlier Alembic shortcut,
   removed in favor of the Calcite mechanism.)
4. **`Children` (not `Inputs`).** The engine needs to navigate and rebuild the tree generically;
   `Children` is the honest, non-dataflow name. This is engine navigation, not rule-driven creation.
5. **Traits are interned and ordered (Calcite's `RelTraitSet`).** `TraitSet` is an
   `ImmutableArray<ITrait>`; a dimension is located by a **linear `FindIndex`** (few dimensions, so no
   ordinals — matching Calcite, *not* the earlier ordinal design). The **intern cache lives inside
   `TraitSet`** (an inner `Cache` shared by every set derived from one `CreateEmpty()`), so equal sets
   are one shared instance and common sets are singletons. The **trait-def registry lives on the
   planner** (`AddTraitDef`/`TraitDefs`/`EmptyTraitSet`), exactly as Calcite's `RelOptPlanner`. There
   is **no `TraitContext`** — Calcite has none; it was an Alembic invention now removed.
6. **Conventions are first-class traits.** Lowering between conventions is done by `IConverterRule`,
   a **mixin interface**: supply `Source`/`Target` and `Convert(node)` (returns null to decline);
   `Matches` (by convention) and `OnMatch` are DIMs. `ConverterRule` is an optional base class.
7. **Default interface methods (DIMs) for contracts; classes for state and convenience.** DIMs carry
   derived/optional behavior (`Satisfies`, `Matches`, `IsLeaf`, `WithChild`, converter `Matches`/`OnMatch`).
   Anything stateful (planner, caches, interning) is a concrete class. Abstract/base classes are used
   as opt-in convenience over the interfaces: `AbstractNode`, `SingleNode` (a single-child node base, the
   `SingleRel` analog), `TraitDef<T>`, `AbstractPlanner`, and `ConverterRule` (holds `Source`/`Target`,
   leaves `Convert` abstract). `Convention` is an unsealed class so it can serve as a base for custom
   conventions.
8. **Node types are hand-written classes, not records.** Records were considered for ergonomics, but
   each node keeps a digest that caches its hash, and a record's `with` copy would carry a stale digest
   to a structurally different node. Caching + records are mutually exclusive; we chose caching. Nodes
   are `INode`/`AbstractNode` classes with kept digests.
9. **HEP first, Volcano later.** HEP does lowering via explicit converter rules and program order. It
   *enforces* a required output trait set (`ChangeTraits` + a post-fixpoint completeness check that
   throws `CannotPlanException`), but it does **not** *insert* converters to reach one — automatic
   converter insertion (the `AbstractConverter` / `RelSubset` machinery) is cost-based search and stays
   deferred to a future Volcano planner.
10. **Namespaces:** `Alembic.Algebra` (node model), `Alembic.Plan` (planner core — **traits,
    conventions, cost, cluster, base rule-call all live here, flat**, mirroring Calcite's `plan`
    package), `Alembic.Plan.Rules`, `Alembic.Plan.Hep`. `Alembic.Plan.Volcano` is reserved for the
    cost-based planner. (Rules could likewise flatten into `Alembic.Plan` to fully mirror Calcite;
    not done yet.)

---

## 4. Engine shape (the INode model)

The engine operates on **`INode` directly**, monomorphically — `INode` is Calcite's `RelNode`. The
planner, rules, operands, and `Convention.register` all take `INode`. (The generic-over-store
`INodeStore<TNode>` design was explored and reverted; see the executive-decision note in §1.)

### 4.1 Planner hierarchy (mirrors Calcite)

- **`IPlanner`** = `RelOptPlanner`: `AddTraitDef` / `TraitDefs` / `EmptyTraitSet`, `AddRule`,
  `SetRoot(INode)`, `FindBestPlan()`.
- **`AbstractPlanner`** = `AbstractRelOptPlanner`: the shared trait-def registry (convention
  auto-registered), `EmptyTraitSet` (built from registered defs, then sealed), and the rule registry.
- **`HepPlanner : AbstractPlanner`** = `HepPlanner`: the heuristic rewrite loop — bottom-up (or
  top-down) per pass, re-passing until `DeepEquals` reports a fixed point; rebuilds the spine with
  `node.Copy(node.Traits, children)`, sharing untouched subtrees by `ReferenceEquals`.
- **`Cluster`** = `RelOptCluster`: a thin per-session environment wrapping the planner (relational
  pieces — row-expression builder, metadata query — omitted).

### 4.2 Rules

- **`IRule`** — `Operand Operand { get; }` + `OnMatch(RuleCall)`. Every rule has an operand (Calcite's
  `RelOptRule`); the planner matches it and calls `OnMatch` on a hit.
- **`RuleCall`** is an **abstract base** (`Node` + abstract `Transform`); **`HepRuleCall`** is the HEP
  subclass that records the single replacement — the `RelOptRuleCall` + `HepRuleCall` split, extensible
  so a future planner (or a consumer) supplies its own.
- **`IConverterRule`** — declares `Source`/`Target` **traits** (named `Source`/`Target`, a deliberate
  divergence from Calcite's `In`/`Out`/`getInTrait`/`getOutTrait`). They are `ITrait`, not just a
  convention, so a rule can convert *any* dimension (sortedness, distribution, …); the operand matches
  nodes carrying the `Source` trait on its dimension. `INode? Convert(INode)` returns the converted node
  or **null to decline** (possible now that nodes are reference types). **`ConverterRule`** is the
  abstract base supplying `Source`/`Target`.
- **Matching** — every rule's `Operand` is matched by `OperandMatcher` (which navigates `node.Children`,
  outside the planner). `HepPlanner.ApplyRules` does `OperandMatcher.Matches(rule.Operand, node)` then
  `OnMatch`. As in Calcite, the operand *is* the match (a predicate refines it); there is no separate
  `Matches` method and no `IOperandRule` capability.

### 4.3 Kind / dispatch index — DEFERRED (Volcano-era)

- Matching is **subtype + predicate** (a boolean); nothing returns a "kind" value just to match
  (confirmed against Calcite: `RelOptRuleOperand.matches` → `clazz.isInstance`).
- A rule-dispatch index (sorting rules by kind) matters only for a cost-based planner; brute-force HEP
  doesn't build one. When built, subtype matching forces a **hierarchy-aware index** (expand each rule
  over the subtype closure of its declared class, keyed by concrete class — Calcite's `classOperands`
  + `onNewClass`).
- **Rejected:** LINQ `Expression` trees for operands — pattern syntax is forbidden in expression trees,
  and tree-introspection would re-couple matching to concrete node types. A source generator is the
  path if ergonomic pattern authoring is wanted.

---

## 5. Project conventions (must follow)

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

## 6. Remaining work (roadmap, roughly prioritized)

### Near-term (engine correctness/usefulness)
1. **✅ Done — planner hierarchy on `INode` (§4).** `IPlanner` (`RelOptPlanner` analog) +
   `AbstractPlanner` (`AbstractRelOptPlanner`) + `HepPlanner`; trait-def registry + `EmptyTraitSet` on
   the planner; `Cluster` (`RelOptCluster`). All `INode`-monomorphic.
2. **✅ Done — rule layering.** Every `IRule` has an `Operand` (Calcite-faithful); the planner matches
   via `OperandMatcher`. `IConverterRule` (Source/Target, `Convert` returns null to decline) +
   `ConverterRule` base supply a convention-matching operand. `RuleCall` abstract + `HepRuleCall`.
3. **✅ Done — shared-DAG HEP planner.** `HepPlanner` holds the plan as a graph of `HepNodeVertex`
   (the `HepRelVertex` analog): equal subexpressions are interned to one vertex (keyed on the node
   digest), so a rule fires once per distinct subexpression and a rewrite is shared by every parent
   that references it. A vertex's identity is stable (`Id`), so replacing its content leaves parents'
   digests intact; the planner's own operand matching sees through a vertex to its current node (as
   Volcano's matcher descends a `NodeSubset` — each planner owns its stand-in node, no shared
   abstraction). `SharedDagTests` proves the sharing (`Assert.Same` on a folded common subexpression).
4. **✅ Done — `HepInstruction` model.** `HepProgram` is an ordered list of `HepInstruction`s
   (`RuleInstance`, `RuleCollection`, `RuleClass`, `RuleLookup`, `ConverterRules`,
   `CommonRelSubExprRules`, `MatchOrder`, `MatchLimit`, `SubProgram`, `BeginGroup`/`EndGroup`), built by
   `HepProgramBuilder`. Execution uses the re-entrant `prepare(PrepareContext) → HepState` design:
   instructions are immutable, all mutable state lives in per-run `State` objects, so a program can be
   reused. `HepPlanner` runs them via `ExecuteProgram` + the `ExecuteXxx` dispatch. `HepProgramTests`
   covers match orders, match limit, subprograms, groups, and rule classes.
5. **✅ Done — general rewrite rules + matcher coverage.** `OperandMatchingTests` exercises nested
   operands + arity; the relational suite adds single-convention rewrites (true-filter removal, filter
   merging) and a push-down rule.
6. **✅ Done — `Cluster` + convention rule-registration.** `register` is declared on `ITrait`
   (`RelTrait.register` analog), takes the planner — `void Register(IPlanner planner)` — and
   `Convention` overrides it to add rules via `planner.AddRule`, reading `planner.EmptyTraitSet`.
7. **✅ Done — `RuleCall` multiple equivalents.** `Transform` appends to a list of results
   (`HepRuleCall.Results`, the `getResults()` analog); a single match may register several equivalents.
   `MultipleEquivalentsTests` registers two physical scans of differing cost and the cost-based planner
   picks the cheaper.
8. **✅ Done — operand on the base rule.** `IRule.Operand` (Calcite-faithful); the planner matches via
   `OperandMatcher.Matches(rule.Operand, node)`. `IOperandRule` retired.
9. **✅ Done — two-child base + a second language.** `BiNode` (the `BiRel` analog) joins `SingleNode`;
   a binary arithmetic-expression language (`Languages/Expression`) proves two conventions, two models,
   converter-driven lowering, constant folding, and a fusion push-down on the same engine.
10. **✅ Done — required output trait + completeness enforcement.** `IPlanner.ChangeTraits`
    (`RelOptPlanner.changeTraits` analog) records the target traits; HEP enforces them across the
    result and throws `CannotPlanException` when lowering is incomplete. Retired the tests'
    `AssertAllPhysical` crutch. (Automatic converter *insertion* to reach the target remains Volcano —
    see #11.)

### Mid-term (richer trait/convention model)
8. **✅ Mostly done — a second trait dimension.** Trait dims register on the planner
   (`AddTraitDef`, convention auto-registered), sealed once `EmptyTraitSet` is materialized;
   `TraitDimensionTests` proves a second dimension (`Sortedness`) with a real `Satisfies` partial
   order and interning via `CreateEmpty`/`Plus`. Remaining: a built-in ordering/collation trait if
   one is wanted in the library itself (the test's is a fixture).
9. **✅ Done — trait conversion hooks** on `TraitDef` (`CanConvert` / `Convert`, the
   `RelTraitDef.canConvert` / `convert` analogs — virtual, default decline). Volcano's
   `ChangeTraitsUsingConverters` consults them alongside converter rules, so a dimension can supply its
   own enforcement (e.g. wrapping a node in a sort) without a registered converter rule.
   `TraitConversionTests` enforces sortedness through the hook with no converter rule present.

### Larger (cost + Volcano)
10. **✅ Done — cost model in core.** `ICost` (the `RelOptCost` analog: `IsInfinite` /
    `IsLessThanOrEqual` / `IsLessThan` / `Plus`), `ICostFactory` (the `RelOptCostFactory` analog:
    `MakeCost(cpu,io)` + zero/infinite/huge/tiny — no `rowCount`, since the engine is not relational),
    and a concrete scalar `Cost` (the
    `RelOptCostImpl` analog) all live in `src/Alembic`. The planner carries the factory
    (`IPlanner.CostFactory`, the `getCostFactory` analog); `INode.ComputeSelfCost(planner)` is the
    `computeSelfCost` analog — a DIM on the node itself (with an `AbstractNode` virtual), **not** a separate
    capability interface (there is no `ICostedNode`; Calcite has no such interface).
11. **✅ Done — Volcano (cost-based) planner** under `Alembic.Plan.Volcano`: `NodeSet` / `NodeSubset`
    (the `RelSet` / `RelSubset` analogs), bottom-up registration that fires rules and propagates cost
    improvements, cheapest-plan extraction, and **converter insertion** to reach a requested output
    convention — `AbstractConverter` + `ExpandConversionRule` (registered automatically) + `ChangeTraits`
    / `ChangeTraitsUsingConverters` + `EnsureRootConverters`. Matches are deferred (`DeferringRuleCall`)
    into a `RuleQueue` and applied by an `IRuleDriver` (`IterativeRuleDriver`). Rules are
    planner-agnostic: they read operand-bound nodes via `RuleCall.Node(i)` (the `RelOptRuleCall.rel`
    analog), so the same rule runs under HEP (concrete children) and Volcano (subset children).
    Deliberately omitted: importance-based queue ordering and the top-down (Cascades) driver
    (`SetTopDownOpt(true)` throws) — the search is exhaustive to a fixed point. `IConverterRule`'s
    `Source`/`Target` are now `ITrait` (any dimension, not just convention). Still open: a
    hierarchy-aware rule-dispatch index, and a top-down driver that exploits non-convention trait
    enforcement.
12. **✅ Done — multi-valued traits.** `IMultipleTrait` + `CompositeTrait<T>`, wired into `TraitSet`
    (`Replace(def, list)` folds to a composite; `GetList` reads the members; `Satisfies` honours them).
13. **✅ Done — planner listener.** `IPlannerListener` (+ nested events) and `IPlanner.AddListener`;
    the planners fire equivalence/rule/chosen events.

### Search & statistics (the big remaining subsystems)
17. **Top-down (Cascades) search.** A guided alternative to the exhaustive iterative driver: demand
    flows from the root down ("optimize this group for these required traits"), with branch-and-bound
    pruning against the best full plan found so far. Needs the `passThrough`/`derive` trait-negotiation
    hooks on physical nodes (a node declaring "I can deliver trait X if my input has trait Y") and a
    `TopDownRuleDriver` selected by `SetTopDownOpt(true)` (which currently throws). The biggest
    remaining piece for the planner to *scale*.
18. **Importance-based rule queue.** The current `RuleQueue` is FIFO and exhaustive. Rank pending
    matches by importance (how much the subset they touch could improve the best plan) so the planner
    explores promising matches first and can stop early — the guided-search counterpart to #17.
19. **Metadata / statistics framework** (the `RelMetadataQuery` analog). A provider-based system for
    derived properties — cardinality/size estimates, selectivity, cumulative cost, etc. — that rules
    and the cost model consult. Today costs come only from `INode.ComputeSelfCost`; this is where
    realistic, statistics-driven costs would come from. A whole subsystem (medium-agnostic in shape,
    even though Calcite's built-in metadata is relational).

### Partial / hardening (started, not finished)
20. **✅ Done — shared-DAG HEP** (see near-term #3) **and the `HepInstruction` model** (see near-term
    #4). `HepPlanner` holds the plan in a generic `DirectedGraph<V, E>` (interface) backed by
    `DefaultDirectedGraph<V, E>` with an `EdgeFactory`, traversed by real iterator types
    (`DepthFirstIterator` / `BreadthFirstIterator` / `TopologicalOrderIterator` / `HepVertexIterator`),
    with mark-and-sweep garbage collection and a fired-rules cache, driven by the instruction/state
    program model. Large-plan mode (`LargePlanMode`) uses `HepVertexIterator` to resume from a
    transformed vertex rather than restart from the root. Non-guaranteed converters are applied bottom-up
    via `TraitMatchingRule` (`AddConverters(false)`). Out of scope (relational or debug-only): metadata
    providers, materializations, and cycle/consistency assertions.
21. **✅ Done — operand policies.** `Operand` carries an `OperandChildPolicy` (`Any` / `Leaf` / `Some` /
    `Unordered`, the `RelOptRuleOperandChildPolicy` analog); a childless operand defaults to `Leaf`, as
    in the model. Both matchers (HEP `OperandMatcher`, Volcano `MatchBindings`) honour all four, with
    backtracking for `Unordered`. `OperandPolicyTests` covers each.
22. **✅ Done — multi-step trait conversion.** `ChangeTraitsUsingConverters` runs a BFS over the
    conversion graph — converter rules *and* trait-def hooks as edges — and applies the shortest chain,
    so multi-hop trait changes are found. `TraitConversionTests` reaches a convention through an
    intermediate (logical → CPU → GPU).
23. **✅ Done — set-merge hardening.** Volcano's `Merge` re-points parents whose child subsets moved to
    the surviving set (the `rename` analog), recomputing their digests and folding any that become
    newly equivalent. `SetMergeTests` triggers a real cross-set merge (a fold makes one subtree equal
    another) and the re-point path runs.
24. **✅ Done — `RuleCall` multiple equivalents** (see near-term #7).

### Cross-cutting / polish
14. **Complete XML docs** and remove the `CS1591` suppression in `Alembic.csproj`.
15. **More tests:** match-limit/termination, deep trees, trait interning edge cases, top-down order.
16. **Naming — settled.** `IConverterRule` converts `Source` → `Target` (not Calcite's `In`/`Out`,
    a deliberate divergence). Children are `Children`, not `Inputs` (`Inputs` is dataflow-specific;
    the engine's view is structural — decision #4).

---

## 7. Build & test

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

## 8. Notes / gotchas

- Build output (`bin`/`obj`/`dist`) is gitignored.
- `D:\geodesk-net` is the user's other .NET project and the canonical reference for house setup.
- `D:\calcite` is a local checkout of Apache Calcite, used to verify the design (operand semantics,
  the planner/trait/cluster placement). Reference only — **never** mention Calcite in Alembic source.
- The user (Jerome Haltom) maintains `IKVM.Core.MSBuild`; reusing it here is intentional.

### Calcite parallels — where things live (the guiding rule: match Calcite, strip relational only)

- **`Convention.register`**: `register` is declared on `ITrait` (the `RelTrait` analog) and takes the
  planner — `void Register(IPlanner planner)`, default no-op (a DIM). `Convention` overrides it; a
  convention bringing lowering rules adds them via `planner.AddRule` and reads `planner.EmptyTraitSet`.
  Non-generic, matching `RelTrait.register(RelOptPlanner)` exactly (the generic engine is gone, so no
  runtime cast).
- **`AbstractRelOptPlanner`**: `AbstractPlanner` — the abstract base holding the shared rule registry,
  trait-def registry, and `EmptyTraitSet`; `HepPlanner` extends it (later `VolcanoPlanner` too).
- **`RelOptCluster`**: `Cluster` — a thin per-session environment wrapping the planner (relational
  pieces omitted). The trait registry/`EmptyTraitSet` live on the planner, as in Calcite.
- **`TraitContext`**: removed. Calcite has none — the trait-def registry is on the planner
  (`addRelTraitDef`/`emptyTraitSet`), the intern cache is inside `TraitSet`/`RelTraitSet`, and there are
  no ordinals (linear `findIndex`). We match all three.
- **Cost** (`RelNode.computeSelfCost` / `RelOptCost`): `ICost` is the cost value; `ICostFactory` is the
  factory (`RelOptCostFactory`); `Cost` is the scalar default (`RelOptCostImpl`). Self-cost is
  `INode.ComputeSelfCost(planner)` — a method on the node, as in Calcite — **not** a separate
  `ICostedNode` interface (Calcite has none). HEP ignores cost; Volcano consults it.
- **`AbstractNode.Explain(INodeWriter)`**: the analog of Calcite's `explainTerms` (`RelWriter`); its
  terms feed the digest, i.e. `DeepEquals`/`DeepHashCode` — exactly as Calcite derives `deepEquals`
  from `explainTerms`. (`INodeWriter` is the `RelWriter` analog.)
- **`SingleRel`**: `Alembic.Algebra.SingleNode` (a single-child `AbstractNode`).
- **Operand placement**: every `IRule` has an `Operand`, as in Calcite's `RelOptRule` (no separate
  `Matches`, no `IOperandRule`). The planner matches via `OperandMatcher`.
- **Visitor / `accept(RelShuttle)`**: not yet added. If wanted, it would go on `INode` (like Calcite's
  `RelNode`), or as a helper that walks `INode.Children`.
- **Push-down / multiple physical realizations**: the relational language shows two physical forms of
  a filtered scan (`PhysicalFilter`-over-`PhysicalSource` vs the pushed-down `PhysicalFilteredSource`).
  Push-down is a deterministic HEP rewrite; *choosing* among alternatives is cost-based (Volcano).
