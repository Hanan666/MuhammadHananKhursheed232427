using System;
using System.Threading;

class Program
{
    static void Worker(int index)
    {
        int cpu = Thread.GetCurrentProcessorId();
        Console.WriteLine($"Thread {index} running on logical CPU {cpu}");
    }

    static void Main()
    {
        int count = Environment.ProcessorCount;
        Console.WriteLine($"Detected logical processors: {count}");

        Thread[] threads = new Thread[count];

        for (int i = 0; i < count; i++)
        {
            int index = i;
            threads[i] = new Thread(() => Worker(index));
            threads[i].Start();
        }

        foreach (Thread thread in threads)
        {
            thread.Join();
        }

        Console.WriteLine($"Threads created: {threads.Length}");
        Console.WriteLine("All threads completed.");
    }
}
