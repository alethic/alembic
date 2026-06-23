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
| `getInput(int)` | `Children[i]` | |
| `copy(traitSet, inputs)` | `Copy(traits, children)` | |
| `getConvention()` | `Convention` | default member |
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
| `itemIf` | `ItemIf` | conditional item (default method) |
| `getDetailLevel`, `nest`, `expand` | — | SQL-EXPLAIN-only formatting; no analog |

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

### `RelTraitDef<T>` → `TraitDef` (base) + `TraitDef<T>` (`src/Alembic/Plan/TraitDef.cs`)

Calcite's `RelTraitDef<T>` is a single abstract class, used raw where the trait type isn't known. C# has no
raw generics, so it is rendered as a non-generic abstract base class `TraitDef` (the non-generic handle) plus
the strongly-typed `TraitDef<T> : TraitDef`. (There is no interface — an earlier `ITraitDef` was removed, since
Calcite has no such interface.)

| Calcite | Alembic | Notes |
|---|---|---|
| `getSimpleName()` | `Name` | abstract on base `TraitDef` |
| `getDefault()` | `Default` | abstract `ITrait` on base; covariantly re-typed `TTrait` on `TraitDef<T>` |
| `canConvert(planner, from, to)` | `CanConvert(planner, from, to)` | virtual on base `TraitDef` |
| `convert(planner, rel, toTrait, allowInfinite)` | `Convert(planner, node, toTrait, allowInfiniteCostConverters)` | virtual on base `TraitDef` |
| `getTraitClass()` | `TraitClass` | abstract on base; `typeof(TTrait)` on `TraitDef<T>` |
| `canonize(t)` | `Canonize(t)` | base `TraitDef`; per-def `WeakInterner<ITrait>` (weak values, as Calcite's `newWeakInterner`) |
| `registerConverterRule` / `deregisterConverterRule` | `RegisterConverterRule` / `DeregisterConverterRule` | virtual no-ops on base, as in Calcite |
| `multiple()` | `Multiple` | virtual `false` on base |

### `RelTraitSet` → `TraitSet` (`src/Alembic/Plan/TraitSet.cs`)

| Calcite | Alembic | Notes |
|---|---|---|
| `createEmpty()` | `CreateEmpty()` | |
| `getTrait(traitDef)` | `Get<T>(def)` / `Get(def)` | |
| `getTraits(index)` (multi) | `GetList<T>(def)` | |
| `replace(index/def, trait)` | `Plus(trait)` / `Replace<T>(def, value)` | `Plus` adds-or-replaces |
| `replace(RelTrait)` (ignore-if-absent) | `Replace(ITrait)` | substitutes if the dimension is present, else returns `this` (distinct from `Plus`, which adds) |
| `replace(List<T>)` (multi) | `Replace<T>(def, values)` | folds to a `CompositeTrait` |
| `replaceIf(def, supplier)` / `replaceIfs(def, supplier)` | `ReplaceIf<T>(def, Func<T?>)` / `ReplaceIfs<T>(def, Func<IReadOnlyList<T>?>)` | supplier-driven conditional replace |
| `plusAll(traits)` / `merge(traitSet)` | `PlusAll(IEnumerable<ITrait>)` / `Merge(TraitSet)` | bulk add |
| `simplify()` / `allSimple()` | `Simplify()` / `AllSimple()` | flatten composites / composite-free check |
| `difference(traitSet)` | `Difference(traitSet)` | the argument's traits that differ, position for position |
| `getConvention()` | `Convention` | |
| `satisfies(that)` | `Satisfies(required)` | |
| `canonize(t)` | `Canonize(t)` | delegates to the def; sets are also interned via `Cache` |
| `contains` / `comprises` / `matches` | `Contains` / `Comprises` / `Matches` | |
| iterable (`Iterable<RelTrait>`) | `IEnumerable<ITrait>` (`GetEnumerator`) | enumerates the traits in order |
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

### `RelCompositeTrait<T>` → `CompositeTrait` (base) + `CompositeTrait<T>` (`src/Alembic/Plan/CompositeTrait.cs`)

| Calcite | Alembic | Notes |
|---|---|---|
| `of(def, traits)` | `Of(def, traits)` | |
| `traitList()` | `Traits` | |
| `getTraitDef()` | `TraitDef` | |
| `satisfies(trait)` | `Satisfies(other)` | |
| `trait(i)` / `size()` | `TraitAt(i)` / `Count` (on non-generic base `CompositeTrait`) | a `TraitSet` flattens composites via the base class without knowing the member type |

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
| `getCpu()` / `getIo()` | `Cpu` / `Io` | on the interface; `Cost` reports `0` for both (as `RelOptCostImpl` does — the scalar is used only to compare/combine) |
| `getRows()` | — | row count dropped (relational; not a database) |
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

Scalar cost mirroring `RelOptCostImpl` member-for-member over a single `double`: `Cpu`/`Io` report `0`;
comparison and arithmetic (`plus`/`minus`/`multiplyBy` as plain `new Cost(...)`, `divideBy` as plain
division) use the scalar, matching Calcite exactly (no infinite short-circuits). No `getRows()`
(relational) and no public cost constants — the named cost singletons live on `VolcanoCost`. The inner
`Factory` makes each named cost inline (`new Cost(0.0)`, `new Cost(double.PositiveInfinity)`, …).

### `VolcanoCost` → `VolcanoCost` (`src/Alembic/Plan/Volcano/VolcanoCost.cs`)

| Calcite | Alembic | Notes |
|---|---|---|
| `cpu` / `io` (+ `rowCount`) | `Cpu` / `Io` | no `rowCount` |
| `isLt` (compares cpu) | `IsLessThan` | compares on cpu |
| `Factory` (`VolcanoCostFactory`) | `VolcanoCost.Factory` | |
| `isInfinite`/`isLe`/`plus`/`minus`/`multiplyBy`/`divideBy`/`isEqWithEpsilon`/`equals` | same names | `divideBy`/`isEqWithEpsilon` over cpu+io only (rowCount dropped) |

---

## 4. Planner core

### `RelOptPlanner` (interface) → `IPlanner` + `AbstractPlanner` (`src/Alembic/Plan/IPlanner.cs`, `AbstractPlanner.cs`)

| Calcite | Alembic | Notes |
|---|---|---|
| `setRoot(rel)` | `SetRoot(node)` | |
| `getRoot()` | `Root` | on `IPlanner` |
| `changeTraits(rel, toTraits)` | `ChangeTraits(node, toTraits)` | |
| `findBestExp()` | `FindBestPlan()` | |
| `addRule(rule)` | `AddRule(rule)` | returns `bool`; rejects a duplicate description (description-keyed registry) |
| `removeRule(rule)` | `RemoveRule(rule)` | on `IPlanner` |
| `getRuleByDescription(desc)` | `GetRuleByDescription(desc)` | `protected` on `AbstractPlanner` |
| `setRuleDescExclusionFilter(pattern)` / `isRuleExcluded(rule)` | `SetRuleDescExclusionFilter(Regex?)` / `IsRuleExcluded(rule)` | description-regex exclusion filter; consulted in `VolcanoRuleCall.OnMatch` / `HepPlanner.ApplyRule` |
| `prune(rel)` | `Prune(node)` | base no-op; `VolcanoPlanner` marks the node pruned |
| `addRelTraitDef(def)` | `AddTraitDef(def)` | |
| `getRelTraitDefs()` | `TraitDefs` | |
| `getCostFactory()` | `CostFactory` | |
| `addListener(l)` | `AddListener(l)` | |
| `setTopDownOpt(b)` | `VolcanoPlanner.SetTopDownOpt(b)` | selects the top-down (Cascades) driver |
| `register(rel, equiv)` / `ensureRegistered(rel, equiv)` | `VolcanoPlanner.Register(node, equiv)` / `EnsureRegistered(node, equiv)` | HEP treats these as no-ops |
| `clear()` | `Clear()` | base no-op; `VolcanoPlanner`/`HepPlanner` reset their search/rule state |
| `clearRelTraitDefs()` | `ClearTraitDefs()` | clears the trait-def registry + the cached empty trait set |
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
| `RelEvent` / `RelChosenEvent` / … (nested) | `PlannerEvent` / `NodeChosenEvent` / … (nested) | `getRel()`→`Node`, `getRuleCall()`→`RuleCall`, `isBefore()`→`before` |
| `RelEquivalenceEvent.getEquivalenceClass()` / `isPhysical()` | `NodeEquivalenceEvent.EquivalenceClass` / `IsPhysical` | set id + physical-convention flag |

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
| `equals` / `hashCode` | `Equals` / `GetHashCode` | equal = same type + description + operand; hash = description (planner requires unique descriptions) |
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
| `equals` / `hashCode` | `Equals` / `GetHashCode` | by matched class + trait + child operands (predicate/policy excluded, as Calcite) |


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
| `id` / `nextId` | `Id` | per-call id, assigned from a static counter in creation order |
| `getChildRels`, `getParents`, `getMetadataQuery`, `builder()`, `isRuleExcluded` | — | metadata / builder |

### `ConverterRule` (abstract) → `ConverterRule : Rule` (`src/Alembic/Plan/Rules/ConverterRule.cs`)

| Calcite | Alembic | Notes |
|---|---|---|
| `getInTrait()` | `Source` | generalized from `Convention` to any `ITrait` |
| `getOutTrait()` / `getOutConvention()` | `Target` | |
| `getTraitDef()` | `TraitDef` (→ `Source.TraitDef`) | the dimension converted on |
| `convert(rel)` | `Convert(node)` | |
| `isGuaranteed()` | `IsGuaranteed` | default `false` (Calcite's); always-convert converters override to `true` |
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
| ctors `(program)`, `(program, …, costFactory)`, no-arg `()` | `HepPlanner(program)`, `HepPlanner(program, ICostFactory)`, `HepPlanner()` | the no-arg form mirrors Calcite's: empty program, large-plan mode + fired-rules cache on. `(program, Context)` / `onCopyHook` have no analog |
| `clear()` | `Clear()` | `base.Clear()` + `ClearRules()` (materializations are relational, omitted) |
| `clearRules()` | `ClearRules()` | |
| `setEnableFiredRulesCache(b)` | `SetEnableFiredRulesCache(b)` | + `NoDag`, `LargePlanMode` properties |
| `executeProgram(program)` / `executeProgram(program, state)` | `ExecuteProgram(program)` / `ExecuteProgram(program, state)` | |
| `executeMatchLimit`/`MatchOrder`/`RuleInstance`/`RuleLookup`/`RuleClass`/`RuleCollection`/`ConverterRules`/`CommonRelSubExprRules`/`SubProgram`/`BeginGroup`/`EndGroup` | identical set (`ExecuteXxx`) | |
| `applyRules` / `depthFirstApply` / `getGraphIterator` / `applyRule` | same | incl. large-plan `HepVertexIterator` branch |
| `matchOperands(...)` | `Match` / `MatchOperand` | sees through vertices; SOME binds first n (`>=`), UNORDERED is `matchAnyChildren` |
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
| `changeTraits(rel, traits)` | `ChangeTraits(node, traits)` | converts to the trait subset; no root side-effect (compose with `SetRoot`, as Calcite does); also `RelOptRule.convert`'s target |
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
| `setLocked(b)` / `locked` | `SetLocked(b)` / `_locked` | a locked planner rejects `AddRule` |
| `clear()` | `Clear()` | removes rules + clears the dispatch table, sets, digest/subset maps, pruned set, rule driver |
| `prune(rel)` / `prunedNodes` / `checkPruned` | `Prune` / `IsPruned` / `CheckPruned` | node pruning; consulted by `VolcanoRuleCall.OnMatch` and `RuleQueue.SkipMatch` |
| `getSubset(rel)` (nullable) | `GetSubset(node)` | nullable lookup (vs `GetSubsetNonNull`) |
| `merge(set1, set2)` / `isSmaller` | `Merge` / `IsSmaller` | swap (older/larger survives) + root re-point |
| `canonize(subset)` | `Canonize` | re-resolves a subset to its live set |
| `propagateCostImprovements(rel)` | `PropagateCostImprovements` | `propagateRels` map + cost-ordered `PriorityQueue` worklist |
| `rename` / `fixUpInputs` | `Rename` / `FixUpInputs` | re-points a renamed node's child subsets + parent back-links |
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
| `add(rel)` | `Add(node)` | entry point → `NodeSubset.Add` → `AddInternal` |
| `addInternal(rel)` | `AddInternal(node)` | raw node-list insert + representative `Rel` |
| `addConverters(subset, required, …)` | `AddConverters(subset, required, useAbstractConverter)` | per-subset converter/enforcer seeding |
| `obliterateRelNode(rel)` | `ObliterateNode(node)` | `Parents.Remove(node)` |
| `equivalentSet` | `EquivalentSet` | the merge-forwarding pointer |
| `exploringState` | `Exploring` | `ExploringState` enum |
| `mergeWith(...)` | `MergeWith(planner, other)` (orchestrated by `VolcanoPlanner.Merge`) | |
| `getChildSets(planner)` | `GetChildSets()` | the live sets this set's members consume; used by the merge swap decision |
| `getRelsFromAllSubsets` | — | |

### `RelSubset` → `NodeSubset` (`src/Alembic/Plan/Volcano/NodeSubset.cs`)

| Calcite | Alembic | Notes |
|---|---|---|
| `getBest()` / `bestCost` | `Best` / `BestCost` | |
| `getSet()` | `Set` | |
| `getOriginal()` / `getBestOrOriginal()` / `stripped()` | `GetOriginal()` / `GetBestOrOriginal()` / `Stripped` | `Stripped` is a default `INode` member (returns `this`) that a subset and a HEP vertex override |
| `getRels()` / `getRelList()` / `contains(rel)` | `GetRels()` (→ `IEnumerable`) / `GetRelList()` (→ `IList`) / `Contains(node)` | members of the (live) set satisfying this subset's traits |
| `add(rel)` | `Add(node)` | fires the equivalence-found event, then delegates to `NodeSet.AddInternal` |
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
| `onMatch()` | `OnMatch()` | base applies the rule (after the staleness guard: skips a match whose bound node lost its subset, had its set merged away, was removed from its subset, or was pruned); `DeferringRuleCall` overrides to enqueue |
| `transformTo(rel, equiv, handler)` | `TransformTo(equivalent)` / `TransformTo(equivalent, equiv)` | registers the equivalent (+ each `equiv` map entry); guards that a transformation rule may not produce an `IPhysicalNode`; the hint `handler` is relational, omitted |
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
| `isEnforcer()` | `IsEnforcer` (→ `true`) | |
| `explainTerms` | `ExplainTerms` | overridden to emit each enforced trait (`Item(trait.TraitDef.Name, trait)`) after the base terms |

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
| `findCycles()` | `FindCycles()` | drains the iterator; returns the un-emitted (cyclic) vertices |

### `Pair<T1, T2>` → `Pair<T1, T2>` (`src/Alembic/Util/Pair.cs`)

| Calcite | Alembic | Notes |
|---|---|---|
| `left` / `right` | `Left` / `Right` | |
| `of(left, right)` | `Pair.Of(left, right)` | on a non-generic companion class so member types infer (C# can't infer them through a generic type's static method) |
| `equals` / `hashCode` / `toString` | `Equals` / `GetHashCode` / `ToString` | value semantics (`hash = leftHash ^ rightHash`, `"<l, r>"`); used by `IPhysicalNode` |
| `Comparable` / `Map.Entry`, `zip`/`toMap`/`left(list)`/`right(list)`/… | — | the list/map utility helpers are not needed |

### `WeakInterner<T>` (`src/Alembic/Util/WeakInterner.cs`)

| Upstream | Alembic | Notes |
|---|---|---|
| Guava `Interners.newWeakInterner()` (used by `RelTraitDef.canonize`) | `WeakInterner<T>.Intern(value)` | weak-value interner backing `TraitDef.Canonize`; no Calcite class of its own. **TODO (§11):** rework into an exact line-by-line Guava port. |

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

**Provenance is recorded in code.** Every Calcite-derived type and member carries a
`[Provenance(className, member?)]` attribute — the authoritative, machine-readable record of what it
derives from (the surface tables in §1–8 mirror the annotated member→Calcite mappings). Its `Source`
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

- **`Cluster`**: its ctor (Calcite's `RelOptCluster` ctors are relational/package-private). **`IPlannerListener.PlannerEvent`**: `Source` (JDK `EventObject`).
- **`TraitDef` / `CompositeTrait`**: the non-generic abstract base classes themselves are an Alembic structural device — Calcite has a single generic class (`RelTraitDef<T>` / `RelCompositeTrait<T>`) used raw, which C# can't express, so the non-generic base + generic subclass split stands in for it. Their members map to the Calcite class (annotated), but the *split into two types* has no Calcite analog. Also Alembic-original: **`TraitDef`**'s `_interned` field. **`TraitSet`**: the explicit non-generic `IEnumerable.GetEnumerator` and the typed `Equals(TraitSet?)` overload (the private ctor, nested `Cache`, `ReplaceAt`, and `FindIndex` are now annotated to their `RelTraitSet` analogs). **`IMultipleTrait`**: the inherited `IComparable<>`. **`Convention`**: the single-arg ctor and `Equals`/`GetHashCode` (by-name — Calcite's `Impl` uses identity).
- **`RuleOperand`**: the convenience constructors (Calcite has only the two real ctors; ours add default-filling overloads). **`TraitMatchingRule`**: the `_converterRule` field.
- **`NodeSet`**: the `Cluster` property and the single-arg `GetOrCreateSubset(traits)`. **`NodeSubset`**: `LiveSet` (EXTRA — equiv-root resolution). **`VolcanoPlanner`**: `ChangeTraits` (the `setRoot`+`changeTraits` convenience), `RootSubset`, `CostEquals`, `OperandsFor`, the multi-step conversion BFS (`FindConversionPath`/`ConversionEdges`/`Reconstruct`/`ConversionStep`), and `_classes`/`_cluster`. **Rule queues/drivers**: the `_seen` dedup sets (replace Calcite's `MatchList`/`names`). **`ExpandConversionRule`**: its public ctor (replaces Calcite's `Config`/singleton).
- **`HepPlanner`**: the `NoDag` property (a get/set accessor with no Calcite analog — Calcite's `noDag` is a private ctor-set field), `EnsureSatisfies`, `ShallowEqual`, `RemoveFiredRules`, `Match`, and the nested **`FiredKey`** type (replaces Calcite's `ImmutableIntList` cache key). **`HepProgram`**: the `_program` back-ref. **`HepProgramBuilder`**: the `Check` helper. **`HepInstruction` states**: the explicit `Instruction` back-references (Java uses the implicit outer `this`).
- **Graph iterators** (`DepthFirst`/`BreadthFirst`/`Topological`/`HepVertexIterator`): the `IEnumerator` plumbing (`Current`, `Reset`, `Dispose`). **`Pair`**: the non-generic `Pair` companion (a C# type-inference workaround — Alembic-original type). **`DefaultDirectedGraph`**: the `VertexSetView`. **`DefaultEdge`**: the `DefaultEdgeFactory` (Calcite uses a lambda).
- **`WeakInterner`**: class-level provenance points to Guava `Interners.newWeakInterner` with `Source = ProvenanceSource.Other` (not Calcite); its members are un-annotated (the .NET implementation is hand-rolled, not a member-for-member port). **`ProvenanceAttribute`** / **`ProvenanceSource`**: Alembic-original infrastructure — no upstream analog.

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
| `propagateCostImprovements` | **RESOLVED (fixed)** | ported verbatim: a `Dictionary<INode,ICost>` (`propagateRels`, identity-keyed) + a `PriorityQueue<INode,ICost>` ordered by cost; reads the current cost from the map, walks `subset.GetParents()` (per-subset, not raw `set.Parents`), and the two `continue` guards match Calcite. No more recursion/stack-overflow. The one adaptation: .NET's `PriorityQueue` has no `remove`, so the decrease-key re-enqueues and the stale entry is harmlessly skipped on poll (the cost is read from the map, so a re-poll is a no-op) — Calcite's `remove`+`offer` and this produce identical processing. |
| `merge` | **RESOLVED (fixed)** | ported the swap (`GetChildSets` + `IsSmaller` → always merge the newer/smaller into the older/larger, or a child into its parent) and the root re-point (re-point `_root` to the survivor's subset for the root traits + re-run `EnsureRootConverters` when the absorbed set held the root). `OnSetMerged` stays in `MergeWith` (the swap just changes which object is `this`). |
| `ensureRegistered` | **RESOLVED (fixed)** | when the node is already registered and a known `equivalent` lives in a different set, the two sets are now merged; the result is `Canonize`d (re-resolved on the live set) — Calcite's `ensureRegistered` + `canonize`. |
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
| `addConverters` | **RESOLVED (ported)** | `NodeSet.AddConverters(subset, required, useAbstractConverter)` seeds converters from each delivered subset to a new required one (and vice-versa): per-pair dedup via a `_conversions` set, `TraitSet.Difference` + `TraitDef.CanConvert` to decide `needsConverter`, then an `AbstractConverter` (bottom-up: `useAbstractConverter = !TopDownOpt`) or `Convention.Enforce` (top-down), registered against the target subset. `IsEnforceDisabled` and `UseAbstractConvertersForConversion` are honoured. *(The pre-existing `VolcanoPlanner.EnsureRootConverters` now overlaps this for the root subset; harmless — duplicate abstract converters dedup by digest — and a candidate for later removal.)* |
| `obliterateRelNode` | **RESOLVED (ported)** | `NodeSet.ObliterateNode(node)` = `Parents.Remove(node)`, matching Calcite. (Its full wiring into the parent-back-link maintenance is part of the P1 4b fix.) |
| `mergeWith` | **RESOLVED (moved)** | now matches Calcite: orchestration in `VolcanoPlanner.Merge` (equiv-root resolution), migration in `NodeSet.MergeWith(planner, other)`; planner's `PropagateCostImprovements`/`FireRules`/`Rename`/`RemoveSet`/`MapNodeToSubset` exposed `internal` (the C# analog of Calcite's package-private access). *(The merge's P1 bugs — `isSmaller` direction, root re-point — are now fixed; see the `merge` row above.)* |
| `rename` + `fixUpInputs` (bundled as `Reregister`) | **RESOLVED (split)** — *decomposition only* | Calcite's `rename(rel)` calls `fixUpInputs(rel)`; the parent re-point on merge had been fused into one `Reregister` method. Now split to match: `FixUpInputs(node)` re-points child subsets at their live sets and returns the rebuilt node (or `null` if unchanged — the immutable analog of Calcite's mutate-in-place + `changeCount>0`); `Rename(node)` calls it, then recomputes the digest, dedups, and merges on collision. The decomposition is faithful. **But see the new `fixUpInputs`/`rename` parent-back-link bug below — a deeper behavioral gap (pre-existing in the old `Reregister`) that the re-verification audit surfaced.** |
| `fixUpInputs`/`rename` parent back-links | **RESOLVED (fixed)** | `Rename` now maintains `NodeSet.Parents` under the immutable rebuild: it drops the old node's back-links from its (live) child sets (`ObliterateNode`, one per input) and adds the rebuilt node's; on the digest-collision branch it reassigns any `subset.Best == oldNode` to the equivalent (and propagates) before merging. Removes the stale-parent lingering that the re-verification flagged. (Found by re-verification; pre-existing in the old `Reregister`, not a regression from the split.) |

### `VolcanoRuleCall` → `VolcanoRuleCall` — findings:

| Calcite member | Verdict | Note |
|---|---|---|
| `onMatch` | **RESOLVED (ported)** | `OnMatch` now runs the guard loop over the bound nodes before firing: skips the match if a node has no subset (`GetSubset` null — removed during a rename), its set was merged away (`Set.EquivalentSet != null`), it was removed from its subset (`!subset.Contains(rel)`), or it is pruned. Fixes firing a rule on a node already removed/merged (theme A/B). |
| `transformTo(rel, equiv)` | **RESOLVED (ported)** | the `equiv` map overload is now present: `TransformTo(equivalent, IReadOnlyDictionary<INode,INode>)` registers each map entry via `EnsureRegistered(key, value)` before the root, so a rule can declare secondary equivalences in one call. Also added the faithful guard — a transformation rule may not produce an `IPhysicalNode`. (Calcite's hint-propagation `handler` overload is RELATIONAL and intentionally omitted.) |
| `matchRecurse` `RelSubset.class` / `setChildRels` branches | DIVERGENT-OK | the omitted subset-operand + unused `childRels` paths — already tracked in §9. |

### `VolcanoRuleMatch` → `VolcanoRuleMatch` — `allNotNull` constructor null-check **RESOLVED (ported)**: the ctor throws `ArgumentException` if any bound node is null. Dedup via `Equals`/`GetHashCode` is FAITHFUL to the digest's purpose.

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
| `AbstractConverter.explainTerms` | **RESOLVED (ported)** | overridden to emit each enforced trait (`Item(trait.TraitDef.Name, trait)`) after the base terms, as Calcite. Required making `TraitSet` enumerable (`IEnumerable<ITrait>`) — the analog of `RelTraitSet` being `Iterable`. |
| `ExpandConversionRule.INSTANCE` / `Config` | DIVERGENT-OK | singleton/Config plumbing replaced by a public ctor + inline operand. |
| `IPhysicalNode.passThrough`/`derive` | FAITHFUL | compose via `IPlanner.ChangeTraits` (= `changeTraits`, `RelOptRule.convert`'s target). |

### `HepPlanner` → `HepPlanner` — ~56 members; findings (PORT.md's "method-audited" claim was optimistic):

| Calcite member | Verdict | Note |
|---|---|---|
| `matchOperands` SOME (default) | **RESOLVED (fixed)** | confirmed by re-verification: Calcite accepts `childRels.size() >= n` (binds the first n). `MatchOperand`'s SOME case changed from exact `==` to `node.Children.Length < operand.Children.Length` → reject; binds the first n positionally. (Current fixed-arity nodes are unaffected, so no test churn.) |
| `matchOperands` UNORDERED | **RESOLVED (fixed; model aligned to Calcite)** | confirmed: Calcite's UNORDERED operand has **one** child operand that matches **any** child, size-agnostic (`matchAnyChildren`); the asserted single child operand is `RelOptRuleOperand`'s contract. Alembic had built UNORDERED as an N↔N exact bijection (a non-Calcite model) which rejected Calcite's actual pattern. Reworked `MatchOperand`'s UNORDERED case to `matchAnyChildren` (each child operand matches any one child; node child count unconstrained), removed the `MatchUnordered` bijection helper, and reworked the `OperandPolicyTests` UNORDERED cases to the single-operand/any-position model. (Alembic does not port Calcite's debug `assert children.size()==1`, so it harmlessly tolerates N unordered operands — each still matches any child.) |
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
| `id`/`nextId` | **RESOLVED (ported)** | `RuleCall.Id`, assigned from a static counter in creation order. |
| `getChildRels`/`getParents`/`builder`/`getMetadataQuery`/`isRuleExcluded` | MISSING | RELATIONAL/METADATA/BUILDER. |

### `ConverterRule` / `Converter` / `ConverterImpl` / `TraitMatchingRule` — findings:

| Member | Verdict | Note |
|---|---|---|
| `ConverterRule.isGuaranteed()` default | **RESOLVED (fixed)** | default flipped to **`false`** (Calcite's safe default — non-guaranteed, applied bottom-up via `TraitMatchingRule`). The always-convert test converters (`SourceConverter`/`FilterConverter`/`ParameterConverter`/the expression+image converters/`SortEnforcer`) now override `IsGuaranteed => true`, as Calcite's guaranteed converters do. Only consulted by HEP's `AddConverters(guaranteed)` instruction, so the flip's observable effect is scoped there. |
| `ConverterRule.onMatch` in-trait re-check | DIVERGENT-OK | operand already constrains to `Source`; re-check redundant. |
| `getTraitDef()` | **RESOLVED (ported)** | `ConverterRule.TraitDef => Source.TraitDef`. |
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
| `RelEquivalenceEvent.equivalenceClass` + `isPhysical` | **RESOLVED (ported)** | `NodeEquivalenceEvent.EquivalenceClass` (the set id, from `NodeSubset.Add`) + `IsPhysical` (node carries a non-`None` convention); the cost-based planner populates both, the heuristic planner leaves the defaults. *Re-verification notes (DIVERGENT-OK, listener-observability only):* (a) the payload is the raw set id, not Calcite's `"equivalence class {id}"` string; (b) `IsPhysical` is computed, where Calcite hard-codes `false` — Alembic matches the interface's own intent better; (c) Alembic raises the event only on node-add (not also on bare subset creation as Calcite does), so a listener sees fewer equivalence events. None affect planning. |
| `RelOptUtil.toString` | DIVERGENT-OK | faithful shape (news up `NodeWriterImpl`, calls `Explain`); drops the `SqlExplainLevel` param, adds `.TrimEnd()`. |
| `Cluster` `getPlanner`/`traitSet`/`traitSetOf` | FAITHFUL | the only in-scope cluster members; the rest (type factory, rex builder, metadata query, hints) correctly RELATIONAL/METADATA. |

### `RelTrait` → `ITrait` / `RelTraitDef` → `TraitDef` (base) + `TraitDef<T>` — findings:

| Member | Verdict | Note |
|---|---|---|
| `RelTraitDef.canonize` interner | **RESOLVED (fixed)** | `TraitDef` now uses a `WeakInterner<ITrait>` (`src/Alembic/Util/WeakInterner.cs`) — canonical traits are held weakly (collectible once no `TraitSet` references them), with dead entries swept on insert; the analog of Calcite's `Interners.newWeakInterner`. Thread-safe via a single lock (no lock-free races — the class that caused the earlier flakiness). **TODO (follow-up):** `WeakInterner` is a hand-rolled first cut; it should be checked against Google Guava's `Interners.newWeakInterner` and reworked into an exact line-by-line port (Guava's is more sophisticated, with a better API), using .NET types where a Java/Guava feature has a direct equivalent. |
| `RelTrait.satisfies` | DIVERGENT-OK | Calcite leaves it abstract; Alembic gives a default `Equals` body (the reflexive base case). |

### `RelTraitSet` → `TraitSet` — findings:

| Member | Verdict | Note |
|---|---|---|
| `getTrait(RelTraitDef)` | **RESOLVED (fixed)** | typed `Get<T>(def)` now returns `TTrait?` — `null` when the dimension is absent, as Calcite's `getTrait`. (Constraint tightened to `class, ITrait`; the always-present `Convention` accessor uses `!`.) The non-generic `Get(TraitDef)` still returns the dimension default — a separate, deliberate convenience overload. |
| `replace(RelTrait)` (ignore-if-absent) | **RESOLVED (ported)** | `TraitSet.Replace(ITrait)` infers the dimension from the trait, substitutes if present, returns `this` if absent — Calcite's `replace(RelTrait)` exactly (distinct from `Plus`, which adds an absent dimension). |
| `Replace<T>(def, value)` | DIVERGENT-OK | doesn't canonize `value` (Calcite does); relies on callers passing canonical traits. |
| `simplify` / `allSimple` | **RESOLVED (ported)** | `TraitSet.Simplify()` (one-member composite → its member; many-member → dimension default) and `TraitSet.AllSimple()`. A non-generic base class `CompositeTrait` (`Count`/`TraitAt`) lets the set flatten composites without knowing the member type (mirroring Calcite's raw `RelCompositeTrait`). |
| `difference` | **RESOLVED (ported)** | `TraitSet.Difference(traitSet)` returns the argument's traits that differ position-for-position — needed by `NodeSet.AddConverters` to decide which dimensions a converter must bridge. |
| `replaceIf`/`replaceIfs`/`plusAll`/`merge` | **RESOLVED (ported)** | `TraitSet.ReplaceIf<T>(def, Func<T?>)`, `ReplaceIfs<T>(def, Func<IReadOnlyList<T>?>)`, `PlusAll(IEnumerable<ITrait>)`, `Merge(TraitSet)` — supplier-driven conditional replace and bulk add, matching Calcite. |

### `Convention` / `ConventionTraitDef` / `CompositeTrait` — findings:

| Member | Verdict | Note |
|---|---|---|
| `ConventionTraitDef.convert`/`canConvert`/`registerConverterRule` + `ConversionData` graph | DIVERGENT-OK (architectural) | Calcite implements convention conversion as a per-planner `DirectedGraph` of conventions + shortest-path inside `ConventionTraitDef`; Alembic ports none of it and instead drives convention conversion through rules (`ExpandConversionRule` → `VolcanoPlanner.ChangeTraitsUsingConverters`). Different mechanism, same capability — worth noting as a deliberate relocation, not a silent gap. |
| `CompositeTrait.of` | **RESOLVED (fixed)** | `Of` now canonizes the single member (size 1) and, for many, canonizes each member then the whole composite (`def.Canonize(...)`), as Calcite — restoring the interning-identity invariant. |
| `Convention.Impl` placement (nested) → top-level `Convention` | **RESOLVED (decision: keep top-level)** | Calcite's concrete convention is `Convention.Impl`, a class nested in the `Convention` interface. Both are extension points for adapters: the interface is implemented directly (`EnumerableConvention`, `BindableConvention`, `InterpretableConvention`) and `Impl` is subclassed as a convenience base (`JdbcConvention extends Convention.Impl`). Alembic splits these into `IConvention` (the interface others implement) + a top-level `Convention` (the extensible concrete base others subclass). **Kept top-level by decision**: a top-level, openly-subclassable class is the idiomatic C# extension point; nesting it inside the interface (the Java form) would only hinder that. This is the chosen Alembic structure, not a deviation to reconcile later. |
| `Convention.Impl` `equals`/`hashCode` | DIVERGENT-OK (intentional) | Calcite's `Impl` uses reference identity (conventions are singletons); Alembic compares by name. Deliberate, documented. |

### `RelOptCost`/`Cost`/`VolcanoCost` — **no bugs.** `getRows()`/`Value` dropped (relational). The scalar `Cost` now mirrors `RelOptCostImpl` member-for-member: `getCpu()`/`getIo()` return `0`; `plus`/`minus`/`multiplyBy` are plain `new Cost(...)` (no infinite short-circuit) and `divideBy` is plain division, matching Calcite exactly; comparison is on the private scalar. `VolcanoCost` keeps its own component-wise behavior — `multiplyBy`/`minus` infinite short-circuits, geometric-mean `divideBy`, per-component `isEqWithEpsilon` — faithful to Calcite's `VolcanoCost`. The named cost singletons (`INFINITY`/`HUGE`/`ZERO`/`TINY`) live on `VolcanoCost` (annotated), as in Calcite; `RelOptCostImpl`/`Cost` have none and build costs inline in their factory.

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
| `RelWriter.itemIf` | **RESOLVED (ported)** | `INodeWriter.ItemIf(name, value, condition)` default method, as Calcite's default. |
| `RelWriter.getDetailLevel` / `nest` / `expand` | DIVERGENT-OK (no analog) | **deliberately not ported**: `getDetailLevel` returns a SQL `SqlExplainLevel`, and `nest`/`expand` are SQL-EXPLAIN rendering flags — all inert for Alembic's medium-agnostic plan output. Porting them would mean inventing SQL concepts, not porting. (The one Bucket-D item where "port all" was overridden by judgment.) |
| `RelWriterImpl.explain_` | DIVERGENT-OK | `=value` vs Calcite's `=[value]`, type-name source differs — cosmetic plan-string format. SingleNode/BiNode `explainTerms` FAITHFUL. |

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
3. ~~`TraitSet.Get<T>` throws on an absent dimension instead of returning null.~~ **RESOLVED**: `Get<T>` returns `null` when absent (Calcite's `getTrait`).
4. ~~`VolcanoPlanner.Merge` — missing `isSmaller` direction + root re-point/`ensureRootConverters`.~~ **RESOLVED**: swap (`GetChildSets`+`IsSmaller`) + root re-point ported.
   - 4b. ~~`VolcanoPlanner.FixUpInputs`/`Rename` — no parent back-link maintenance.~~ **RESOLVED**: `Rename` drops the old node's back-links and links the rebuilt node's; collision branch reassigns `best`.
5. ~~`VolcanoPlanner.EnsureRegistered` — skips the equivalence-driven set merge when already registered.~~ **RESOLVED**: merges the sets, returns the `Canonize`d subset.
6. ~~`TraitDef.Canonize` — strong-ref interner (leak) vs Calcite's weak interner.~~ **RESOLVED**: `WeakInterner<ITrait>` (weak values, swept, single-lock).
7. ~~`CompositeTrait.Of` — no canonization (breaks interning identity).~~ **RESOLVED**: canonizes members + the whole composite.
8. ~~HEP matcher (`MatchOperand` SOME / `MatchUnordered`) stricter than Calcite — rejects valid matches *(verify)*.~~ **RESOLVED**: SOME now `>=` (binds first n); UNORDERED reworked to Calcite's single-operand/any-child `matchAnyChildren` (model aligned; tests reworked). See the `HepPlanner` findings rows.

**P2 — themes / structural gaps:**
- ~~**Node pruning** absent (drives the hollow `VolcanoRuleCall.OnMatch` staleness guard, `skipMatch`).~~ — **RESOLVED**: `prunedNodes`/`Prune`/`CheckPruned` machinery + the `OnMatch` staleness guard + `SkipMatch` are ported (see the `VolcanoPlanner`/`VolcanoRuleCall`/`RuleQueue` rows).
- ~~**`transformTo(equiv)`** map dropped port-wide~~ — **RESOLVED**: the equiv-map overload is ported (see `VolcanoRuleCall`/`RuleCall` rows).
- **Canonization**: `EquivRoot` returns the set, never the leader subset; root not re-canonized after merge.
- ~~**Per-subset converter seeding** (`RelSet.getOrCreateSubset → addConverters`) absent~~ — **RESOLVED**: `NodeSet.AddConverters` ported (see the `RelSet` rows).
- ~~**Rule registry**: `AddRule` no dedup/unique-description; no `Rule.equals`/exclusion-filter.~~ — **RESOLVED**: description-keyed registry with dedup, `Rule`/`RuleOperand` value equality, and the exclusion filter (see the `RuleOperand`/`AbstractRelOptPlanner` rows).
- `GetCost` none-convention infinite/nudge; HEP `applyRule` forced-conversion guard; HEP large-plan `contractVertices` merge.

**P3 — minor/cosmetic:** ~~`RelWriter.itemIf`/`getDetailLevel`, `findCycles`, `obliterateRelNode`, `allNotNull`, listener event payloads (`equivalenceClass`/`isPhysical`), `VolcanoRuleMatch`/cost helper gaps~~ — **RESOLVED** (Bucket D + Bucket C): all ported except `getDetailLevel`/`nest`/`expand` (SQL-EXPLAIN-only, no analog). Remaining: digest input-string id (cosmetic, DIVERGENT-OK as recorded).

**Deliberate, verified-OK:** convention conversion is rule-driven (not `ConventionTraitDef`'s graph); FIFO queue vs importance ranking; metadata-disabled lower-bound pruning; row-count-free cost; `Convention` by-name equality.

---

## 11. Follow-ups (tracked, not yet done)

- **`WeakInterner` → Guava parity.** `src/Alembic/Util/WeakInterner.cs` is a hand-rolled first cut; rework it into an exact line-by-line port of Google Guava's `Interners.newWeakInterner` (more sophisticated, better API), using .NET types where a Java/Guava feature has a direct equivalent.
- **`TraitDef.TraitClass` is concrete; Calcite's `getTraitClass()` is `abstract`.** Ours returns `typeof(TTrait)`; Calcite leaves it abstract. Minor divergence — acceptable.
- **`TraitDef.TraitClass` should be named `TraitType`** (acceptable-divergence rename; `Type`/`Class` C# idiom).
- **`TraitDef<TTrait>` generic-typing boundary.** Calcite types `canonize`/`getDefault` (etc.) as `T` throughout; ours types `Canonize`/`CanConvert`/`Convert` as `ITrait` (non-generic, on the base `TraitDef`) and only `Default` as `TTrait` (covariant override on `TraitDef<T>`). Consider tightening `Canonize` to `TTrait Canonize(TTrait)` to match Calcite's `final T canonize(T)`.
- **`TraitDef.Canonize` does not check for a composite trait.** Calcite's `canonize` special-cases `RelCompositeTrait` (`if (!(trait instanceof RelCompositeTrait)) { assert getTraitClass().isInstance(trait); }`) — guarding a type assertion. Ours interns directly without the check.
- ~~**`[Provenance]` attribute — project-wide.**~~ **DONE.** `ProvenanceAttribute(className, member?)` is applied across the whole project — every Calcite-derived type/member/field carries the FQN + real Calcite signature; Alembic-originals are listed in §10. Prose Calcite mentions were scrubbed in the same pass (source is now Calcite-free except machine-readable attribute values).
