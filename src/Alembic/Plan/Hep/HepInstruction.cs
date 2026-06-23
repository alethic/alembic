using System;
using System.Collections.Generic;
using System.Collections.Immutable;

using Alembic.Plan.Rules;

namespace Alembic.Plan.Hep;

/// <summary>
/// One instruction in a <see cref="HepProgram"/>. The instruction set is defined here as nested types.
/// An instruction is immutable; <see cref="Prepare"/> allocates the mutable <see cref="HepState"/> that
/// runs it.
/// </summary>
[Provenance("org.apache.calcite.plan.hep.HepInstruction")]
public abstract class HepInstruction
{

    private protected HepInstruction()
    {

    }

    /// <summary>
    /// Creates runtime state for this instruction. The state copies from the context everything it will
    /// need to execute, and is discarded afterwards.
    /// </summary>
    [Provenance("org.apache.calcite.plan.hep.HepInstruction", "prepare(PrepareContext)")]
    internal abstract HepState Prepare(PrepareContext px);

    /// <summary>
    /// Executes all rules of a given type.
    /// </summary>
    [Provenance("org.apache.calcite.plan.hep.HepInstruction.RuleClass")]
    internal sealed class RuleClass : HepInstruction
    {

        [Provenance("org.apache.calcite.plan.hep.HepInstruction.RuleClass.ruleClass")]
        internal readonly Type RuleType;

        [Provenance("org.apache.calcite.plan.hep.HepInstruction.RuleClass", "RuleClass(Class<R>)")]
        internal RuleClass(Type ruleType)
        {
            RuleType = ruleType;
        }

        [Provenance("org.apache.calcite.plan.hep.HepInstruction.RuleClass", "prepare(PrepareContext)")]
        internal override HepState Prepare(PrepareContext px) => new State(px, this);

        [Provenance("org.apache.calcite.plan.hep.HepInstruction.RuleClass.State")]
        internal sealed class State : HepState
        {
            internal readonly RuleClass Instruction;

            [Provenance("org.apache.calcite.plan.hep.HepInstruction.RuleClass.State.ruleSet")]
            internal HashSet<Rule>? RuleSet;

            [Provenance("org.apache.calcite.plan.hep.HepInstruction.RuleClass.State", "State(PrepareContext)")]
            internal State(PrepareContext px, RuleClass instruction) : base(px) => Instruction = instruction;

            [Provenance("org.apache.calcite.plan.hep.HepInstruction.RuleClass.State", "execute()")]
            internal override void Execute() => Planner.ExecuteRuleClass(Instruction, this);
        }

    }

    /// <summary>
    /// Executes all rules in a given collection.
    /// </summary>
    [Provenance("org.apache.calcite.plan.hep.HepInstruction.RuleCollection")]
    internal sealed class RuleCollection : HepInstruction
    {

        [Provenance("org.apache.calcite.plan.hep.HepInstruction.RuleCollection.rules")]
        internal readonly ImmutableArray<Rule> Rules;

        [Provenance("org.apache.calcite.plan.hep.HepInstruction.RuleCollection", "RuleCollection(Collection<RelOptRule>)")]
        internal RuleCollection(IEnumerable<Rule> rules)
        {
            Rules = rules.ToImmutableArray();
        }

        [Provenance("org.apache.calcite.plan.hep.HepInstruction.RuleCollection", "prepare(PrepareContext)")]
        internal override HepState Prepare(PrepareContext px) => new State(px, this);

        [Provenance("org.apache.calcite.plan.hep.HepInstruction.RuleCollection.State")]
        internal sealed class State : HepState
        {
            internal readonly RuleCollection Instruction;

            [Provenance("org.apache.calcite.plan.hep.HepInstruction.RuleCollection.State", "State(PrepareContext)")]
            internal State(PrepareContext px, RuleCollection instruction) : base(px) => Instruction = instruction;

            [Provenance("org.apache.calcite.plan.hep.HepInstruction.RuleCollection.State", "execute()")]
            internal override void Execute() => Planner.ExecuteRuleCollection(Instruction, this);
        }

    }

    /// <summary>
    /// Executes converter rules, but only where a conversion is actually required.
    /// </summary>
    [Provenance("org.apache.calcite.plan.hep.HepInstruction.ConverterRules")]
    internal sealed class ConverterRules : HepInstruction
    {

        [Provenance("org.apache.calcite.plan.hep.HepInstruction.ConverterRules.guaranteed")]
        internal readonly bool Guaranteed;

        [Provenance("org.apache.calcite.plan.hep.HepInstruction.ConverterRules", "ConverterRules(boolean)")]
        internal ConverterRules(bool guaranteed)
        {
            Guaranteed = guaranteed;
        }

        [Provenance("org.apache.calcite.plan.hep.HepInstruction.ConverterRules", "prepare(PrepareContext)")]
        internal override HepState Prepare(PrepareContext px) => new State(px, this);

        [Provenance("org.apache.calcite.plan.hep.HepInstruction.ConverterRules.State")]
        internal sealed class State : HepState
        {
            internal readonly ConverterRules Instruction;

            [Provenance("org.apache.calcite.plan.hep.HepInstruction.ConverterRules.State.ruleSet")]
            internal HashSet<Rule>? RuleSet;

            [Provenance("org.apache.calcite.plan.hep.HepInstruction.ConverterRules.State", "State(PrepareContext)")]
            internal State(PrepareContext px, ConverterRules instruction) : base(px) => Instruction = instruction;

            [Provenance("org.apache.calcite.plan.hep.HepInstruction.ConverterRules.State", "execute()")]
            internal override void Execute() => Planner.ExecuteConverterRules(Instruction, this);
        }

    }

    /// <summary>
    /// Finds common sub-expressions (vertices with more than one parent) and applies the
    /// <see cref="ICommonSubExprRule"/>s to them.
    /// </summary>
    [Provenance("org.apache.calcite.plan.hep.HepInstruction.CommonRelSubExprRules")]
    internal sealed class CommonRelSubExprRules : HepInstruction
    {

        [Provenance("org.apache.calcite.plan.hep.HepInstruction.CommonRelSubExprRules", "prepare(PrepareContext)")]
        internal override HepState Prepare(PrepareContext px) => new State(px, this);

        [Provenance("org.apache.calcite.plan.hep.HepInstruction.CommonRelSubExprRules.State")]
        internal sealed class State : HepState
        {
            internal readonly CommonRelSubExprRules Instruction;

            [Provenance("org.apache.calcite.plan.hep.HepInstruction.CommonRelSubExprRules.State.ruleSet")]
            internal HashSet<Rule>? RuleSet;

            [Provenance("org.apache.calcite.plan.hep.HepInstruction.CommonRelSubExprRules.State", "State(PrepareContext)")]
            internal State(PrepareContext px, CommonRelSubExprRules instruction) : base(px) => Instruction = instruction;

            [Provenance("org.apache.calcite.plan.hep.HepInstruction.CommonRelSubExprRules.State", "execute()")]
            internal override void Execute() => Planner.ExecuteCommonRelSubExprRules(Instruction, this);
        }

    }

    /// <summary>
    /// Executes a given rule.
    /// </summary>
    [Provenance("org.apache.calcite.plan.hep.HepInstruction.RuleInstance")]
    internal sealed class RuleInstance : HepInstruction
    {

        [Provenance("org.apache.calcite.plan.hep.HepInstruction.RuleInstance.rule")]
        internal readonly Rule Rule;

        [Provenance("org.apache.calcite.plan.hep.HepInstruction.RuleInstance", "RuleInstance(RelOptRule)")]
        internal RuleInstance(Rule rule)
        {
            Rule = rule;
        }

        [Provenance("org.apache.calcite.plan.hep.HepInstruction.RuleInstance", "prepare(PrepareContext)")]
        internal override HepState Prepare(PrepareContext px) => new State(px, this);

        [Provenance("org.apache.calcite.plan.hep.HepInstruction.RuleInstance.State")]
        internal sealed class State : HepState
        {
            internal readonly RuleInstance Instruction;

            [Provenance("org.apache.calcite.plan.hep.HepInstruction.RuleInstance.State", "State(PrepareContext)")]
            internal State(PrepareContext px, RuleInstance instruction) : base(px) => Instruction = instruction;

            [Provenance("org.apache.calcite.plan.hep.HepInstruction.RuleInstance.State", "execute()")]
            internal override void Execute() => Planner.ExecuteRuleInstance(Instruction, this);
        }

    }

    /// <summary>
    /// Executes a rule looked up by its description.
    /// </summary>
    [Provenance("org.apache.calcite.plan.hep.HepInstruction.RuleLookup")]
    internal sealed class RuleLookup : HepInstruction
    {

        [Provenance("org.apache.calcite.plan.hep.HepInstruction.RuleLookup.ruleDescription")]
        internal readonly string RuleDescription;

        [Provenance("org.apache.calcite.plan.hep.HepInstruction.RuleLookup", "RuleLookup(String)")]
        internal RuleLookup(string ruleDescription)
        {
            RuleDescription = ruleDescription;
        }

        [Provenance("org.apache.calcite.plan.hep.HepInstruction.RuleLookup", "prepare(PrepareContext)")]
        internal override HepState Prepare(PrepareContext px) => new State(px, this);

        [Provenance("org.apache.calcite.plan.hep.HepInstruction.RuleLookup.State")]
        internal sealed class State : HepState
        {
            internal readonly RuleLookup Instruction;

            [Provenance("org.apache.calcite.plan.hep.HepInstruction.RuleLookup.State.rule")]
            internal Rule? Rule;

            [Provenance("org.apache.calcite.plan.hep.HepInstruction.RuleLookup.State", "State(PrepareContext)")]
            internal State(PrepareContext px, RuleLookup instruction) : base(px) => Instruction = instruction;

            [Provenance("org.apache.calcite.plan.hep.HepInstruction.RuleLookup.State", "init()")]
            internal override void Init() => Rule = null;

            [Provenance("org.apache.calcite.plan.hep.HepInstruction.RuleLookup.State", "execute()")]
            internal override void Execute() => Planner.ExecuteRuleLookup(Instruction, this);
        }

    }

    /// <summary>
    /// Sets the match order for subsequent instructions.
    /// </summary>
    [Provenance("org.apache.calcite.plan.hep.HepInstruction.MatchOrder")]
    internal sealed class MatchOrder : HepInstruction
    {

        [Provenance("org.apache.calcite.plan.hep.HepInstruction.MatchOrder.order")]
        internal readonly HepMatchOrder Order;

        [Provenance("org.apache.calcite.plan.hep.HepInstruction.MatchOrder", "MatchOrder(HepMatchOrder)")]
        internal MatchOrder(HepMatchOrder order)
        {
            Order = order;
        }

        [Provenance("org.apache.calcite.plan.hep.HepInstruction.MatchOrder", "prepare(PrepareContext)")]
        internal override HepState Prepare(PrepareContext px) => new State(px, this);

        [Provenance("org.apache.calcite.plan.hep.HepInstruction.MatchOrder.State")]
        internal sealed class State : HepState
        {
            internal readonly MatchOrder Instruction;

            [Provenance("org.apache.calcite.plan.hep.HepInstruction.MatchOrder.State", "State(PrepareContext)")]
            internal State(PrepareContext px, MatchOrder instruction) : base(px) => Instruction = instruction;

            [Provenance("org.apache.calcite.plan.hep.HepInstruction.MatchOrder.State", "execute()")]
            internal override void Execute() => Planner.ExecuteMatchOrder(Instruction, this);
        }

    }

    /// <summary>
    /// Sets the match limit for subsequent instructions.
    /// </summary>
    [Provenance("org.apache.calcite.plan.hep.HepInstruction.MatchLimit")]
    internal sealed class MatchLimit : HepInstruction
    {

        [Provenance("org.apache.calcite.plan.hep.HepInstruction.MatchLimit.limit")]
        internal readonly int Limit;

        [Provenance("org.apache.calcite.plan.hep.HepInstruction.MatchLimit", "MatchLimit(int)")]
        internal MatchLimit(int limit)
        {
            Limit = limit;
        }

        [Provenance("org.apache.calcite.plan.hep.HepInstruction.MatchLimit", "prepare(PrepareContext)")]
        internal override HepState Prepare(PrepareContext px) => new State(px, this);

        [Provenance("org.apache.calcite.plan.hep.HepInstruction.MatchLimit.State")]
        internal sealed class State : HepState
        {
            internal readonly MatchLimit Instruction;

            [Provenance("org.apache.calcite.plan.hep.HepInstruction.MatchLimit.State", "State(PrepareContext)")]
            internal State(PrepareContext px, MatchLimit instruction) : base(px) => Instruction = instruction;

            [Provenance("org.apache.calcite.plan.hep.HepInstruction.MatchLimit.State", "execute()")]
            internal override void Execute() => Planner.ExecuteMatchLimit(Instruction, this);
        }

    }

    /// <summary>
    /// Executes a sub-program, repeatedly, until it reaches a fixed point.
    /// </summary>
    [Provenance("org.apache.calcite.plan.hep.HepInstruction.SubProgram")]
    internal sealed class SubProgram : HepInstruction
    {

        [Provenance("org.apache.calcite.plan.hep.HepInstruction.SubProgram.subProgram")]
        internal readonly HepProgram Program;

        [Provenance("org.apache.calcite.plan.hep.HepInstruction.SubProgram", "SubProgram(HepProgram)")]
        internal SubProgram(HepProgram program)
        {
            Program = program;
        }

        [Provenance("org.apache.calcite.plan.hep.HepInstruction.SubProgram", "prepare(PrepareContext)")]
        internal override HepState Prepare(PrepareContext px) => new State(px, this);

        [Provenance("org.apache.calcite.plan.hep.HepInstruction.SubProgram.State")]
        internal sealed class State : HepState
        {
            internal readonly SubProgram Instruction;

            [Provenance("org.apache.calcite.plan.hep.HepInstruction.SubProgram.State.subProgramState")]
            internal readonly HepProgram.State SubProgramState;

            [Provenance("org.apache.calcite.plan.hep.HepInstruction.SubProgram.State", "State(PrepareContext)")]
            internal State(PrepareContext px, SubProgram instruction) : base(px)
            {
                Instruction = instruction;
                SubProgramState = (HepProgram.State)instruction.Program.Prepare(px);
            }

            [Provenance("org.apache.calcite.plan.hep.HepInstruction.SubProgram.State", "init()")]
            internal override void Init() => SubProgramState.Init();

            [Provenance("org.apache.calcite.plan.hep.HepInstruction.SubProgram.State", "execute()")]
            internal override void Execute() => Planner.ExecuteSubProgram(Instruction, this);
        }

    }

    /// <summary>
    /// Begins a group of rules.
    /// </summary>
    [Provenance("org.apache.calcite.plan.hep.HepInstruction.BeginGroup")]
    internal sealed class BeginGroup : HepInstruction
    {

        [Provenance("org.apache.calcite.plan.hep.HepInstruction.BeginGroup.endGroup")]
        internal new readonly EndGroup EndGroup;

        [Provenance("org.apache.calcite.plan.hep.HepInstruction.BeginGroup", "BeginGroup(EndGroup)")]
        internal BeginGroup(EndGroup endGroup)
        {
            EndGroup = endGroup;
        }

        [Provenance("org.apache.calcite.plan.hep.HepInstruction.BeginGroup", "prepare(PrepareContext)")]
        internal override HepState Prepare(PrepareContext px) => new State(px, this);

        [Provenance("org.apache.calcite.plan.hep.HepInstruction.BeginGroup.State")]
        internal sealed class State : HepState
        {
            internal readonly BeginGroup Instruction;

            [Provenance("org.apache.calcite.plan.hep.HepInstruction.BeginGroup.State.endGroup")]
            internal readonly EndGroup.State EndGroup;

            [Provenance("org.apache.calcite.plan.hep.HepInstruction.BeginGroup.State", "State(PrepareContext)")]
            internal State(PrepareContext px, BeginGroup instruction) : base(px)
            {
                Instruction = instruction;
                EndGroup = px.EndGroupState ?? throw new InvalidOperationException("endGroupState");
            }

            [Provenance("org.apache.calcite.plan.hep.HepInstruction.BeginGroup.State", "execute()")]
            internal override void Execute() => Planner.ExecuteBeginGroup(Instruction, this);
        }

    }

    /// <summary>
    /// Placeholder that marks the beginning of a group under construction.
    /// </summary>
    [Provenance("org.apache.calcite.plan.hep.HepInstruction.Placeholder")]
    internal sealed class Placeholder : HepInstruction
    {

        [Provenance("org.apache.calcite.plan.hep.HepInstruction.Placeholder", "prepare(PrepareContext)")]
        internal override HepState Prepare(PrepareContext px) => throw new NotSupportedException();

    }

    /// <summary>
    /// Ends a group of rules, firing the group collectively.
    /// </summary>
    [Provenance("org.apache.calcite.plan.hep.HepInstruction.EndGroup")]
    internal sealed class EndGroup : HepInstruction
    {

        [Provenance("org.apache.calcite.plan.hep.HepInstruction.EndGroup", "prepare(PrepareContext)")]
        internal override HepState Prepare(PrepareContext px) => new State(px, this);

        [Provenance("org.apache.calcite.plan.hep.HepInstruction.EndGroup.State")]
        internal sealed class State : HepState
        {
            internal readonly EndGroup Instruction;

            [Provenance("org.apache.calcite.plan.hep.HepInstruction.EndGroup.State.ruleSet")]
            internal readonly HashSet<Rule> RuleSet = new HashSet<Rule>();

            [Provenance("org.apache.calcite.plan.hep.HepInstruction.EndGroup.State.collecting")]
            internal bool Collecting = true;

            [Provenance("org.apache.calcite.plan.hep.HepInstruction.EndGroup.State", "State(PrepareContext)")]
            internal State(PrepareContext px, EndGroup instruction) : base(px) => Instruction = instruction;

            [Provenance("org.apache.calcite.plan.hep.HepInstruction.EndGroup.State", "init()")]
            internal override void Init() => Collecting = true;

            [Provenance("org.apache.calcite.plan.hep.HepInstruction.EndGroup.State", "execute()")]
            internal override void Execute() => Planner.ExecuteEndGroup(Instruction, this);
        }

    }

    /// <summary>
    /// Everything that might be needed to initialize a <see cref="HepState"/> for an instruction.
    /// </summary>
    [Provenance("org.apache.calcite.plan.hep.HepInstruction.PrepareContext")]
    internal sealed class PrepareContext
    {

        [Provenance("org.apache.calcite.plan.hep.HepInstruction.PrepareContext.planner")]
        internal readonly HepPlanner Planner;

        [Provenance("org.apache.calcite.plan.hep.HepInstruction.PrepareContext.programState")]
        internal readonly HepProgram.State? ProgramState;

        [Provenance("org.apache.calcite.plan.hep.HepInstruction.PrepareContext.endGroupState")]
        internal readonly EndGroup.State? EndGroupState;

        [Provenance("org.apache.calcite.plan.hep.HepInstruction.PrepareContext", "PrepareContext(HepPlanner, HepProgram.State, EndGroup.State)")]
        PrepareContext(HepPlanner planner, HepProgram.State? programState, EndGroup.State? endGroupState)
        {
            Planner = planner;
            ProgramState = programState;
            EndGroupState = endGroupState;
        }

        [Provenance("org.apache.calcite.plan.hep.HepInstruction.PrepareContext", "create(HepPlanner)")]
        internal static PrepareContext Create(HepPlanner planner) => new PrepareContext(planner, null, null);

        [Provenance("org.apache.calcite.plan.hep.HepInstruction.PrepareContext", "withProgramState(HepProgram.State)")]
        internal PrepareContext WithProgramState(HepProgram.State programState) => new PrepareContext(Planner, programState, EndGroupState);

        [Provenance("org.apache.calcite.plan.hep.HepInstruction.PrepareContext", "withEndGroupState(EndGroup.State)")]
        internal PrepareContext WithEndGroupState(EndGroup.State endGroupState) => new PrepareContext(Planner, ProgramState, endGroupState);

    }

}
