namespace Alembic.Plan.Hep;

/// <summary>
/// Able to execute an instruction or program, and holds all the mutable state for that execution.
/// </summary>
/// <remarks>
/// Programs and instructions are immutable so they can be reused; all mutable state lives in a state
/// object, allocated just before execution by <see cref="HepInstruction.Prepare"/> on the program and,
/// recursively, on each of its instructions.
/// </remarks>
abstract class HepState
{

    internal readonly HepPlanner Planner;
    internal readonly HepProgram.State? ProgramState;

    protected HepState(HepInstruction.PrepareContext px)
    {
        Planner = px.Planner;
        ProgramState = px.ProgramState;
    }

    /// <summary>
    /// Executes the instruction.
    /// </summary>
    internal abstract void Execute();

    /// <summary>
    /// Re-initializes the state before a (re-)run.
    /// </summary>
    internal virtual void Init()
    {

    }

}
