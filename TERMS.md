# Terms

A plain-language glossary of the concepts in Alembic's architecture. Each entry describes what the
thing *is* and what role it plays, independent of any one class's implementation details. This is the
vocabulary the rest of the codebase (and its design discussions) assumes.

## Cluster

The shared environment for a single planning session. Every op built during that session carries a
reference to the same cluster, and the cluster carries a reference to the **planner** that will optimize
those ops. It is the thread that ties a tree of ops to the engine working on them.

Concretely a cluster holds:

- **The planner.** An op reaches its planner through its cluster (`op.Cluster.Planner`). This is how a
  op-side operation that needs the engine — for example, a physical op converting one of its inputs
  to a required trait set — gets hold of it without being passed the planner explicitly. Because of this,
  the invariant is strict: an op's cluster must point at the planner that actually optimizes it. (Tests
  build `new Cluster(planner)` for exactly this reason; there is no shared, planner-agnostic cluster.)

- **The default trait set.** The cluster exposes a baseline `TraitSet` — by default the planner's empty
  trait set, meaning every registered trait dimension sitting at its default value — plus a helper
  (`TraitSetOf`) to produce that baseline with specific traits applied. Ops use this as the starting
  point for describing their own physical properties. The accessor is named generically rather than
  "empty" because a cluster is free to override what its default is.

A cluster is deliberately *minimal*: it is the planning environment stripped of anything specific to a
particular algebra (a query engine's cluster would also carry things like an expression builder or a
metadata cache; Alembic's carries neither). It is just "the planner, as seen from inside an op," plus
the default trait set to build from.

Created once per planning session and passed down as the tree is constructed.
