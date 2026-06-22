# Alembic — Theory of Operation

This document explains *how Alembic thinks*. It is the conceptual companion to the code: read it to
understand the goals of the library, what the pieces mean, and why the planners behave the way they do.
`PLAN.md` tracks the concrete state and roadmap; this file is the durable mental model.

Alembic descends from the query-optimizer lineage (Volcano → Cascades, and Apache Calcite's planner),
but it is **medium-agnostic**: it keeps the optimizer *ideas* and throws away everything specific to
relational algebra and SQL. Wherever this document says "plan," "node," or "operation," it means *your*
domain — image pipelines, tensor graphs, build steps, query trees, anything that can be expressed as a
tree of immutable operations.

---

## 1. What the library is for

Alembic takes a **tree of operations** you wrote and finds an **equivalent tree that is better** —
cheaper, or expressed in a form that can actually be executed. "Equivalent" means *produces the same
result*; "better" means *lower cost* or *in a required target form*.

Two things make this non-trivial and worth a framework:

1. There are usually **many equivalent ways** to compute the same result (do this op on the CPU or the
   GPU; filter before or after; fuse two steps into one). The space is large.
2. Which one is **best depends on cost**, and cost depends on choices made elsewhere in the tree.

Alembic explores that space with **rules** (which generate equivalent alternatives), prunes/chooses
with a **cost model**, and tracks the physical properties of each alternative with **traits**. You
supply the domain (the node types, the rules, the costs); Alembic supplies the search.

The canonical end-to-end shape:

> You write a **logical plan** (what to compute, abstractly). Alembic **lowers** it to a **physical
> plan** (how to compute it, on a concrete backend), choosing the **cheapest** physical realization and
> inserting whatever **conversions** are needed so the whole plan is executable.

---

## 2. The node model

A plan is a tree (more precisely, a DAG during planning) of **nodes**. The node contract is `INode`:

- `Traits` — the node's physical properties (see §4).
- `Children` — its inputs, in order (an immutable array).
- `Copy(traits, children)` — produce a new node like this one but with different traits/children. This
  is how the engine rebuilds the tree; **nodes are immutable**, so rewriting never mutates in place.
- `DeepEquals` / `DeepHashCode` — **structural identity** (see §3).
- DIMs (default interface methods) for convenience: `Convention`, `IsLeaf`, `WithChild`,
  `ComputeSelfCost`, `GetDigest`.

**Why immutable?** Immutability is what makes structural sharing, interning, and copy-on-write sound.
When the planner rewrites one part of a tree, every untouched subtree is shared by reference, and the
same subexpression appearing in many places is one object. Mutation would make all of that unsafe.

`AbstractNode` is an optional convenience base (most node types use it). It derives `DeepEquals` /
`DeepHashCode` from a node's **explain terms** (§3) and keeps a cached digest. `SingleNode` (one child)
and `BiNode` (two children, `Left`/`Right`) are thin bases over it. A node type can also implement
`INode` directly — the base classes are a convenience, not a requirement.

---

## 3. Identity: what makes two nodes "the same"

The optimizer constantly asks "have I seen this node before?" (to deduplicate) and "are these two nodes
the same?" (to detect equivalences). A node's ordinary `Equals`/`GetHashCode` stay as **reference
identity**; the meaningful, structural notion is a *separate* contract:

- **`DeepEquals(other)`** — structurally equivalent: same type, same traits, same attributes, and
  recursively equal children.
- **`DeepHashCode()`** — a hash consistent with `DeepEquals`.

That is the actual definition of identity. Everything else is a way to *populate* it.

**Explain terms.** `AbstractNode` computes `DeepEquals`/`DeepHashCode` from the node's *terms*: the
named attributes and inputs a node lists in `Explain(INodeWriter)`. A node writes its own attributes
with `Item(name, value)` and its inputs with `Input(name, child)`; identity is then `type + traits +
terms`. (Traits and type are folded in by the base; the "terms" are the node-specific part.) The same
`Explain` output also produces a human-readable rendering — see `ToPlanString()` and the digest string
— so one mechanism serves both identity and display.

**Digest.** `INodeDigest` is a small cacheable handle on this contract: its `Equals`/`GetHashCode`
delegate to the node's `DeepEquals`/`DeepHashCode`, and because each node *keeps* one digest instance,
the hash is computed once and reused. The cost-based planner keys its dedup dictionary on digests.

A node may **override** `DeepEquals`/`DeepHashCode` directly (e.g., with a hand-rolled or binary
representation) instead of using the explain-term derivation — the contract is what matters, the
derivation is just the default.

---

## 4. Traits — the heart of the model

Start from one idea: the optimizer groups plans into **equivalence classes** — sets of plans that
produce the same result — and wants the cheapest member of each class. Within one class, two plans can
still differ **physically**. *That difference is a trait.*

> **A trait is a property of a node's output that can vary among plans that compute the identical
> result.** It changes *how* the result is delivered, never *what* the result is.

The litmus test for "is this a trait?": **can two nodes have the same logical result but differ in
this property?**

- **Sortedness** — `scan` vs `sort(scan)`: same rows, different order → *trait*.
- **Convention** — a logical operation vs its GPU implementation: same result, different machinery →
  *trait*.
- **A filter's predicate** (`x>5` vs `x>3`) — different results → **not a trait; it's identity**.
- **Output schema / shape** — a different schema is a different result → **not a trait; identity**.

Two further properties make a trait *useful to the optimizer*:

1. **A consumer can require a value.** A merge-style join needs sorted inputs; a GPU op needs its input
   on the GPU. So the trait isn't decoration — downstream nodes care.
2. **An enforcer can establish it without changing the result.** Wrap an unsorted node in a `Sort`; the
   rows are unchanged, the trait now holds. This is what lets the optimizer *insert* a property where
   it's needed.

Often there is a **partial order**: sorted-by-`[a,b,c]` already satisfies a request for sorted-by-`[a]`.
That order is `ITrait.Satisfies` — a value can stand in for a weaker one.

### Trait machinery

- **`ITraitDef`** — a *dimension* / axis (e.g. "sortedness", "convention"). Has a `Name` and a
  `Default`.
- **`ITrait`** — a *value* on a dimension (e.g. "Sorted"). `Def` names its dimension; `Satisfies(other)`
  is the partial order (defaults to equality); `Register(planner)` lets a trait contribute rules.
- **`TraitSet`** — one value per dimension: a node's full physical fingerprint, a point in trait-space.
  It is **interned** (equal sets are one shared instance, via a cache shared by every set derived from
  a common `CreateEmpty()`), and ordered, with a linear `FindIndex`. Key operations: `Plus(trait)`
  (add or replace), `Replace(def, value)`, `Get(def)`, `Satisfies(required)` (does this set meet a
  requirement on every named dimension?), `Convention`.
- **Multi-valued dimensions** — some traits can hold several values at once (sorted by two keys).
  `IMultipleTrait` marks such a trait; `CompositeTrait<T>` bundles several values of one dimension; and
  `TraitSet.Replace(def, list)` / `GetList(def)` store and read them.

A node's identity includes its traits, but traits are compared specially (interned `TraitSet` equality),
not as ordinary terms — which is both faithful to the model and efficient.

---

## 5. Conventions, and logical vs. physical plans

A **convention** is the coarsest, most important trait: the *family* a node belongs to. It answers
"in what world does this operation run?" — a logical world (abstract), or a particular physical backend
(CPU, GPU, an interpreter, a codegen target). It is just an `ITrait` whose dimension is always present
(`ConventionTraitDef`).

This is where the **logical vs. physical** distinction lives, and it is central:

- A **logical plan** says *what* to compute. Its nodes are in a logical convention. It is abstract: it
  describes the operation (a filter, a blur, an addition) without committing to how or where it runs.
  This is normally what you, the author, write.
- A **physical plan** says *how* to compute it. Its nodes are in a physical convention (e.g. CPU or GPU)
  — a concrete, executable realization. The same logical operation can have several physical forms with
  different costs.

**Lowering** is the process of turning a logical plan into a physical one — i.e., converting nodes from
the logical convention to a physical convention. A *fully lowered* plan is one whose nodes are all in a
target physical convention (no logical leftovers). Because convention is a trait, lowering is just a
special, very common case of **trait conversion** (§8): you require the root in a physical convention,
and the planner converts.

`IConvention` carries:
- `Name`.
- `Interface` — the node interface members of this convention must implement (defaults to `INode`; a
  convention may demand its nodes implement a marker so the planner can reject mismatches).
- `CanConvertConvention` / `UseAbstractConvertersForConversion` / `Enforce` — hooks the planner can use
  to decide how/whether to bridge conventions.
- `Register(planner)` — a convention can contribute the rules that *produce* it (its lowering rules), so
  enabling a backend is "register its convention."

`Convention` is the default implementation; `IConvention.None` is the sentinel "no convention," which is
not implementable and has infinite cost (logical-style nodes start here in spirit).

---

## 6. Rules — how alternatives are generated

The optimizer never invents transformations on its own; it applies **rules** you provide. A rule
(`IRule`) has two parts:

- **`Operand`** — a *pattern* that selects where the rule applies. An operand is a predicate over a node
  plus optional child operands (matched positionally); `Operand.Of<T>()` matches a node type, and a
  child-less predicate operand matches "anything." Matching is done by `OperandMatcher`, outside the
  planner.
- **`OnMatch(RuleCall)`** — the action. Given a match, the rule builds one or more **equivalent** nodes
  and registers them via `call.Transform(equivalent)`.

A crucial detail: a rule reaches its matched nodes through **`call.Node(i)`** (the operand-bound nodes),
*not* by navigating `node.Children`. Under the heuristic planner a node's children are concrete; under
the cost-based planner they are equivalence *subsets* — so only the operand-bound nodes are guaranteed
to be the concrete types the rule expects. Because rules use `Node(i)`, **the same rule works under both
planners**.

`RuleCall` is the context of a single match (the bound nodes + `Transform`); each planner provides its
own subclass that decides what `Transform` does — replace-in-place (heuristic) or register-an-equivalent
(cost-based).

### Converter rules

A **converter rule** (`IConverterRule`) is the special, central kind of rule that changes a *trait*:
it declares a `Source` trait and a `Target` trait (both `ITrait` — usually conventions, but any
dimension, e.g. unsorted→sorted), and a `Convert(node)` that returns the converted node, or `null` to
decline. The operand (match anything carrying the `Source` trait) and the match action are provided as
a mixin; `ConverterRule` is a convenience base. Lowering rules and trait enforcers are all converter
rules.

Converters also exist as *nodes*: `IConverter` / `ConverterImpl` is a node that bridges a trait (its
input has one value, its output another) — e.g. a GPU→CPU `Download`, or the `AbstractConverter`
placeholder (§8).

---

## 7. The two planners

Alembic ships two planners over the same node/trait/rule model. Both take a root, apply rules, and
return a best plan; they differ in *how* they search.

### 7a. The heuristic planner (`HepPlanner`)

A **deterministic, program-driven rewriter**. It walks the immutable tree in a configured order
(`HepMatchOrder`), applies each matching rule (at most one transform per node per pass), and re-passes
until nothing changes (a fixed point). There is **no cost model** — it simply applies the rules you give
it, in order, to convergence. Use it for deterministic rewriting: simplifications, canonicalization, and
straightforward lowering where you don't need cost-based choice.

It still *enforces* a required output: `ChangeTraits(root, traits)` records the traits the final plan
must carry, and `FindBestPlan` verifies every node satisfies them, throwing `CannotPlanException` if the
rewrite couldn't get there. (Because conversions rewrite nodes in place rather than wrapping them, a
complete plan is convention-uniform, so a single unconverted node means the lowering failed.)

### 7b. The cost-based planner (`VolcanoPlanner`)

A **cost-based search over equivalence classes**. This is the heavier machine, and the interesting one.

The data model:
- **`NodeSet`** — an equivalence class: all nodes known to produce the same result.
- **`NodeSubset`** — the members of a set that share one trait set. A subset is itself an `INode` (so it
  can stand in as a child), and it remembers its **cheapest member** (`Best` / `BestCost`).

> The mental picture: a **`NodeSet` is a result**; its **subsets are the different physical ways to
> deliver that result**, keyed by trait set. Optimization = for the required delivery (the root's
> requested trait set), find the cheapest member, inserting enforcers where a cheaper member delivers a
> different physical form that can be converted.

The lifecycle:
1. **Register** (`SetRoot` → `RegisterImpl`). Each node's children are replaced by their subsets
   (`OnRegister`); the node is deduplicated by digest, placed in a set, costed, and its rules are fired.
2. **Fire rules.** When a node is registered, a `DeferringRuleCall` matches each rule's operand and
   *defers* every match as a `VolcanoRuleMatch` onto a `RuleQueue` (rather than applying it inline).
3. **Drive.** `FindBestPlan` hands the queue to an `IRuleDriver` (`IterativeRuleDriver`), which pops each
   match and applies it; the match's `Transform` registers the equivalent into the matched node's set.
   New equivalents fire more rules, until the queue drains (a fixed point).
4. **Propagate cost.** Whenever a node is costed, each subset it belongs to keeps the cheapest member,
   and improvements propagate to parents.
5. **Extract.** `BuildCheapestPlan` walks from the root subset, replacing each subset with its `Best`
   member, rebuilding the concrete tree (throwing `CannotPlanException` if a needed subset is empty).

`SetTopDownOpt(true)` is reserved for a future top-down (Cascades) driver and currently throws — see §10.

### When to use which

- **Heuristic** — deterministic transformations, no cost trade-offs, "always apply these rewrites."
- **Cost-based** — genuine choices between alternatives, lowering where the cheapest backend depends on
  context, trait enforcement with transfer costs (the image GPU/CPU example).

---

## 8. Trait enforcement and conversion

How does the planner *reach* a required output trait (the root must be physical / sorted / on the CPU)?

Two complementary mechanisms, both grounded in one principle Calcite states well: **explicit converters
are only needed at the root; everywhere else, a parent has already asked for its inputs in a particular
form.**

1. **Inputs request their parent's convention.** In the cost-based planner, `OnRegister` makes every
   node require its inputs in *its own* convention (a GPU operator wants GPU inputs). This propagates a
   lowering down the tree automatically: a physical node's child subset acquires a physical member via
   the child's own converter rule. (Converters are exempt — they exist precisely to bridge.)
2. **The root is enforced explicitly.** The root has no consumer to ask. So `ChangeTraits(root, traits)`
   records the requested traits, and `EnsureRootConverters` puts an **`AbstractConverter`** — an
   infinite-cost placeholder demanding "deliver my input in these traits" — into the root's set.
   `ExpandConversionRule` (registered automatically) then turns each abstract converter into a real
   conversion via `ChangeTraitsUsingConverters`, which applies the registered converter rules.

This is why converter `Source`/`Target` are `ITrait`, not just conventions: the same machinery enforces
*any* trait. A `Sort` is the enforcer for sortedness; a `Download`/`Upload` is the enforcer for the
CPU/GPU dimension. The planner inserts them, costs them, and only keeps them when they pay off.

Worked example (the image language): asked for a CPU result over a pipeline of GPU-favorable ops with a
CPU-only operation in the middle, the cost-based planner produces a plan that crosses **CPU → GPU → CPU
→ GPU → CPU**, inserting uploads and downloads exactly where a run of GPU work outweighs the transfer
cost — and *declines* to cross for a lone op when the round-trip costs more than just staying put.

---

## 9. Cost

Cost is how the optimizer chooses. The model is intentionally small and opaque:

- **`ICost`** — a comparable, combinable cost: `IsInfinite`, `IsLessThanOrEqual`, `IsLessThan`,
  `Plus`. The engine only ever compares costs and adds them up; it attaches no units.
- **`ICostFactory`** — makes the well-known costs the planner needs: `MakeCost(cpu, io)`, plus
  `MakeZeroCost` (a free leaf), `MakeInfiniteCost` (unimplementable/rejected), and huge/tiny bookends.
  A planner carries one (`IPlanner.CostFactory`) and nodes build their costs through it.
- **`Cost`** — a scalar default (a single magnitude). **`VolcanoCost`** — the cost-based planner's
  default, with CPU and I/O dimensions (compared on CPU). *(There is deliberately no row count — the
  engine is not a database.)*

A node states its **own** cost via `INode.ComputeSelfCost(planner)` (a method on the node, defaulting to
a tiny cost; a real cost model overrides it). The **cumulative** cost of a node is its self-cost plus the
best cost of each input subset:

```
cost(node) = node.ComputeSelfCost() + Σ cost(child-subset.Best)
```

A subset's `Best`/`BestCost` is the minimum over its members; `BuildCheapestPlan` follows those minima.
**Infinite** cost is how "this cannot be implemented" enters the arithmetic: an abstract converter, or a
node in a non-implementable convention, has infinite self-cost, so the planner routes around it (or
fails with `CannotPlanException` if there is no finite alternative).

Self-cost is the *only* source of cost numbers today; a statistics/metadata framework that derives costs
from cardinality and selectivity estimates is a future subsystem (see `PLAN.md`).

---

## 10. Search strategy: iterative vs. top-down

Both planners reach a fixed point, but cost-based search has a deeper design axis worth understanding,
because it governs whether the optimizer can *scale*.

- **Iterative (what's implemented).** Registration eagerly fires every matching rule; matches queue up;
  the driver drains the queue exhaustively. It enumerates the whole reachable space and then extracts the
  cheapest plan. Simple and complete, but it explores alternatives that cannot win.
- **Top-down / Cascades (future).** Demand flows from the root *down*: "optimize this group for these
  required traits," recursing into inputs and applying rules lazily only where needed. It carries a cost
  upper bound and **prunes** any branch whose lower-bound cost already exceeds the best full plan found.
  It explores the same space but visits far less of it. It needs `passThrough`/`derive` hooks (a node
  negotiating "I can deliver trait X if my input has trait Y") and a top-down rule driver — selected by
  `SetTopDownOpt(true)`, which currently throws.

Importantly, **top-down is a performance optimization, not a correctness one**: the exhaustive iterative
search already finds the optimal plan. "Cascades" is the architecture (after Graefe's Volcano → Cascades
line) that makes that search efficient on large inputs.

---

## 11. The lifecycle of a plan (putting it together)

1. **Define a domain** — node types (your operations), conventions (logical + one or more physical
   backends), rules (lowering converters, simplifications, fusions), and `ComputeSelfCost` on the
   physical nodes.
2. **Author a logical plan** — a tree of logical-convention nodes describing what to compute.
3. **Configure a planner** — register the rules (often via each convention's `Register`), and, for
   cost-based planning, set the root and the required output traits (`ChangeTraits`).
4. **Plan** — the planner fires rules to discover equivalents, costs them, enforces the required output
   (inserting converters), and returns the cheapest plan that satisfies the request — or throws
   `CannotPlanException` if no rule chain can reach it.
5. **Inspect** — `INode.ToPlanString()` renders the result as an indented tree (type, traits,
   attributes; inputs nested), which is how the tests display the plans they produce.

Supporting cast: a **`Cluster`** is the per-session environment (wraps the planner, offers `TraitSet`
/ `TraitSetOf`); **`IPlannerListener`** receives events (equivalences found, rules attempted/succeeded,
nodes chosen) for tracing; **`INodeImplementor`** is the marker for a convention's "turn this plan into
something runnable" callback.

---

## 12. What Alembic deliberately is *not*

To stay medium-agnostic, Alembic omits everything specific to relational algebra and SQL, even though it
inherits its structure from a relational optimizer:

- **No row types / schemas, no row expressions, no SQL.** A node's "output shape" (if your domain needs
  one) is just one of its identity-bearing attributes, modeled however you like — not a built-in.
- **No metadata/statistics framework** (cardinality, selectivity). Costs come from `ComputeSelfCost`
  today.
- **No materialized views, lattices, hints, correlation variables.**

The bet is that the *optimizer* — equivalence classes, traits, conventions, converters, cost-based
search — is general, and the relational parts are not. Everything in this document is phrased to apply
to any tree-shaped computation.

---

## 13. Type map (quick reference)

| Concept | Type(s) |
|---|---|
| A plan node | `INode`; bases `AbstractNode`, `SingleNode`, `BiNode` |
| Structural identity | `DeepEquals` / `DeepHashCode`; `Explain(INodeWriter)`; `INodeDigest` |
| Plan rendering | `INode.ToPlanString()` |
| A physical property (value / dimension) | `ITrait` / `ITraitDef`; multi-valued: `IMultipleTrait`, `CompositeTrait` |
| A node's full property fingerprint | `TraitSet` |
| Calling convention (logical/physical family) | `IConvention` / `Convention` |
| A transformation rule | `IRule`, `Operand`, `OperandMatcher`, `RuleCall` |
| A trait-changing rule / node | `IConverterRule` / `ConverterRule`; `IConverter` / `ConverterImpl`; `AbstractConverter` |
| Cost | `ICost`, `ICostFactory`, `Cost`, `VolcanoCost`; `INode.ComputeSelfCost` |
| Heuristic planner | `HepPlanner`, `HepProgram`, `HepRuleCall` |
| Cost-based planner | `VolcanoPlanner`, `NodeSet`, `NodeSubset`, `RuleQueue`, `IRuleDriver` / `IterativeRuleDriver`, `VolcanoRuleCall` / `VolcanoRuleMatch` / `DeferringRuleCall`, `ExpandConversionRule` |
| Session / observation | `Cluster`, `IPlannerListener`, `INodeImplementor` |
| Failure to plan | `CannotPlanException` |
