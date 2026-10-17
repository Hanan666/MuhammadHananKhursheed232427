using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

class Program
{
    const int SIZE = 20_000_000;

    static void WorkA(double[] arr)
    {
        for (int i = 0; i < arr.Length; i++) arr[i] = arr[i] * 1.5 + 2.0;
    }

    static void WorkB(double[] arr)
    {
        for (int i = 0; i < arr.Length; i++) arr[i] = arr[i] * 2.5 - 1.0;
    }

    static void WorkC(double[] arr)
    {
        for (int i = 0; i < arr.Length; i++) arr[i] = arr[i] * 0.5 + 3.0;
    }

    static void Main(string[] args)
    {
        int maxDegree = args.Length > 0 ? int.Parse(args[0]) : Environment.ProcessorCount;
        var options = new ParallelOptions { MaxDegreeOfParallelism = maxDegree };

        double[] a = new double[SIZE];
        double[] b = new double[SIZE];
        double[] c = new double[SIZE];
        for (int i = 0; i < SIZE; i++) { a[i] = 1.0; b[i] = 1.0; c[i] = 1.0; }

        var sw = Stopwatch.StartNew();
        WorkA(a); double tA = sw.Elapsed.TotalSeconds; sw.Restart();
        WorkB(b); double tB = sw.Elapsed.TotalSeconds; sw.Restart();
        WorkC(c); double tC = sw.Elapsed.TotalSeconds; sw.Stop();
        double totalSerial = tA + tB + tC;

        Console.WriteLine($"Serial times (s): A = {tA:F4} | B = {tB:F4} | C = {tC:F4} | Total = {totalSerial:F4}");

        for (int i = 0; i < SIZE; i++) { a[i] = 1.0; b[i] = 1.0; c[i] = 1.0; }

        int tidA = -1, tidB = -1, tidC = -1;

        sw.Restart();
        Parallel.Invoke(options,
            () => { tidA = Environment.CurrentManagedThreadId; WorkA(a); },
            () => { tidB = Environment.CurrentManagedThreadId; WorkB(b); },
            () => { tidC = Environment.CurrentManagedThreadId; WorkC(c); }
        );
        sw.Stop();
        double wallTime = sw.Elapsed.TotalSeconds;

        bool match = (a[0] == 3.5) && (b[0] == 1.5) && (c[0] == 3.5);

        Console.WriteLine($"Degree: {maxDegree} | Wall time: {wallTime:F4}s | Speedup: {(totalSerial / wallTime):F2}x | Threads: A={tidA}, B={tidB}, C={tidC} | Match: {(match ? "YES" : "NO")}");
    }
}