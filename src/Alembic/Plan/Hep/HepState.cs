namespace Alembic.Plan.Hep;

/// <summary>
/// Able to execute an instruction or program, and holds all the mutable state for that execution.
/// </summary>
/// <remarks>
/// Programs and instructions are immutable so they can be reused; all mutable state lives in a state
/// object, allocated just before execution by <see cref="HepInstruction.Prepare"/> on the program and,
/// recursively, on each of its instructions.
/// </remarks>
[Provenance("org.apache.calcite.plan.hep.HepState")]
abstract class HepState
{

    [Provenance("org.apache.calcite.plan.hep.HepState.planner")]
    internal readonly HepPlanner Planner;

    [Provenance("org.apache.calcite.plan.hep.HepState.programState")]
    internal readonly HepProgram.State? ProgramState;

    [Provenance("org.apache.calcite.plan.hep.HepState", "HepState(HepInstruction.PrepareContext)")]
    protected HepState(HepInstruction.PrepareContext px)
    {
        Planner = px.Planner;
        ProgramState = px.ProgramState;
    }

    /// <summary>
    /// Executes the instruction.
    /// </summary>
    [Provenance("org.apache.calcite.plan.hep.HepState", "execute()")]
    internal abstract void Execute();

    /// <summary>
    /// Re-initializes the state before a (re-)run.
    /// </summary>
    [Provenance("org.apache.calcite.plan.hep.HepState", "init()")]
    internal virtual void Init()
    {

    }

}
