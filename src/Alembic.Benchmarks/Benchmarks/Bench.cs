using System;
using System.Diagnostics;

namespace Alembic.Benchmarks;

/// <summary>
/// A tiny in-process micro-benchmark harness: warm up, then time a fixed number of iterations and
/// report mean time and bytes allocated per operation. Everything runs directly in this process — no
/// child <c>dotnet</c> builds or spawned runners — so build and run in <b>Release</b> for meaningful
/// numbers (a Debug build is unoptimized and its figures are noise).
/// </summary>
static class Bench
{

    // A sink the benchmark bodies write into so the JIT can't elide the work being measured.
    public static long Sink;

    public static void Header(string suite)
    {
        Console.WriteLine();
        Console.WriteLine($"== {suite} ==");
        Console.WriteLine($"{"Benchmark",-24}{"Mean",14}{"Allocated",16}{"Iterations",14}");
    }

    public static void Run(string name, int iterations, Action body)
    {
        // Warm the JIT and fill the per-thread pools so we measure steady state, not first-touch.
        int warmup = Math.Clamp(iterations / 10, 1, 10_000);
        for (int i = 0; i < warmup; i++)
            body();

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        long allocBefore = GC.GetAllocatedBytesForCurrentThread();
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
            body();
        sw.Stop();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - allocBefore;

        double nsPerOp = sw.Elapsed.TotalMilliseconds * 1_000_000.0 / iterations;
        double bytesPerOp = allocated / (double)iterations;
        Console.WriteLine($"{name,-24}{FormatTime(nsPerOp),14}{bytesPerOp,13:N1} B{iterations,14:N0}");
    }

    static string FormatTime(double ns)
        => ns >= 1_000_000 ? $"{ns / 1_000_000:N3} ms"
            : ns >= 1_000 ? $"{ns / 1_000:N3} us"
            : $"{ns:N1} ns";

}
