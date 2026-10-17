using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

class Program
{
    static void Main(string[] args)
    {
        int maxDegree = args.Length > 0 ? int.Parse(args[0]) : Environment.ProcessorCount;
        int totalIterations = 16;
        int sleepMs = 100;

        Console.WriteLine($"Environment.ProcessorCount: {Environment.ProcessorCount}");

        var options = new ParallelOptions { MaxDegreeOfParallelism = maxDegree };
        var threadIds = new ConcurrentBag<int>();

        var sw = Stopwatch.StartNew();
        Parallel.For(0, totalIterations, options, i =>
        {
            int tid = Environment.CurrentManagedThreadId;
            threadIds.Add(tid);
            Console.WriteLine($"Iteration {i:D2} running on Managed Thread ID {tid}");
            Thread.Sleep(sleepMs);
        });
        sw.Stop();

        var distinctThreads = threadIds.Distinct().ToList();
        double predictedMs = (double)totalIterations * sleepMs / Math.Min(maxDegree, Environment.ProcessorCount);

        Console.WriteLine($"\nDistinct threads used: {distinctThreads.Count}");
        Console.WriteLine($"Elapsed time: {sw.ElapsedMilliseconds} ms");
        Console.WriteLine($"Predicted time: {predictedMs:F0} ms");
        Console.WriteLine($"Thread IDs seen: {string.Join(", ", distinctThreads)}");
    }
}