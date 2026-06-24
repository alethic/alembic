using System;
using System.Collections.Generic;


namespace Alembic.Plan.Hep;

/// <summary>
/// Builds a <see cref="HepProgram"/> from an ordered list of instructions.
/// </summary>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.hep.HepProgramBuilder")]
public class HepProgramBuilder
{

    readonly List<HepInstruction> _instructions = new List<HepInstruction>();

    /// <summary>
    /// If a group is under construction, the ordinal of its first instruction; otherwise -1.
    /// </summary>
    int _group = -1;

    /// <summary>
    /// Adds an instruction to attempt every rule of a given type. The rules themselves must also be
    /// added to the planner via <see cref="AbstractOpPlanner.AddRule"/>.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.hep.HepProgramBuilder", "addRuleClass(Class<R>)")]
    public HepProgramBuilder AddRuleClass<TRule>()
        where TRule : OpRule
    {
        return AddInstruction(new HepInstruction.RuleClass(typeof(TRule)));
    }

    /// <summary>
    /// Adds an instruction to attempt every rule in a collection. The collection is re-read on each run.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.hep.HepProgramBuilder", "addRuleCollection(Collection<RelOptRule>)")]
    public HepProgramBuilder AddRuleCollection(IEnumerable<OpRule> rules)
    {
        return AddInstruction(new HepInstruction.RuleCollection(rules));
    }

    /// <summary>
    /// Adds an instruction to attempt a specific rule.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.hep.HepProgramBuilder", "addRuleInstance(RelOptRule)")]
    public HepProgramBuilder AddRuleInstance(OpRule rule)
    {
        return AddInstruction(new HepInstruction.RuleInstance(rule));
    }

    /// <summary>
    /// Adds an instruction to attempt the rule with the given description (looked up from the rules
    /// added to the planner).
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.hep.HepProgramBuilder", "addRuleByDescription(String)")]
    public HepProgramBuilder AddRuleByDescription(string ruleDescription)
    {
        return AddInstruction(new HepInstruction.RuleLookup(ruleDescription));
    }

    /// <summary>
    /// Begins a group of rules. Subsequent rules are collected into the group until
    /// <see cref="AddGroupEnd"/> fires them together.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.hep.HepProgramBuilder", "addGroupBegin()")]
    public HepProgramBuilder AddGroupBegin()
    {
        Check(_group < 0);
        _group = _instructions.Count;
        return AddInstruction(new HepInstruction.Placeholder());
    }

    /// <summary>
    /// Ends a group of rules, firing the group collectively.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.hep.HepProgramBuilder", "addGroupEnd()")]
    public HepProgramBuilder AddGroupEnd()
    {
        Check(_group >= 0);
        var endGroup = new HepInstruction.EndGroup();
        _instructions[_group] = new HepInstruction.BeginGroup(endGroup);
        _group = -1;
        return AddInstruction(endGroup);
    }

    /// <summary>
    /// Adds an instruction to attempt converter rules, but only where a conversion is required.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.hep.HepProgramBuilder", "addConverters(boolean)")]
    public HepProgramBuilder AddConverters(bool guaranteed)
    {
        Check(_group < 0);
        return AddInstruction(new HepInstruction.ConverterRules(guaranteed));
    }

    /// <summary>
    /// Adds an instruction to attempt <see cref="ICommonSubExprRule"/>s, only where a vertex has more
    /// than one parent.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.hep.HepProgramBuilder", "addCommonRelSubExprInstruction()")]
    public HepProgramBuilder AddCommonRelSubExprInstruction()
    {
        Check(_group < 0);
        return AddInstruction(new HepInstruction.CommonOpSubExprRules());
    }

    /// <summary>
    /// Adds an instruction to change the match order for subsequent instructions.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.hep.HepProgramBuilder", "addMatchOrder(HepMatchOrder)")]
    public HepProgramBuilder AddMatchOrder(HepMatchOrder order)
    {
        Check(_group < 0);
        return AddInstruction(new HepInstruction.MatchOrder(order));
    }

    /// <summary>
    /// Adds an instruction to limit the number of matches for subsequent instructions.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.hep.HepProgramBuilder", "addMatchLimit(int)")]
    public HepProgramBuilder AddMatchLimit(int limit)
    {
        Check(_group < 0);
        return AddInstruction(new HepInstruction.MatchLimit(limit));
    }

    /// <summary>
    /// Adds an instruction to execute a subprogram, repeatedly, until it reaches a fixed point.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.hep.HepProgramBuilder", "addSubprogram(HepProgram)")]
    public HepProgramBuilder AddSubprogram(HepProgram program)
    {
        Check(_group < 0);
        return AddInstruction(new HepInstruction.SubProgram(program));
    }

    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.hep.HepProgramBuilder", "addInstruction(HepInstruction)")]
    HepProgramBuilder AddInstruction(HepInstruction instruction)
    {
        _instructions.Add(instruction);
        return this;
    }

    /// <summary>
    /// Returns the constructed program, clearing this builder.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.hep.HepProgramBuilder", "build()")]
    public HepProgram Build()
    {
        Check(_group < 0);
        var program = new HepProgram(_instructions);
        _instructions.Clear();
        _group = -1;
        return program;
    }

    static void Check(bool condition)
    {
        if (!condition)
            throw new InvalidOperationException("Illegal program builder state.");
    }

}
