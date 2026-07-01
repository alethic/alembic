using System;

namespace Alembic.Benchmarks;

/// <summary>
/// Entry point for the Alembic benchmarks — a plain console app that runs everything in-process. Pass
/// a suite name to select one: <c>digest</c>, <c>planning</c>, or <c>alloc</c> (the type-level allocation
/// profile); with no argument it runs the timed suites. Build and run in Release for meaningful numbers.
/// </summary>
public static class Program
{

    public static void Main(string[] args)
    {
#if DEBUG
        Console.WriteLine("WARNING: this is a Debug build — the numbers are meaningless. Run with -c Release.");
#endif

        var suite = args.Length > 0 ? args[0].ToLowerInvariant() : "all";
        switch (suite)
        {
            case "alloc":
                AllocProfile.Run(depth: 6, iterations: 4000);
                break;
            case "digest":
                new DigestBenchmarks().Run();
                break;
            case "planning":
                new PlanningBenchmarks().Run();
                break;
            case "all":
                new DigestBenchmarks().Run();
                new PlanningBenchmarks().Run();
                break;
            default:
                Console.WriteLine($"Unknown suite '{suite}'. Use: digest | planning | alloc | all.");
                break;
        }
    }

}
