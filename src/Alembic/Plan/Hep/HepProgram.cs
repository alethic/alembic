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
public sealed class HepProgram : HepInstruction
{

    /// <summary>
    /// Match-limit value meaning "keep matching until no more matches occur".
    /// </summary>
    public const int MatchUntilFixpoint = int.MaxValue;

    internal readonly ImmutableArray<HepInstruction> Instructions;

    internal HepProgram(IEnumerable<HepInstruction> instructions)
    {
        Instructions = instructions.ToImmutableArray();
    }

    /// <summary>
    /// Starts building a program. The program begins with a match order of
    /// <see cref="HepMatchOrder.DepthFirst"/> and a match limit of <see cref="MatchUntilFixpoint"/>.
    /// </summary>
    public static HepProgramBuilder Builder()
    {
        return new HepProgramBuilder();
    }

    internal override HepState Prepare(PrepareContext px)
    {
        return new State(px, this);
    }

    /// <summary>
    /// The mutable state for one execution of a program.
    /// </summary>
    internal sealed class State : HepState
    {

        readonly HepProgram _program;
        internal readonly ImmutableArray<HepState> InstructionStates;
        internal int MatchLimit = MatchUntilFixpoint;
        internal HepMatchOrder MatchOrder = HepMatchOrder.DepthFirst;
        internal EndGroup.State? Group;

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

        internal override void Init()
        {
            MatchLimit = MatchUntilFixpoint;
            MatchOrder = HepMatchOrder.DepthFirst;
            Group = null;
        }

        internal override void Execute()
        {
            Planner.ExecuteProgram(_program, this);
        }

        internal bool SkippingGroup()
        {
            // Skip if we are in a group but have already collected its rule set.
            return Group is not null && !Group.Collecting;
        }

    }

}
