using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Tasks;

class Program
{
    const int N = 5_000_000;
    const int PASSES = 20;

    static double[] a = new double[N];
    static double[] b = new double[N];
    static double[] c = new double[N];

    static void VecAdd(ParallelOptions options)
    {
        Parallel.ForEach(Partitioner.Create(0, N), options, static range =>
        {
            for (int i = range.Item1; i < range.Item2; i++)
            {
                c[i] = a[i] + b[i];
            }
        });
    }

    static void Main(string[] args)
    {
        int maxDegree = args.Length > 0 ? int.Parse(args[0]) : Environment.ProcessorCount;
        var options = new ParallelOptions { MaxDegreeOfParallelism = maxDegree };

        for (int i = 0; i < N; i++) { a[i] = 1.0; b[i] = 2.0; }

        VecAdd(options);

        var sw = Stopwatch.StartNew();
        for (int pass = 0; pass < PASSES; pass++) VecAdd(options);
        sw.Stop();

        bool correct = true;
        for (int i = 0; i < N; i++) { if (c[i] != 3.0) { correct = false; break; } }

        Console.WriteLine($"Degree: {maxDegree} | Elapsed: {sw.ElapsedMilliseconds} ms | Correct: {(correct ? "YES" : "NO")}");
    }
}