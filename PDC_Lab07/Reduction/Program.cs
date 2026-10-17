using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

class Program
{
    static readonly int[] data = Enumerable.Range(1, 1_000_000).ToArray();
    static readonly object gate = new object();

    static int threads;
    static long expectedSum;
    static int expectedMax;

    static void Main(string[] args)
    {
        threads = args.Length > 0 ? int.Parse(args[0]) : 4;

        if (threads < 1 || threads > 512)
        {
            Console.WriteLine("Enter a thread count from 1 to 512.");
            return;
        }

        expectedSum = data.Sum(x => (long)x);
        expectedMax = data.Max();

        Console.WriteLine($"Logical cores: {Environment.ProcessorCount}");
        Console.WriteLine($"Threads: {threads}");
        Console.WriteLine($"Expected sum: {expectedSum}");
        Console.WriteLine($"Expected max: {expectedMax}\n");

        Console.WriteLine(
            "{0,-14} {1,-12} {2,-12} {3,12}",
            "Variant", "sum_ok", "max_ok", "Average ms");

        Measure("serial", SerialVersion);
        Measure("racy", RacyVersion);
        Measure("lock-each", LockEachVersion);
        Measure("lock-partial", LockPartialVersion);
        Measure("plinq", PlinqVersion);
    }

    static void Measure(string name, Func<(long sum, int max)> method)
    {
        method(); // Warm up before timing.

        double totalMs = 0;
        bool sumCorrect = true;
        bool maxCorrect = true;

        for (int run = 0; run < 3; run++)
        {
            Stopwatch timer = Stopwatch.StartNew();
            var result = method();
            timer.Stop();

            totalMs += timer.Elapsed.TotalMilliseconds;
            sumCorrect &= result.sum == expectedSum;
            maxCorrect &= result.max == expectedMax;

            if (name == "racy")
            {
                Console.WriteLine(
                    $"  Racy run {run + 1}: sum={result.sum}, max={result.max}");
            }
        }

        Console.WriteLine(
            "{0,-14} {1,-12} {2,-12} {3,12:F3}",
            name,
            sumCorrect ? "YES" : "NO",
            maxCorrect ? "YES" : "NO",
            totalMs / 3);
    }

    static (long sum, int max) SerialVersion()
    {
        long sum = 0;
        int max = int.MinValue;

        foreach (int value in data)
        {
            sum += value;

            if (value > max)
                max = value;
        }

        return (sum, max);
    }

    static (long sum, int max) RacyVersion()
    {
        long sum = 0;
        int max = int.MinValue;

        Parallel.For(0, threads, worker =>
        {
            int start = (int)((long)worker * data.Length / threads);
            int end = (int)((long)(worker + 1) * data.Length / threads);

            for (int i = start; i < end; i++)
            {
                // Intentionally unsafe for this experiment.
                sum += data[i];

                if (data[i] > max)
                    max = data[i];
            }
        });

        return (sum, max);
    }

    static (long sum, int max) LockEachVersion()
    {
        long sum = 0;
        int max = int.MinValue;

        Parallel.For(0, threads, worker =>
        {
            int start = (int)((long)worker * data.Length / threads);
            int end = (int)((long)(worker + 1) * data.Length / threads);

            for (int i = start; i < end; i++)
            {
                lock (gate)
                {
                    sum += data[i];

                    if (data[i] > max)
                        max = data[i];
                }
            }
        });

        return (sum, max);
    }

    static (long sum, int max) LockPartialVersion()
    {
        long sum = 0;
        int max = int.MinValue;

        Parallel.For(0, threads, worker =>
        {
            int start = (int)((long)worker * data.Length / threads);
            int end = (int)((long)(worker + 1) * data.Length / threads);

            long localSum = 0;
            int localMax = int.MinValue;

            for (int i = start; i < end; i++)
            {
                localSum += data[i];

                if (data[i] > localMax)
                    localMax = data[i];
            }

            // Combine once per worker.
            lock (gate)
            {
                sum += localSum;

                if (localMax > max)
                    max = localMax;
            }
        });

        return (sum, max);
    }

    static (long sum, int max) PlinqVersion()
    {
        return data
            .AsParallel()
            .WithDegreeOfParallelism(threads)
            .Aggregate(
                () => (sum: 0L, max: int.MinValue),

                (local, value) =>
                    (local.sum + value, Math.Max(local.max, value)),

                (left, right) =>
                    (left.sum + right.sum, Math.Max(left.max, right.max)),

                result => result
            );
    }
}