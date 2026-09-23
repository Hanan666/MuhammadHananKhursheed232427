using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

class Program
{
    const int Iterations = 50;

    static void Main()
    {
        // Search for ProcessLab.exe executable
        string processLabPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "ProcessLab", "bin", "Release", "net9.0", "ProcessLab.exe"));

        if (!File.Exists(processLabPath))
        {
            processLabPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "ProcessLab", "bin", "Debug", "net9.0", "ProcessLab.exe"));
        }

        if (!File.Exists(processLabPath))
        {
            Console.WriteLine("Error: ProcessLab.exe not found! Please build ProcessLab in Release mode first.");
            return;
        }

        var processStopwatch = Stopwatch.StartNew();
        for (int i = 0; i < Iterations; i++)
        {
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = processLabPath,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            psi.ArgumentList.Add("--child");

            using (Process? p = Process.Start(psi))
            {
                p?.WaitForExit();
            }
        }
        processStopwatch.Stop();

        var threadStopwatch = Stopwatch.StartNew();
        for (int i = 0; i < Iterations; i++)
        {
            Thread t = new Thread(() => { });
            t.Start();
            t.Join();
        }
        threadStopwatch.Stop();

        double avgProcessMs = processStopwatch.Elapsed.TotalMilliseconds / Iterations;
        double avgThreadMs = threadStopwatch.Elapsed.TotalMilliseconds / Iterations;

        Console.WriteLine($"Average process creation time: {avgProcessMs:F3} ms");
        Console.WriteLine($"Average thread creation time:  {avgThreadMs:F3} ms");
        Console.WriteLine($"Process creation was {(avgProcessMs / avgThreadMs):F1}x more expensive than thread creation.");
    }
}