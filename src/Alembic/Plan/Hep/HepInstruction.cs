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
public abstract class HepInstruction
{

    private protected HepInstruction()
    {

    }

    /// <summary>
    /// Creates runtime state for this instruction. The state copies from the context everything it will
    /// need to execute, and is discarded afterwards.
    /// </summary>
    internal abstract HepState Prepare(PrepareContext px);

    /// <summary>
    /// Executes all rules of a given type.
    /// </summary>
    internal sealed class RuleClass : HepInstruction
    {

        internal readonly Type RuleType;

        internal RuleClass(Type ruleType)
        {
            RuleType = ruleType;
        }

        internal override HepState Prepare(PrepareContext px) => new State(px, this);

        internal sealed class State : HepState
        {
            internal readonly RuleClass Instruction;
            internal HashSet<Rule>? RuleSet;

            internal State(PrepareContext px, RuleClass instruction) : base(px) => Instruction = instruction;

            internal override void Execute() => Planner.ExecuteRuleClass(Instruction, this);
        }

    }

    /// <summary>
    /// Executes all rules in a given collection.
    /// </summary>
    internal sealed class RuleCollection : HepInstruction
    {

        internal readonly ImmutableArray<Rule> Rules;

        internal RuleCollection(IEnumerable<Rule> rules)
        {
            Rules = rules.ToImmutableArray();
        }

        internal override HepState Prepare(PrepareContext px) => new State(px, this);

        internal sealed class State : HepState
        {
            internal readonly RuleCollection Instruction;

            internal State(PrepareContext px, RuleCollection instruction) : base(px) => Instruction = instruction;

            internal override void Execute() => Planner.ExecuteRuleCollection(Instruction, this);
        }

    }

    /// <summary>
    /// Executes converter rules, but only where a conversion is actually required.
    /// </summary>
    internal sealed class ConverterRules : HepInstruction
    {

        internal readonly bool Guaranteed;

        internal ConverterRules(bool guaranteed)
        {
            Guaranteed = guaranteed;
        }

        internal override HepState Prepare(PrepareContext px) => new State(px, this);

        internal sealed class State : HepState
        {
            internal readonly ConverterRules Instruction;
            internal HashSet<Rule>? RuleSet;

            internal State(PrepareContext px, ConverterRules instruction) : base(px) => Instruction = instruction;

            internal override void Execute() => Planner.ExecuteConverterRules(Instruction, this);
        }

    }

    /// <summary>
    /// Finds common sub-expressions (vertices with more than one parent) and applies the
    /// <see cref="ICommonSubExprRule"/>s to them.
    /// </summary>
    internal sealed class CommonRelSubExprRules : HepInstruction
    {

        internal override HepState Prepare(PrepareContext px) => new State(px, this);

        internal sealed class State : HepState
        {
            internal readonly CommonRelSubExprRules Instruction;
            internal HashSet<Rule>? RuleSet;

            internal State(PrepareContext px, CommonRelSubExprRules instruction) : base(px) => Instruction = instruction;

            internal override void Execute() => Planner.ExecuteCommonRelSubExprRules(Instruction, this);
        }

    }

    /// <summary>
    /// Executes a given rule.
    /// </summary>
    internal sealed class RuleInstance : HepInstruction
    {

        internal readonly Rule Rule;

        internal RuleInstance(Rule rule)
        {
            Rule = rule;
        }

        internal override HepState Prepare(PrepareContext px) => new State(px, this);

        internal sealed class State : HepState
        {
            internal readonly RuleInstance Instruction;

            internal State(PrepareContext px, RuleInstance instruction) : base(px) => Instruction = instruction;

            internal override void Execute() => Planner.ExecuteRuleInstance(Instruction, this);
        }

    }

    /// <summary>
    /// Executes a rule looked up by its description.
    /// </summary>
    internal sealed class RuleLookup : HepInstruction
    {

        internal readonly string RuleDescription;

        internal RuleLookup(string ruleDescription)
        {
            RuleDescription = ruleDescription;
        }

        internal override HepState Prepare(PrepareContext px) => new State(px, this);

        internal sealed class State : HepState
        {
            internal readonly RuleLookup Instruction;
            internal Rule? Rule;

            internal State(PrepareContext px, RuleLookup instruction) : base(px) => Instruction = instruction;

            internal override void Init() => Rule = null;

            internal override void Execute() => Planner.ExecuteRuleLookup(Instruction, this);
        }

    }

    /// <summary>
    /// Sets the match order for subsequent instructions.
    /// </summary>
    internal sealed class MatchOrder : HepInstruction
    {

        internal readonly HepMatchOrder Order;

        internal MatchOrder(HepMatchOrder order)
        {
            Order = order;
        }

        internal override HepState Prepare(PrepareContext px) => new State(px, this);

        internal sealed class State : HepState
        {
            internal readonly MatchOrder Instruction;

            internal State(PrepareContext px, MatchOrder instruction) : base(px) => Instruction = instruction;

            internal override void Execute() => Planner.ExecuteMatchOrder(Instruction, this);
        }

    }

    /// <summary>
    /// Sets the match limit for subsequent instructions.
    /// </summary>
    internal sealed class MatchLimit : HepInstruction
    {

        internal readonly int Limit;

        internal MatchLimit(int limit)
        {
            Limit = limit;
        }

        internal override HepState Prepare(PrepareContext px) => new State(px, this);

        internal sealed class State : HepState
        {
            internal readonly MatchLimit Instruction;

            internal State(PrepareContext px, MatchLimit instruction) : base(px) => Instruction = instruction;

            internal override void Execute() => Planner.ExecuteMatchLimit(Instruction, this);
        }

    }

    /// <summary>
    /// Executes a sub-program, repeatedly, until it reaches a fixed point.
    /// </summary>
    internal sealed class SubProgram : HepInstruction
    {

        internal readonly HepProgram Program;

        internal SubProgram(HepProgram program)
        {
            Program = program;
        }

        internal override HepState Prepare(PrepareContext px) => new State(px, this);

        internal sealed class State : HepState
        {
            internal readonly SubProgram Instruction;
            internal readonly HepProgram.State SubProgramState;

            internal State(PrepareContext px, SubProgram instruction) : base(px)
            {
                Instruction = instruction;
                SubProgramState = (HepProgram.State)instruction.Program.Prepare(px);
            }

            internal override void Init() => SubProgramState.Init();

            internal override void Execute() => Planner.ExecuteSubProgram(Instruction, this);
        }

    }

    /// <summary>
    /// Begins a group of rules.
    /// </summary>
    internal sealed class BeginGroup : HepInstruction
    {

        internal new readonly EndGroup EndGroup;

        internal BeginGroup(EndGroup endGroup)
        {
            EndGroup = endGroup;
        }

        internal override HepState Prepare(PrepareContext px) => new State(px, this);

        internal sealed class State : HepState
        {
            internal readonly BeginGroup Instruction;
            internal readonly EndGroup.State EndGroup;

            internal State(PrepareContext px, BeginGroup instruction) : base(px)
            {
                Instruction = instruction;
                EndGroup = px.EndGroupState ?? throw new InvalidOperationException("endGroupState");
            }

            internal override void Execute() => Planner.ExecuteBeginGroup(Instruction, this);
        }

    }

    /// <summary>
    /// Placeholder that marks the beginning of a group under construction.
    /// </summary>
    internal sealed class Placeholder : HepInstruction
    {

        internal override HepState Prepare(PrepareContext px) => throw new NotSupportedException();

    }

    /// <summary>
    /// Ends a group of rules, firing the group collectively.
    /// </summary>
    internal sealed class EndGroup : HepInstruction
    {

        internal override HepState Prepare(PrepareContext px) => new State(px, this);

        internal sealed class State : HepState
        {
            internal readonly EndGroup Instruction;
            internal readonly HashSet<Rule> RuleSet = new HashSet<Rule>();
            internal bool Collecting = true;

            internal State(PrepareContext px, EndGroup instruction) : base(px) => Instruction = instruction;

            internal override void Init() => Collecting = true;

            internal override void Execute() => Planner.ExecuteEndGroup(Instruction, this);
        }

    }

    /// <summary>
    /// Everything that might be needed to initialize a <see cref="HepState"/> for an instruction.
    /// </summary>
    internal sealed class PrepareContext
    {

        internal readonly HepPlanner Planner;
        internal readonly HepProgram.State? ProgramState;
        internal readonly EndGroup.State? EndGroupState;

        PrepareContext(HepPlanner planner, HepProgram.State? programState, EndGroup.State? endGroupState)
        {
            Planner = planner;
            ProgramState = programState;
            EndGroupState = endGroupState;
        }

        internal static PrepareContext Create(HepPlanner planner) => new PrepareContext(planner, null, null);

        internal PrepareContext WithProgramState(HepProgram.State programState) => new PrepareContext(Planner, programState, EndGroupState);

        internal PrepareContext WithEndGroupState(EndGroup.State endGroupState) => new PrepareContext(Planner, ProgramState, endGroupState);

    }

}
