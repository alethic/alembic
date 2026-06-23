# Calcite → Alembic port audit

Alembic ports the core of Apache Calcite's planner — the cost-based (Volcano/Cascades) and heuristic
(HEP) optimizers, made medium-agnostic. The **per-member Calcite → Alembic mapping lives in code**, as
`[Provenance(className, member?)]` attributes on every ported type and member; that is the authoritative,
machine-readable ledger. This file keeps only what *can't* live in an attribute: what was deliberately
**not** ported (§1), the **fidelity re-audit** of divergences from Calcite (§2), and tracked **follow-ups**
(§3).

**Naming.** "Op" is Alembic's counterpart to Calcite's "Rel" — the medium-agnostic operation the engine
plans over. So `RelNode` → `IOp`, `PhysicalNode` → `IPhysicalOp`, `SingleRel` → `SingleOp`,
`RelOptCluster` → `OpCluster`, `RelTraitSet` → `OpTraitSet`, etc.; the optimizer "Opt" and the tree-node
"Node" are both dropped. Members keep their Calcite-faithful names (`getCluster()` → the `Cluster`
property, and so on).

---

## 1. Deliberately not ported

Whole subsystems Alembic does not implement, by design or as future work. **Scope rule:** the *only*
legitimately out-of-scope category is genuinely **relational** (row types, Rex, SQL, materialized views,
hints, correlation, the operator library). Everything else (metadata, debug, cancellation, timeouts) is
deferred/unported work, not "out of scope", and is fair game for the audit.

| Calcite | Status |
|---|---|
| `RelMetadataQuery` / metadata providers / `VolcanoRelMetadataProvider` / `HepRelMetadataProvider` | **future** (not relational) — costs come only from `ComputeSelfCost`; the top-down driver's lower-bound pruning is disabled (`GetLowerBound` returns zero) pending `getLowerBoundCost` |
| `RelBuilder` and the relational operator library | **out of scope** — relational |
| `RexNode`, `RelDataType`/row types, row counts, hints, correlation, schemas | **out of scope** — relational |
| `RelShuttle`/`RexShuttle` | **out of scope** — shuttles (relational) |
| `RelVisitor` | **ported** — `OpVisitor` (`Go`/`Visit`/`ReplaceRoot`, descending via `IOp.ChildrenAccept`) |
| `RelNode.getId()` global id sequence | **ported** — `IOp.Id` / `AbstractOp.Id`, atomic creation-order counter (`NEXT_ID.getAndIncrement()`) |
| `Dumpers`, `assertNoCycles`, `assertGraphConsistent`, `dumpGraph`, `VolcanoTimeoutException` | **debug/ops, not ported** (not relational — see audit D13/D23) |
| materialized views (`RelOptMaterialization`) | **out of scope** — relational |
| `Convention.getRelFactories` | **out of scope** — relational operator factories |
| `RelRule.Config` / per-rule `Config` (immutables) | **n/a** — Alembic ships no rule library; rules take ctor args |

---

## 2. Fidelity re-audit (2026-06-23, fresh cold-fork agents)

A from-scratch pass: one isolated cold agent per class, fed only the .NET file path + the Calcite file
path + the `[Provenance]` mapping, asked whether each ported member is the *same implementation* as its
Calcite original. (The earlier first-pass audit — which liberally stamped non-relational differences as
"DIVERGENT-OK" — has been discarded; those calls were not reliable.)

**Scope:** only genuinely **relational** concerns are auto-out-of-scope. Dropped Java `assert`s,
`checkCancel`, `ruleCallStack`, listener-event counts, metadata-query plumbing, timeouts, ordering, and
structural rewrites are **genuine divergences flagged for a decision**, not pre-approved.

Legend: 🔴 behaviour-affecting / defect to fix · ⚠️ divergence needing a decision (behaviour-preserving,
unverified, or deferred-feature) · ✅ resolved (fixed to match Calcite). Refer to findings by `D<n>`.

| # | Alembic member | Calcite (Java) member | Finding |
|---|---|---|---|
| ✅ D1 | `OpSet.MergeWith` | `RelSet.mergeWith` | ~~Substantially diverges: no subset-merge loop (so no `changedRels` best-cost propagation, no `passThroughCache` merge), no enforcer-parent prune, no post-`rename` `if (equivalentSet != null) return` guard, no 2nd `propagateCostImprovements` over `getParentRels()`, and `fireRules` on ops only — not subsets.~~ **RESOLVED** — rewritten as a line-by-line port: (1) subset-merge loop recreating each subset with its required/delivered flags, folding `passThroughCache` (new `OpSubset.AdoptPassThroughCache`) and collecting `changedRels`; (2) rels loop with the enforcer-parent prune + `planner.ReRegister` (new — Calcite's `reregister`: digest-dedup → `CheckPruned`, else `AddOpToSet` unless pruned); (3) propagate `changedRels`; (4) rename `previousParents`; (5) post-`rename` `if (EquivalentSet != null) return` guard; (6) 2nd `PropagateCostImprovements` over `Parents`; (7) `FireRules` over ops **and** subsets. The explicit `Parents.AddRange` was dropped — `rename`→`FixUpInputs` migrates parents, as Calcite relies on. `OnSetMerged` moved from `MergeWith` into `Merge` (Calcite calls it from `merge`, after the root re-point). |
| ✅ D2 | `VolcanoPlanner.SetTopDownOpt` | `VolcanoPlanner.setTopDownOpt` | ~~Omits Calcite's `if (value == current) return` no-op guard; always rebuilds the rule driver, discarding state.~~ **RESOLVED** — added the `if (_topDownOpt == value) return;` guard before rebuilding the driver. |
| ✅ D3 | `AbstractOp.DeepHashCode` / `DeepEquals` | `AbstractRelNode.deepHashCode` / `deepEquals` | ~~Folds type + term-names; Calcite folds **values only** (`31 + traitSet.hashCode()`, then `31*r + valueHash`). `DeepEquals` enforces term-name equality on op-valued items where Calcite compares only the values.~~ **RESOLVED** — `DeepHashCode` now seeds `31 + Traits.GetHashCode()` and folds term **values only** (no type, no names) with the `r*31 + h` accumulation; `DeepEquals` compares op-valued items by `DeepEquals` alone and compares the `(name, value)` entry only for non-op items. |
| ✅ D4 | `HepOpVertex.DeepHashCode` | `HepRelVertex.deepHashCode` | ~~Returns `_currentOp.GetHashCode()` (identity hash) instead of Calcite's `currentRel.getId()`.~~ **RESOLVED** — now returns `_currentOp.Id`. |
| ✅ D5 | `OpCompositeTrait<T>.Satisfies` | `RelCompositeTrait.satisfies` | ~~Adds a composite-vs-composite "every member of `other` satisfied by some member of `this`" branch Calcite lacks.~~ **RESOLVED** — dropped the extra branch; now `_traits.Any(t => t.Satisfies(other))`, matching Calcite's member-iteration. |
| ✅ D6 | `HepPlanner.ExecuteConverterRules` / `ExecuteCommonRelSubExprRules` (+ `ExecuteRuleClass`) | `HepPlanner` converter / common-subexpr / rule-class instruction execution | ~~Builds the rule set with `HashSet` vs Calcite's `LinkedHashSet` — non-deterministic rule application order.~~ **RESOLVED** — the three states' `RuleSet` is now an insertion-ordered `List<OpRule>` (the source `Rules` is ordered and duplicate-free), so application order is deterministic, matching `LinkedHashSet`. |
| ✅ D7 | `HepPlanner.ApplyRule` | `HepPlanner.applyRule` | ~~Adds an `IsRuleExcluded(rule)` guard Calcite's `applyRule` lacks.~~ **RESOLVED** (owner decision: match Calcite) — removed the guard; HEP no longer consults the exclusion filter (Calcite's HepPlanner has zero exclusion references — it is Volcano-only). The two HEP exclusion tests were retargeted to a Volcano spy-rule exercising `VolcanoRuleCall.OnMatch`'s exclusion gate. |
| ✅ D8 | `HepPlanner.FindBestPlan` | `HepPlanner.findBestExp` | ~~Adds an Alembic-original `EnsureSatisfies(plan, requestedRootTraits)` enforcement Calcite lacks (can throw where Calcite returns).~~ **RESOLVED** (owner decision: match Calcite) — removed `EnsureSatisfies`; `FindBestPlan` now returns the best-effort plan as `findBestExp` does. `_requestedRootTraits` is still consulted by `DoesConverterApply` (Calcite's `doesConverterApply`), so it is not orphaned. Two `Incomplete_lowering_throws` tests became `…returns_a_best_effort_partial_plan`. |
| ✅ D9 | `VolcanoPlanner.RegisterImpl` | `VolcanoPlanner.registerImpl` | ~~Equivalence branch (`equivExp == rel`), already-registered early-return, and `fireRules(subset)` differ.~~ **VERIFIED + RESOLVED** — ported the tail faithfully: the `equivExp == op` early-return (`GetSubsetNonNull`); `registerClass`/`OnNewClass` moved *before* `AddOpToSet`; the `putIfAbsent` "registered while registering children" early-return; and the conditional `FireRules(subset)` when a new subset appeared or `subset.TriggerRule`. `OnProduce` moved into `AddOpToSet` (so `ReRegister` fires it too), as Calcite's `addRelToSet`. |
| ✅ D10 | `OpSubset` ctor | `RelSubset` ctor / `computeBestCost` | ~~Omits `computeBestCost`'s init-scan (best/bestCost not seeded).~~ **VERIFIED + RESOLVED** — the ctor now calls `ComputeBestCost`, scanning `GetRels()` and seeding `Best`/`BestCost` from the cheapest already-present member (matters when a subset is created over a set that already holds satisfying ops, e.g. the merge subset loop). |
| ✅ D11 | `Convention.Satisfies` / `Equals` / `GetHashCode` | `Convention.Impl.satisfies` (no `equals`/`hashCode` in Calcite) | ~~By-name equality vs Calcite's reference identity.~~ **RESOLVED** — `Satisfies` is now `ReferenceEquals(this, trait)`, and the by-name `Equals`/`GetHashCode` overrides were removed so conventions compare by object identity (Calcite's `Impl` overrides neither). All conventions are singletons, so behaviour is unchanged. |
| ⚠️ D12 | `VolcanoPlanner.GetLowerBound` / `TopDownRuleDriver.CheckLowerBound` | `VolcanoPlanner.getLowerBoundCost` / `checkLowerBound` | Lower-bound pruning disabled (`GetLowerBound` returns 0), so `CheckLowerBound`/`OptimizeInputs` never prune; `EnsureGroupExplored`/`clearCache` metadata-cache invalidation dropped. Deferred metadata subsystem (not relational). |
| ⚠️ D13 | `IterativeRuleDriver.Drive` / `TopDownRuleDriver.Drive` | same (`VolcanoTimeoutException` handling) | Omit Calcite's timeout try/catch — no timeout mechanism ported. |
| ✅ D14 | `VolcanoRuleCall.TransformTo` / `HepPlanner.ApplyRule` / `AbstractOpPlanner.FireRuleProductionSucceeded` | `VolcanoRuleCall.transformTo` / `HepPlanner.applyRule` / `AbstractRelOptPlanner.notifyTransformation` | ~~Fire the production event once (`false`) vs Calcite's `true`-then-`false` pair; drops the `before` parameter.~~ **RESOLVED** — `FireRuleProductionSucceeded` takes `before`; both `VolcanoRuleCall.TransformTo` and `HepPlanner.ApplyRule` now fire `before:true` (pre-registration) and `before:false` (post), matching Calcite's two `notifyTransformation` calls. |
| ⚠️ D15 | `VolcanoRuleCall.OnMatch` / `AbstractOpPlanner.FireRuleAttempted` | `VolcanoRuleCall.onMatch` / `AbstractRelOptPlanner.fireRule` | Drop cooperative cancellation (`checkCancel`) and the rule-call stack; `OnMatch` adds a `Rule.Matches` gate Calcite only asserts. |
| ⚠️ D16 | `AbstractOpPlanner.AddTraitDef` / `EmptyTraitSet` / `TraitDefs` / `ClearTraitDefs` | `AbstractRelOptPlanner.addRelTraitDef` / `emptyTraitSet` / … (base no-ops) | Trait-def machinery pulled up from `VolcanoPlanner` into the base (Calcite base versions are no-ops) — inverts the base contract. Confirm Hep/Volcano rely on the base. |
| ✅ D17 | `OpCost.ToString` / `VolcanoCost.ToString` | `RelOptCostImpl.toString` / `VolcanoCost.toString` | ~~Drop Calcite's `{inf}`/`{huge}`/`{tiny}`/`{0}` tokens.~~ **RESOLVED** — added the shared `IOpCost.ToString(double)` helper (Calcite's `RelOptCost.toString`); `OpCost.ToString` routes through it, and `VolcanoCost.ToString` returns the token for each named singleton, else `{cpu cpu, io io}` (no `rows` — relational). |
| ✅ D18 | `OpWriterImpl.Explain` | `RelWriterImpl.explain_` | ~~`name=value`, no id-prefix, prints `Traits` inline.~~ **RESOLVED** — now `id:TypeName(name=[value], …)`: id-prefix (Calcite's single-arg ctor defaults `withIdPrefix=true`), `name=[value]`, simple class name (`getRelTypeName`), and no inline trait set (Calcite's `explain_` prints none). |
| ✅ D19 | `AbstractOp.DigestWriter.Item` | `AbstractRelNode.RelDigestWriter.item` | ~~Array values not string-normalised.~~ **RESOLVED** — an array value is stringified per-instance (`type@identityHash`, matching Calcite's `"" + value`). |
| ⚠️ D20 | `HepPlanner.SetRoot` / `AddOpToGraph` / `MatchOperand` | `HepPlanner.setRoot` / `addRelToGraph` / `matchOperands` | Drop the `initRelToVertexCache` large-plan fast-skip; `AddOpToGraph` has no explicit pre-lookup `RecomputeDigest` (digest-staleness unverified); `MatchOperand` drops the `nodeChildren` map and its UNORDERED binding-rollback differs. |
| ✅ D21 | `VolcanoPlanner.EquivRoot` | `VolcanoPlanner.equivRoot` | ~~Plain `while` loop vs Calcite's tortoise/hare cycle-detecting walk.~~ **RESOLVED** — ported the tortoise/hare walk with `Forward1`/`Forward2` helpers; throws on a cycle in the equivalence tree. |
| ⚠️ D22 | `HepRuleCall.TransformTo` | `HepRuleCall.transformTo` | Drops `rel0.getCluster().invalidateMetadataQuery()` (metadata-cache invalidation); ctor omits the `nodeChildren` map parameter. |
| ⚠️ D23 | (many) | (many) `assert` / `checkArgument` | Dropped Java invariant checks across ConventionTraitDef, OpSubset, OpSet.AddInternal, OpRuleOperand validation, AbstractConverter `allSimple`, OpCompositeTrait ordering, HepOpVertex double-wrap + copy, BiOp/SingleOp `replaceInput`, the drivers, the Hep group guards, the graph utils. Decide: port as guards, or accept the drop. |
| ⚠️ D24 | `IPlannerListener` events / `OpEquivalenceEvent` / `AbstractOpPlanner.AddListener` | `RelOptListener` events / `RelEquivalenceEvent` / `addListener` | Event op fields widened to nullable (Calcite non-null); `OpEquivalenceEvent` adds optional `equivalenceClass=null` / `isPhysical=false` defaults Calcite requires; `AddListener` uses a plain list vs `MulticastRelOptListener`. |
| ✅ D25 | `RuleQueue.Clear` / `IterativeRuleQueue.Clear` / `TopDownRuleQueue.Clear` | `RuleQueue.clear()` / `TopDownRuleQueue.clear()` | ~~Return `void`; Calcite returns `boolean`.~~ **RESOLVED** — the queue `Clear()`s now return whether they held matches (was-non-empty). (`IRuleDriver.Clear` was a false alarm — Calcite's `RuleDriver.clear()` is `void`; left as-is.) |
| ✅ D26 | `AbstractOpPlanner.IsRuleExcluded` | `AbstractRelOptPlanner.isRuleExcluded` | ~~Unanchored `Regex.IsMatch` vs Calcite's full-string-anchored `matcher().matches()`.~~ **RESOLVED** — now requires the match to span the entire description (`Success && Index == 0 && Length == description.Length`), replicating `Matcher.matches()`. |
| ✅ D27 | `ConverterRule.OnMatch` / `OpRule.ConvertOperand` / `OpRule.Any`·`Leaf` | `ConverterRule.onMatch` / `RelOptRule.convertOperand` / `operand(...)` | ~~`OnMatch` drops the `contains(inTrait)` re-check; `ConvertOperand` drops the converter-on-converter guard; loose `Any`/`Leaf` provenance.~~ **RESOLVED** — `OnMatch` re-checks `op.Traits.Contains(Source)`; `OpRuleOperand.Matches` carries the n²-guard (a converter operand rejects an `IConverter` whose `TraitDef` matches the owning `ConverterRule`'s), set via `ConvertOperand`'s `IsConverterOperand`; `Any`/`Leaf` now cite the operand factory rather than `any()`/`none()`. |
| ⚠️ D28 | `VolcanoRuleCall.TransformTo` | `VolcanoRuleCall.transformTo` | Omits `SubstitutionRule.autoPruneOld` → `prune` (substitution rules unported). |
| ✅ D29 | `PlanUtil.ToString` | `RelOptUtil.toString` | ~~Adds `.TrimEnd()` Calcite doesn't.~~ **RESOLVED** — removed `.TrimEnd()`; returns the raw builder string as Calcite does. |
| ⚠️ D30 | `ExpandConversionRule` / `AbstractConverter` / `Convention` ctors | `AbstractConverter.ExpandConversionRule` (`Config`/`INSTANCE`) / `AbstractConverter` ctor / `Convention.Impl` ctor | ExpandConversionRule replaces the `Config`/`INSTANCE` singleton with a parameterless ctor; `Convention(string)` is an Alembic-original convenience ctor; AbstractConverter ctor reshapes params. |
| ✅ D31 | `DefaultDirectedGraph` `_edges` / `Graphs.GetPaths` | `DefaultDirectedGraph` (`LinkedHashSet` edges) / `Graphs.FrozenGraph.getPaths` | ~~`_edges` is a `HashSet`; `GetPaths` uses a non-stable sort.~~ **RESOLVED** — `_edges` is now an insertion-ordered `LinkedHashSet` (new `Util.LinkedHashSet`, the JDK analog), and `GetPaths` uses the stable `OrderBy` (Calcite's `Collections.sort`), so equal-length conversion paths keep discovery order — deterministic `ConventionTraitDef.convert` selection. |
| ✅ D32 | `DefaultDirectedGraph.RemoveAllVertices` / `Pair.Equals` (etc.) | `removeAllVertices` / `Pair.equals` (etc.) | ~~`RemoveAllVertices` collapses the minority/majority dual strategy.~~ **RESOLVED** — ported the 0.35-threshold dual strategy (`RemoveMinorityVertices`/`RemoveMajorityVertices`). The remaining bits are genuinely moot: `PredecessorListOf`/`FindCycles` return fresh lists as Calcite's `Graphs.predecessorListOf` does, and `Pair.Equals`'s `Pair`-only narrowing has no `Map.Entry` to widen to. |

| ⚠️ D33 | `IOp.ComputeSelfCost` / `RecomputeDigest`; `IOpTrait.Satisfies` / `Register` | `RelNode.computeSelfCost` / `recomputeDigest`; `RelTrait.satisfies` / `register` | Alembic's *interfaces* supply default method bodies where Calcite leaves the interface member **abstract** (the body lives on `AbstractRelNode`/`AbstractRelTrait`). Most defaults are behaviour-equal, but `ComputeSelfCost` returns `MakeTinyCost()` whereas Calcite's `AbstractRelNode` impl returns `makeCost(rowCount, rowCount, 0)` (row-proportional). The row-count value is relational/unportable, but baking *any* cost default into the interface — instead of forcing each op to supply one — is the in-scope divergence. |
| ⚠️ D34 | `IOp.Convention` | `RelNode.getConvention()` | Default getter returns non-nullable `IConvention` (`Traits.Convention`); Calcite's `getConvention()` is `@Nullable` (an op may have no convention) and abstract on the interface. Drops the nullable contract (Alembic leans on `Convention.None` instead). |
| ✅ D35 | `IOpTrait` | `RelTrait.isDefault()` | ~~Missing.~~ **RESOLVED** — added the default `IsDefault => ReferenceEquals(this, TraitDef.Default)`. |
| ✅ D36 | `IOpWriter` | `RelWriter.nest()` / `expand()` | ~~Missing.~~ **RESOLVED** — added `Nest`/`Expand` defaults (both `false`, as Calcite). (`getDetailLevel()` → `SqlExplainLevel` stays out of scope.) |
| ✅ D37 | `IOp.Children` / `Cluster` / `Traits` provenance | `RelOptNode.getInputs()` / `getCluster()` / `getTraitSet()` | ~~`[Provenance]` cites `RelNode`.~~ **RESOLVED** — re-pointed to `org.apache.calcite.plan.RelOptNode` (verified: all three are declared there). |
| ✅ D38 | `OpTraitSet.Replace<T>(def, IReadOnlyList<T>)` | `RelTraitSet.replace(RelTraitDef, List)` | ~~When the dimension is **absent**, Alembic `_traits.Add(trait)` — *adds* it; Calcite delegates to `replace(RelTrait)`, which returns `this` unchanged.~~ **RESOLVED** — now delegates to `Replace(OpCompositeTrait<T>.Of(def, values))`, exactly as Calcite, so an absent dimension is ignored. (Four `CompositeTraitTests` that relied on add-if-absent now seed the dimension's default first, as a real planner trait set carries it.) |
| ✅ D39 | `OpTraitSet.Get(int)` | `RelTraitSet.getTrait(int)` | ~~Provenanced to `getTrait(int)` but returns `_traits[index]` raw — that is Calcite's *`get(int)`*; the composite guard is dropped.~~ **RESOLVED** — `Get(int)` now throws `InvalidOperationException` when the slot holds an `OpCompositeTrait` (directing callers to `GetList`), faithfully implementing `getTrait(int)`. |
| ✅ D40 | `OpTraitSet.Plus` / `Replace` / `ReplaceAt` / `Equals` | `RelTraitSet.plus` / `replace` / `replace(int,…)` / `equals` | ~~Drop Calcite's identity fast-paths and asserts.~~ **RESOLVED** — `Plus` now `contains`-fast-paths then delegates to `ReplaceAt` when the dimension exists; `Replace` adds the `ContainsShallow` (`==`) fast-path; `ReplaceAt` carries the trait-def `Debug.Assert`; `Equals` adds the cached-hash short-circuit and compares by reference (traits are interned). (`Comprises` was a false alarm — Calcite's `Arrays.equals` is value equality, which Alembic already matched.) |
| ✅ D41 | `OpTraitSet.ToString` | `RelTraitSet.toString` / `computeString` | ~~Joins with `", "` wrapped in `[...]`; Calcite joins with `.` and caches.~~ **RESOLVED** — `ToString` caches in `_string` and delegates to a `ComputeString` that joins with `.` (no brackets), matching Calcite. (The `{null}` single-null case is unreachable — `_traits` holds no nulls.) |
| ✅ D42 | `OpTraitSet.CreateEmpty` | `RelTraitSet.createEmpty()` | ~~Interns the empty set via `cache.GetOrAdd`.~~ **RESOLVED** — returns a bare `new OpTraitSet(new Cache(), Empty)`, not cached, as Calcite. |
| ✅ D43 | `OpTraitSet.EqualsSansConvention` | `RelTraitSet.equalsSansConvention` | ~~Structural rewrite via `Replace(...).Equals(...)`.~~ **RESOLVED** — ported Calcite's element walk: identity short-circuit, size check, then per-dimension reference compare skipping `ConventionTraitDef`. |
| ✅ D44 | `OpTraitSet.Canonize` | `RelTraitSet.canonize(T)` | ~~Drops the `instanceof RelCompositeTrait → return as-is` short-circuit.~~ **RESOLVED** — returns a composite as-is (already canonized by `OpCompositeTrait.Of`); otherwise delegates to the def's interner. |
| ✅ D45 | `OpTraitSet` | `isEnabled` / `isDefault` / `isDefaultSansConvention` / `getDefault` / `getDefaultSansConvention` / `containsIfApplicable` | ~~Six non-relational accessors missing.~~ **RESOLVED** — all six ported faithfully (`IsEnabled<T>`, `IsDefault`, `IsDefaultSansConvention`, `GetDefault`, `GetDefaultSansConvention`, `ContainsIfApplicable`). (`apply(Mappings)`, `getCollation(s)`, `getDistribution(s)` remain relational → out of scope.) |

_Audit coverage:_ all substantive Calcite-derived classes have now been through a cold-fork pass. Remaining
gaps, if any, are minor enums (`DeriveMode`, `HepMatchOrder`, `CannotPlanException`) and the non-Calcite
Guava/infra ports (`Interner`/`Interners`, `ProvenanceAttribute`).

_Confirmed faithful (no divergence):_ `VolcanoRuleMatch`, the `IterativeRuleQueue`/`RuleQueue` core,
`OpRule` core, `ConverterRule`-family core, `OpTraitDef` core, `OpVisitor`, `SingleOp`/`BiOp` core,
`OpCost` arithmetic, `ConventionTraitDef` graph logic, the Hep program/instruction/builder family core,
`IOpDigest`, `IOpMultipleTrait`, the `IOpWriter.Item`/`ItemIf`/`Input`/`Done` core, and the `OpTraitSet`
core (ctor, `Convention`, typed `Get<T>`/`GetList<T>`, `Count`, `Satisfies`, `Contains`, `Matches`,
`Difference`, `Merge`, `PlusAll`, `ReplaceIf`/`ReplaceIfs`, `AllSimple`, `Simplify`, `GetHashCode`,
`GetEnumerator`, `FindIndex`, the nested `Cache`).

---

## 3. Follow-ups (tracked, not yet done)

- **Re-evaluate the op mutability model.** Ops were immutable early in the port; this was abandoned (2026-06-23) to match Calcite's `RelNode`, which mutates in place during planning (`replaceInput` + recomputable digest) — `fixUpInputs`/`rename`/the converter recheck are now strict mutate-in-place ports, with `replaceInput` throwing on `AbstractOp` and mutating on `SingleOp`/`BiOp` (and any multi-input op such as `PhysicalFma`), as Calcite does. Once the port is complete, decide whether to restore immutability or keep Calcite's mutable model.
- **`OpTraitDef.TraitType` (was `TraitClass`) is concrete; Calcite's `getTraitClass()` is `abstract`.** Ours returns `typeof(TTrait)` from `OpTraitDef<TTrait>`; Calcite leaves it abstract. Minor divergence — **acceptable, kept**.
- **`OpTraitDef<TTrait>` generic-typing boundary — kept (not feasible to tighten).** Calcite types `canonize`/`getDefault` as `T`; ours types `Canonize`/`CanConvert`/`Convert` as `IOpTrait` on the non-generic base, because every call site invokes them through a non-generic `OpTraitDef` handle (e.g. `trait.TraitDef.Canonize(trait)`). The base/generic split is the C# stand-in for Calcite's raw `RelTraitDef`.
