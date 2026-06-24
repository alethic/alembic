using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Alembic.Plan.Hep;

/// <summary>
/// Specifies the order in which a <see cref="HepPlanner"/> attempts its rules. Build one with a
/// <see cref="HepProgramBuilder"/>.
/// </summary>
/// <remarks>
/// A program is immutable, but the planner uses its state as read/write during planning, so a program
/// can only be used by one planner at a time.
/// </remarks>
[Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.hep.HepProgram")]
public class HepProgram : HepInstruction
{

    /// <summary>
    /// Match-limit value meaning "keep matching until no more matches occur".
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.hep.HepProgram.MATCH_UNTIL_FIXPOINT")]
    public const int MatchUntilFixpoint = int.MaxValue;

    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.hep.HepProgram.instructions")]
    internal readonly ImmutableArray<HepInstruction> Instructions;

    /// <summary>
    /// Creates a program from <paramref name="instructions"/>. Use <see cref="Builder"/> to build one.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.hep.HepProgram", "HepProgram(List<HepInstruction>)")]
    internal HepProgram(IEnumerable<HepInstruction> instructions)
    {
        Instructions = instructions.ToImmutableArray();
    }

    /// <summary>
    /// Starts building a program. The program begins with a match order of
    /// <see cref="HepMatchOrder.DepthFirst"/> and a match limit of <see cref="MatchUntilFixpoint"/>.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.hep.HepProgram", "builder()")]
    public static HepProgramBuilder Builder()
    {
        return new HepProgramBuilder();
    }

    /// <inheritdoc/>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.hep.HepProgram", "prepare(PrepareContext)")]
    internal override HepState Prepare(PrepareContext px)
    {
        return new State(px, this);
    }

    /// <summary>
    /// The mutable state for one execution of a program.
    /// </summary>
    [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.hep.HepProgram.State")]
    internal sealed class State : HepState
    {

        readonly HepProgram _program;

        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.hep.HepProgram.State.instructionStates")]
        internal readonly ImmutableArray<HepState> InstructionStates;

        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.hep.HepProgram.State.matchLimit")]
        internal int MatchLimit = MatchUntilFixpoint;

        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.hep.HepProgram.State.matchOrder")]
        internal HepMatchOrder MatchOrder = HepMatchOrder.DepthFirst;

        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.hep.HepProgram.State.group")]
        internal EndGroup.State? Group;

        /// <summary>
        /// Creates the state for <paramref name="program"/>, preparing a child state for each instruction.
        /// </summary>
        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.hep.HepProgram.State", "State(PrepareContext, List<HepInstruction>)")]
        internal State(PrepareContext px, HepProgram program) : base(px)
        {
            _program = program;
            var px2 = px.WithProgramState(this);
            var states = new List<HepState>();
            var actions = new Dictionary<HepInstruction, Action<HepState>>();
            foreach (var instruction in program.Instructions)
            {
                HepState state;
                if (instruction is BeginGroup begin)
                {
                    // The BeginGroup state needs the EndGroup state, which we have not seen yet. Put a
                    // placeholder in the list now, and register an action to replace it when we reach
                    // the matching EndGroup.
                    var slot = states.Count;
                    var beginInstruction = instruction;
                    actions[begin.EndGroup] = endState =>
                        states[slot] = beginInstruction.Prepare(px2.WithEndGroupState((EndGroup.State)endState));
                    state = null!;
                }
                else
                {
                    state = instruction.Prepare(px2);
                    if (actions.TryGetValue(instruction, out var action))
                        action(state);
                }

                states.Add(state);
            }

            InstructionStates = states.ToImmutableArray();
        }

        /// <inheritdoc/>
        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.hep.HepProgram.State", "init()")]
        internal override void Init()
        {
            MatchLimit = MatchUntilFixpoint;
            MatchOrder = HepMatchOrder.DepthFirst;
            Group = null;
        }

        /// <inheritdoc/>
        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.hep.HepProgram.State", "execute()")]
        internal override void Execute()
        {
            Planner.ExecuteProgram(_program, this);
        }

        /// <summary>
        /// Whether execution is inside a group whose rule set has already been collected (so further
        /// matches are skipped).
        /// </summary>
        [Provenance(ProvenanceSource.Calcite, "org.apache.calcite.plan.hep.HepProgram.State", "skippingGroup()")]
        internal bool SkippingGroup()
        {
            // Skip if we are in a group but have already collected its rule set.
            return Group is not null && !Group.Collecting;
        }

    }

}
