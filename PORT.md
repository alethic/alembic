# Calcite → Alembic port audit

Alembic ports the core of Apache Calcite's planner — the cost-based (Volcano/Cascades) and heuristic
(HEP) optimizers, made medium-agnostic. The **per-member Calcite → Alembic mapping lives in code**, as
`[Provenance(className, member?)]` attributes on every ported type and member; that is the authoritative,
machine-readable ledger. This file keeps only what *can't* live in an attribute: what was deliberately
**not** ported (§1), the **fidelity audit** of divergences from Calcite (§2), and tracked **follow-ups**
(§3).

**Naming.** "Op" is Alembic's counterpart to Calcite's "Rel" — the medium-agnostic operation the engine
plans over. So `RelNode` → `IOp`, `PhysicalNode` → `IPhysicalOp`, `SingleRel` → `SingleOp`,
`RelOptCluster` → `OpCluster`, `RelTraitSet` → `OpTraitSet`, etc.; the optimizer "Opt" and the tree-node
"Node" are both dropped. Members keep their Calcite-faithful names (`getCluster()` → the `Cluster`
property, and so on).

---

## 1. Deliberately not ported

Whole subsystems Alembic does not implement, by design or as future work:

| Calcite | Status |
|---|---|
| `IterativeRuleQueue` importance ranking (`IterativeRuleQueue` is FIFO) | **future** |
| `RelMetadataQuery` / metadata providers / `VolcanoRelMetadataProvider` / `HepRelMetadataProvider` | **future** — costs come only from `ComputeSelfCost`; in particular the top-down driver's lower-bound pruning is disabled (`GetLowerBound` returns zero) pending `getLowerBoundCost` |
| `RelSubset` `passThrough`/`derive` *trait-derivation logic* | **done** — covered by `CascadesTests`: `PassThrough`/`Derive` composition unit tests, an end-to-end top-down pass-through (a filter pushes a sortedness requirement down to its source), and an end-to-end bottom-up derive (a filter derives a sorted equivalent from a source that independently offers a sorted scan) |
| `RelBuilder` and the relational operator library | **out of scope** — medium-agnostic engine |
| `RexNode`, `RelDataType`/row types, row counts, hints, correlation, schemas | **out of scope** — relational |
| `RelShuttle`/`RexShuttle`/`RelVisitor` | **out of scope** — visitors |
| `RelNode.getId()` global id sequence | **n/a** — Alembic uses object identity |
| `Dumpers`, `assertNoCycles`, `assertGraphConsistent`, `dumpGraph`, `VolcanoTimeoutException` | **debug/ops** — not ported |
| materialized views (`RelOptMaterialization`) | **out of scope** — relational |
| `Convention.getRelFactories` | **out of scope** — relational operator factories |
| `TransformationRule` / `PhysicalNode` markers and the convention-pruning they drive in `matchRecurse` / `addRule` | **n/a** — Alembic has no built-in logical/physical rule split; `MatchRecurse` therefore omits the "skip transformation-rule matches that cross conventions" filter |
| `RelSubset.class` operands + `getSubsetsSatisfyingThis`, and `setChildRels`/`getChildRels` bookkeeping in `matchRecurse` | **not ported** — Alembic operands never match a subset directly, and Calcite itself notes the unordered `childRels` path is unused |
| `RelRule.Config` / per-rule `Config` (immutables) | **n/a** — Config exists to keep Calciteʼs built-in rule library uniformly configurable; Alembic ships no rules, so rules take constructor args. A consumer who builds a rule library can layer the same pattern over `OpRule` themselves (C# records + `with`). |

---

## 2. Fidelity audit

A method-by-method comparison of each ported class against its Calcite original, performed by isolated
single-class agents (cold context, fed only the two file paths + the `[Provenance]` mapping). Only **divergences**
are listed; members verified `FAITHFUL` are omitted, with a coverage line per class. Verdicts:
`DIVERGENT-OK` (differs, justified — reason given) · `DIVERGENT-BUG` (behavior-affecting) ·
`MISSING` (`GAP` = genuine omission; otherwise an out-of-scope tag) · `MOVED` (relocated — verified in the
named file) · `EXTRA` (Alembic member with no Calcite counterpart).

**Provenance is recorded in code.** Every Calcite-derived type and member carries a
`[Provenance(className, member?)]` attribute — the authoritative, machine-readable record of what it
derives from (those `[Provenance]` attributes are the per-member Calcite→Alembic mapping). Its `Source`
property (a `ProvenanceSource` of `Calcite` / `Other` / `Local`) is the **required first constructor
argument**, so it can never be omitted — all 885 carry it: 884 `Calcite`, and the lone `Other` is
`WeakInterner` (Guava). `Local` is a human-only mark — an explicit "reviewed, intentionally
Alembic-original" assertion, never added automatically. An ordinary attribute can only assert derivation,
not its *absence*, so the audit records that other half here: the **Alembic-original members that
deliberately carry no `[Provenance]`** (no Calcite analog), listed in the subsection immediately below.

### Alembic-original members (no `[Provenance]` — no Calcite analog)

Compiled across the project; everything not listed here is annotated — except **private/protected fields**,
which are exempt from provenance (implementation detail, not public surface) and so are neither annotated
nor listed here.

- **`OpCluster`**: its ctor (Calcite's `RelOptCluster` ctors are relational/package-private). **`IPlannerListener.PlannerEvent`**: `Source` (JDK `EventObject`).
- **`OpTraitDef` / `OpCompositeTrait`**: the non-generic abstract base classes themselves are an Alembic structural device — Calcite has a single generic class (`RelTraitDef<T>` / `RelCompositeTrait<T>`) used raw, which C# can't express, so the non-generic base + generic subclass split stands in for it. Their members map to the Calcite class (annotated), but the *split into two types* has no Calcite analog. Also Alembic-original: **`OpTraitDef`**'s `_interned` field. **`OpTraitSet`**: the explicit non-generic `IEnumerable.GetEnumerator` and the typed `Equals(OpTraitSet?)` overload (the private ctor, nested `Cache`, `ReplaceAt`, and `FindIndex` are now annotated to their `RelTraitSet` analogs). **`IOpMultipleTrait`**: the inherited `IComparable<>`. **`Convention`**: the single-arg ctor and `Equals`/`GetHashCode` (by-name — Calcite's `Impl` uses identity).
- **`OpRuleOperand`**: the convenience constructors (Calcite has only the two real ctors; ours add default-filling overloads). **`TraitMatchingRule`**: the `_converterRule` field.
- **`OpSet`**: the `OpCluster` property and the single-arg `GetOrCreateSubset(traits)`. **`OpSubset`**: `LiveSet` (EXTRA — equiv-root resolution). **`VolcanoPlanner`**: `ChangeTraits` (the `setRoot`+`changeTraits` convenience), `RootSubset`, `CostEquals`, `OperandsFor`, the multi-step conversion BFS (`FindConversionPath`/`ConversionEdges`/`Reconstruct`/`ConversionStep`), and `_classes`/`_cluster`. **OpRule queues/drivers**: the `_seen` dedup sets (replace Calcite's `MatchList`/`names`). **`ExpandConversionRule`**: its public ctor (replaces Calcite's `Config`/singleton).
- **`HepPlanner`**: the `NoDag` property (a get/set accessor with no Calcite analog — Calcite's `noDag` is a private ctor-set field), `EnsureSatisfies`, `ShallowEqual`, `RemoveFiredRules`, `Match`, and the nested **`FiredKey`** type (replaces Calcite's `ImmutableIntList` cache key). **`HepProgram`**: the `_program` back-ref. **`HepProgramBuilder`**: the `Check` helper. **`HepInstruction` states**: the explicit `Instruction` back-references (Java uses the implicit outer `this`).
- **Graph iterators** (`DepthFirst`/`BreadthFirst`/`Topological`/`HepVertexIterator`): the `IEnumerator` plumbing (`Current`, `Reset`, `Dispose`). **`Pair`**: the non-generic `Pair` companion (a C# type-inference workaround — Alembic-original type). **`DefaultDirectedGraph`**: the `VertexSetView`. **`DefaultEdge`**: the `DefaultEdgeFactory` (Calcite uses a lambda).
- **`WeakInterner`**: class-level provenance points to Guava `Interners.newWeakInterner` with `Source = ProvenanceSource.Other` (not Calcite); its members are un-annotated (the .NET implementation is hand-rolled, not a member-for-member port). **`ProvenanceAttribute`** / **`ProvenanceSource`**: Alembic-original infrastructure — no upstream analog.

### `RelSubset` → `OpSubset` — ~50 members, all `FAITHFUL` except:

| Calcite member | Verdict | Note |
|---|---|---|
| `copy(traitSet, inputs)` | **RESOLVED (ported)** | `Copy` now mirrors Calcite: for empty inputs it returns `this` when `traits.Simplify()` equals its trait set, else `Set.GetOrCreateSubset(simplified, IsRequired)`; non-empty inputs throw. |
| `getBestOrOriginal()` / `stripped()` | DIVERGENT-OK | soften Calcite's null-assertion to a `null`/`this` fallback; benign |
| `getOriginal()` | DIVERGENT-OK | routes through `LiveSet` (equiv-root) because Alembic leaves dead sets in place after merge |
| ctor / `disableEnforcing` / `startOptimize` asserts | DIVERGENT-OK (DEBUG) | Java `assert`s dropped |
| `computeBestCost` | DIVERGENT-OK (METADATA) | init-scan of subsuming subsets dropped; cost is passed into the ctor instead |
| `CheapestPlanReplacer.visit` | DIVERGENT-OK (DEBUG) | omits the `provenanceMap` write after `copy` (provenance/dump not ported) and the dead-end diagnostic dump |
| `add(rel)` | **RESOLVED (moved)** | now ported onto `OpSubset.Add` to match Calcite's three-method layering: `OpSet.Add` (entry — `getOrCreateSubset` → `subset.Add` → return subset) / `OpSubset.Add` (guard on already-present → fire the equivalence-found event → delegate) / `OpSet.AddInternal` (the raw `Ops` insert + representative `Rel`). The equivalence event moved out of `VolcanoPlanner.AddOpToSet` into `OpSubset.Add`, and is fired the faithful way — via `Set.Cluster.Planner` (Calcite's `rel.getCluster().getPlanner()`). This required fixing the test fixtures so an op's cluster is bound to the planner optimizing it (the shared throwaway-planner `Clusters.Default` was deleted; each test now builds `new OpCluster(planner)`), restoring Calcite's cluster↔planner invariant that `IPhysicalOp.PassThrough`/`Derive` already assumed. Merge-time op moves (`OpSet.MergeWith`) deliberately use the raw `AddInternal` path (no add-event), matching Calcite's merge routing through `reregister`, not `subset.add`. |
| `LiveSet`, `infiniteCost` ctor param | EXTRA (justified) | equiv-root resolution; cost decoupled from metadata. |

Three themes recur across the Volcano findings below and are worth fixing as units rather than piecemeal:
**(A) op pruning** — ~~Calcite's `prunedOps`/`checkPruned`/`prune` are entirely absent, which also hollows the staleness guards in `VolcanoRuleCall.onMatch`, `RuleQueue.skipMatch`, and the iterative queue.~~ **RESOLVED**: the pruning machinery, the `OnMatch` staleness guard, and `SkipMatch` are ported (see the `VolcanoPlanner`/`VolcanoRuleCall`/`RuleQueue` rows). **(B) canonization** — ~~Alembic's `EquivRoot` returns the live *set*, never re-resolving a subset to the leader set's subset (Calcite's `canonize(subset)`), and the root subset is never re-canonized after a merge.~~ **RESOLVED**: `RegisterSubset` returns `Canonize(subset)`; the no-arg `Canonize()` re-points `_root` after each match in `IterativeRuleDriver.Drive`. (The one remaining `canonize(subset)` call site is the `registerImpl` converter recheck, tracked in the `registerImpl` row.) **(C) converter seeding** — ~~Calcite seeds abstract/enforcer converters per-subset inside `RelSet.getOrCreateSubset → addConverters`; Alembic only seeds them at the root (`EnsureRootConverters`).~~ **RESOLVED**: `OpSet.GetOrCreateSubset`/`AddConverters` ported (see the `RelSet` rows); `EnsureRootConverters` is the faithful port of Calcite's `ensureRootConverters`, kept alongside it as Calcite does.

### `VolcanoPlanner` → `VolcanoPlanner` — ~53 members; findings:

| Calcite member | Verdict | Note |
|---|---|---|
| `propagateCostImprovements` | **RESOLVED (fixed)** | ported verbatim: a `Dictionary<IOp,IOpCost>` (`propagateOps`, identity-keyed) + a `PriorityQueue<IOp,IOpCost>` ordered by cost; reads the current cost from the map, walks `subset.GetParents()` (per-subset, not raw `set.Parents`), and the two `continue` guards match Calcite. No more recursion/stack-overflow. The one adaptation: .NET's `PriorityQueue` has no `remove`, so the decrease-key re-enqueues and the stale entry is harmlessly skipped on poll (the cost is read from the map, so a re-poll is a no-op) — Calcite's `remove`+`offer` and this produce identical processing. |
| `merge` | **RESOLVED (fixed)** | ported the swap (`GetChildSets` + `IsSmaller` → always merge the newer/smaller into the older/larger, or a child into its parent) and the root re-point (re-point `_root` to the survivor's subset for the root traits + re-run `EnsureRootConverters` when the absorbed set held the root). `OnSetMerged` stays in `MergeWith` (the swap just changes which object is `this`). |
| `ensureRegistered` | **RESOLVED (fixed)** | when the op is already registered and a known `equivalent` lives in a different set, the two sets are now merged; the result is `Canonize`d (re-resolved on the live set) — Calcite's `ensureRegistered` + `canonize`. |
| `ensureRootConverters` | **RESOLVED (ported)** | rewritten to mirror Calcite: a dedup `subsets` set seeded from existing `AbstractConverter`s (when not top-down), then for each root-set subset differing from the root by exactly one trait (and not already converted) registers `AbstractConverter(root.Traits, subset, difference[0].TraitDef)`. Calcite keeps `ensureRootConverters` alongside the per-subset `addConverters`, so Alembic does too — not removed. |
| `getCost` (NONE convention) | **RESOLVED (ported)** | `GetCost` mirrors Calcite: when `_noneConventionHasInfiniteCost` (default true, toggled by `SetNoneConventionHasInfiniteCost`) and the op's convention is `Convention.None`, returns infinite cost; otherwise nudges a non-positive self-cost to tiny before summing input costs. |
| `canonize(subset)` | **RESOLVED (ported)** | `RegisterSubset` now ends with `Canonize(subset)` (re-resolving to the leader set's subset after its internal merge, on Calcite's exact `set != subset.set && set.equivalentSet == null` condition), matching `registerSubset`; `EnsureRegistered` already did. `Canonize(OpSubset)` itself matches Calcite's `canonize(RelSubset)`. |
| `prune` / `prunedOps` / `checkPruned` | **RESOLVED (ported)** | `VolcanoPlanner` now has a `_prunedOps` identity set with `Prune` (public, overriding the `IOpPlanner`/`AbstractOpPlanner` no-op), `IsPruned`, and `CheckPruned` (propagates pruned-ness across a discovered equivalence, wired into the `RegisterImpl` and `Rename` dedup branches). *Omitted as unreachable:* the `registerImpl` pruned-skip-add — Alembic has no caller that prunes an op before it is registered, so an op is never pruned at registration time; pruning is consulted by the `OnMatch` guard and `SkipMatch` below. The `prune()` call sites in Calcite (a `SubstitutionRule`'s auto-prune, `mergeWith`'s redundant-enforcer prune) depend on features Alembic doesn't have yet, so `Prune` is public-and-available rather than self-invoked. |
| `setLocked`/`locked` | **RESOLVED (ported)** | `VolcanoPlanner._locked` + `SetLocked(bool)`; `AddRule` returns `false` immediately when locked (before the base dedup), matching Calcite. |
| `clear` | **RESOLVED (ported)** | `IOpPlanner.Clear()` (base no-op); `VolcanoPlanner.Clear()` removes all rules and clears `_classOperands`/`_classes`/`_allSets`/`_digestToOp`/`_opToSubset`/`_prunedOps`/the rule driver, and resets root/cluster/`_nextSetId`. (Relational-only state — materializations, lattices, provenance — has no analog.) |
| `registerImpl` converter post-merge `fixUpInputs` recheck | **RESOLVED (ported)** | the converter block now mirrors Calcite: after `Merge(set, childSet)` it runs `FixUpInputs(op)`, and if the re-pointed op now coincides with an existing expression, `ObliterateOp`s it from the set and returns the existing subset. (Enabled by the mutable op model — see §3.) |
| `setRoot` omitting `ensureRootConverters` | DIVERGENT-OK (false-positive) | Alembic runs `EnsureRootConverters` in `FindBestPlan` instead — net equivalent; the agent flagged it without that context. |
| `ChangeTraitsUsingConverters` | DIVERGENT-OK | reimplemented as BFS over a conversion graph vs Calcite's per-trait-index sequential convert; broader search, plausibly equivalent. |

### `RelSet` → `OpSet` — ~25 members; findings:

| Calcite member | Verdict | Note |
|---|---|---|
| `getOrCreateSubset` → `addConverters` | **RESOLVED (ported)** | `OpSet.GetOrCreateSubset(traits, required)` now follows Calcite: tracks `needsConverter` (set on subset creation, or when a subset first becomes required/delivered; cleared for `Convention.None`), and calls `AddConverters` when set. |
| `addConverters` | **RESOLVED (ported)** | `OpSet.AddConverters(subset, required, useAbstractConverter)` seeds converters from each delivered subset to a new required one (and vice-versa): per-pair dedup via a `_conversions` set, `OpTraitSet.Difference` + `OpTraitDef.CanConvert` to decide `needsConverter`, then an `AbstractConverter` (bottom-up: `useAbstractConverter = !TopDownOpt`) or `Convention.Enforce` (top-down), registered against the target subset. `IsEnforceDisabled` and `UseAbstractConvertersForConversion` are honoured. *(`VolcanoPlanner.EnsureRootConverters` is Calcite's separate `ensureRootConverters`, kept alongside this — both exist in Calcite.)* |
| `obliterateRelNode` | **RESOLVED (ported)** | `OpSet.ObliterateOp(op)` = `Parents.Remove(op)`, matching Calcite. (Its full wiring into the parent-back-link maintenance is part of the P1 4b fix.) |
| `mergeWith` | **RESOLVED (moved)** | now matches Calcite: orchestration in `VolcanoPlanner.Merge` (equiv-root resolution), migration in `OpSet.MergeWith(planner, other)`; planner's `PropagateCostImprovements`/`FireRules`/`Rename`/`RemoveSet`/`MapOpToSubset` exposed `internal` (the C# analog of Calcite's package-private access). *(The merge's P1 bugs — `isSmaller` direction, root re-point — are now fixed; see the `merge` row above.)* |
| `rename` + `fixUpInputs` | **RESOLVED (ported)** | both are now line-by-line mutate-in-place ports (the op model became mutable — see §3). `FixUpInputs(op)` `Canonize`s each child subset, moves the parent back-link to the new child set, and on change removes the old digest, `ReplaceInput`s each input, recomputes the digest, and returns `bool`. `Rename(op)` re-adds under the new digest and, on collision, restores the equivalent, drops op from its subset, reassigns any `subset.Best == op`, and merges the sets. |
| `fixUpInputs`/`rename` parent back-links | **RESOLVED (ported)** | back-links are maintained exactly as Calcite does: `FixUpInputs` moves a parent from a child's old set to its new set (`Parents.Remove`/`Add`) when that child is re-canonized; `Rename`'s collision branch removes op's back-links from all its children before discarding it. |

### `VolcanoRuleCall` → `VolcanoRuleCall` — findings:

| Calcite member | Verdict | Note |
|---|---|---|
| `onMatch` | **RESOLVED (ported)** | `OnMatch` now runs the guard loop over the bound ops before firing: skips the match if an op has no subset (`GetSubset` null — removed during a rename), its set was merged away (`Set.EquivalentSet != null`), it was removed from its subset (`!subset.Contains(rel)`), or it is pruned. Fixes firing a rule on an op already removed/merged (theme A/B). |
| `transformTo(rel, equiv)` | **RESOLVED (ported)** | the `equiv` map overload is now present: `TransformTo(equivalent, IReadOnlyDictionary<IOp,IOp>)` registers each map entry via `EnsureRegistered(key, value)` before the root, so a rule can declare secondary equivalences in one call. Also added the faithful guard — a transformation rule may not produce an `IPhysicalOp`. (Calcite's hint-propagation `handler` overload is RELATIONAL and intentionally omitted.) |
| `matchRecurse` `RelSubset.class` / `setChildRels` branches | DIVERGENT-OK | the omitted subset-operand + unused `childRels` paths — already tracked in §1. |

### `VolcanoRuleMatch` → `VolcanoRuleMatch` — `allNotNull` constructor null-check **RESOLVED (ported)**: the ctor throws `ArgumentException` if any bound op is null. Dedup via `Equals`/`GetHashCode` is FAITHFUL to the digest's purpose.

### `TopDownRuleDriver` → `TopDownRuleDriver` — **no bugs/gaps.** All tasks FAITHFUL; the only divergences are the already-documented metadata lower-bound, materialization roots, and timeout omissions (DIVERGENT-OK).

### OpRule queues / drivers — findings:

| Class · member | Verdict | Note |
|---|---|---|
| `RuleQueue.skipMatch` (+ iterative `popMatch`) | **RESOLVED (ported)** | `SkipMatch` now skips a match when any bound op is pruned, or when the same subset repeats along a root-to-leaf operand path (a cycle — an op consuming its own output, via `HasDuplicateSubsetOnPath`). `IterativeRuleQueue.PopMatch` now loops, dropping skipped matches (`TopDownRuleQueue` already called it). |
| `IterativeRuleQueue` phase/importance ranking, `MatchList` | DIVERGENT-OK | FIFO + `_seen` replaces the phase/substitution-priority queue — intentional simplification. |
| `RuleQueue.clear()` `boolean`→`void` | DIVERGENT-OK | the "was non-empty" signal is dropped consistently; verify no caller needed it. |
| `IterativeRuleDriver.drive` post-match `canonize()` | **RESOLVED (ported)** | added the no-arg `VolcanoPlanner.Canonize()` (`_root = Canonize(_root)`, Calcite's `canonize()`); `Drive` reshaped to Calcite's `while(true)` loop and calls `_planner.Canonize()` after each `OnMatch()`, re-finding the root subset when a merge moves it. |

### `AbstractConverter` / `ExpandConversionRule` / `IPhysicalOp` — findings:

| Member | Verdict | Note |
|---|---|---|
| `AbstractConverter.explainTerms` | **RESOLVED (ported)** | overridden to emit each enforced trait (`Item(trait.TraitDef.Name, trait)`) after the base terms, as Calcite. Required making `OpTraitSet` enumerable (`IEnumerable<IOpTrait>`) — the analog of `RelTraitSet` being `Iterable`. |
| `ExpandConversionRule.INSTANCE` / `Config` | DIVERGENT-OK | singleton/Config plumbing replaced by a public ctor + inline operand. |
| `IPhysicalOp.passThrough`/`derive` | FAITHFUL | compose via `IOpPlanner.ChangeTraits` (= `changeTraits`, `RelOptRule.convert`'s target). |

### `HepPlanner` → `HepPlanner` — ~56 members; findings (PORT.md's "method-audited" claim was optimistic):

| Calcite member | Verdict | Note |
|---|---|---|
| `matchOperands` SOME (default) | **RESOLVED (fixed)** | confirmed by re-verification: Calcite accepts `childRels.size() >= n` (binds the first n). `MatchOperand`'s SOME case changed from exact `==` to `op.Children.Length < operand.Children.Length` → reject; binds the first n positionally. (Current fixed-arity ops are unaffected, so no test churn.) |
| `matchOperands` UNORDERED | **RESOLVED (fixed; model aligned to Calcite)** | confirmed: Calcite's UNORDERED operand has **one** child operand that matches **any** child, size-agnostic (`matchAnyChildren`); the asserted single child operand is `RelOptRuleOperand`'s contract. Alembic had built UNORDERED as an N↔N exact bijection (a non-Calcite model) which rejected Calcite's actual pattern. Reworked `MatchOperand`'s UNORDERED case to `matchAnyChildren` (each child operand matches any one child; op child count unconstrained), removed the `MatchUnordered` bijection helper, and reworked the `OperandPolicyTests` UNORDERED cases to the single-operand/any-position model. (Alembic does not port Calcite's debug `assert children.size()==1`, so it harmlessly tolerates N unordered operands — each still matches any child.) |
| `applyRule` converter force-conversion guard | **RESOLVED (ported)** | the `doesConverterApply`/`parentTrait` check is now wrapped in `if (converter.IsGuaranteed \|\| !forceConversions)`, as Calcite — a force-converted non-guaranteed converter skips the gate and fires. (Reworked `ConverterProgramTests` to Calcite's behavior: an `AddRuleInstance`-forced non-guaranteed converter now fires directly.) |
| `contractVertices` large-plan recursive merge | **RESOLVED (ported)** | `ContractVertices` mutates each parent in place via `ReplaceInput` (the op model is now mutable) and includes the `!_noDag && _largePlanMode` recursive parent-path merge: when a re-pointed parent's digest matches an existing vertex, it recurses to contract them. |
| `applyRule` CommonRelSubExprRule parents | **RESOLVED (ported)** | `ApplyRule` now builds the parent list (each parent vertex's `CurrentOp`) and passes it through `HepRuleCall` to the base `OpRuleCall`, which gained a `parents` ctor overload + `Parents` getter (`getParents()`); Volcano calls pass `null`, as in Calcite. |
| no-arg / `(program, context)` ctors, `clear()` override | **RESOLVED (ported, minus Context)** | added `HepPlanner(program, IOpCostFactory)` and a no-arg `HepPlanner()` (empty program, large-plan mode + fired-rules cache on, for multi-phase reuse); `Clear()` override = `base.Clear()` + `ClearRules()`. The `(program, Context)` and `onCopyHook` variants have no analog — Alembic has no `Context`/copy-hook. |
| `onCopyHook`/`onCopy` | MISSING (RELATIONAL) | copy-notification hook absent. |
| many asserts / `dumpGraph` / `assertNoCycles` / metadata-cache clears | DIVERGENT-OK | DEBUG/METADATA omissions. |

### `HepProgram` / `HepProgramBuilder` — **no bugs.** 16/18 FAITHFUL; only DIVERGENT-OK (`ImmutableList`→`ImmutableArray`, `Class<R>`→generic `<TRule>`, `checkArgument`→`InvalidOperationException`).

### `HepInstruction` (+ nested) / `HepState` — findings:

| Member | Verdict | Note |
|---|---|---|
| `SubProgram.prepare` | **RESOLVED (ported)** | `Prepare` now returns `Program.Prepare(px)` directly, as Calcite — the nested `SubProgram.State` and `ExecuteSubProgram` are kept but dead, exactly mirroring Calcite's structure. |
| `BeginGroup.EndGroup` field | DIVERGENT-OK | `new` keyword hides the nested type name; cosmetic. |

### `HepRelVertex` → `HepOpVertex` — findings:

| Member | Verdict | Note |
|---|---|---|
| `explain` | DIVERGENT-OK | Calcite's vertex is transparent (delegates explain to the wrapped op); Alembic prints the vertex wrapping the op. Internal-only; final plans are stripped. |
| `getDigest` (`"HepRelVertex(rel)"`) | DIVERGENT-OK | no override; the `AbstractOp` digest (with the `current` input term) stands in. |
| `deepHashCode` | DIVERGENT-OK | identity hash vs `getId()` — equivalent (one id per instance). |

### `HepRuleCall` — `transformTo` drops `verifyTypeEquivalence` (RELATIONAL row-type check) + hint propagation; the `equiv` map is unused by Calcite's HEP anyway → **DIVERGENT-OK**. `results`/`getResults`/ctor FAITHFUL.

### `RelOptRule` → `OpRule` / `RelOptRuleOperand` → `OpRuleOperand` — findings (flatten/solve-order/matches all FAITHFUL):

| Member | Verdict | Note |
|---|---|---|
| `RelOptRule.equals`/`hashCode` (by description+class+operand) | **RESOLVED (ported)** | `OpRule.GetHashCode` = `Description.GetHashCode`; `OpRule.Equals` = same type + same `Description` + equal root `Operand`. Drives the `AddRule` dedup below. |
| `OpRuleOperand.equals`/`hashCode` | **RESOLVED (ported)** | by `MatchedClass` + `Trait` + child operands (recursive), as Calcite — predicate and child policy are excluded from identity (matching them is the rule's job). |
| `ConverterRelOptRuleOperand` (same-`OpTraitDef` `matches` guard) | DIVERGENT-OK (minor) | `ConvertOperand` yields a plain `OpRuleOperand` without the n²-guard override. |
| `UNORDERED` child-count assert | DIVERGENT-OK | dev-only invariant dropped. |

### `RelOptRuleCall` → `OpRuleCall` — findings:

| Member | Verdict | Note |
|---|---|---|
| `operand0` / `getOperand0()` | **RESOLVED (moved)** | Calcite's `operand0` lives on the base `RelOptRuleCall`; ours had been declared on `VolcanoRuleCall`. Now moved to the `OpRuleCall` base, and `OpRule` is *derived* from it (`OpRule = operand0.Rule`) as Calcite does — so the base ctor takes the seed operand, not the rule. All subclasses pass an operand0: `VolcanoRuleMatch`/`DeferringRuleCall` carry the real seed operand; `HepRuleCall` and `VolcanoRuleMatch`'s bound-op ctor pass `rule.Operand` (the root). Faithful. |
| `transformTo(rel, equiv)` | **RESOLVED (ported)** | added on the base `OpRuleCall` as `TransformTo(equivalent, IReadOnlyDictionary<IOp,IOp>)`, with the no-arg form delegating to it; `VolcanoRuleCall` registers the map entries, `HepRuleCall` ignores them (single best plan). See the `VolcanoRuleCall` row. |
| `id`/`nextId` | **RESOLVED (ported)** | `OpRuleCall.Id`, assigned from a static counter in creation order. |
| `getChildRels`/`getParents`/`builder`/`getMetadataQuery`/`isRuleExcluded` | MISSING | RELATIONAL/METADATA/BUILDER. |

### `ConverterRule` / `Converter` / `ConverterImpl` / `TraitMatchingRule` — findings:

| Member | Verdict | Note |
|---|---|---|
| `ConverterRule.isGuaranteed()` default | **RESOLVED (fixed)** | default flipped to **`false`** (Calcite's safe default — non-guaranteed, applied bottom-up via `TraitMatchingRule`). The always-convert test converters (`SourceConverter`/`FilterConverter`/`ParameterConverter`/the expression+image converters/`SortEnforcer`) now override `IsGuaranteed => true`, as Calcite's guaranteed converters do. Only consulted by HEP's `AddConverters(guaranteed)` instruction, so the flip's observable effect is scoped there. |
| `ConverterRule.onMatch` in-trait re-check | DIVERGENT-OK | operand already constrains to `Source`; re-check redundant. |
| `getTraitDef()` | **RESOLVED (ported)** | `ConverterRule.TraitDef => Source.TraitDef`. |
| `TraitMatchingRule` operand build | DIVERGENT-OK | re-applies the converter operand's predicate (Calcite doesn't); tighter but consistent. |

### `RelOptPlanner` → `IOpPlanner` + `AbstractOpPlanner` — findings:

| Member | Verdict | Note |
|---|---|---|
| `addRule` | **RESOLVED (ported)** | description-keyed dedup, the unique-description guard, and the bool add/no-add return are all present — see the `setRuleDescExclusionFilter` row immediately below. |
| `setRuleDescExclusionFilter`/`isRuleExcluded`, `getRuleByDescription` | **RESOLVED (ported)** | `AbstractOpPlanner` now keeps a `Dictionary<string,OpRule>` description map: `AddRule` returns `bool` and rejects a duplicate description (no-op if equal, throws if a different rule collides); `GetRuleByDescription`, `SetRuleDescExclusionFilter(Regex?)`, and `IsRuleExcluded(OpRule)` are present, with the exclusion check wired into `VolcanoRuleCall.OnMatch` and `HepPlanner.ApplyRule`. *(Re-verification nit: `IsRuleExcluded` uses unanchored `Regex.IsMatch`, whereas Calcite's `Matcher.matches()` is full-string-anchored; identical for Calcite's always-anchored usage, but a user-supplied unanchored pattern would behave as a substring match. Harmless today; anchor with `^…$` if it ever matters. Calcite's second, hint-driven `ruleCall.isRuleExcluded()` is intentionally not modelled — no hints subsystem.)* |
| `clear()` / `clearRelTraitDefs()` | **RESOLVED (ported)** | `Clear()` (above) + `ClearTraitDefs()` on `AbstractOpPlanner` (clears `_traitDefs` and resets the empty-trait-set cache). Implemented on the base because Alembic's trait-def registry lives there, rather than Calcite's base-no-op/Volcano-override split. |
| `fireRule` orchestration (checkCancel + exclusion + onMatch dispatch) | DIVERGENT-OK | base keeps only the listener split (`FireRuleAttempted`); `onMatch` dispatch is in Hep/Volcano. `checkCancel`/exclusion absent. |
| `emptyTraitSet` / `addRelTraitDef` / listeners / cost factory | DIVERGENT-OK / FAITHFUL | defaults-population + convention seeding folded into the base (consolidation), with a freeze guard (EXTRA, benign). |

### `RelOptCluster` → `OpCluster` / `RelOptListener` → `IPlannerListener` / `RelOptUtil` → `PlanUtil` — findings:

| Member | Verdict | Note |
|---|---|---|
| `RelEquivalenceEvent.equivalenceClass` + `isPhysical` | **RESOLVED (ported)** | `OpEquivalenceEvent.EquivalenceClass` (the set id, from `OpSubset.Add`) + `IsPhysical` (op carries a non-`None` convention); the cost-based planner populates both, the heuristic planner leaves the defaults. *Re-verification notes (DIVERGENT-OK, listener-observability only):* (a) the payload is the raw set id, not Calcite's `"equivalence class {id}"` string; (b) `IsPhysical` is computed, where Calcite hard-codes `false` — Alembic matches the interface's own intent better; (c) Alembic raises the event only on op-add (not also on bare subset creation as Calcite does), so a listener sees fewer equivalence events. None affect planning. |
| `RelOptUtil.toString` | DIVERGENT-OK | faithful shape (news up `OpWriterImpl`, calls `Explain`); drops the `SqlExplainLevel` param, adds `.TrimEnd()`. |
| `OpCluster` `getPlanner`/`traitSet`/`traitSetOf` | FAITHFUL | the only in-scope cluster members; the rest (type factory, rex builder, metadata query, hints) correctly RELATIONAL/METADATA. |

### `RelTrait` → `IOpTrait` / `RelTraitDef` → `OpTraitDef` (base) + `OpTraitDef<T>` — findings:

| Member | Verdict | Note |
|---|---|---|
| `RelTraitDef.canonize` interner | **RESOLVED (fixed)** | `OpTraitDef` now uses a `WeakInterner<IOpTrait>` (`src/Alembic/Util/WeakInterner.cs`) — canonical traits are held weakly (collectible once no `OpTraitSet` references them), with dead entries swept on insert; the analog of Calcite's `Interners.newWeakInterner`. Thread-safe via a single lock (no lock-free races — the class that caused the earlier flakiness). **TODO (follow-up):** `WeakInterner` is a hand-rolled first cut; it should be checked against Google Guava's `Interners.newWeakInterner` and reworked into an exact line-by-line port (Guava's is more sophisticated, with a better API), using .NET types where a Java/Guava feature has a direct equivalent. |
| `RelTrait.satisfies` | DIVERGENT-OK | Calcite leaves it abstract; Alembic gives a default `Equals` body (the reflexive base case). |

### `RelTraitSet` → `OpTraitSet` — findings:

| Member | Verdict | Note |
|---|---|---|
| `getTrait(RelTraitDef)` | **RESOLVED (fixed)** | typed `Get<T>(def)` now returns `TTrait?` — `null` when the dimension is absent, as Calcite's `getTrait`. (Constraint tightened to `class, IOpTrait`; the always-present `Convention` accessor uses `!`.) The non-generic `Get(OpTraitDef)` still returns the dimension default — a separate, deliberate convenience overload. |
| `replace(RelTrait)` (ignore-if-absent) | **RESOLVED (ported)** | `OpTraitSet.Replace(IOpTrait)` infers the dimension from the trait, substitutes if present, returns `this` if absent — Calcite's `replace(RelTrait)` exactly (distinct from `Plus`, which adds an absent dimension). |
| `Replace<T>(def, value)` | DIVERGENT-OK | doesn't canonize `value` (Calcite does); relies on callers passing canonical traits. |
| `simplify` / `allSimple` | **RESOLVED (ported)** | `OpTraitSet.Simplify()` (one-member composite → its member; many-member → dimension default) and `OpTraitSet.AllSimple()`. A non-generic base class `OpCompositeTrait` (`Count`/`TraitAt`) lets the set flatten composites without knowing the member type (mirroring Calcite's raw `RelCompositeTrait`). |
| `difference` | **RESOLVED (ported)** | `OpTraitSet.Difference(traitSet)` returns the argument's traits that differ position-for-position — needed by `OpSet.AddConverters` to decide which dimensions a converter must bridge. |
| `replaceIf`/`replaceIfs`/`plusAll`/`merge` | **RESOLVED (ported)** | `OpTraitSet.ReplaceIf<T>(def, Func<T?>)`, `ReplaceIfs<T>(def, Func<IReadOnlyList<T>?>)`, `PlusAll(IEnumerable<IOpTrait>)`, `Merge(OpTraitSet)` — supplier-driven conditional replace and bulk add, matching Calcite. |

### `Convention` / `ConventionTraitDef` / `OpCompositeTrait` — findings:

| Member | Verdict | Note |
|---|---|---|
| `ConventionTraitDef.convert`/`canConvert`/`registerConverterRule` + `ConversionData` graph | DIVERGENT-OK (architectural) | Calcite implements convention conversion as a per-planner `DirectedGraph` of conventions + shortest-path inside `ConventionTraitDef`; Alembic ports none of it and instead drives convention conversion through rules (`ExpandConversionRule` → `VolcanoPlanner.ChangeTraitsUsingConverters`). Different mechanism, same capability — worth noting as a deliberate relocation, not a silent gap. |
| `OpCompositeTrait.of` | **RESOLVED (fixed)** | `Of` now canonizes the single member (size 1) and, for many, canonizes each member then the whole composite (`def.Canonize(...)`), as Calcite — restoring the interning-identity invariant. |
| `Convention.Impl` placement (nested) → top-level `Convention` | **RESOLVED (decision: keep top-level)** | Calcite's concrete convention is `Convention.Impl`, a class nested in the `Convention` interface. Both are extension points for adapters: the interface is implemented directly (`EnumerableConvention`, `BindableConvention`, `InterpretableConvention`) and `Impl` is subclassed as a convenience base (`JdbcConvention extends Convention.Impl`). Alembic splits these into `IConvention` (the interface others implement) + a top-level `Convention` (the extensible concrete base others subclass). **Kept top-level by decision**: a top-level, openly-subclassable class is the idiomatic C# extension point; nesting it inside the interface (the Java form) would only hinder that. This is the chosen Alembic structure, not a deviation to reconcile later. |
| `Convention.Impl` `equals`/`hashCode` | DIVERGENT-OK (intentional) | Calcite's `Impl` uses reference identity (conventions are singletons); Alembic compares by name. Deliberate, documented. |

### `RelOptCost`/`OpCost`/`VolcanoCost` — **no bugs.** `getRows()`/`Value` dropped (relational). The scalar `OpCost` now mirrors `RelOptCostImpl` member-for-member: `getCpu()`/`getIo()` return `0`; `plus`/`minus`/`multiplyBy` are plain `new OpCost(...)` (no infinite short-circuit) and `divideBy` is plain division, matching Calcite exactly; comparison is on the private scalar. `VolcanoCost` keeps its own component-wise behavior — `multiplyBy`/`minus` infinite short-circuits, geometric-mean `divideBy`, per-component `isEqWithEpsilon` — faithful to Calcite's `VolcanoCost`. The named cost singletons (`INFINITY`/`HUGE`/`ZERO`/`TINY`) live on `VolcanoCost` (annotated), as in Calcite; `RelOptCostImpl`/`OpCost` have none and build costs inline in their factory.

### `RelNode`/`AbstractRelNode` → `IOp`/`AbstractOp` — findings (structural core FAITHFUL):

| Member | Verdict | Note |
|---|---|---|
| `onRegister` | **DEFERRED** | Calcite's `RelNode.onRegister` is an op method (register inputs + copy + recompute); ours is `VolcanoPlanner.OnRegister`, *fused* with convention coercion that Calcite doesn't do in `onRegister`. Coupled to the P2 convention-handling divergence — will be moved to `IOp.OnRegister` when that theme is addressed, not before. (Not justified — pending.) |
| `DigestWriter.Done` input rendering | DIVERGENT-OK (cosmetic) | Calcite renders inputs as `typeName#id`; Alembic renders `typeName` only (no id). Affects the *display* digest string for same-type siblings, but dedup uses `DeepEquals` (recurses actual ops), so correctness is unaffected. |
| `deepHashCode` seed | DIVERGENT-OK | Alembic folds type + term names too (Calcite folds values only) — stronger, consistent with its `DeepEquals`. |
| `computeSelfCost` default | DIVERGENT-OK | tiny constant vs Calcite's row-count-based default (METADATA). |

### `SingleRel`/`BiRel`/`RelWriter`/`RelWriterImpl`/`RelDigest` — findings:

| Member | Verdict | Note |
|---|---|---|
| `RelWriter.itemIf` | **RESOLVED (ported)** | `IOpWriter.ItemIf(name, value, condition)` default method, as Calcite's default. |
| `RelWriter.getDetailLevel` / `nest` / `expand` | DIVERGENT-OK (no analog) | **deliberately not ported**: `getDetailLevel` returns a SQL `SqlExplainLevel`, and `nest`/`expand` are SQL-EXPLAIN rendering flags — all inert for Alembic's medium-agnostic plan output. Porting them would mean inventing SQL concepts, not porting. (The one Bucket-D item where "port all" was overridden by judgment.) |
| `RelWriterImpl.explain_` | DIVERGENT-OK | `=value` vs Calcite's `=[value]`, type-name source differs — cosmetic plan-string format. SingleOp/BiOp `explainTerms` FAITHFUL. |

### Graph core (`DirectedGraph`/`DefaultDirectedGraph`/`DefaultEdge`/`Graphs`) — **no bugs.** `vertexSet` (ordered view vs live keySet), `removeAllVertices` (always majority-strategy vs Calcite's 0.35 threshold), `predecessorListOf` (copy vs live view) all DIVERGENT-OK with identical observable results. `FrozenGraph`/`makeImmutable` intentionally absent (unused by HEP).

### Graph iterators + `Pair` — findings (traversal order FAITHFUL throughout):

| Member | Verdict | Note |
|---|---|---|
| `TopologicalOrderIterator.findCycles` | **RESOLVED (ported)** | `FindCycles()` drains the iterator and returns the un-emitted vertices (those still carrying unsatisfied incoming edges — the ones on a cycle), as Calcite. |
| `Pair` `Comparable`/`Map.Entry`/`of(Map.Entry)` | DIVERGENT-OK | interfaces + entry-ctor intentionally dropped; list/map helpers (`zip`/`toMap`/…) UNUSED-HELPER.

---

### Audit summary — triage

Across **all ~45 classes**, the algebra/graph/cost/program layers are faithful; the divergences concentrate in the **un-audited Volcano core** and a few **cross-cutting semantic defaults**. Prioritized:

**P1 — behavior-affecting bugs to fix:**
1. ~~`VolcanoPlanner.PropagateCostImprovements` — naive recursion (cycle/stack-overflow risk, possibly-wrong propagation).~~ **RESOLVED**: ported Calcite's `propagateRels` map + cost-ordered `PriorityQueue` worklist over `subset.GetParents()`.
2. ~~`ConverterRule.IsGuaranteed` default **inverted** (`true` vs Calcite `false`).~~ **RESOLVED**: default now `false`; always-convert converters override to `true`. (Only HEP's `AddConverters` reads it.)
3. ~~`OpTraitSet.Get<T>` throws on an absent dimension instead of returning null.~~ **RESOLVED**: `Get<T>` returns `null` when absent (Calcite's `getTrait`).
4. ~~`VolcanoPlanner.Merge` — missing `isSmaller` direction + root re-point/`ensureRootConverters`.~~ **RESOLVED**: swap (`GetChildSets`+`IsSmaller`) + root re-point ported.
   - 4b. ~~`VolcanoPlanner.FixUpInputs`/`Rename` — no parent back-link maintenance.~~ **RESOLVED**: `Rename` drops the old op's back-links and links the rebuilt op's; collision branch reassigns `best`.
5. ~~`VolcanoPlanner.EnsureRegistered` — skips the equivalence-driven set merge when already registered.~~ **RESOLVED**: merges the sets, returns the `Canonize`d subset.
6. ~~`OpTraitDef.Canonize` — strong-ref interner (leak) vs Calcite's weak interner.~~ **RESOLVED**: `WeakInterner<IOpTrait>` (weak values, swept, single-lock).
7. ~~`OpCompositeTrait.Of` — no canonization (breaks interning identity).~~ **RESOLVED**: canonizes members + the whole composite.
8. ~~HEP matcher (`MatchOperand` SOME / `MatchUnordered`) stricter than Calcite — rejects valid matches *(verify)*.~~ **RESOLVED**: SOME now `>=` (binds first n); UNORDERED reworked to Calcite's single-operand/any-child `matchAnyChildren` (model aligned; tests reworked). See the `HepPlanner` findings rows.

**P2 — themes / structural gaps:**
- ~~**Op pruning** absent (drives the hollow `VolcanoRuleCall.OnMatch` staleness guard, `skipMatch`).~~ — **RESOLVED**: `prunedOps`/`Prune`/`CheckPruned` machinery + the `OnMatch` staleness guard + `SkipMatch` are ported (see the `VolcanoPlanner`/`VolcanoRuleCall`/`RuleQueue` rows).
- ~~**`transformTo(equiv)`** map dropped port-wide~~ — **RESOLVED**: the equiv-map overload is ported (see `VolcanoRuleCall`/`OpRuleCall` rows).
- ~~**Canonization**: `EquivRoot` returns the set, never the leader subset; root not re-canonized after merge.~~ — **RESOLVED**: `RegisterSubset` returns `Canonize(subset)`; the no-arg `Canonize()` re-points `_root` after each match (see the `canonize(subset)` / `IterativeRuleDriver` rows).
- ~~**Per-subset converter seeding** (`RelSet.getOrCreateSubset → addConverters`) absent~~ — **RESOLVED**: `OpSet.AddConverters` ported (see the `RelSet` rows).
- ~~**OpRule registry**: `AddRule` no dedup/unique-description; no `OpRule.equals`/exclusion-filter.~~ — **RESOLVED**: description-keyed registry with dedup, `OpRule`/`OpRuleOperand` value equality, and the exclusion filter (see the `OpRuleOperand`/`AbstractRelOptPlanner` rows).
- `GetCost` none-convention infinite/nudge; HEP `applyRule` forced-conversion guard; HEP large-plan `contractVertices` merge.

**P3 — minor/cosmetic:** ~~`RelWriter.itemIf`/`getDetailLevel`, `findCycles`, `obliterateRelNode`, `allNotNull`, listener event payloads (`equivalenceClass`/`isPhysical`), `VolcanoRuleMatch`/cost helper gaps~~ — **RESOLVED** (Bucket D + Bucket C): all ported except `getDetailLevel`/`nest`/`expand` (SQL-EXPLAIN-only, no analog). Remaining: digest input-string id (cosmetic, DIVERGENT-OK as recorded).

**Deliberate, verified-OK:** convention conversion is rule-driven (not `ConventionTraitDef`'s graph); FIFO queue vs importance ranking; metadata-disabled lower-bound pruning; row-count-free cost; `Convention` by-name equality.

---

## 3. Follow-ups (tracked, not yet done)

- **Re-evaluate the op mutability model.** Ops were immutable early in the port; this was abandoned (2026-06-23) to match Calcite's `RelNode`, which mutates in place during planning (`replaceInput` + recomputable digest) — `fixUpInputs`/`rename`/the converter recheck are now strict mutate-in-place ports, with `replaceInput` throwing on `AbstractOp` and mutating on `SingleOp`/`BiOp` (and any multi-input op such as `PhysicalFma`), as Calcite does. Once the port is complete, decide whether to restore immutability or keep Calcite's mutable model.
- **`WeakInterner` → Guava parity.** `src/Alembic/Util/WeakInterner.cs` is a hand-rolled first cut; rework it into an exact line-by-line port of Google Guava's `Interners.newWeakInterner` (more sophisticated, better API), using .NET types where a Java/Guava feature has a direct equivalent.
- **`OpTraitDef.TraitClass` is concrete; Calcite's `getTraitClass()` is `abstract`.** Ours returns `typeof(TTrait)`; Calcite leaves it abstract. Minor divergence — acceptable.
- **`OpTraitDef.TraitClass` should be named `TraitType`** (acceptable-divergence rename; `Type`/`Class` C# idiom).
- **`OpTraitDef<TTrait>` generic-typing boundary.** Calcite types `canonize`/`getDefault` (etc.) as `T` throughout; ours types `Canonize`/`CanConvert`/`Convert` as `IOpTrait` (non-generic, on the base `OpTraitDef`) and only `Default` as `TTrait` (covariant override on `OpTraitDef<T>`). Consider tightening `Canonize` to `TTrait Canonize(TTrait)` to match Calcite's `final T canonize(T)`.
- **`OpTraitDef.Canonize` does not check for a composite trait.** Calcite's `canonize` special-cases `RelCompositeTrait` (`if (!(trait instanceof RelCompositeTrait)) { assert getTraitClass().isInstance(trait); }`) — guarding a type assertion. Ours interns directly without the check.
- ~~**`[Provenance]` attribute — project-wide.**~~ **DONE.** `ProvenanceAttribute(className, member?)` is applied across the whole project — every Calcite-derived type/member/field carries the FQN + real Calcite signature; Alembic-originals are listed in §2. Prose Calcite mentions were scrubbed in the same pass (source is now Calcite-free except machine-readable attribute values).
