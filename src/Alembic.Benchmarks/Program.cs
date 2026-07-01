using BenchmarkDotNet.Running;

namespace Alembic.Benchmarks;

/// <summary>
/// Entry point for the Alembic benchmarks. With no arguments it runs the full BenchmarkDotNet switcher;
/// pass a filter such as <c>--filter *Digest*</c> or <c>--filter *Planning*</c> to select a suite, or the
/// bare word <c>alloc</c> to run the type-level allocation profile (<see cref="AllocProfile"/>) instead.
/// </summary>
public static class Program
{

    public static void Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "alloc")
        {
            AllocProfile.Run(depth: 6, iterations: 4000);
            return;
        }

        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
    }

}
