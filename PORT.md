# Calcite → Alembic port map

This file maps every Apache Calcite class that Alembic ports to its Alembic counterpart, member by
member. It is a faithfulness ledger: it records what corresponds to what, and what was deliberately
**not** ported (and why).

**How to read it.** Each section is one Calcite class → one (or more) Alembic types. Tables read
*Calcite member → Alembic member → notes*. `—` means "no counterpart." Recurring reasons a member is
not ported:

- **relational** — depends on SQL/relational concepts (`RelDataType`/row type, `RexNode`, row counts,
  hints, correlation variables, schemas). Alembic is medium-agnostic.
- **metadata** — depends on the `RelMetadataQuery` statistics framework (not built; see PLAN.md §6).
- **visitor** — `RelShuttle`/`RexShuttle`/`RelVisitor` traversal (not built).
- **id** — Calcite's global per-`RelNode` integer id sequence (Alembic uses object identity instead).
- **builder** — `RelBuilder` plan construction (not built).
- **debug** — dump/assert tooling.

Naming conventions used throughout: Calcite's `Rel*` → Alembic `*Node`/`I*Node`; `getX()`/`setX()` →
a C# property `X`; `RelTrait*` → `*Trait*`.

---

## 1. Algebra (the node model)

### `RelNode` (interface) → `INode` (`src/Alembic/Algebra/INode.cs`)

| Calcite | Alembic | Notes |
|---|---|---|
| `getTraitSet()` | `Traits` | |
| `getInputs()` | `Children` | |
| `getInput(int)` | `Children[i]` | + `WithChild(i, child)` to replace one |
| `copy(traitSet, inputs)` | `Copy(traits, children)` | |
| `getConvention()` | `Convention` | default member |
| `isLeaf` (via inputs) | `IsLeaf` | default member |
| `computeSelfCost(planner, mq)` | `ComputeSelfCost(planner)` | no `mq` (metadata) |
| `getRelDigest()` | `GetDigest()` | returns `INodeDigest` |
| `deepEquals(obj)` | `DeepEquals(other)` | |
| `deepHashCode()` | `DeepHashCode()` | |
| `explainTerms(RelWriter)` | `ExplainTerms(INodeWriter)` | on `AbstractNode` (protected) |
| `recomputeDigest()` | `RecomputeDigest()` | default member; discards the cached digest (`GetDigest().Clear()`) |
| `getRowType()`, `estimateRowCount(mq)` | — | relational / metadata |
| `getId()` | — | id |
| `onRegister(planner)`, `register(planner)` | — | planners register internally; trait registration is `ITrait.Register` |
| `accept(RelShuttle/RexShuttle)`, `childrenAccept(RelVisitor)` | — | visitor |
| `getCluster()` | `Cluster` | every node carries its cluster, as in Calcite |

### `AbstractRelNode` (abstract) → `AbstractNode` (`src/Alembic/Algebra/AbstractNode.cs`)

| Calcite | Alembic | Notes |
|---|---|---|
| ctor `(cluster, traitSet)` | ctor `(cluster, traits, children)` | `SingleNode`/`BiNode` derive the cluster from their child(ren) |
| `getCluster()` | `Cluster` | |
| `getTraitSet()` / `getInputs()` | `Traits` / `Children` | |
| `copy(...)` (abstract) | `Copy(...)` (abstract) | |
| `computeSelfCost(...)` | `ComputeSelfCost(planner)` | default = `MakeTinyCost()` |
| `explainTerms(RelWriter)` | `ExplainTerms(INodeWriter)` | public; calls base, adds terms, returns the writer (chainable, as in Calcite) |
| `explain(RelWriter)` | `Explain(INodeWriter)` | `ExplainTerms(writer).Done(this)` (on `INode`, like Calcite's `RelNode.explain`) |
| `deepEquals` / `deepHashCode` | `DeepEquals` / `DeepHashCode` | derived from explain terms + traits. In Calcite a subclass with extra fields splits this into a protected `deepEquals0` / `deepHashCode0` (the shared base portion, computed from `explainTerms`) which the final `deepEquals` then ANDs with its own fields — the `0` suffix is Calcite's name for that reusable base helper. Alembic has no such subclasses, so it needs only the single method |
| `getRelDigest()` | `GetDigest()` | returns the kept `InnerNodeDigest` |
| `AbstractRelNode.InnerRelDigest` (nested) | `AbstractNode.InnerNodeDigest` (nested) | cached-hash digest; `Clear()` resets |
| `RelOptUtil.toString(rel)` | `PlanUtil.ToString(node)` | indented plan string — a util (the `RelOptUtil` analog), not a method on the node; drives `NodeWriterImpl` (§7) |
| `getId()` | — | a global node-id sequence Calcite uses only for display (`rel#42` in `toString`/`Dumpers`) and as memo keys; Alembic uses object identity throughout, so it is unneeded (see §9) |

### `SingleRel` → `SingleNode` (`src/Alembic/Algebra/SingleNode.cs`)

| Calcite | Alembic | Notes |
|---|---|---|
| `getInput()` | `Child` | |
| `getInputs()` | `Children` | (base) |
| `explainTerms(pw)` | `ExplainTerms(writer)` | writes `Input("input", Child)` |
| `estimateRowCount`, `deriveRowType`, `childrenAccept` | — | relational / visitor |

### `BiRel` → `BiNode` (`src/Alembic/Algebra/BiNode.cs`)

| Calcite | Alembic | Notes |
|---|---|---|
| `getLeft()` / `getRight()` | `Left` / `Right` | |
| `getInputs()` | `Children` | |
| `explainTerms(pw)` | `ExplainTerms(writer)` | writes both inputs |
| `childrenAccept` | — | visitor |

### `RelWriter` (interface) → `INodeWriter` (`src/Alembic/Algebra/INodeWriter.cs`)

| Calcite | Alembic | Notes |
|---|---|---|
| `item(term, value)` | `Item(name, value)` | |
| `input(term, input)` | `Input(name, input)` | default member = `Item(name, input)`, as in Calcite |
| `done(rel)` | `Done(node)` | finalizes/emits the node |
| `itemIf`, `getDetailLevel`, `nest`, `expand` | — | relational explain formatting |

### `RelWriterImpl` → `NodeWriterImpl` (`src/Alembic/Algebra/NodeWriterImpl.cs`)

| Calcite | Alembic | Notes |
|---|---|---|
| `item` (collects) / `done` (emits) | `Item` / `Done` | `Done` writes the node's line and recurses its inputs one indent deeper |
| `explain_` / `explainInputs` (indent via `Spacer`) | (in `Done`) | inputs are the node-valued items; recursion is over `Children` |
| `withIdPrefix`, `getDetailLevel`, cost/rowcount lines | — | relational / id / detail-level formatting |

### `RelDigest` (interface) → `INodeDigest` (`src/Alembic/Algebra/INodeDigest.cs`)

| Calcite | Alembic | Notes |
|---|---|---|
| `getRel()` | `Node` | |
| `clear()` (via recompute) | `Clear()` | resets cached hash |
| `equals`/`hashCode` (deep) | `Equals`/`GetHashCode` | delegate to `DeepEquals`/`DeepHashCode` |

### `RelImplementor` → `INodeImplementor` (`src/Alembic/Plan/INodeImplementor.cs`)

Structural analog only — Calcite's is Enumerable/Java-codegen specific; Alembic's is a minimal hook.

---

## 2. Traits & conventions

### `RelTrait` (interface) → `ITrait` (`src/Alembic/Plan/ITrait.cs`)

| Calcite | Alembic | Notes |
|---|---|---|
| `getTraitDef()` | `TraitDef` | |
| `satisfies(trait)` | `Satisfies(other)` | |
| `register(planner)` | `Register(planner)` | |
| `equals`/`hashCode`/`toString` | (value semantics required) | |
| `apply(mapping)`, `isDefault()` | — | relational (mapping) |

### `RelTraitDef<T>` → `ITraitDef` + `TraitDef<T>` (`src/Alembic/Plan/ITraitDef.cs`, `TraitDef.cs`)

| Calcite | Alembic | Notes |
|---|---|---|
| `getSimpleName()` | `Name` | |
| `getDefault()` | `Default` | |
| `canConvert(planner, from, to)` | `CanConvert(planner, from, to)` | virtual on `TraitDef<T>` |
| `convert(planner, rel, toTrait, allowInfinite)` | `Convert(planner, node, toTrait, allowInfiniteCostConverters)` | virtual on `TraitDef<T>` |
| `getTraitClass()` | `TraitClass` | returns `typeof(TTrait)` |
| `canonize(t)` | `Canonize(t)` | per-trait interner on the def |
| `registerConverterRule` / `deregisterConverterRule` | `RegisterConverterRule` / `DeregisterConverterRule` | default no-ops, as in Calcite |
| `multiple()` | `Multiple` | default `false` |

### `RelTraitSet` → `TraitSet` (`src/Alembic/Plan/TraitSet.cs`)

| Calcite | Alembic | Notes |
|---|---|---|
| `createEmpty()` | `CreateEmpty()` | |
| `getTrait(traitDef)` | `Get<T>(def)` / `Get(def)` | |
| `getTraits(index)` (multi) | `GetList<T>(def)` | |
| `replace(index/def, trait)` | `Plus(trait)` / `Replace<T>(def, value)` | `Plus` adds-or-replaces |
| `replace(List<T>)` (multi) | `Replace<T>(def, values)` | folds to a `CompositeTrait` |
| `getConvention()` | `Convention` | |
| `satisfies(that)` | `Satisfies(required)` | |
| `canonize(t)` | `Canonize(t)` | delegates to the def; sets are also interned via `Cache` |
| `contains` / `comprises` / `matches` | `Contains` / `Comprises` / `Matches` | |
| `getCollation()`, `getDistribution()`, `isDefaultSansConvention()`, `apply(mapping)` | — | relational trait dimensions |

### `Convention` (interface) → `IConvention` (`src/Alembic/Plan/IConvention.cs`)

| Calcite | Alembic | Notes |
|---|---|---|
| `getInterface()` | `Interface` | |
| `canConvertConvention(to)` | `CanConvertConvention(to)` | default member |
| `useAbstractConvertersForConversion(from, to)` | `UseAbstractConvertersForConversion(from, to)` | default member |
| `enforce(input, required)` | `Enforce(input, required)` | default member |
| `getRelFactories()` | — | relational |

### `Convention.Impl` → `Convention` (`src/Alembic/Plan/Convention.cs`)

| Calcite | Alembic | Notes |
|---|---|---|
| ctor `(name, relClass)` | ctor `(name, interface)` | |
| `getName()` / `toString()` | `Name` / `ToString()` | |
| `getTraitDef()` | `TraitDef` (→ `ConventionTraitDef.Instance`) | |
| `getInterface()` | `Interface` | |
| `satisfies(trait)` | `Satisfies(trait)` | explicit; `Equals` by name |
| `register(planner)` | `Register(planner)` | virtual; convention contributes its rules |
| `enforce` / `canConvertConvention` / `useAbstractConvertersForConversion` | same names | virtual |
| `Convention.NONE` | `Convention.None` | |

### `ConventionTraitDef` → `ConventionTraitDef` (`src/Alembic/Plan/ConventionTraitDef.cs`)

| Calcite | Alembic | Notes |
|---|---|---|
| `INSTANCE` | `Instance` | singleton |
| `getSimpleName()` | `Name` | |
| `getDefault()` | `Default` (→ `Convention.None`) | |
| `convert(...)` / `canConvert(...)` | (inherited defaults) | convention conversion is rule-driven |

### `RelMultipleTrait` → `IMultipleTrait` (`src/Alembic/Plan/IMultipleTrait.cs`)

| Calcite | Alembic | Notes |
|---|---|---|
| `isTop()` | (marker only) | marks a trait dimension as multi-valued |

### `RelCompositeTrait<T>` → `CompositeTrait<T>` (`src/Alembic/Plan/CompositeTrait.cs`)

| Calcite | Alembic | Notes |
|---|---|---|
| `of(def, traits)` | `Of(def, traits)` | |
| `traitList()` | `Traits` | |
| `getTraitDef()` | `TraitDef` | |
| `satisfies(trait)` | `Satisfies(other)` | |
| `trait(i)` / `size()` | (via `Traits`) | |

---

## 3. Cost

### `RelOptCost` (interface) → `ICost` (`src/Alembic/Plan/ICost.cs`)

| Calcite | Alembic | Notes |
|---|---|---|
| `isInfinite()` | `IsInfinite` | |
| `isLe(cost)` | `IsLessThanOrEqual(other)` | |
| `isLt(cost)` | `IsLessThan(other)` | |
| `plus(cost)` | `Plus(other)` | |
| `equals(cost)` | `Equals` | |
| `getCpu()` / `getIo()` | `Cpu` / `Io` | on the interface; `Cost` reports its value as `Cpu`, `0` as `Io` |
| `getRows()` | — | row count dropped (not a database) |
| `minus`, `multiplyBy`, `divideBy`, `isEqWithEpsilon` | `Minus`, `MultiplyBy`, `DivideBy`, `IsEqWithEpsilon` | all present on `ICost` |

### `RelOptCostFactory` (interface) → `ICostFactory` (`src/Alembic/Plan/ICostFactory.cs`)

| Calcite | Alembic | Notes |
|---|---|---|
| `makeCost(rowCount, cpu, io)` | `MakeCost(cpu, io)` | no `rowCount` |
| `makeZeroCost()` | `MakeZeroCost()` | |
| `makeInfiniteCost()` | `MakeInfiniteCost()` | |
| `makeHugeCost()` | `MakeHugeCost()` | |
| `makeTinyCost()` | `MakeTinyCost()` | |

### `RelOptCostImpl` → `Cost` (`src/Alembic/Plan/Cost.cs`)

Scalar cost. `Zero`/`Tiny`/`Huge`/`Infinity` constants + `Factory`; implements the `ICost` and
`ICostFactory` members above over a single `double`.

### `VolcanoCost` → `VolcanoCost` (`src/Alembic/Plan/Volcano/VolcanoCost.cs`)

| Calcite | Alembic | Notes |
|---|---|---|
| `cpu` / `io` (+ `rowCount`) | `Cpu` / `Io` | no `rowCount` |
| `isLt` (compares cpu) | `IsLessThan` | compares on cpu |
| `Factory` (`VolcanoCostFactory`) | `VolcanoCost.Factory` | |
| `isInfinite`/`isLe`/`plus`/`equals` | same names | |

---

## 4. Planner core

### `RelOptPlanner` (interface) → `IPlanner` + `AbstractPlanner` (`src/Alembic/Plan/IPlanner.cs`, `AbstractPlanner.cs`)

| Calcite | Alembic | Notes |
|---|---|---|
| `setRoot(rel)` | `SetRoot(node)` | |
| `getRoot()` | `Root` | on `IPlanner` |
| `changeTraits(rel, toTraits)` | `ChangeTraits(node, toTraits)` | |
| `findBestExp()` | `FindBestPlan()` | |
| `addRule(rule)` | `AddRule(rule)` | |
| `removeRule(rule)` | `RemoveRule(rule)` | on `IPlanner` |
| `addRelTraitDef(def)` | `AddTraitDef(def)` | |
| `getRelTraitDefs()` | `TraitDefs` | |
| `getCostFactory()` | `CostFactory` | |
| `addListener(l)` | `AddListener(l)` | |
| `setTopDownOpt(b)` | `VolcanoPlanner.SetTopDownOpt(b)` | throws for `true` (Cascades not built) |
| `register(rel, equiv)` / `ensureRegistered(rel, equiv)` | `VolcanoPlanner.Register(node, equiv)` | HEP treats these as no-ops |
| `clear()` / `clearRelTraitDefs()` | `HepPlanner.ClearRules()` (partial) | |
| `chooseDelegate()`, `registerSchema`, `registerClass`, `registerMetadataProviders`, `onCopy` | — | relational / metadata / multi-planner |
| `emptyTraitSet()` | `EmptyTraitSet` | the default trait set built from the registered defs |

### `RelOptCluster` → `Cluster` (`src/Alembic/Plan/Cluster.cs`)

| Calcite | Alembic | Notes |
|---|---|---|
| `getPlanner()` | `Planner` | |
| `traitSet()` | `TraitSet` | |
| `traitSetOf(traits...)` | `TraitSetOf(traits)` | |
| `getTypeFactory`, `getRexBuilder`, `getMetadataQuery`, `createCorrel`, `getHintStrategies`, … | — | relational / metadata |

### `RelOptListener` (interface) → `IPlannerListener` (`src/Alembic/Plan/IPlannerListener.cs`)

| Calcite | Alembic | Notes |
|---|---|---|
| `relEquivalenceFound(e)` | `NodeEquivalenceFound(e)` | |
| `ruleAttempted(e)` | `RuleAttempted(e)` | |
| `ruleProductionSucceeded(e)` | `RuleProductionSucceeded(e)` | |
| `relDiscarded(e)` | `NodeDiscarded(e)` | |
| `relChosen(e)` | `NodeChosen(e)` | |
| `RelEvent` / `RelChosenEvent` / … (nested) | `PlannerEvent` / `NodeChosenEvent` / … (nested) | `getRel()`→`Node`, `getRuleCall()`→`RuleCall`, `isBefore()`→`before`, `isPhysical()` omitted |

### `RelOptUtil` → `PlanUtil` (`src/Alembic/Plan/PlanUtil.cs`)

| Calcite | Alembic | Notes |
|---|---|---|
| `toString(rel)` | `ToString(node)` | renders the plan via `NodeWriterImpl` |
| `areRowTypesEqual`, `createCastRel`, `verifyTypeEquivalence`, `getVariablesSet/Used`, `equal`, `getFullTypeDifferenceString` | — | relational (row types, casts, correlation variables) — the only `RelOptUtil` members the ported planner code referenced, all out of scope |
| `registerAbstractRelationalRules` | (in `VolcanoPlanner` ctor: `AddRule(new ExpandConversionRule())`) | the abstract-converter rule is registered directly |

---

## 5. Rules

### `RelOptRule` (abstract) → `Rule` (abstract) (`src/Alembic/Plan/Rules/Rule.cs`)

| Calcite | Alembic | Notes |
|---|---|---|
| `getOperand()` / `operand` | `Operand` | the root operand |
| `operands` (flattened list) | `Operands` | prefix-order flattened list, built in the ctor |
| `flattenOperands` / `flattenRecurse` | `FlattenOperands` / `FlattenRecurse` | wires each operand's rule/parent/ordinals |
| `assignSolveOrder` | `AssignSolveOrder` | fills each operand's `SolveOrder` |
| `matches(call)` | `Matches(call)` | side-condition hook; default `true` |
| `onMatch(call)` | `OnMatch(call)` | |
| `toString()` / description | `Description` | default = type name |
| `operand`/`some`/`any`/`none`/`unordered`/`convertOperand` statics | `Some`/`Any`/`Leaf`/`Unordered`/`ConvertOperand` (`protected static`) | the only way to build operands (closed world) |

### `RelOptRuleOperand` → `RuleOperand` (`src/Alembic/Plan/Rules/RuleOperand.cs`)

Constructors are **`internal`**: operands form a closed world, built only through the `protected static`
factory methods on `Rule` (mirrors Calcite, where `RelOptRuleOperand`'s constructor is package-private and
operands are created via `RelOptRule`'s static methods). `[InternalsVisibleTo]` lets tests reach them.

| Calcite | Alembic | Notes |
|---|---|---|
| `childPolicy` | `ChildPolicy` | |
| `getChildOperands()` | `Children` | |
| `matches(rel)` | `Matches(node)` | tests class + trait + predicate; children are matched by the matcher |
| `clazz` / `getMatchedClass()` | `MatchedClass` | drives the planner's dispatch table |
| `trait` | `Trait` | optional required trait (null if none) |
| `predicate` | `Predicate` | extra condition; defaults to always-true |
| `getRule()`/`setRule()` | `Rule` | back-reference, set during flattening |
| `getParent()`/`setParent()` | `Parent` | |
| `ordinalInParent` | `OrdinalInParent` | |
| `ordinalInRule` | `OrdinalInRule` | index into `Rule.Operands` |
| `solveOrder` | `SolveOrder` | seed-then-parents-then-rest order used by the matcher |


### `RelOptRuleOperandChildPolicy` → `RuleOperandChildPolicy` (enum)

| Calcite | Alembic |
|---|---|
| `ANY` / `LEAF` / `SOME` / `UNORDERED` | `Any` / `Leaf` / `Some` / `Unordered` |

### `RelOptRuleCall` → `RuleCall` (`src/Alembic/Plan/Rules/RuleCall.cs`)

| Calcite | Alembic | Notes |
|---|---|---|
| `rel(int)` | `Node(int)` | |
| `getRels()` / `getRelList()` | `Nodes` | |
| `transformTo(rel)` | `TransformTo(equivalent)` | |
| `transformTo(rel, equiv)` | `TransformTo(equivalent, equiv)` | secondary-equivalence map (each key registered as equivalent to its value) |
| `getRule()` | `Rule` | on the base `RuleCall` (derived from `Operand0`) |
| `getPlanner()` | `Planner` | on the base `RuleCall` |
| `getOperand0()` | `Operand0` | on the base `RuleCall`; the seed operand the rule (and bound nodes) are reached through |
| `transformTo(rel, equiv map)`, `getChildRels`, `getParents`, `getMetadataQuery`, `builder()`, `isRuleExcluded` | — | equivalence maps / metadata / builder |

### `ConverterRule` (abstract) → `ConverterRule : Rule` (`src/Alembic/Plan/Rules/ConverterRule.cs`)

| Calcite | Alembic | Notes |
|---|---|---|
| `getInTrait()` | `Source` | generalized from `Convention` to any `ITrait` |
| `getOutTrait()` / `getOutConvention()` | `Target` | |
| `convert(rel)` | `Convert(node)` | |
| `isGuaranteed()` | `IsGuaranteed` | default `true` |
| `getOperand()` (matches in-trait) | `Operand` (= `ConvertOperand<INode>(source)`) | matches nodes carrying `Source` |
| `onMatch(call)` | `OnMatch(call)` | calls `Convert`, transforms |
| `Config` (immutables) | — | constructor args instead; see §9 |

### `Converter` (interface) → `IConverter` + `ConverterImpl` (`src/Alembic/Algebra/Convert/`)

| Calcite | Alembic | Notes |
|---|---|---|
| `getInputTraits()` | `InputTraits` | |
| `getTraitDef()` | `TraitDef` | |
| `getInput()` | `Input` | |

### `TraitMatchingRule` → `TraitMatchingRule` (`src/Alembic/Plan/Rules/TraitMatchingRule.cs`)

| Calcite | Alembic | Notes |
|---|---|---|
| ctor `(converterRule, …)` | ctor `(converterRule)` | |
| `onMatch(call)` (fires if input has out-trait) | `OnMatch(call)` | same condition |
| operand `matchedClass.oneInput(any)` | `Operand` (Some with one Any child) | |
| `Config` | — | constructor instead; see §9 |

### `CommonRelSubExprRule` (marker) → `ICommonSubExprRule` (`src/Alembic/Plan/Rules/ICommonSubExprRule.cs`)

Marker interface; fired by `HepProgramBuilder.AddCommonRelSubExprInstruction()` only on shared vertices.

---

## 6. The heuristic planner (HEP)

### `HepPlanner` → `HepPlanner` (`src/Alembic/Plan/Hep/HepPlanner.cs`)

| Calcite | Alembic | Notes |
|---|---|---|
| fields `mainProgram`, `root`, `requestedRootTraits`, `mapDigestToVertex`, `graph`, `nTransformations`, `graphSizeLastGC`, `nTransformationsLastGC`, `noDag`, `firedRulesCache`(+index), `enableFiredRulesCache`, `largePlanMode` | same set | (`graph` is `DirectedGraph<HepNodeVertex, DefaultEdge>`) |
| `setRoot` / `getRoot` | `SetRoot` / `Root` | + `_rootNode` (raw root) for `ChangeTraits` |
| `changeTraits` | `ChangeTraits` | matches `root` / `root.CurrentNode` |
| `findBestExp()` | `FindBestPlan()` | `ExecuteProgram` → `CollectGarbage` → `BuildFinalPlan` |
| `buildFinalPlan()` / `buildFinalPlan(vertex)` | `BuildFinalPlan(vertex, memo)` | memoized (immutable rebuild preserves sharing) |
| `clearRules()` | `ClearRules()` | |
| `setEnableFiredRulesCache(b)` | `SetEnableFiredRulesCache(b)` | + `NoDag`, `LargePlanMode` properties |
| `executeProgram(program)` / `executeProgram(program, state)` | `ExecuteProgram(program)` / `ExecuteProgram(program, state)` | |
| `executeMatchLimit`/`MatchOrder`/`RuleInstance`/`RuleLookup`/`RuleClass`/`RuleCollection`/`ConverterRules`/`CommonRelSubExprRules`/`SubProgram`/`BeginGroup`/`EndGroup` | identical set (`ExecuteXxx`) | |
| `applyRules` / `depthFirstApply` / `getGraphIterator` / `applyRule` | same | incl. large-plan `HepVertexIterator` branch |
| `matchOperands(...)` | `Match` / `MatchOperand` / `MatchUnordered` | sees through vertices |
| `applyTransformationResults` | `ApplyTransformationResults` | |
| `addRelToGraph` | `AddNodeToGraph` | |
| `contractVertices` | `ContractVertices` | rebuilds parents (immutable) instead of `replaceInput` |
| `updateVertex` / `clearCache` | `UpdateVertex` / `ClearCache` | `ClearCache` clears digest caches up inward edges |
| `getVertexParents` / `doesConverterApply` | `GetVertexParents` / `DoesConverterApply` | |
| `collectGarbage()` / `collectGarbage(set)` / `tryCleanVertices` | same | mark-and-sweep |
| `getRuleByDescription` | `GetRuleByDescription` | |
| (trait enforcement) | `EnsureSatisfies` + `CannotPlanException` | Alembic addition |
| `register`/`onCopy`/`ensureRegistered`/`isRegistered` | — | Volcano-compat no-ops |
| `assertNoCycles`/`assertGraphConsistent`/`dumpGraph` | — | debug |
| `registerMetadataProviders`/`getRelMetadataTimestamp`/materializations | — | relational / metadata |

### `HepProgram` → `HepProgram` (`src/Alembic/Plan/Hep/HepProgram.cs`)

| Calcite | Alembic | Notes |
|---|---|---|
| `MATCH_UNTIL_FIXPOINT` | `MatchUntilFixpoint` | |
| `instructions` | `Instructions` | |
| `builder()` | `Builder()` | |
| `prepare(px)` → `State` | `Prepare(px)` → `State` | re-entrant prepare/state model |
| `State`: `instructionStates`, `matchLimit`, `matchOrder`, `group`, `init()`, `execute()`, `skippingGroup()` | same (PascalCase) | |

### `HepProgramBuilder` → `HepProgramBuilder` (`src/Alembic/Plan/Hep/HepProgramBuilder.cs`)

| Calcite | Alembic |
|---|---|
| `addRuleInstance` / `addRuleCollection` / `addRuleClass` / `addRuleByDescription` | `AddRuleInstance` / `AddRuleCollection` / `AddRuleClass<T>` / `AddRuleByDescription` |
| `addConverters(guaranteed)` / `addCommonRelSubExprInstruction` | `AddConverters` / `AddCommonRelSubExprInstruction` |
| `addMatchOrder` / `addMatchLimit` / `addSubprogram` | `AddMatchOrder` / `AddMatchLimit` / `AddSubprogram` |
| `addGroupBegin` / `addGroupEnd` | `AddGroupBegin` / `AddGroupEnd` |
| `build()` | `Build()` |

### `HepInstruction` (+ nested) → `HepInstruction` (`src/Alembic/Plan/Hep/HepInstruction.cs`)

| Calcite | Alembic | Notes |
|---|---|---|
| `prepare(px)` | `Prepare(px)` | |
| nested `RuleClass`, `RuleCollection`, `ConverterRules`, `CommonRelSubExprRules`, `RuleInstance`, `RuleLookup`, `MatchOrder`, `MatchLimit`, `SubProgram`, `BeginGroup`, `Placeholder`, `EndGroup` | identical set | each with a nested `State : HepState` |
| `PrepareContext` (+ `create`/`withProgramState`/`withEndGroupState`) | `PrepareContext` (`Create`/`WithProgramState`/`WithEndGroupState`) | |

### `HepState` → `HepState` (`src/Alembic/Plan/Hep/HepState.cs`)

| Calcite | Alembic |
|---|---|
| `planner`, `programState` | `Planner`, `ProgramState` |
| `execute()` (abstract) / `init()` | `Execute()` / `Init()` |

### `HepRelVertex` → `HepNodeVertex` (`src/Alembic/Plan/Hep/HepNodeVertex.cs`)

| Calcite | Alembic | Notes |
|---|---|---|
| `getCurrentRel()` | `CurrentNode` | |
| `replaceRel(newRel)` | `ReplaceNode(newNode)` | |
| `stripped()` | `Stripped` | |
| `copy(...)` → `this` | `Copy(...)` → `this` | |
| `explain(pw)` (delegates) | `ExplainTerms(writer)` (writes current node) | |
| `deepEquals` (`this == obj \|\| same currentRel`) | `DeepEquals` (same shape) | |
| `deepHashCode()` (`currentRel.getId()`) | `DeepHashCode()` (`currentNode.GetHashCode()`) | identity of wrapped node |
| `computeSelfCost` (tiny) | (inherited `AbstractNode` tiny) | |
| `estimateRowCount`/`deriveRowType`/`getMetadataDelegateRel` | — | relational / metadata |

### `HepMatchOrder` → `HepMatchOrder` (enum)

| Calcite | Alembic |
|---|---|
| `ARBITRARY` / `BOTTOM_UP` / `TOP_DOWN` / `DEPTH_FIRST` | `Arbitrary` / `BottomUp` / `TopDown` / `DepthFirst` |

### `HepRuleCall` → `HepRuleCall` (`src/Alembic/Plan/Hep/HepRuleCall.cs`)

| Calcite | Alembic | Notes |
|---|---|---|
| `transformTo(rel, …)` | `TransformTo(equivalent)` | appends to results |
| `getResults()` | `Results` | |
| `getParents()` / `getMetadataQuery()` | — | parent context / metadata |

### `HepVertexIterator` → `HepVertexIterator` (`src/Alembic/Plan/Hep/HepVertexIterator.cs`)

| Calcite | Alembic | Notes |
|---|---|---|
| `of(root, visitedSet)` | `Of(root, visited)` | yields a fresh iterator |
| `continueFrom(v)` | `ContinueFrom(v)` | |
| `hasNext`/`next` | `MoveNext`/`Current` | `IEnumerator<HepNodeVertex>` |

---

## 7. The cost-based planner (Volcano)

> **Audit status:** unlike the HEP package, `VolcanoPlanner` and friends predate the method-by-method
> audit and are a reduced port (≈1,200 lines vs Calcite's ≈1,670 in `VolcanoPlanner` alone). The
> mappings below are accurate, but this package has known gaps (see §9) and likely undiscovered
> simplifications.

### `VolcanoPlanner` → `VolcanoPlanner` (`src/Alembic/Plan/Volcano/VolcanoPlanner.cs`)

| Calcite | Alembic | Notes |
|---|---|---|
| `setRoot` / `findBestExp` | `SetRoot` / `FindBestPlan` | |
| `changeTraits(rel, traits)` | `Convert(node, traits)` | the conversion primitive (no root side-effect); `RelOptRule.convert`'s target |
| (setRoot + changeTraits convenience) | `ChangeTraits(node, traits)` | `Convert` plus recording the root request — Alembic packages the common `setRoot(changeTraits(...))` into one call |
| `register` / `ensureRegistered` | `Register(node, equiv)` | |
| `registerImpl` | `RegisterImpl` | |
| `addRule` / `removeRule` | `AddRule` / `RemoveRule` | build/tear down the dispatch table; register converter rules with their trait def |
| `classOperands` | `_classOperands` | `Dictionary<Type, List<RuleOperand>>`: node class → operands that can bind it |
| `subClasses(matchedClass)` | `SubClasses` | seen classes assignable to a matched class |
| `onNewClass(rel)` / `registerClass` | `OnNewClass` | indexes operands for a node class the first time it is seen |
| `fireRules(rel)` | `FireRules` | seeds a `DeferringRuleCall` per matching operand from the table |
| `getSubsetNonNull` | `GetSubsetNonNull` | the subset a node belongs to (subset traversal itself lives on `NodeSubset`) |
| `changeTraitsUsingConverters` | `ChangeTraitsUsingConverters` | BFS over converter rules + trait-def hooks |
| `ensureRootConverters` / `addAbstractConverters` | `EnsureRootConverters` | |
| `getCost` / `getSubset` | `GetCost` / (subset lookup) | |
| `setTopDownOpt(b)` | `SetTopDownOpt(b)` | selects iterative vs top-down driver |
| (set merge) | `Merge` + `Rename`/`FixUpInputs` | re-points parents (the `rename` → `fixUpInputs` analog) |
| `RelSet`/`RelSubset` management | `NodeSet`/`NodeSubset` | |
| `isLogical` / `isTransformationRule` / `isSubstituteRule` | `IsLogical` / `IsTransformationRule` / `IsSubstituteRule` | drive the top-down search; `IsSubstituteRule` is always false (no substitution rules) |
| `getLowerBound` / `upperBoundForInputs` / `zeroCost` / `infCost` | `GetLowerBound` / `UpperBoundForInputs` / `ZeroCost` / `InfiniteCost` | `GetLowerBound` returns zero (metadata deferred) |
| `getSet` / `equivRoot` / `rootConvention` | `GetSet` / `EquivRoot` / `RootConvention` | |
| importance, timeout, dumpers, metadata | — | see §9 |

### `RelSet` → `NodeSet` (`src/Alembic/Plan/Volcano/NodeSet.cs`)

| Calcite | Alembic | Notes |
|---|---|---|
| `rels` / `subsets` / `parents` | `Nodes` / `Subsets` / `Parents` | |
| `rel` (representative) | `Rel` | first node registered; backs `NodeSubset.GetOriginal` |
| `getSubset(traits)` / `getOrCreateSubset(...)` | `GetSubset(traits)` / `GetOrCreateSubset(traits[, required])` | the `required` overload marks delivered/required |
| `add(rel)` | `Add(node)` | |
| `equivalentSet` | `EquivalentSet` | the merge-forwarding pointer |
| `exploringState` | `Exploring` | `ExploringState` enum |
| `mergeWith(...)` | (in `VolcanoPlanner.Merge`) | |
| `getChildSets`, `getRelsFromAllSubsets`, `obliterateRelNode` | — | |

### `RelSubset` → `NodeSubset` (`src/Alembic/Plan/Volcano/NodeSubset.cs`)

| Calcite | Alembic | Notes |
|---|---|---|
| `getBest()` / `bestCost` | `Best` / `BestCost` | |
| `getSet()` | `Set` | |
| `getOriginal()` / `getBestOrOriginal()` / `stripped()` | `GetOriginal()` / `GetBestOrOriginal()` / `Stripped` | `Stripped` is a default `INode` member (returns `this`) that a subset and a HEP vertex override |
| `getRels()` / `getRelList()` / `contains(rel)` | `GetRels()` (→ `IEnumerable`) / `GetRelList()` (→ `IList`) / `Contains(node)` | members of the (live) set satisfying this subset's traits |
| `buildCheapestPlan(planner)` + `CheapestPlanReplacer` | `BuildCheapestPlan(planner)` + nested `CheapestPlanReplacer` | replaces each subset with its best member; memoizes by node; fires `NodeChosen` (the dead-end diagnostic dump is not ported) |
| `getParents()` / `getParentSubsets(planner)` / `getParentRels()` | `GetParents()` / `GetParentSubsets(planner)` / `GetParentRels()` | the live set's parents filtered by this subset (liveness via `NodeSet.Live`) |
| `getSubsetsSatisfyingThis()` / `getSatisfyingSubsets()` | `GetSubsetsSatisfyingThis()` / `GetSatisfyingSubsets()` | sibling subsets ordered by the trait partial order |
| `computeSelfCost(...)` | `ComputeSelfCost(...)` → zero | a subset has no cost of its own |
| `explain(pw)` | `ExplainTerms(writer)` | writes `Item("subset", Set.Id)` |
| `copy(...)` → `this` | `Copy(...)` → `this` | |
| `taskState`/`upperBound`/`getWinnerCost`/`startOptimize`/`setOptimized`/`resetTaskState` | `TaskState`/`UpperBound`/`GetWinnerCost`/`StartOptimize`/`SetOptimized`/`ResetTaskState` | top-down optimization state (`OptimizeState` enum) |
| `isDelivered`/`isRequired`/`setDelivered`/`setRequired`/`disableEnforcing`/`triggerRule` | `IsDelivered`/`IsRequired`/`SetDelivered`/`SetRequired`/`DisableEnforcing`/`TriggerRule` | physical-property state for the top-down search |
| `passThrough(rel)`/`explore`/`isExplored`/`setExplored` | `PassThrough`/`Explore`/`IsExplored`/`SetExplored` | `exploringState` lives on `NodeSet` (`ExploringState` enum) |
| `estimateRowCount`/`deriveRowType` | — | relational / metadata |

### `VolcanoRuleCall` → `VolcanoRuleCall` (`src/Alembic/Plan/Volcano/VolcanoRuleCall.cs`)

| Calcite | Alembic | Notes |
|---|---|---|
| `getRule()` | `Rule` | |
| `getPlanner()` | `Planner` | |
| `getOperand0()` / `rels` | `RuleCall.Operand0` / `Rels` | `Operand0` now lives on the `RuleCall` base (Calcite's `RelOptRuleCall.operand0`); `Rels` is the per-operand bound nodes (by `OrdinalInRule`) |
| `onMatch()` | `OnMatch()` | base applies the rule; `DeferringRuleCall` overrides to enqueue |
| `transformTo(...)` | `TransformTo(equivalent)` | |
| `match(rel)` / `matchRecurse(solve)` | `Match(node)` / `MatchRecurse(solve)` | seed-and-solve over subsets using `Operand0.SolveOrder` |

### `VolcanoRuleCall.DeferringRuleCall` → `DeferringRuleCall` (`src/Alembic/Plan/Volcano/DeferringRuleCall.cs`)

| Calcite | Alembic | Notes |
|---|---|---|
| ctor `(planner, operand)` | ctor `(planner, operand0)` | seeded at the operand the node matched |
| `onMatch()` (enqueues) | `OnMatch()` | builds a `VolcanoRuleMatch` from `Rels` and adds it to the queue |

### `VolcanoRuleMatch` → `VolcanoRuleMatch` (`src/Alembic/Plan/Volcano/VolcanoRuleMatch.cs`)

| Calcite | Alembic | Notes |
|---|---|---|
| `computeDigest` / `recomputeDigest` / `toString` | `Equals` / `GetHashCode` | dedup of matches (by rule + bound nodes) |

### `RuleQueue` (abstract) → `RuleQueue` (`src/Alembic/Plan/Volcano/RuleQueue.cs`)

| Calcite | Alembic | Notes |
|---|---|---|
| `addMatch(match)` | `AddMatch(match)` | abstract |
| `clear()` | `Clear()` | abstract |
| `skipMatch(match)` | `SkipMatch(match)` | virtual; skips pruned nodes and duplicate-subset cycles (`HasDuplicateSubsetOnPath`) |

### `IterativeRuleQueue` → `IterativeRuleQueue` (`src/Alembic/Plan/Volcano/IterativeRuleQueue.cs`)

| Calcite | Alembic | Notes |
|---|---|---|
| `addMatch` / `clear` | `AddMatch` / `Clear` | |
| (pop) | `PopMatch()` | FIFO; no importance ranking |

### `TopDownRuleQueue` → `TopDownRuleQueue` (`src/Alembic/Plan/Volcano/TopDownRuleQueue.cs`)

| Calcite | Alembic | Notes |
|---|---|---|
| `matches` (per-rel deques) | `_matches` | matches bucketed by the node they are rooted at |
| `addMatch` (substitution last) | `AddMatch` | non-substitution to the front, substitution to the back |
| `popMatch(category)` | `PopMatch(rel, predicate)` | next match for a node passing the predicate |
| `clear` | `Clear` | |

### `RuleDriver` (interface) → `IRuleDriver` (`src/Alembic/Plan/Volcano/IRuleDriver.cs`)

| Calcite | Alembic |
|---|---|
| `getRuleQueue()` | `Queue` |
| `drive()` | `Drive()` |
| `onProduce(rel, subset)` | `OnProduce(node, subset)` |
| `onSetMerged(set)` | `OnSetMerged(set)` |
| `clear()` | `Clear()` |

### `IterativeRuleDriver` → `IterativeRuleDriver` (`src/Alembic/Plan/Volcano/IterativeRuleDriver.cs`)

Implements the `IRuleDriver` members above (drain the queue to a fixed point).

### `TopDownRuleDriver` → `TopDownRuleDriver` (`src/Alembic/Plan/Volcano/TopDownRuleDriver.cs`)

The Cascades top-down driver: a task stack over `OptimizeGroup` / `GroupOptimized` / `OptimizeMExpr` /
`ExploreInput` / `EnsureGroupExplored` / `ApplyRules` / `ApplyRule` / `OptimizeInput1` / `OptimizeInputs` /
`CheckInput` / `DeriveTrait` (the same task set as Calcite, minus materialization roots and timeout). The
branch-and-bound structure is faithful; lower-bound pruning is inert pending metadata (see §9).

### `PhysicalNode` → `IPhysicalNode` (`src/Alembic/Algebra/IPhysicalNode.cs`)

| Calcite | Alembic | Notes |
|---|---|---|
| `passThrough` / `passThroughTraits` | `PassThrough` / `PassThroughTraits` | default `PassThrough` composes the `Pair<TraitSet, IList<TraitSet>>`; `PassThroughTraits` throws until overridden |
| `derive(traits, childId)` / `deriveTraits` | `Derive(TraitSet, int)` / `DeriveTraits` | as above |
| `derive(List)` (OMAKASE) | `Derive(IList<IList<TraitSet>>)` | |
| `getDeriveMode()` | `DeriveMode` | default `LeftFirst` |

### `DeriveMode` → `DeriveMode` (`src/Alembic/Plan/DeriveMode.cs`)

`LEFT_FIRST` / `RIGHT_FIRST` / `BOTH` / `OMAKASE` / `PROHIBITED` → `LeftFirst` / `RightFirst` / `Both` /
`Omakase` / `Prohibited`.

### `TransformationRule` (marker) → `ITransformationRule` (`src/Alembic/Plan/Rules/ITransformationRule.cs`)

Marker interface; the planner keeps a transformation rule from being indexed against, or matched on,
physical nodes (`AddRule` / `OnNewClass` / `VolcanoRuleCall.MatchRecurse`). `SubstitutionRule` is **not
ported** (no substitution rules), so `IsSubstituteRule` is always false.

### `AbstractConverter` → `AbstractConverter` (`src/Alembic/Plan/Volcano/AbstractConverter.cs`)

| Calcite | Alembic | Notes |
|---|---|---|
| ctor `(cluster, child, traitDef, traits)` | ctor `(target, input, traitDef)` | |
| `copy(...)` | `Copy(...)` | |
| `computeSelfCost(...)` (huge) | `ComputeSelfCost(planner)` | |
| `isEnforcer()` | — | |
| `explainTerms` | (inherited) | |

### `AbstractConverter.ExpandConversionRule` → `ExpandConversionRule` (`src/Alembic/Plan/Volcano/ExpandConversionRule.cs`)

| Calcite | Alembic | Notes |
|---|---|---|
| operand (matches `AbstractConverter`) | `Operand` | |
| `onMatch(call)` (→ `changeTraitsUsingConverters`) | `OnMatch(call)` | |

---

## 8. Graph utility (`org.apache.calcite.util.graph` → `Alembic.Util.Graph`)

### `DirectedGraph<V, E>` (interface) → `DirectedGraph<V, E>` (`src/Alembic/Util/Graph/DirectedGraph.cs`)

| Calcite | Alembic | Notes |
|---|---|---|
| `addVertex(v)` | `AddVertex(v)` | |
| `addEdge(v, target)` | `AddEdge(v, target)` | returns the edge or null |
| `getEdge(s, t)` | `GetEdge(s, t)` | |
| `removeEdge(v, target)` | `RemoveEdge(v, target)` | |
| `vertexSet()` | `VertexSet` | `IReadOnlySet<V>`, insertion-ordered |
| `edgeSet()` | `EdgeSet` | `IReadOnlySet<E>` |
| `removeAllVertices(c)` | `RemoveAllVertices(c)` | |
| `getOutwardEdges(s)` / `getInwardEdges(v)` | `GetOutwardEdges(s)` / `GetInwardEdges(v)` | |
| `EdgeFactory.createEdge(v0, v1)` | `EdgeFactory.CreateEdge(v0, v1)` | nested interface |

### `DefaultDirectedGraph<V, E>` → `DefaultDirectedGraph<V, E>` (`src/Alembic/Util/Graph/DefaultDirectedGraph.cs`)

| Calcite | Alembic | Notes |
|---|---|---|
| `create()` / `create(factory)` | `Create()` / `Create(factory)` | |
| `getVertex(v)` (protected) | `GetVertex(v)` (protected) | |
| `VertexInfo` (in/out edges) | `VertexInfo` | nested |
| all `DirectedGraph` members | same | |
| `toString` / `toStringUnordered` | — | debug |

### `DefaultEdge` → `DefaultEdge` (`src/Alembic/Util/Graph/DefaultEdge.cs`)

| Calcite | Alembic |
|---|---|
| `source` / `target` | `Source` / `Target` |
| `hashCode`/`equals`/`toString` | `GetHashCode`/`Equals`/`ToString` |
| `factory()` | `Factory<V>()` |

### `Graphs` → `Graphs` (`src/Alembic/Util/Graph/Graphs.cs`)

| Calcite | Alembic | Notes |
|---|---|---|
| `predecessorListOf(graph, v)` | `PredecessorListOf(graph, v)` | |
| `makeImmutable` / `FrozenGraph` | — | all-pairs shortest paths (unused by HEP) |

### `DepthFirstIterator<V, E>` → `DepthFirstIterator<V, E>` (`src/Alembic/Util/Graph/DepthFirstIterator.cs`)

| Calcite | Alembic | Notes |
|---|---|---|
| ctor `(graph, start)` | ctor `(graph, start)` | builds the list, iterates it |
| `of(graph, start)` | `Of(graph, start)` | `yield`s; returns `IEnumerable<V>` |
| `reachable(coll, graph, start)` | `Reachable(coll, graph, start)` | |
| `hasNext`/`next` | `MoveNext`/`Current` | |

### `BreadthFirstIterator<V, E>` → `BreadthFirstIterator<V, E>` (`src/Alembic/Util/Graph/BreadthFirstIterator.cs`)

| Calcite | Alembic | Notes |
|---|---|---|
| ctor `(graph, root)` | ctor `(graph, root)` | |
| `of(graph, root)` | `Of(graph, root)` | `yield`s |
| `reachable(set, graph, root)` | `Reachable(set, graph, root)` | |
| `hasNext`/`next` | `MoveNext`/`Current` | |

### `TopologicalOrderIterator<V, E>` → `TopologicalOrderIterator<V, E>` (`src/Alembic/Util/Graph/TopologicalOrderIterator.cs`)

| Calcite | Alembic | Notes |
|---|---|---|
| ctor `(graph)` / `(graph, order)` | ctor `(graph)` / `(graph, order)` | populates count map |
| `of(graph)` / `of(graph, order)` | `Of(graph)` / `Of(graph, order)` | `yield`s |
| `populate` (private) | `Populate` (private) | |
| `hasNext`/`next` | `MoveNext`/`Current` | |

### `Pair<T1, T2>` → `Pair<T1, T2>` (`src/Alembic/Util/Pair.cs`)

| Calcite | Alembic | Notes |
|---|---|---|
| `left` / `right` | `Left` / `Right` | |
| `of(left, right)` | `Pair.Of(left, right)` | on a non-generic companion class so member types infer (C# can't infer them through a generic type's static method) |
| `equals` / `hashCode` / `toString` | `Equals` / `GetHashCode` / `ToString` | value semantics (`hash = leftHash ^ rightHash`, `"<l, r>"`); used by `IPhysicalNode` |
| `Comparable` / `Map.Entry`, `zip`/`toMap`/`left(list)`/`right(list)`/… | — | the list/map utility helpers are not needed |

---

## 9. Deliberately not ported

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
| `RelRule.Config` / per-rule `Config` (immutables) | **n/a** — Config exists to keep Calciteʼs built-in rule library uniformly configurable; Alembic ships no rules, so rules take constructor args. A consumer who builds a rule library can layer the same pattern over `Rule` themselves (C# records + `with`). |

---

## 10. Fidelity audit

A method-by-method comparison of each ported class against its Calcite original, performed by isolated
single-class agents (cold context, fed only the two file paths + the mapping above). Only **divergences**
are listed; members verified `FAITHFUL` are omitted, with a coverage line per class. Verdicts:
`DIVERGENT-OK` (differs, justified — reason given) · `DIVERGENT-BUG` (behavior-affecting) ·
`MISSING` (`GAP` = genuine omission; otherwise an out-of-scope tag) · `MOVED` (relocated — verified in the
named file) · `EXTRA` (Alembic member with no Calcite counterpart).

### `RelSubset` → `NodeSubset` — ~50 members, all `FAITHFUL` except:

| Calcite member | Verdict | Note |
|---|---|---|
| `copy(traitSet, inputs)` | **DIVERGENT-BUG** | `Copy` returns `this` unconditionally — drops Calcite's `traitSet.simplify()` + `getOrCreateSubset` branch and the non-empty-inputs throw. Latent: wrong subset if ever copied to a simplifiable/differing trait set. |
| `getBestOrOriginal()` / `stripped()` | DIVERGENT-OK | soften Calcite's null-assertion to a `null`/`this` fallback; benign |
| `getOriginal()` | DIVERGENT-OK | routes through `LiveSet` (equiv-root) because Alembic leaves dead sets in place after merge |
| ctor / `disableEnforcing` / `startOptimize` asserts | DIVERGENT-OK (DEBUG) | Java `assert`s dropped |
| `computeBestCost` | DIVERGENT-OK (METADATA) | init-scan of subsuming subsets dropped; cost is passed into the ctor instead |
| `CheapestPlanReplacer.visit` | DIVERGENT-OK (DEBUG) | omits the `provenanceMap` write after `copy` (provenance/dump not ported) and the dead-end diagnostic dump |
| `add(rel)` | **RESOLVED (moved)** | now ported onto `NodeSubset.Add` to match Calcite's three-method layering: `NodeSet.Add` (entry — `getOrCreateSubset` → `subset.Add` → return subset) / `NodeSubset.Add` (guard on already-present → fire the equivalence-found event → delegate) / `NodeSet.AddInternal` (the raw `Nodes` insert + representative `Rel`). The equivalence event moved out of `VolcanoPlanner.AddNodeToSet` into `NodeSubset.Add`, and is fired the faithful way — via `Set.Cluster.Planner` (Calcite's `rel.getCluster().getPlanner()`). This required fixing the test fixtures so a node's cluster is bound to the planner optimizing it (the shared throwaway-planner `Clusters.Default` was deleted; each test now builds `new Cluster(planner)`), restoring Calcite's cluster↔planner invariant that `IPhysicalNode.PassThrough`/`Derive` already assumed. Merge-time node moves (`NodeSet.MergeWith`) deliberately use the raw `AddInternal` path (no add-event), matching Calcite's merge routing through `reregister`, not `subset.add`. |
| `LiveSet`, `infiniteCost` ctor param | EXTRA (justified) | equiv-root resolution; cost decoupled from metadata. |

Three themes recur across the Volcano findings below and are worth fixing as units rather than piecemeal:
**(A) node pruning** — ~~Calcite's `prunedNodes`/`checkPruned`/`prune` are entirely absent, which also hollows the staleness guards in `VolcanoRuleCall.onMatch`, `RuleQueue.skipMatch`, and the iterative queue.~~ **RESOLVED**: the pruning machinery, the `OnMatch` staleness guard, and `SkipMatch` are ported (see the `VolcanoPlanner`/`VolcanoRuleCall`/`RuleQueue` rows). **(B) canonization** — Alembic's `EquivRoot` returns the live *set*, never re-resolving a subset to the leader set's subset (Calcite's `canonize(subset)`), and the root subset is never re-canonized after a merge. **(C) converter seeding** — ~~Calcite seeds abstract/enforcer converters per-subset inside `RelSet.getOrCreateSubset → addConverters`; Alembic only seeds them at the root (`EnsureRootConverters`).~~ **RESOLVED**: `NodeSet.GetOrCreateSubset`/`AddConverters` ported (see the `RelSet` rows); the root-only `EnsureRootConverters` now overlaps and is a candidate for removal.

### `VolcanoPlanner` → `VolcanoPlanner` — ~53 members; findings:

| Calcite member | Verdict | Note |
|---|---|---|
| `propagateCostImprovements` | **DIVERGENT-BUG** | naive recursion over `set.Parents` replaces Calcite's priority-queue/visited worklist; no cycle guard (stack-overflow risk on cyclic graphs), recurses on raw set parents not per-subset (`subset.getParents()`), no timestamp/reconvergence. Most behavior-affecting. |
| `merge` | **DIVERGENT-BUG** | drops the `isSmaller`/parent-child swap (merge direction), and the root re-point + `ensureRootConverters` when the root set is merged (theme B/C). |
| `ensureRegistered` | **DIVERGENT-BUG** | when the node is already registered it ignores `equivalent` entirely, skipping the equivalence-driven set merge (theme B). |
| `ensureRootConverters` | DIVERGENT-BUG (superseded) | omits the single-trait-difference guard and the AbstractConverter dedup set — seeds a converter for every differing subset. Now largely moot: the ported per-subset `NodeSet.AddConverters` (theme C) carries those guards and seeds the root's converters correctly; `EnsureRootConverters` is redundant and slated for removal. |
| `getCost` (NONE convention) | DIVERGENT-BUG | omits infinite cost for `Convention.None` nodes and the positive-cost nudge; cost diverges for none-convention/zero-cost nodes. |
| `canonize(subset)` | DIVERGENT-BUG | `EquivRoot` returns the live set only; never re-resolves a subset to the leader set's subset (theme B). |
| `prune` / `prunedNodes` / `checkPruned` | **RESOLVED (ported)** | `VolcanoPlanner` now has a `_prunedNodes` identity set with `Prune` (public, overriding the `IPlanner`/`AbstractPlanner` no-op), `IsPruned`, and `CheckPruned` (propagates pruned-ness across a discovered equivalence, wired into the `RegisterImpl` and `Rename` dedup branches). *Omitted as unreachable:* the `registerImpl` pruned-skip-add — Alembic has no caller that prunes a node before it is registered, so a node is never pruned at registration time; pruning is consulted by the `OnMatch` guard and `SkipMatch` below. The `prune()` call sites in Calcite (a `SubstitutionRule`'s auto-prune, `mergeWith`'s redundant-enforcer prune) depend on features Alembic doesn't have yet, so `Prune` is public-and-available rather than self-invoked. |
| `setLocked`/`locked` | **RESOLVED (ported)** | `VolcanoPlanner._locked` + `SetLocked(bool)`; `AddRule` returns `false` immediately when locked (before the base dedup), matching Calcite. |
| `clear` | **RESOLVED (ported)** | `IPlanner.Clear()` (base no-op); `VolcanoPlanner.Clear()` removes all rules and clears `_classOperands`/`_classes`/`_allSets`/`_digestToNode`/`_nodeToSubset`/`_prunedNodes`/the rule driver, and resets root/cluster/`_nextSetId`. (Relational-only state — materializations, lattices, provenance — has no analog.) |
| `registerImpl` converter post-merge `fixUpInputs` recheck | DIVERGENT-BUG | stale digest may slip through after a converter set-merge. |
| `setRoot` omitting `ensureRootConverters` | DIVERGENT-OK (false-positive) | Alembic runs `EnsureRootConverters` in `FindBestPlan` instead — net equivalent; the agent flagged it without that context. |
| `ChangeTraitsUsingConverters` | DIVERGENT-OK | reimplemented as BFS over a conversion graph vs Calcite's per-trait-index sequential convert; broader search, plausibly equivalent. |

### `RelSet` → `NodeSet` — ~25 members; findings:

| Calcite member | Verdict | Note |
|---|---|---|
| `getOrCreateSubset` → `addConverters` | **RESOLVED (ported)** | `NodeSet.GetOrCreateSubset(traits, required)` now follows Calcite: tracks `needsConverter` (set on subset creation, or when a subset first becomes required/delivered; cleared for `Convention.None`), and calls `AddConverters` when set. |
| `addConverters` | **RESOLVED (ported)** | `NodeSet.AddConverters(subset, required, useAbstractConverter)` seeds converters from each delivered subset to a new required one (and vice-versa): per-pair dedup via a `_conversions` set, `TraitSet.Difference` + `ITraitDef.CanConvert` to decide `needsConverter`, then an `AbstractConverter` (bottom-up: `useAbstractConverter = !TopDownOpt`) or `Convention.Enforce` (top-down), registered against the target subset. `IsEnforceDisabled` and `UseAbstractConvertersForConversion` are honoured. *(The pre-existing `VolcanoPlanner.EnsureRootConverters` now overlaps this for the root subset; harmless — duplicate abstract converters dedup by digest — and a candidate for later removal.)* |
| `obliterateRelNode` | GAP | `parents.remove(rel)` cleanup has no counterpart (small). |
| `mergeWith` | **RESOLVED (moved)** | now matches Calcite: orchestration in `VolcanoPlanner.Merge` (equiv-root resolution), migration in `NodeSet.MergeWith(planner, other)`; planner's `PropagateCostImprovements`/`FireRules`/`Rename`/`RemoveSet`/`MapNodeToSubset` exposed `internal` (the C# analog of Calcite's package-private access). *(The merge's P1 bugs — `isSmaller` direction, root re-point — remain a separate fix.)* |
| `rename` + `fixUpInputs` (bundled as `Reregister`) | **RESOLVED (split)** — *decomposition only* | Calcite's `rename(rel)` calls `fixUpInputs(rel)`; the parent re-point on merge had been fused into one `Reregister` method. Now split to match: `FixUpInputs(node)` re-points child subsets at their live sets and returns the rebuilt node (or `null` if unchanged — the immutable analog of Calcite's mutate-in-place + `changeCount>0`); `Rename(node)` calls it, then recomputes the digest, dedups, and merges on collision. The decomposition is faithful. **But see the new `fixUpInputs`/`rename` parent-back-link bug below — a deeper behavioral gap (pre-existing in the old `Reregister`) that the re-verification audit surfaced.** |
| `fixUpInputs`/`rename` parent back-links | **DIVERGENT-BUG** (found by re-verification) | Calcite's `fixUpInputs` moves the parent between the old and new child-sets' `parents` lists (`subset.set.parents.remove(rel)` / `newSubset.set.parents.add(rel)`), and `rename`'s collision branch removes the old rel's back-links and reassigns `best`. Alembic's `FixUpInputs`/`Rename` do **none** of this: the rebuilt node is registered via `NodeSet.Add` (which never links `Parents` — only `RegisterImpl` does), and the old node is dropped from `_nodeToSubset`/`Nodes`/`_digestToNode` yet **left in its children's `NodeSet.Parents`**. `Parents` is only ever appended/read, never pruned, so a later child-set cost improvement walks the stale parent and hits the unguarded `_nodeToSubset[parent]` in `PropagateCostImprovements` → `KeyNotFoundException`; rebuilt parents also miss subsequent cost propagation. Latent (no current test triggers the sequence). Pre-existing in the old `Reregister`; not a regression from the split. |

### `VolcanoRuleCall` → `VolcanoRuleCall` — findings:

| Calcite member | Verdict | Note |
|---|---|---|
| `onMatch` | **RESOLVED (ported)** | `OnMatch` now runs the guard loop over the bound nodes before firing: skips the match if a node has no subset (`GetSubset` null — removed during a rename), its set was merged away (`Set.EquivalentSet != null`), it was removed from its subset (`!subset.Contains(rel)`), or it is pruned. Fixes firing a rule on a node already removed/merged (theme A/B). |
| `transformTo(rel, equiv)` | **RESOLVED (ported)** | the `equiv` map overload is now present: `TransformTo(equivalent, IReadOnlyDictionary<INode,INode>)` registers each map entry via `EnsureRegistered(key, value)` before the root, so a rule can declare secondary equivalences in one call. Also added the faithful guard — a transformation rule may not produce an `IPhysicalNode`. (Calcite's hint-propagation `handler` overload is RELATIONAL and intentionally omitted.) |
| `matchRecurse` `RelSubset.class` / `setChildRels` branches | DIVERGENT-OK | the omitted subset-operand + unused `childRels` paths — already tracked in §9. |

### `VolcanoRuleMatch` → `VolcanoRuleMatch` — `allNotNull` constructor null-check dropped (GAP, low: callers bind real nodes). Dedup via `Equals`/`GetHashCode` is FAITHFUL to the digest's purpose.

### `TopDownRuleDriver` → `TopDownRuleDriver` — **no bugs/gaps.** All tasks FAITHFUL; the only divergences are the already-documented metadata lower-bound, materialization roots, and timeout omissions (DIVERGENT-OK).

### Rule queues / drivers — findings:

| Class · member | Verdict | Note |
|---|---|---|
| `RuleQueue.skipMatch` (+ iterative `popMatch`) | **RESOLVED (ported)** | `SkipMatch` now skips a match when any bound node is pruned, or when the same subset repeats along a root-to-leaf operand path (a cycle — a node consuming its own output, via `HasDuplicateSubsetOnPath`). `IterativeRuleQueue.PopMatch` now loops, dropping skipped matches (`TopDownRuleQueue` already called it). |
| `IterativeRuleQueue` phase/importance ranking, `MatchList` | DIVERGENT-OK | FIFO + `_seen` replaces the phase/substitution-priority queue — intentional simplification. |
| `RuleQueue.clear()` `boolean`→`void` | DIVERGENT-OK | the "was non-empty" signal is dropped consistently; verify no caller needed it. |
| `IterativeRuleDriver.drive` post-match `canonize()` | **DIVERGENT-BUG (verify)** | Calcite re-canonizes the root subset after every match (root can go stale after a set merge); Alembic's `Drive` doesn't. Given Alembic *does* merge sets, `_root` may end up pointing at a dead subset — needs verification (theme B). |

### `AbstractConverter` / `ExpandConversionRule` / `IPhysicalNode` — findings:

| Member | Verdict | Note |
|---|---|---|
| `AbstractConverter.explainTerms` | GAP (display) | Calcite emits the enforced trait into plan output; Alembic has no override (inherits the input-only terms). Cosmetic. |
| `ExpandConversionRule.INSTANCE` / `Config` | DIVERGENT-OK | singleton/Config plumbing replaced by a public ctor + inline operand. |
| `IPhysicalNode.passThrough`/`derive` | FAITHFUL | compose via `IPlanner.Convert` (= `RelOptRule.convert`). |

### `HepPlanner` → `HepPlanner` — ~56 members; findings (PORT.md's "method-audited" claim was optimistic):

| Calcite member | Verdict | Note |
|---|---|---|
| `matchOperands` SOME (default) | **DIVERGENT-BUG (verify)** | Calcite's HEP matcher accepts `childRels.size() >= n` (binds the first n); Alembic's `MatchOperand` requires exact `Children.Length == operand.Children.Length`. Alembic is stricter — rejects matches Calcite accepts. Verify against the operand model. |
| `matchOperands` UNORDERED | **DIVERGENT-BUG (verify)** | Calcite allows one child per operand / `matchAnyChildren`; Alembic requires an exact-size bijection (`MatchUnordered`). Stricter. |
| `applyRule` converter force-conversion guard | **DIVERGENT-BUG** | Calcite skips `doesConverterApply`/`parentTrait` when a non-guaranteed converter is force-converted (`isGuaranteed() \|\| !forceConversions`); Alembic always checks + sets the trait — can suppress forced conversions. |
| `contractVertices` large-plan recursive merge | **DIVERGENT-BUG** | Alembic omits the `!noDag && largePlanMode` recursive parent-path merge — duplicate parent subexpressions aren't contracted in large-plan DAG mode. |
| `applyRule` CommonRelSubExprRule parents | DIVERGENT-BUG (verify) | Calcite collects the parent list and passes it to the call; Alembic checks parent count but never passes parents to `HepRuleCall`. |
| no-arg / `(program, context)` ctors, `clear()` override | **RESOLVED (ported, minus Context)** | added `HepPlanner(program, ICostFactory)` and a no-arg `HepPlanner()` (empty program, large-plan mode + fired-rules cache on, for multi-phase reuse); `Clear()` override = `base.Clear()` + `ClearRules()`. The `(program, Context)` and `onCopyHook` variants have no analog — Alembic has no `Context`/copy-hook. |
| `onCopyHook`/`onCopy` | MISSING (RELATIONAL) | copy-notification hook absent. |
| many asserts / `dumpGraph` / `assertNoCycles` / metadata-cache clears | DIVERGENT-OK | DEBUG/METADATA omissions. |

### `HepProgram` / `HepProgramBuilder` — **no bugs.** 16/18 FAITHFUL; only DIVERGENT-OK (`ImmutableList`→`ImmutableArray`, `Class<R>`→generic `<TRule>`, `checkArgument`→`InvalidOperationException`).

### `HepInstruction` (+ nested) / `HepState` — findings:

| Member | Verdict | Note |
|---|---|---|
| `SubProgram.prepare` | DIVERGENT-BUG (verify) | Calcite returns `subProgram.prepare(px)` directly (its `SubProgram.State` is dead code); Alembic returns a `SubProgram.State` wrapper that delegates to `ExecuteSubProgram`. Different path at the sub-program boundary — verify the wrapper is equivalent. |
| `BeginGroup.EndGroup` field | DIVERGENT-OK | `new` keyword hides the nested type name; cosmetic. |

### `HepRelVertex` → `HepNodeVertex` — findings:

| Member | Verdict | Note |
|---|---|---|
| `explain` | DIVERGENT-OK | Calcite's vertex is transparent (delegates explain to the wrapped node); Alembic prints the vertex wrapping the node. Internal-only; final plans are stripped. |
| `getDigest` (`"HepRelVertex(rel)"`) | DIVERGENT-OK | no override; the `AbstractNode` digest (with the `current` input term) stands in. |
| `deepHashCode` | DIVERGENT-OK | identity hash vs `getId()` — equivalent (one id per instance). |

### `HepRuleCall` — `transformTo` drops `verifyTypeEquivalence` (RELATIONAL row-type check) + hint propagation; the `equiv` map is unused by Calcite's HEP anyway → **DIVERGENT-OK**. `results`/`getResults`/ctor FAITHFUL.

### `RelOptRule` → `Rule` / `RelOptRuleOperand` → `RuleOperand` — findings (flatten/solve-order/matches all FAITHFUL):

| Member | Verdict | Note |
|---|---|---|
| `RelOptRule.equals`/`hashCode` (by description+class+operand) | **RESOLVED (ported)** | `Rule.GetHashCode` = `Description.GetHashCode`; `Rule.Equals` = same type + same `Description` + equal root `Operand`. Drives the `AddRule` dedup below. |
| `RuleOperand.equals`/`hashCode` | **RESOLVED (ported)** | by `MatchedClass` + `Trait` + child operands (recursive), as Calcite — predicate and child policy are excluded from identity (matching them is the rule's job). |
| `ConverterRelOptRuleOperand` (same-`TraitDef` `matches` guard) | DIVERGENT-OK (minor) | `ConvertOperand` yields a plain `RuleOperand` without the n²-guard override. |
| `UNORDERED` child-count assert | DIVERGENT-OK | dev-only invariant dropped. |

### `RelOptRuleCall` → `RuleCall` — findings:

| Member | Verdict | Note |
|---|---|---|
| `operand0` / `getOperand0()` | **RESOLVED (moved)** | Calcite's `operand0` lives on the base `RelOptRuleCall`; ours had been declared on `VolcanoRuleCall`. Now moved to the `RuleCall` base, and `Rule` is *derived* from it (`Rule = operand0.Rule`) as Calcite does — so the base ctor takes the seed operand, not the rule. All subclasses pass an operand0: `VolcanoRuleMatch`/`DeferringRuleCall` carry the real seed operand; `HepRuleCall` and `VolcanoRuleMatch`'s bound-node ctor pass `rule.Operand` (the root). Faithful. |
| `transformTo(rel, equiv)` | **RESOLVED (ported)** | added on the base `RuleCall` as `TransformTo(equivalent, IReadOnlyDictionary<INode,INode>)`, with the no-arg form delegating to it; `VolcanoRuleCall` registers the map entries, `HepRuleCall` ignores them (single best plan). See the `VolcanoRuleCall` row. |
| `id`/`nextId` | GAP (minor) | per-call id (ordering/dedup) not ported. |
| `getChildRels`/`getParents`/`builder`/`getMetadataQuery`/`isRuleExcluded` | MISSING | RELATIONAL/METADATA/BUILDER. |

### `ConverterRule` / `Converter` / `ConverterImpl` / `TraitMatchingRule` — findings:

| Member | Verdict | Note |
|---|---|---|
| `ConverterRule.isGuaranteed()` default | **DIVERGENT-BUG** | Calcite defaults **`false`** (not guaranteed — the safe default); Alembic defaults **`true`**. A converter that doesn't override now claims it always converts, which changes whether it fires eagerly vs only bottom-up via `TraitMatchingRule`. Inverted semantics. |
| `ConverterRule.onMatch` in-trait re-check | DIVERGENT-OK | operand already constrains to `Source`; re-check redundant. |
| `getTraitDef()` | GAP (minor) | no dimension accessor (callers can read `Source.TraitDef`). |
| `TraitMatchingRule` operand build | DIVERGENT-OK | re-applies the converter operand's predicate (Calcite doesn't); tighter but consistent. |

### `RelOptPlanner` → `IPlanner` + `AbstractPlanner` — findings:

| Member | Verdict | Note |
|---|---|---|
| `addRule` | **DIVERGENT-BUG** | drops description-keyed dedup, the unique-description guard, and the bool add/no-add return — duplicate rules silently accumulate in `_rules`. |
| `setRuleDescExclusionFilter`/`isRuleExcluded`, `getRuleByDescription` | **RESOLVED (ported)** | `AbstractPlanner` now keeps a `Dictionary<string,Rule>` description map: `AddRule` returns `bool` and rejects a duplicate description (no-op if equal, throws if a different rule collides); `GetRuleByDescription`, `SetRuleDescExclusionFilter(Regex?)`, and `IsRuleExcluded(Rule)` are present, with the exclusion check wired into `VolcanoRuleCall.OnMatch` and `HepPlanner.ApplyRule`. *(Re-verification nit: `IsRuleExcluded` uses unanchored `Regex.IsMatch`, whereas Calcite's `Matcher.matches()` is full-string-anchored; identical for Calcite's always-anchored usage, but a user-supplied unanchored pattern would behave as a substring match. Harmless today; anchor with `^…$` if it ever matters. Calcite's second, hint-driven `ruleCall.isRuleExcluded()` is intentionally not modelled — no hints subsystem.)* |
| `clear()` / `clearRelTraitDefs()` | **RESOLVED (ported)** | `Clear()` (above) + `ClearTraitDefs()` on `AbstractPlanner` (clears `_traitDefs` and resets the empty-trait-set cache). Implemented on the base because Alembic's trait-def registry lives there, rather than Calcite's base-no-op/Volcano-override split. |
| `fireRule` orchestration (checkCancel + exclusion + onMatch dispatch) | DIVERGENT-OK | base keeps only the listener split (`FireRuleAttempted`); `onMatch` dispatch is in Hep/Volcano. `checkCancel`/exclusion absent. |
| `emptyTraitSet` / `addRelTraitDef` / listeners / cost factory | DIVERGENT-OK / FAITHFUL | defaults-population + convention seeding folded into the base (consolidation), with a freeze guard (EXTRA, benign). |

### `RelOptCluster` → `Cluster` / `RelOptListener` → `IPlannerListener` / `RelOptUtil` → `PlanUtil` — findings:

| Member | Verdict | Note |
|---|---|---|
| `RelEquivalenceEvent.equivalenceClass` + `isPhysical` | GAP (minor) | `NodeEquivalenceEvent` drops both — a listener can't tell which class, or logical-vs-physical. |
| `RelOptUtil.toString` | DIVERGENT-OK | faithful shape (news up `NodeWriterImpl`, calls `Explain`); drops the `SqlExplainLevel` param, adds `.TrimEnd()`. |
| `Cluster` `getPlanner`/`traitSet`/`traitSetOf` | FAITHFUL | the only in-scope cluster members; the rest (type factory, rex builder, metadata query, hints) correctly RELATIONAL/METADATA. |

### `RelTrait` → `ITrait` / `RelTraitDef` → `ITraitDef`+`TraitDef` — findings:

| Member | Verdict | Note |
|---|---|---|
| `RelTraitDef.canonize` interner | **DIVERGENT-BUG** | Calcite uses a **weak** interner (`Interners.newWeakInterner`) so canonized traits can be GC'd; Alembic's `ConcurrentDictionary` holds **strong** refs forever — unbounded growth / leak. (The concurrency fix was needed; the fix should have used a weak concurrent interner.) |
| `RelTrait.satisfies` | DIVERGENT-OK | Calcite leaves it abstract; Alembic gives a default `Equals` body (the reflexive base case). |

### `RelTraitSet` → `TraitSet` — findings:

| Member | Verdict | Note |
|---|---|---|
| `getTrait(RelTraitDef)` | **DIVERGENT-BUG** | typed `Get<T>(def)` indexes `_traits[FindIndex(def)]` and **throws `IndexOutOfRange` when the dimension is absent**; Calcite returns `null`. (The non-generic `Get(ITraitDef)` returns the default — inconsistent.) |
| `replace(RelTrait)` (ignore-if-absent) | **RESOLVED (ported)** | `TraitSet.Replace(ITrait)` infers the dimension from the trait, substitutes if present, returns `this` if absent — Calcite's `replace(RelTrait)` exactly (distinct from `Plus`, which adds an absent dimension). |
| `Replace<T>(def, value)` | DIVERGENT-OK | doesn't canonize `value` (Calcite does); relies on callers passing canonical traits. |
| `simplify` / `allSimple` | **RESOLVED (ported)** | `TraitSet.Simplify()` (one-member composite → its member; many-member → dimension default) and `TraitSet.AllSimple()`. A non-generic `ICompositeTrait` (`Count`/`TraitAt`) was added so the set can flatten composites without knowing the member type (the analog of Calcite's `RelCompositeTrait`). |
| `difference` | **RESOLVED (ported)** | `TraitSet.Difference(traitSet)` returns the argument's traits that differ position-for-position — needed by `NodeSet.AddConverters` to decide which dimensions a converter must bridge. |
| `replaceIf`/`replaceIfs`/`plusAll`/`merge` | **RESOLVED (ported)** | `TraitSet.ReplaceIf<T>(def, Func<T?>)`, `ReplaceIfs<T>(def, Func<IReadOnlyList<T>?>)`, `PlusAll(IEnumerable<ITrait>)`, `Merge(TraitSet)` — supplier-driven conditional replace and bulk add, matching Calcite. |

### `Convention` / `ConventionTraitDef` / `CompositeTrait` — findings:

| Member | Verdict | Note |
|---|---|---|
| `ConventionTraitDef.convert`/`canConvert`/`registerConverterRule` + `ConversionData` graph | DIVERGENT-OK (architectural) | Calcite implements convention conversion as a per-planner `DirectedGraph` of conventions + shortest-path inside `ConventionTraitDef`; Alembic ports none of it and instead drives convention conversion through rules (`ExpandConversionRule` → `VolcanoPlanner.ChangeTraitsUsingConverters`). Different mechanism, same capability — worth noting as a deliberate relocation, not a silent gap. |
| `CompositeTrait.of` | **DIVERGENT-BUG** | Calcite canonizes each member and the whole composite; Alembic's `Of` canonizes nothing (returns `traits[0]` raw / `new CompositeTrait` un-interned) — breaks the interning identity invariant. |
| `Convention.Impl` placement (nested) → top-level `Convention` | **RESOLVED (decision: keep top-level)** | Calcite's concrete convention is `Convention.Impl`, a class nested in the `Convention` interface. Both are extension points for adapters: the interface is implemented directly (`EnumerableConvention`, `BindableConvention`, `InterpretableConvention`) and `Impl` is subclassed as a convenience base (`JdbcConvention extends Convention.Impl`). Alembic splits these into `IConvention` (the interface others implement) + a top-level `Convention` (the extensible concrete base others subclass). **Kept top-level by decision**: a top-level, openly-subclassable class is the idiomatic C# extension point; nesting it inside the interface (the Java form) would only hinder that. This is the chosen Alembic structure, not a deviation to reconcile later. |
| `Convention.Impl` `equals`/`hashCode` | DIVERGENT-OK (intentional) | Calcite's `Impl` uses reference identity (conventions are singletons); Alembic compares by name. Deliberate, documented. |

### `RelOptCost`/`Cost`/`VolcanoCost` — **no bugs.** rowCount dropped → comparison shifts to `Cpu` (DIVERGENT-OK); the `TINY` ctor-arg reorder is correct (FAITHFUL). `multiplyBy`/`divideBy`/`isEqWithEpsilon` now **RESOLVED (ported)** on `ICost`/`Cost`/`VolcanoCost`: `MultiplyBy` scales components (infinite stays infinite); `DivideBy` is the geometric mean of per-component ratios over the non-zero/finite components (1.0 if none); `IsEqWithEpsilon` compares each component within 1e-5.

### `RelNode`/`AbstractRelNode` → `INode`/`AbstractNode` — findings (structural core FAITHFUL):

| Member | Verdict | Note |
|---|---|---|
| `onRegister` | **DEFERRED** | Calcite's `RelNode.onRegister` is a node method (register inputs + copy + recompute); ours is `VolcanoPlanner.OnRegister`, *fused* with convention coercion that Calcite doesn't do in `onRegister`. Coupled to the P2 convention-handling divergence — will be moved to `INode.OnRegister` when that theme is addressed, not before. (Not justified — pending.) |
| `DigestWriter.Done` input rendering | DIVERGENT-OK (cosmetic) | Calcite renders inputs as `typeName#id`; Alembic renders `typeName` only (no id). Affects the *display* digest string for same-type siblings, but dedup uses `DeepEquals` (recurses actual nodes), so correctness is unaffected. |
| `deepHashCode` seed | DIVERGENT-OK | Alembic folds type + term names too (Calcite folds values only) — stronger, consistent with its `DeepEquals`. |
| `computeSelfCost` default | DIVERGENT-OK | tiny constant vs Calcite's row-count-based default (METADATA). |

### `SingleRel`/`BiRel`/`RelWriter`/`RelWriterImpl`/`RelDigest` — findings:

| Member | Verdict | Note |
|---|---|---|
| `RelWriter.itemIf` | GAP (minor) | no conditional-item method; node `ExplainTerms` overrides must use an `if` instead. |
| `RelWriter.getDetailLevel` / `nest` / `expand` | GAP / DIVERGENT-OK | no detail-level concept; `nest`/`expand` inert for the ported path. |
| `RelWriterImpl.explain_` | DIVERGENT-OK | `=value` vs Calcite's `=[value]`, type-name source differs — cosmetic plan-string format. SingleNode/BiNode `explainTerms` FAITHFUL. |

### Graph core (`DirectedGraph`/`DefaultDirectedGraph`/`DefaultEdge`/`Graphs`) — **no bugs.** `vertexSet` (ordered view vs live keySet), `removeAllVertices` (always majority-strategy vs Calcite's 0.35 threshold), `predecessorListOf` (copy vs live view) all DIVERGENT-OK with identical observable results. `FrozenGraph`/`makeImmutable` intentionally absent (unused by HEP).

### Graph iterators + `Pair` — findings (traversal order FAITHFUL throughout):

| Member | Verdict | Note |
|---|---|---|
| `TopologicalOrderIterator.findCycles` | GAP (minor) | cycle-detection helper not ported (HEP's `assertNoCycles` also dropped as DEBUG). |
| `Pair` `Comparable`/`Map.Entry`/`of(Map.Entry)` | DIVERGENT-OK | interfaces + entry-ctor intentionally dropped; list/map helpers (`zip`/`toMap`/…) UNUSED-HELPER.

---

### Audit summary — triage

Across **all ~45 classes**, the algebra/graph/cost/program layers are faithful; the divergences concentrate in the **un-audited Volcano core** and a few **cross-cutting semantic defaults**. Prioritized:

**P1 — behavior-affecting bugs to fix:**
1. `VolcanoPlanner.PropagateCostImprovements` — naive recursion (cycle/stack-overflow risk, possibly-wrong propagation).
2. `ConverterRule.IsGuaranteed` default **inverted** (`true` vs Calcite `false`) — changes converter firing.
3. `TraitSet.Get<T>` throws on an absent dimension instead of returning null.
4. `VolcanoPlanner.Merge` — missing `isSmaller` direction + root re-point/`ensureRootConverters`.
   - 4b. `VolcanoPlanner.FixUpInputs`/`Rename` — no parent back-link maintenance (stale parent left in `NodeSet.Parents`; latent `KeyNotFoundException` in `PropagateCostImprovements`; missed cost propagation to rebuilt parents). Found by re-verification; fix alongside the merge/rename work. See the `fixUpInputs`/`rename` row above.
5. `VolcanoPlanner.EnsureRegistered` — skips the equivalence-driven set merge when already registered.
6. `TraitDef.Canonize` — strong-ref interner (leak) vs Calcite's weak interner.
7. `CompositeTrait.Of` — no canonization (breaks interning identity).
8. HEP matcher (`MatchOperand` SOME / `MatchUnordered`) stricter than Calcite — rejects valid matches *(verify)*.

**P2 — themes / structural gaps:**
- ~~**Node pruning** absent (drives the hollow `VolcanoRuleCall.OnMatch` staleness guard, `skipMatch`).~~ — **RESOLVED**: `prunedNodes`/`Prune`/`CheckPruned` machinery + the `OnMatch` staleness guard + `SkipMatch` are ported (see the `VolcanoPlanner`/`VolcanoRuleCall`/`RuleQueue` rows).
- ~~**`transformTo(equiv)`** map dropped port-wide~~ — **RESOLVED**: the equiv-map overload is ported (see `VolcanoRuleCall`/`RuleCall` rows).
- **Canonization**: `EquivRoot` returns the set, never the leader subset; root not re-canonized after merge.
- ~~**Per-subset converter seeding** (`RelSet.getOrCreateSubset → addConverters`) absent~~ — **RESOLVED**: `NodeSet.AddConverters` ported (see the `RelSet` rows).
- ~~**Rule registry**: `AddRule` no dedup/unique-description; no `Rule.equals`/exclusion-filter.~~ — **RESOLVED**: description-keyed registry with dedup, `Rule`/`RuleOperand` value equality, and the exclusion filter (see the `RuleOperand`/`AbstractRelOptPlanner` rows).
- `GetCost` none-convention infinite/nudge; HEP `applyRule` forced-conversion guard; HEP large-plan `contractVertices` merge.

**P3 — minor/cosmetic:** `RelWriter.itemIf`/`getDetailLevel`, `findCycles`, `obliterateRelNode`, `allNotNull`, listener event payloads (`equivalenceClass`/`isPhysical`), digest input-string id, `VolcanoRuleMatch`/cost helper gaps.

**Deliberate, verified-OK:** convention conversion is rule-driven (not `ConventionTraitDef`'s graph); FIFO queue vs importance ranking; metadata-disabled lower-bound pruning; row-count-free cost; `Convention` by-name equality.
