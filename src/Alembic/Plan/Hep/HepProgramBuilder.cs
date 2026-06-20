using System.Collections.Immutable;

using Alembic.Plan.Rules;

namespace Alembic.Plan.Hep;

/// <summary>
/// Fluent builder for a <see cref="HepProgram"/>.
/// </summary>
public sealed class HepProgramBuilder
{

    readonly ImmutableArray<IRule>.Builder _rules = ImmutableArray.CreateBuilder<IRule>();
    HepMatchOrder _matchOrder = HepMatchOrder.BottomUp;
    int _matchLimit = int.MaxValue;

    /// <summary>
    /// Adds a rule.
    /// </summary>
    public HepProgramBuilder AddRule(IRule rule)
    {
        _rules.Add(rule);
        return this;
    }

    /// <summary>
    /// Sets the traversal order.
    /// </summary>
    public HepProgramBuilder AddMatchOrder(HepMatchOrder matchOrder)
    {
        _matchOrder = matchOrder;
        return this;
    }

    /// <summary>
    /// Sets the maximum number of passes.
    /// </summary>
    public HepProgramBuilder AddMatchLimit(int matchLimit)
    {
        _matchLimit = matchLimit;
        return this;
    }

    /// <summary>
    /// Builds the program.
    /// </summary>
    public HepProgram Build()
    {
        return new HepProgram(_rules.ToImmutable(), _matchOrder, _matchLimit);
    }

}
