# Alembic

**A language-agnostic planning and rewrite engine for .NET.**

Alembic plans operations over a tree of nodes. You bring your own node types and your own rules;
Alembic provides the machinery that matches rules against the tree, applies them, and drives the
rewrite to a result. It attaches no meaning to your nodes — they may model a query, a data
pipeline, a build graph, an expression language, or anything else expressible as a tree of
operations. It is not a query engine and assumes no query language; it is the planning core on its
own.

The design distills ideas from the planner of [Apache Calcite](https://calcite.apache.org/) —
immutable nodes, physical *traits*, *conventions*, pattern-matching *rules*, and a *planner* — but
strips away the relational algebra so the same machinery can be applied to any domain.

## What it gives you

- **A node model.** Immutable nodes with a small, generic contract. The planner rewrites by
  producing new trees and sharing the subtrees it does not touch.
- **Traits and conventions.** Interned, low-overhead physical properties carried by each node. A
  *convention* marks the family a node belongs to — for example a logical form versus a particular
  execution backend.
- **Rules.** Strongly-typed pattern matching over subtrees, including *converter rules* that lower a
  node from one convention to another.
- **A heuristic planner.** A program-driven planner that applies a set of rules to a tree until it
  reaches a fixed point — for example, lowering an entire tree from a logical convention to a
  physical one.

## Example

Define your node types and a rule, then run the planner:

```csharp
var program = HepProgram.Builder()
    .AddRuleCollection(new IRule[] { new MyConverterRule(physicalTraits) })
    .Build();

var planner = new HepPlanner(program);
planner.SetRoot(root);
var plan = planner.FindBestPlan();
```

## Status

Early and evolving. The current release provides the node model, interned trait sets, conventions
with converter rules, a typed rule/operand matcher, and a heuristic (HEP) planner.

## License

Apache 2.0. See [LICENSE.txt](LICENSE.txt).
