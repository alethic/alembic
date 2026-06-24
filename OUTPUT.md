# Re-introducing op "output" — the rowType hit list

Alembic stripped Calcite's `RelDataType` row type from ops (it's relational). But row type is
load-bearing in the **planner core**, not just the relational operators. This file is the **total
hit assessment** for re-introducing it as a medium-neutral, opaque *output type* — every method and
signature on the ported surface that refers to it, with what each one would become.

See the design discussion for *why* (the equivalence invariant + the structural-identity term). This
file is purely the *where* and *how much*.

---

## Scope & method

The "hit" is the hit **to Alembic**, so the universe is the **ported surface** — the Calcite classes
Alembic has `[Provenance]` to (extracted from the provenance attributes: ~95 source files). Every
`rowType` / `RelDataType` / `areRowTypesEqual` / `verifyTypeEquivalence` / `equalsSansFieldNames`
reference in those files is classified below. Calcite's thousands of *relational* row-type uses
(Rex, SQL, the operator library, relational metadata) are **not** counted — Alembic never ports them.

**Baseline:** a search of `src/**/*.cs` for any `rowType` / `RowType` / `OutputType` /
`RelDataType` returns **zero matches**. The strip was clean, so this change is **purely additive** —
nothing to rip out, only slots to re-introduce.

**Ported-surface files containing rowType references** (count of matching lines):

| File | refs | relevance |
|---|---:|---|
| `plan/RelOptUtil.java` | 216 | only the 4 type-equality helpers are in scope; the rest are relational |
| `rel/AbstractRelNode.java` | 16 | **core** — the field, accessor, derivation, onRegister, deepEquals |
| `plan/volcano/VolcanoPlanner.java` | 11 | **core** — 2 equivalence guards (+1 materialization, relational) |
| `plan/RelOptCluster.java` | 5 | relational — the `RelDataTypeFactory` (not ported) |
| `rel/RelNode.java` | 5 | 1 interface decl in scope; rest relational |
| `plan/RelOptNode.java` | 3 | **core** — the `getRowType()` interface declaration |
| `plan/hep/HepRelVertex.java` | 3 | **core** — `deriveRowType` plumbing |
| `plan/volcano/RelSubset.java` | 3 | **core** — `deriveRowType` plumbing |
| `rel/SingleRel.java` | 3 | **core** — `deriveRowType` pass-through |
| `rel/metadata/RelMetadataQuery.java` | 3 | relational metadata methods (unported) |
| `plan/hep/HepPlanner.java` | 2 | doc comment only |
| `rel/metadata/BuiltInMetadata.java` | 2 | relational (`getTypeFactory`, unported) |
| `plan/volcano/RelSet.java` | 1 | **core** — `verifyTypeEquivalence` guard |
| `plan/hep/HepRuleCall.java` | 1 | **core** — `verifyTypeEquivalence` guard |

---

## The new API this introduces (recap)

Two new types + one slot on the op:

```csharp
// Port of RelDataType, reduced to the only thing the planner asks of it.
[Provenance(Calcite, "org.apache.calcite.rel.type.RelDataType")]
public interface IOutputType
{
    // areRowTypesEqual(_, _, /*compareNames=*/false) / equalsSansFieldNames
    [Provenance(Calcite, "...RelDataType", "equalsSansFieldNames(RelDataType)")]
    bool IsEquivalentTo(IOutputType other);
}

// The trivial output type: the single "no meaningful output" type. A singleton, equivalent ONLY to
// itself — NOT an absorbing "matches anything" type. A medium that doesn't type its outputs leaves
// every op Void, so all ops share one output type and merge freely — for the right reason.
public sealed class VoidOutputType : IOutputType
{
    public static readonly VoidOutputType Instance = new();
    public bool IsEquivalentTo(IOutputType other) => other is VoidOutputType;   // identity, not always-true
}
```

`IOp` gains `IOutputType OutputType { get; }` (port of `getRowType()`); `AbstractOp` gains the cached
field + `virtual IOutputType DeriveOutputType()` (port of `deriveRowType()`), defaulting to `Void`.

### Output types never convert — transformations are explicit rules

`IOutputType` exposes **only an equivalence relation** (`IsEquivalentTo`, reflexive/symmetric/transitive
≈ equality). It deliberately has **no `Convert` / `CanConvert` / `Satisfies`**. This is the hard line
between an *output type* and a *trait*:

| | relation | how it changes | who changes it |
|---|---|---|---|
| **trait** (`IOpTrait`) | partial order (`Satisfies`) | converts *within* a set via an enforcer (`OpTraitDef.Convert`) | the planner, silently |
| **output type** (`IOutputType`) | equivalence only | **never converts** — it *partitions* sets | a **rule**, by rewriting the op |

To take an op from output type `A` to output type `B` you apply a **rule** that produces a *different
op* — a different e-class/set with its own output type. There is no implicit coercion. (E-graph framing:
you can't convert a sort; you apply a function that yields a different sort.) So `VoidOutputType` is a
real point in the type space, not an escape hatch — and the absence of a conversion hook is intentional.

### Naming (settled)

| Concept | Calcite | **Alembic** | Why |
|---|---|---|---|
| an op's input ops | `getInputs()` | **`Inputs`** | The faithful port of `getInputs()`; `Children` was an unflagged member rename. Restores the input-side vocabulary already used by `ReplaceInput`/`IOpWriter.Input`. *(`ChildrenAccept` ← `childrenAccept` and the `OpRuleOperandChildren` operand-DSL family keep "children" — Calcite does too.)* |
| an op's output descriptor | `RelDataType` / `getRowType()` | **`IOutputType`** / **`OutputType`** | Role-based, medium-neutral (names the *egress slot*, not what flows — unlike "Data"/"Row"). Keeps the `Type` suffix because "output" reads value-ish and needs marking as a *descriptor*, not a result (cf. `Convention`/`Collation`, which are inherently categorical and drop it). Matches the e-graph framing: the output type is the e-class's **sort**. |

`Inputs` (concrete ops) and `OutputType` (a descriptor) are deliberately **non-parallel** — the
asymmetry signals they are different kinds of things, so a reader never expects to "consume" an
`OutputType` the way they index `Inputs`. Rejected: `Output` (reads as the produced value/result, and
falsely mirrors `Inputs`-as-ops), `DataType` ("Data" re-leaks a data-processing medium), `Sort`
(collides with the `Sortedness` ordering trait), `OpSignature`/`Codomain` (ambiguous / jargon).

---

## A. Core load-bearing hits (must port)

Each entry = one method/signature that must change. **13 touch points across 8 types.**

| # | Calcite member (file:line) | Alembic target | Role | The hit |
|---|---|---|---|---|
| A1 | `RelOptNode.getRowType()` — `RelDataType getRowType()` (`RelOptNode.java:66`) | `IOp.OutputType` | identity + invariant | **New interface member.** Re-add the accessor, retyped `RelDataType`→`IOutputType`. (Calcite's `RelNode.java:116` re-declares the same method; Alembic merges `RelOptNode`+`RelNode` into one `IOp`, so it's a single member.) |
| A2 | `AbstractRelNode.rowType` — `protected RelDataType rowType` field (`AbstractRelNode.java:75`) | `AbstractOp` (new field) | cache backing | **New field.** `IOutputType? _outputType` lazily filled by the accessor. |
| A3 | `AbstractRelNode.getRowType()` — `final RelDataType getRowType()` (`AbstractRelNode.java:173`) | `AbstractOp.OutputType` | identity + invariant | **New property.** Lazy: `_outputType ??= DeriveOutputType()` (mirrors the `checkNotNull(deriveRowType())` cache). |
| A4 | `AbstractRelNode.deriveRowType()` — `protected RelDataType deriveRowType()` (`AbstractRelNode.java:180`) | `AbstractOp.DeriveOutputType()` | derivation hook | **New virtual.** Base returns `VoidOutputType.Instance` (Calcite's base throws; Alembic's default is the trivial "no meaningful output" type). User ops override. |
| A5 | `AbstractRelNode.onRegister()` — `assert ... RelOptUtil.equal("rowtype ...before", input.getRowType(), ...after, e.getRowType(), THROW)` (`AbstractRelNode.java:275–279`) | `AbstractOp.OnRegister` | invariant guard | **Re-add dropped assert.** Currently `OnRegister` (`AbstractOp.cs:142`) registers inputs with no type check; add `Debug.Assert(ReferenceEquals(e,input) || input.OutputType.IsEquivalentTo(e.OutputType))`. |
| A6 | `AbstractRelNode.deepEquals()` — `&& this.getRowType().equalsSansFieldNames(that.getRowType())` (`AbstractRelNode.java:368`) | `AbstractOp.DeepEquals` | **structural identity** | **Re-add dropped term.** `DeepEquals` (`AbstractOp.cs:166`) compares traits + digest items only; add `&& OutputType.IsEquivalentTo(that.OutputType)`. *(Note: `deepHashCode` legitimately omits it — `AbstractRelNode.java:395` doesn't hash rowType — so `DeepHashCode` at `AbstractOp.cs:201` needs no change.)* |
| A7 | `VolcanoPlanner.register()` — `areRowTypesEqual(rel.getRowType(), equivRel.getRowType(), false)` guard + `getFullTypeDifferenceString` throw (`VolcanoPlanner.java:589–595`) | `VolcanoPlanner.Register` | invariant guard | **Re-add guard.** `if (!rel.OutputType.IsEquivalentTo(equivRel.OutputType)) throw ...`. |
| A8 | `VolcanoPlanner.registerImpl()` — `areRowTypesEqual(equivExp.getRowType(), rel.getRowType(), false)` guard + throw (`VolcanoPlanner.java:1314–1318`) | `VolcanoPlanner.RegisterImpl` | invariant guard | **Re-add guard** at the digest-equal branch. |
| A9 | `RelSet.addInternal()` — `RelOptUtil.verifyTypeEquivalence(this.rel, rel, this)` (`RelSet.java:338`) | `OpSet.AddInternal` | invariant guard | **Re-add guard.** Every member checked against the set representative. |
| A10 | `HepRuleCall.transformTo()` — `RelOptUtil.verifyTypeEquivalence(rel0, rel, rel0)` (`HepRuleCall.java:58`) | `HepRuleCall.TransformTo` | invariant guard | **Re-add guard** (currently dropped — flagged as A35 in PORT.md "relational"). A rule's output must match its input's output type. |

---

## B. Derivation plumbing (the `deriveRowType` overrides)

Ops/placeholders that compute their output type from a child. **3 touch points.**

| # | Calcite member (file:line) | Alembic target | The hit |
|---|---|---|---|
| B1 | `RelSubset.deriveRowType()` — `return set.rel.getRowType()` (`RelSubset.java:288`) | `OpSubset.DeriveOutputType` | **New override.** A subset's output = its set representative's output. |
| B2 | `HepRelVertex.deriveRowType()` — `return currentRel.getRowType()` (`HepRelVertex.java:80`) | `HepOpVertex.DeriveOutputType` | **New override.** A vertex's output = its wrapped op's output. |
| B3 | `SingleRel.deriveRowType()` — `return input.getRowType()` (`SingleRel.java:88`) | `SingleOp.DeriveOutputType` | **New override.** Single-input op passes its input's output through. (`BiRel` has no override — skip.) |

---

## C. Helper methods to port (`RelOptUtil`)

The equality/verification helpers the guards above call. **The equivalence relation itself collapses
into `IOutputType.IsEquivalentTo`** (it's the only thing the planner ever needed of `RelDataType`), so
`areRowTypesEqual` largely *disappears* into the interface. **2–4 touch points** depending on how much
of the error-message machinery is wanted.

| # | Calcite member (file:line) | Alembic target | The hit |
|---|---|---|---|
| C1 | `RelOptUtil.areRowTypesEqual(RelDataType, RelDataType, boolean)` (`RelOptUtil.java:377`) | `IOutputType.IsEquivalentTo` | **Folds into the interface.** The field-by-field comparison (`getFieldCount`, `equals`, the `ANY` escape) is the *implementation's* business; the planner only ever passes `compareNames=false`, so one method on `IOutputType` covers it. No standalone port needed. |
| C2 | `RelOptUtil.verifyTypeEquivalence(RelNode, RelNode, Object)` (`RelOptUtil.java:417`) | `PlanUtil.VerifyOutputEquivalence` (new) | **New helper** (or inline at A9/A10). Throws on mismatch; the message uses the next two. |
| C3 | `RelOptUtil.getFullTypeDifferenceString(...)` (`RelOptUtil.java:2319`) | error rendering | **Optional.** Diff text for the throw. Could reduce to `a.ToString()` vs `b.ToString()` since `IOutputType` is opaque — full field-diff is relational. |
| C4 | `RelOptUtil.equalType(String, RelNode, ...)` / `equal(...)` (`RelOptUtil.java:2369`) | assert helper | **Optional.** Only consumer on the ported surface is the A5 `onRegister` assert; can inline as a `Debug.Assert` on `IsEquivalentTo`. |

---

## D. Relational rowType refs on the ported surface — explicitly NOT in scope

Listed for completeness so the audit is total. These appear in ported *files* but in relational
*members* Alembic does not port; re-introducing output type does **not** touch them.

| Calcite member (file:line) | Why out of scope |
|---|---|
| `RelOptCluster.typeFactory` field + `getTypeFactory()` (`RelOptCluster.java:52,132`) | `RelDataTypeFactory` builds `RelDataType` — a relational concern. Output types are user-supplied, so core needs no factory. (`OpCluster` does not carry one.) **Confirmed:** zero `getTypeFactory()` calls anywhere in `plan/volcano` or `plan/hep` — the planner machinery never touches it; every call site is a relational `RelOptUtil`/`SubstitutionVisitor` helper Alembic doesn't port. The factory's two jobs split on the relational line: **construction** (`createStructType`/`createSqlType`/`copyType`…) is relational; its only medium-neutral kernel is **canonization** (`canonize → DATATYPE_CACHE.intern`), which is already covered by Alembic's Guava interner (`Interners`, the same machinery that canonizes traits). A downstream output-type factory interns with that; the core needs nothing. `IsEquivalentTo` works without interning (structural compare = Calcite's fallback after the `==` fast path); interning is a user-side optimization that, post-A6, also makes the digest path reference-cheap. |
| `RelNode.getExpectedInputRowType(int)` (`RelNode.java:126`; `AbstractRelNode.java:186`) | Expected-input-type for relational coercion. Not on `IOp`. |
| `RelNode` default `isNullable`-style accessor — `getRowType().getFieldList().get(i)...` (`RelNode.java:425`) | Per-field nullability — relational. |
| `VolcanoPlanner` materialization — `materialization.queryRel.getRowType()` (`VolcanoPlanner.java:342`) | Materialized views — relational, unported. |
| `RelMetadataQuery` field-count uses (`RelMetadataQuery.java:476,568,738`) | Relational metadata handlers (column origins, predicates) — Alembic ports only cost/memory/parallelism. |
| `BuiltInMetadata.getTypeFactory()` (`BuiltInMetadata.java:916`) | Relational metadata — unported. |
| `HepPlanner` javadoc mention of `RelDataType` (`HepPlanner.java:97`) | Doc comment only. |
| `RelOptNode` javadoc mention of `getRowType()` (`RelOptNode.java:38`) | Doc comment only (the `isDistinct`/equivalence note). The actual member is A1. |
| `RelOptUtil.java` remaining ~210 `RelDataType` refs | Relational helpers (`createCastRel`, type coercion, field manipulation) — never ported. |

---

## Total hit

| Bucket | Touch points |
|---|---|
| **A. Core load-bearing** (interface member, op field/accessor/derivation, 2 dropped asserts re-added, 4 planner guards re-added) | **10** |
| **B. Derivation plumbing** (3 `deriveRowType` overrides) | **3** |
| **C. Helpers** (1 folds into the interface; 1 new verify helper; 2 optional message bits) | **2–4** |
| **New types** (`IOutputType`, `VoidOutputType`) | **2** |
| **D. Out-of-scope relational refs** (documented, untouched) | 0 |

**~15–17 edited members across 8 existing types, + 2 new types.** Small, well-bounded, and additive —
no existing behaviour is removed. The two *re-added asserts* (A5, A6) close real gaps where Alembic's
structural identity is currently coarser than Calcite's; the four *re-added guards* (A7–A10) restore
the equivalence invariant that makes a `RelSet` sound.

---

## Forward compatibility: e-graphs (egg / egglog)

This API is deliberately shaped so a future e-graph backend (à la [egg](https://egraphs-good.github.io/)
/ [egglog](https://github.com/egraphs-good/egglog)) needs **no rework of the output-type concept** — it
generalizes by widening exactly one method. Recorded here so the intent survives.

**The memo already *is* an e-graph** (modulo `RelSubset`, which is Cascades' physical-property index and
has no e-graph analog):

| Cascades / Alembic | egg / egglog |
|---|---|
| `OpSet` (bag of equivalent ops) | **e-class** |
| `IOp` (operator + child sets) | **e-node** `f(c₁,…,cₙ)` over child e-class ids |
| `GetOpDigest` / `DeepEquals` / `DeepHashCode` | **hashcons key** (e-node canonicalization) |
| set-merge on discovered equivalence | **union** + rebuild (congruence closure) |
| `OpSubset` (set × traitset) | *no analog* — physical-property index |

**Output type maps to an e-class analysis (egg) / a sort (egglog).** An egg *e-class analysis* is a
semilattice value on each e-class with `make(enode)→Data` and `merge(a,b)→(Data, DidMerge)`. The API
already supplies both:

- `AbstractOp.DeriveOutputType()` **is** `make`.
- `IOutputType.IsEquivalentTo(other)` **is the bool projection of** `merge` for the *flat-equality
  lattice* `D ∪ {⊤}`: `merge(a,b) = a` if `a ≡ b`, else `⊤` (conflict). Calcite/Alembic's
  "throw / `Debug.Assert` on mismatch" (A5–A10) is precisely "treat `⊤` as an illegal union."

**The one-method generalization.** To become a *full* analysis (one that can *refine* — e.g. nullability
narrowing on union, re-propagated upward through `make`), widen the equivalence check to a merge:

```csharp
// faithful Calcite port (flat equality, never refines) — ship this now:
bool IsEquivalentTo(IOutputType other);

// e-graph generalization (egg's merge + DidMerge) — the only change required later:
(IOutputType merged, bool changed) Merge(IOutputType other);   // ⊤ ⇒ illegal union
```

`IsEquivalentTo` is the height-2 degenerate `Merge`; nothing else in the design moves.

**egglog framing (stronger).** With egglog's sort system, **output type becomes the e-class's sort**: an
op is a typed function symbol whose codomain sort is its output type, two ops of different output type
*cannot* share an e-class (well-sortedness, not a runtime guard), and congruence is sort-respecting by
construction. A5–A10 dissolve from checks into typing.

**Why output type and traits diverge here.** The e-graph view confirms the invariant-vs-convertible split:
*invariant ⇒ e-class analysis / sort* (output type); *convertible ⇒ e-nodes + rewrite rules* (a trait —
an enforcer is literally a rewrite `c_sorted ≡ enforce(c_any)`, and `OpSubset` is the index an e-graph
wouldn't need). Keeping them in separate slots is forward-compatible; folding traits into output type
would not be.

**Deep end — structured output types as their own sort.** If output types are composite (the "series of
outputs" idea), egglog lets them be *e-classes in a second sort*, making type-equality congruence closure
on the type terms — i.e. **type unification via union-find** for free. `IsEquivalentTo` then isn't
implemented at all; it's "same type e-class?".

**Caveat — termination.** Flat equality (`D ∪ {⊤}`, height 2) saturates trivially. Any refining lattice
(e.g. subtyping) must satisfy the ascending-chain condition or `Merge` won't converge.
