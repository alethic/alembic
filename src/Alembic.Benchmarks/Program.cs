using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.InProcess.Emit;

namespace Alembic.Benchmarks;

/// <summary>
/// Entry point for the Alembic benchmarks. With no arguments it runs the full BenchmarkDotNet switcher;
/// pass a filter such as <c>--filter *Digest*</c> or <c>--filter *Planning*</c> to select a suite, or the
/// bare word <c>alloc</c> to run the type-level allocation profile (<see cref="AllocProfile"/>) instead.
/// Benchmarks run via the in-process emit toolchain, so BenchmarkDotNet does not generate, build, or
/// spawn a separate runner process — build and run this project in Release for meaningful numbers.
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

        // The in-process toolchain runs each benchmark in this process instead of emitting a per-benchmark
        // project and shelling out to `dotnet build`/`run`. Everything else (warmup, pilot, statistics,
        // MemoryDiagnoser) behaves as normal.
        var config = DefaultConfig.Instance
            .AddJob(Job.Default.WithToolchain(InProcessEmitToolchain.Instance));

        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, config);
    }

}
