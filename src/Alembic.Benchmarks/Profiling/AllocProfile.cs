using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;

namespace Alembic.Benchmarks;

/// <summary>
/// A quick type-level allocation profile of a full Volcano plan, driven off the CLR's
/// <c>GCAllocationTick</c> events (sampled every ~100 KB per type). Run with the <c>alloc</c> argument.
/// Not a benchmark — just answers "which types dominate a plan's allocation?" so we know what to attack
/// next now that digest hashing is allocation-free.
/// </summary>
static class AllocProfile
{

    public static void Run(int depth, int iterations)
    {
        using var listener = new TickListener();

        var bench = new PlanningBenchmarks { Depth = depth };

        // Warm up JIT/statics so their one-time allocations don't pollute the profile.
        for (int i = 0; i < 3; i++)
            _ = bench.Plan();

        listener.Reset();
        long before = GC.GetTotalAllocatedBytes(precise: true);
        for (int i = 0; i < iterations; i++)
            _ = bench.Plan();
        long total = GC.GetTotalAllocatedBytes(precise: true) - before;

        Console.WriteLine();
        Console.WriteLine($"Plan(depth={depth}) x{iterations}: {total / 1024.0 / 1024.0:F1} MB total, " +
            $"{total / (double)iterations:F0} B/plan");
        Console.WriteLine($"Sampled allocation by type (GCAllocationTick, ~100 KB/type granularity):");
        Console.WriteLine($"{"Type",-60} {"SampledBytes",14} {"Ticks",8}");

        foreach (var (type, bytes, ticks) in listener.Top(25))
            Console.WriteLine($"{Shorten(type),-60} {bytes,14:N0} {ticks,8}");
    }

    static string Shorten(string type)
    {
        // Drop namespaces for readability, keep generic arity hints.
        int lastDot = type.LastIndexOf('.');
        return type.Length > 60 && lastDot >= 0 ? type[(lastDot + 1)..] : type;
    }

    sealed class TickListener : EventListener
    {

        readonly object _gate = new();
        Dictionary<string, (long Bytes, long Ticks)> _byType = new();

        protected override void OnEventSourceCreated(EventSource source)
        {
            if (source.Name == "Microsoft-Windows-DotNETRuntime")
                // Keyword 0x1 = GC; AllocationTick rides the GC keyword at Verbose level.
                EnableEvents(source, EventLevel.Verbose, (EventKeywords)0x1);
        }

        protected override void OnEventWritten(EventWrittenEventArgs e)
        {
            if (e.EventName is null || !e.EventName.StartsWith("GCAllocationTick") || e.Payload is null)
                return;

            string? type = null;
            long amount = 0;
            for (int i = 0; i < e.Payload.Count; i++)
            {
                switch (e.PayloadNames?[i])
                {
                    case "TypeName": type = e.Payload[i] as string; break;
                    case "AllocationAmount64" when amount == 0: amount = Convert.ToInt64(e.Payload[i]); break;
                    case "AllocationAmount" when amount == 0: amount = Convert.ToInt64(e.Payload[i]); break;
                }
            }

            if (type is null)
                return;

            lock (_gate)
            {
                _byType.TryGetValue(type, out var cur);
                _byType[type] = (cur.Bytes + amount, cur.Ticks + 1);
            }
        }

        public void Reset()
        {
            lock (_gate)
                _byType = new();
        }

        public IEnumerable<(string Type, long Bytes, long Ticks)> Top(int n)
        {
            lock (_gate)
                return _byType
                    .Select(kv => (kv.Key, kv.Value.Bytes, kv.Value.Ticks))
                    .OrderByDescending(t => t.Bytes)
                    .Take(n)
                    .ToList();
        }

    }

}
