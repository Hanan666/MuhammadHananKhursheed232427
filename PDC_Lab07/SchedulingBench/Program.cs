using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

class Program
{
    const int N = 20_000;

    static int workers;
    static bool uniform;
    static long expected;

    record Result(
        double Wall,
        double[] Busy,
        bool Correct
    );

    static readonly string[] policies =
    {
        "static",
        "static,1",
        "static,64",
        "dynamic,1",
        "dynamic,64",
        "guided,1",
        "guided,64",
        "Parallel.For"
    };

    static void Main(string[] args)
    {
        workers = args.Length > 0 ? int.Parse(args[0]) : 4;
        uniform = args.Length > 1 &&
                  args[1].Equals(
                      "uniform",
                      StringComparison.OrdinalIgnoreCase);

        if (workers < 1)
        {
            Console.WriteLine("Thread count must be positive.");
            return;
        }

        Console.WriteLine($"Logical cores: {Environment.ProcessorCount}");
        Console.WriteLine($"Threads: {workers}");
        Console.WriteLine(
            $"Workload: {(uniform ? "uniform" : "uneven")}");

        // Warm up the work function.
        for (int i = 0; i < 100; i++)
            Work(i);

        Stopwatch serialTimer = Stopwatch.StartNew();

        expected = 0;
        for (int i = 0; i < N; i++)
            expected += Work(i);

        serialTimer.Stop();

        Console.WriteLine(
            $"Serial time: {serialTimer.Elapsed.TotalMilliseconds:F3} ms");
        Console.WriteLine($"Expected checksum: {expected}\n");

        Console.WriteLine(
            "{0,-14} {1,11} {2,11} {3,11} {4,10} {5,9}",
            "Policy", "Wall ms", "Busy max", "Busy min",
            "Imbalance", "Correct");

        foreach (string policy in policies)
        {
            // Untimed warm-up.
            Run(policy);

            Result? best = null;
            bool allCorrect = true;

            for (int trial = 0; trial < 3; trial++)
            {
                Result result = Run(policy);
                allCorrect &= result.Correct;

                if (best == null || result.Wall < best.Wall)
                    best = result;
            }

            Result selected = best!;

            double max = selected.Busy.Max();
            double min = selected.Busy.Min();
            double imbalance =
                min > 0 ? max / min : double.PositiveInfinity;

            Console.WriteLine(
                "{0,-14} {1,11:F3} {2,11:F3} {3,11:F3} {4,10:F3} {5,9}",
                policy, selected.Wall, max, min, imbalance,
                allCorrect ? "YES" : "NO");

            Console.WriteLine(
                "  Worker busy times: " +
                string.Join(", ",
                    selected.Busy.Select(
                        (time, id) => $"W{id}={time:F3}")));

            if (policy == "static")
            {
                int longest = Array.IndexOf(selected.Busy, max);
                Console.WriteLine(
                    $"  Longest static worker: W{longest}");
            }
        }
    }

    // The last quarter has heavier iterations.
    static long Work(int index)
    {
        int rounds = uniform
            ? 4000
            : (index >= N * 3 / 4 ? 15000 : 300);

        uint value = (uint)(index + 1);

        unchecked
        {
            for (int j = 0; j < rounds; j++)
                value = value * 1664525u + 1013904223u;
        }

        return value;
    }

    static Result Run(string policy)
    {
        if (policy == "Parallel.For")
            return RunParallelFor();

        long[] sums = new long[workers];
        double[] busy = new double[workers];
        Thread[] threads = new Thread[workers];

        int next = 0;
        object claimGate = new object();

        using var ready = new CountdownEvent(workers);
        using var start = new ManualResetEventSlim(false);

        void Worker(int id)
        {
            ready.Signal();
            start.Wait();

            Stopwatch timer = Stopwatch.StartNew();
            long localSum = 0;

            void Process(int begin, int end)
            {
                for (int i = begin; i < end; i++)
                    localSum += Work(i);
            }

            if (policy == "static")
            {
                int begin = (int)((long)id * N / workers);
                int end = (int)((long)(id + 1) * N / workers);
                Process(begin, end);
            }
            else if (policy.StartsWith("static,"))
            {
                int chunk = int.Parse(policy.Split(',')[1]);

                for (int begin = id * chunk;
                     begin < N;
                     begin += workers * chunk)
                {
                    Process(begin, Math.Min(begin + chunk, N));
                }
            }
            else if (policy.StartsWith("dynamic,"))
            {
                int chunk = int.Parse(policy.Split(',')[1]);

                while (true)
                {
                    int begin =
                        Interlocked.Add(ref next, chunk) - chunk;

                    if (begin >= N)
                        break;

                    Process(begin, Math.Min(begin + chunk, N));
                }
            }
            else if (policy.StartsWith("guided,"))
            {
                int minimumChunk = int.Parse(policy.Split(',')[1]);

                while (true)
                {
                    int begin;
                    int end;

                    lock (claimGate)
                    {
                        if (next >= N)
                            break;

                        int remaining = N - next;

                        int chunk = Math.Max(
                            minimumChunk,
                            (remaining + workers - 1) / workers);

                        begin = next;
                        end = Math.Min(begin + chunk, N);
                        next = end;
                    }

                    Process(begin, end);
                }
            }

            timer.Stop();

            sums[id] = localSum;
            busy[id] = timer.Elapsed.TotalMilliseconds;
        }

        for (int id = 0; id < workers; id++)
        {
            int workerId = id;
            threads[id] = new Thread(() => Worker(workerId));
            threads[id].Start();
        }

        ready.Wait();

        Stopwatch wallTimer = Stopwatch.StartNew();
        start.Set();

        foreach (Thread thread in threads)
            thread.Join();

        wallTimer.Stop();

        return new Result(
            wallTimer.Elapsed.TotalMilliseconds,
            busy,
            sums.Sum() == expected);
    }

    sealed class LocalState
    {
        public long Sum;
        public Stopwatch Timer = Stopwatch.StartNew();
    }

    static Result RunParallelFor()
    {
        long total = 0;

        var busyByThread = new ConcurrentDictionary<int, double>();

        Stopwatch wallTimer = Stopwatch.StartNew();

        Parallel.For<LocalState>(
            0,
            N,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = workers
            },
            () => new LocalState(),
            (index, loopState, local) =>
            {
                local.Sum += Work(index);
                return local;
            },
            local =>
            {
                local.Timer.Stop();

                Interlocked.Add(ref total, local.Sum);

                int id = Environment.CurrentManagedThreadId;
                double elapsed = local.Timer.Elapsed.TotalMilliseconds;

                busyByThread.AddOrUpdate(
                    id,
                    elapsed,
                    (_, previous) => previous + elapsed);
            });

        wallTimer.Stop();

        double[] busy = busyByThread
            .OrderBy(entry => entry.Key)
            .Select(entry => entry.Value)
            .ToArray();

        return new Result(
            wallTimer.Elapsed.TotalMilliseconds,
            busy,
            total == expected);
    }
}