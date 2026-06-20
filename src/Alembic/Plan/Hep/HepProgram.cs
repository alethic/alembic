using System.Collections.Immutable;

using Alembic.Plan.Rules;

namespace Alembic.Plan.Hep;

/// <summary>
/// The set of rules a <see cref="HepPlanner"/> applies, plus the order and a limit on how many
/// passes it makes.
/// </summary>
public sealed class HepProgram
{

    /// <summary>
    /// Creates a program.
    /// </summary>
    public HepProgram(ImmutableArray<IRule> rules, HepMatchOrder matchOrder, int matchLimit)
    {
        Rules = rules;
        MatchOrder = matchOrder;
        MatchLimit = matchLimit;
    }

    /// <summary>
    /// The rules, applied in order within each pass.
    /// </summary>
    public ImmutableArray<IRule> Rules { get; }

    /// <summary>
    /// The traversal order.
    /// </summary>
    public HepMatchOrder MatchOrder { get; }

    /// <summary>
    /// The maximum number of passes, or <see cref="int.MaxValue"/> for the default.
    /// </summary>
    public int MatchLimit { get; }

    /// <summary>
    /// Starts building a program.
    /// </summary>
    public static HepProgramBuilder Builder()
    {
        return new HepProgramBuilder();
    }

}
