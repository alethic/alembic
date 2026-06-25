# Terms

A plain-language glossary of the concepts in Alembic's architecture. Each entry describes what the
thing *is* and what role it plays, independent of any one class's implementation details. This is the
vocabulary the rest of the codebase (and its design discussions) assumes.

Entries are alphabetical. Throughout, "op," "plan," and "result" mean *your* domain — Alembic attaches
no meaning to them.

## Convention

The coarsest and most important **trait**: the family an op belongs to, the "world" it runs in. It
answers *in what form does this operation exist?* — a **logical** world (abstract: *what* to compute) or
a particular **physical** one (a concrete backend that can actually run it: a CPU, a GPU, an
interpreter, a codegen target). Because convention is a trait, the distinction between a logical plan
and a physical plan, and the act of **lowering** one to the other, are all just trait machinery (an op
in a logical convention being converted to a physical one). A convention can also declare an interface
its ops must implement, so the planner rejects mismatches, and can contribute the rules that *produce*
it — so "enabling a backend" is "registering its convention."

## Converter

The special, central kind of thing that changes a **trait** without changing the result. It comes in
two forms: a **converter rule** declares a source trait and a target trait and knows how to produce an
op carrying the target (a lowering rule, or a trait *enforcer* like a sort); a **converter op** is an op
that sits in the plan and bridges the two (its input has one trait value, its output another — a GPU→CPU
download, say). Converters are how the planner *reaches* a required output form: it inserts them where
needed, costs them, and keeps them only when they pay off.

## Cost

How the optimizer chooses between alternatives. The model is deliberately small and opaque: a cost can
be compared with another and added to another, and that is all — it carries no units and no row counts
(Alembic is not a database). An op states its **self-cost** (its own cost, ignoring its inputs); the
**cumulative** cost of an op is its self-cost plus the best cost of each input. An **infinite** cost is
how "this cannot be implemented" enters the arithmetic, so the planner routes around it. Self-cost is
the only source of cost numbers today; statistics-derived costs are future work.

## Digest (and identity)

The optimizer constantly asks "have I seen this op before?" and "are these two ops the same?". The
answer is **structural identity**, a contract separate from an op's ordinary reference equality: two ops
are the same when they have the same type, the same traits, the same **output type**, the same
attributes, and recursively equal inputs. A **digest** is a small cached handle on that contract — the
key the cost-based planner deduplicates on. Identity is the bedrock of the whole engine: it is what lets
the planner recognize that two differently-built subtrees are really one, and share the work.

## Equivalence class (Set and Subset)

The cost-based planner's core data model. An **equivalence class** (a *set*) is all the ops known to
produce the same result — the same output, reachable from one another by rules. A **subset** is the
members of a set that share one trait set. The mental picture: *a set is a result; its subsets are the
different physical ways to deliver that result.* A subset is itself usable as an op's input (so the tree
becomes a graph of subsets during planning), and it remembers its cheapest member. Optimization is then:
for the delivery the root asks for, find the cheapest member of each set, inserting converters where a
cheaper member delivers a form that must be converted.

## Op

The unit of a plan: a node in the tree (a DAG, during planning) that the engine works over. Alembic
attaches no meaning to an op — it may be a query operator, a pipeline stage, a build step, an arithmetic
expression, anything. Its contract is small: ordered **inputs**, a **trait set** (its physical
properties), an **output type** (what it produces), a structural-**identity** contract the planner
deduplicates on, and a way to **copy** itself with new traits/inputs (how a rule rewrites it). Most op
types extend the convenience base that derives identity and the output type for them; an op can also
implement the contract directly. The planner rewrites by producing new ops and sharing the subtrees it
does not touch.

## OpCluster

The shared environment for a single planning session. Every op built during that session carries a
reference to the same cluster, and the cluster carries a reference to the **planner** that will optimize
those ops. It is the thread that ties a tree of ops to the engine working on them.

Concretely a cluster holds:

- **The planner.** An op reaches its planner through its cluster (`op.Cluster.Planner`). This is how an
  op-side operation that needs the engine — for example, a physical op converting one of its inputs
  to a required trait set — gets hold of it without being passed the planner explicitly. Because of this,
  the invariant is strict: an op's cluster must point at the planner that actually optimizes it. (Tests
  build `new OpCluster(planner)` for exactly this reason; there is no shared, planner-agnostic cluster.)

- **The default trait set.** The cluster exposes a baseline `OpTraitSet` — by default the planner's empty
  trait set, meaning every registered trait dimension sitting at its default value — plus a helper
  (`TraitSetOf`) to produce that baseline with specific traits applied. Ops use this as the starting
  point for describing their own physical properties. The accessor is named generically rather than
  "empty" because a cluster is free to override what its default is.

A cluster is deliberately *minimal*: it is the planning environment stripped of anything specific to a
particular algebra (a query engine's cluster would also carry things like an expression builder or a
metadata cache; Alembic's carries neither). It is just "the planner, as seen from inside an op," plus
the default trait set to build from.

Created once per planning session and passed down as the tree is constructed.

## Output type

What an op *produces*, described as an opaque value. Alembic attaches no meaning to it: the only thing
the engine ever asks of an output type is whether two of them are **equivalent**. That single relation
is the **equivalence invariant** — two ops can be treated as interchangeable (deduplicated, placed in
the same equivalence class, registered as equivalents of one another) only when their outputs match.

An output type is **derived, not assigned**. Each op computes its own from its inputs; a leaf returns an
intrinsic type it was given when constructed. An op that attaches no meaning to its output uses the
trivial **Void** type, which is equivalent only to itself — so a tree of ops that don't care about
output shape are all Void and freely interchangeable, while a tree that does care propagates real types
up from its leaves.

Output type is deliberately *not* a trait. A trait (sortedness, a backend convention) can be established
within an equivalence class by inserting an enforcer, because it changes only *how* a result is
delivered. An output type is the part that says *what* the result is — so it never converts; changing it
is the job of a rule that rewrites one op into a different one. This is the slot a relational layer would
fill with a row type; a non-relational medium leaves it Void.

Supplied entirely by the domain: the engine never constructs an output type, it only compares them.

## Planner

The engine that takes a root op and produces a better equivalent. Alembic ships two over the same
op/trait/rule model. The **heuristic** planner is a deterministic, program-driven rewriter: it applies a
configured program of rules to a fixed point, with no cost model — use it for simplification,
canonicalization, and straightforward lowering. The **cost-based** planner searches **equivalence
classes**, costs the alternatives, and extracts the cheapest plan that satisfies a required output form,
inserting converters along the way. The *same* rules work under both, because a rule reaches its matched
ops through the match (not by navigating inputs), and each planner decides what "transform" means —
rewrite-in-place (heuristic) or register-an-equivalent (cost-based).

## Rule

A transformation the planner applies — the engine never invents rewrites on its own, it only applies the
rules you give it. A rule has two parts: an **operand**, a pattern that selects where it applies (an op
type plus optional child patterns), and an **action** that, given a match, builds one or more
**equivalent** ops and offers them back to the planner. A rule reaches its matched ops through the match
itself, which is what lets one rule serve both planners. A **converter rule** is the special kind that
changes a trait (see *Converter*).

## Trait

A property of an op's output that can vary among plans that compute the **identical result** — it
changes *how* the result is delivered, never *what* the result is. The litmus test: *can two ops have
the same logical result but differ in this property?* Sortedness (`scan` vs `sort(scan)`) and convention
(a logical op vs its GPU form) are traits; a filter's predicate, or the op's output type, are not — they
change the result, so they are **identity**, not traits. Two things make a trait useful to the
optimizer: a consumer can **require** a value (a merge join needs sorted inputs), and an **enforcer** can
establish it without changing the result (wrap an op in a sort). Often there is a partial order — sorted
by `[a,b]` already satisfies a request for sorted by `[a]` — so a stronger value can stand in for a
weaker one.

## Trait set

One value per trait dimension: an op's full physical fingerprint, a point in trait-space. It answers, in
one object, every physical question about an op — its convention, its sortedness, and so on. Trait sets
are **interned** (two equal sets are one shared instance), so they compare and hash cheaply, which
matters because the planner manipulates them constantly. The key question a trait set answers is
*satisfaction*: does this set meet a requirement on every dimension the requirement names? That is what
the planner asks when deciding whether a candidate already delivers what a consumer (or the root) wants.
