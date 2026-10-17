using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;

class Program
{
    const int Iterations = 20_000_000;

    // 16 longs = 128 bytes between padded counters.
    const int PaddingStride = 16;

    record Result(double Milliseconds, bool Correct);

    static void Main(string[] args)
    {
        int workers = args.Length > 0 ? int.Parse(args[0]) : 4;

        if (workers < 1)
        {
            Console.WriteLine("Thread count must be positive.");
            return;
        }

        Console.WriteLine(
            $"Logical cores: {Environment.ProcessorCount}");
        Console.WriteLine($"Threads: {workers}");
        Console.WriteLine("Assumed cache-line size: 64 bytes");
        Console.WriteLine("Padded counter spacing: 128 bytes");
        Console.WriteLine($"Increments per worker: {Iterations}\n");

        // Warm up each version.
        Run("adjacent", workers);
        Run("padded", workers);
        Run("local", workers);

        Console.WriteLine(
            "{0,-6} {1,14} {2,14} {3,14} {4,12} {5,10}",
            "Run", "Adjacent ms", "Padded ms",
            "Local ms", "Slowdown", "Correct");

        double adjacentTotal = 0;
        double paddedTotal = 0;
        double localTotal = 0;
        bool allCorrect = true;

        for (int trial = 1; trial <= 3; trial++)
        {
            // Rotate execution order to reduce ordering bias.
            Result adjacent;
            Result padded;
            Result local;

            if (trial == 1)
            {
                adjacent = Run("adjacent", workers);
                padded = Run("padded", workers);
                local = Run("local", workers);
            }
            else if (trial == 2)
            {
                padded = Run("padded", workers);
                local = Run("local", workers);
                adjacent = Run("adjacent", workers);
            }
            else
            {
                local = Run("local", workers);
                adjacent = Run("adjacent", workers);
                padded = Run("padded", workers);
            }

            bool correct =
                adjacent.Correct && padded.Correct && local.Correct;

            allCorrect &= correct;
            adjacentTotal += adjacent.Milliseconds;
            paddedTotal += padded.Milliseconds;
            localTotal += local.Milliseconds;

            Console.WriteLine(
                "{0,-6} {1,14:F3} {2,14:F3} {3,14:F3} {4,12:F3} {5,10}",
                trial,
                adjacent.Milliseconds,
                padded.Milliseconds,
                local.Milliseconds,
                adjacent.Milliseconds / padded.Milliseconds,
                correct ? "YES" : "NO");
        }

        double adjacentAverage = adjacentTotal / 3;
        double paddedAverage = paddedTotal / 3;
        double localAverage = localTotal / 3;

        Console.WriteLine(
            "\nAverage adjacent: {0:F3} ms", adjacentAverage);
        Console.WriteLine(
            "Average padded: {0:F3} ms", paddedAverage);
        Console.WriteLine(
            "Average local-accumulate: {0:F3} ms", localAverage);

        Console.WriteLine(
            "Slowdown adjacent / padded: {0:F3}",
            adjacentAverage / paddedAverage);

        Console.WriteLine(
            $"Counters correct: {(allCorrect ? "YES" : "NO")}");
    }

    static Result Run(string version, int workers)
    {
        int stride = version == "padded" ? PaddingStride : 1;

        long[] counters = new long[workers * stride];
        Thread[] threads = new Thread[workers];

        using var ready = new CountdownEvent(workers);
        using var start = new ManualResetEventSlim(false);

        for (int worker = 0; worker < workers; worker++)
        {
            int id = worker;

            threads[id] = new Thread(() =>
            {
                int index = id * stride;

                ready.Signal();
                start.Wait();

                if (version == "local")
                {
                    long localCounter = 0;

                    for (int i = 0; i < Iterations; i++)
                        localCounter++;

                    // Only one shared-array write per worker.
                    counters[index] = localCounter;
                }
                else
                {
                    for (int i = 0; i < Iterations; i++)
                    {
                        // Force a memory access on each iteration.
                        // Each worker owns a different counter.
                        long value = Volatile.Read(ref counters[index]);
                        Volatile.Write(ref counters[index], value + 1);
                    }
                }
            });

            threads[id].Start();
        }

        ready.Wait();

        Stopwatch timer = Stopwatch.StartNew();
        start.Set();

        foreach (Thread thread in threads)
            thread.Join();

        timer.Stop();

        bool correct = Enumerable.Range(0, workers)
            .All(id => counters[id * stride] == Iterations);

        return new Result(timer.Elapsed.TotalMilliseconds, correct);
    }
}