using System;
using System.Diagnostics;
using System.Threading.Tasks;
using MPI;

class Program
{
    const int N = 10000000;
    const int Repetitions = 3;

    static long Sum(int[] values)
    {
        long total = 0;

        for (int i = 0; i < values.Length; i++)
            total += values[i];

        return total;
    }

    static void Main(string[] args)
    {
        using (new MPI.Environment(ref args))
        {
            Intracommunicator comm = Communicator.world;
            int rank = comm.Rank;
            int size = comm.Size;

            if (N % size != 0)
            {
                if (rank == 0)
                    Console.WriteLine("Ranks must divide 10,000,000.");

                return;
            }

            int count = N / size;
            long expected = (long)N * (N + 1) / 2;

            int[] data = null;
            int[][] chunks = null;

            if (rank == 0)
            {
                data = new int[N];
                chunks = new int[size][];

                for (int i = 0; i < N; i++)
                    data[i] = i + 1;

                for (int worker = 0; worker < size; worker++)
                {
                    chunks[worker] = new int[count];
                    Array.Copy(data, worker * count, chunks[worker], 0, count);
                }

                Console.WriteLine($"Ranks / work partitions: {size}");
                Console.WriteLine($"Logical cores: {System.Environment.ProcessorCount}");
                Console.WriteLine($"Numbers: {N}");
                Console.WriteLine($"Expected sum: {expected}");
                Console.WriteLine("Average of 3 repetitions after 1 warm-up");
            }

            double collectiveTotal = 0;
            double pointTotal = 0;
            double sharedTotal = 0;

            bool collectiveCorrect = true;
            bool pointCorrect = true;
            bool sharedCorrect = true;

            for (int repetition = -1; repetition < Repetitions; repetition++)
            {
                comm.Barrier();
                Stopwatch timer = Stopwatch.StartNew();

                int[] local = comm.Scatter<int[]>(chunks, 0);
                long partial = Sum(local);
                long collectiveSum = comm.Reduce(
                    partial, Operation<long>.Add, 0);

                timer.Stop();

                double collectiveMs = comm.Reduce(
                    timer.Elapsed.TotalMilliseconds,
                    Operation<double>.Max, 0);

                if (rank == 0)
                {
                    collectiveCorrect &= collectiveSum == expected;

                    if (repetition >= 0)
                        collectiveTotal += collectiveMs;
                }

                comm.Barrier();
                timer.Restart();

                long pointSum = 0;

                if (rank == 0)
                {
                    for (int worker = 1; worker < size; worker++)
                        comm.Send(chunks[worker], worker, 10);

                    pointSum = Sum(chunks[0]);

                    for (int worker = 1; worker < size; worker++)
                        pointSum += comm.Receive<long>(worker, 20);
                }
                else
                {
                    int[] received = new int[count]; comm.Receive<int>(0, 10, ref received);
                    long workerSum = Sum(received);
                    comm.Send(workerSum, 0, 20);
                }

                timer.Stop();

                double pointMs = comm.Reduce(
                    timer.Elapsed.TotalMilliseconds,
                    Operation<double>.Max, 0);

                if (rank == 0)
                {
                    pointCorrect &= pointSum == expected;

                    if (repetition >= 0)
                        pointTotal += pointMs;
                }

                comm.Barrier();

                if (rank == 0)
                {
                    long[] partials = new long[size];

                    timer.Restart();

                    Parallel.For(
                        0,
                        size,
                        new ParallelOptions
                        {
                            MaxDegreeOfParallelism = size
                        },
                        worker =>
                        {
                            int start = worker * count;
                            int end = start + count;
                            long workerSum = 0;

                            for (int i = start; i < end; i++)
                                workerSum += data[i];

                            partials[worker] = workerSum;
                        });

                    long sharedSum = 0;

                    for (int worker = 0; worker < size; worker++)
                        sharedSum += partials[worker];

                    timer.Stop();

                    sharedCorrect &= sharedSum == expected;

                    if (repetition >= 0)
                        sharedTotal += timer.Elapsed.TotalMilliseconds;
                }

                comm.Barrier();
            }

            if (rank == 0)
            {
                Console.WriteLine();
                Console.WriteLine(
                    $"MPI collective: {collectiveTotal / Repetitions:F3} ms, correct={(collectiveCorrect ? "YES" : "NO")}");
                Console.WriteLine(
                    $"MPI point-to-point: {pointTotal / Repetitions:F3} ms, correct={(pointCorrect ? "YES" : "NO")}");
                Console.WriteLine(
                    $"Shared memory (TPL): {sharedTotal / Repetitions:F3} ms, correct={(sharedCorrect ? "YES" : "NO")}");
                Console.WriteLine(
                    $"All correct: {(collectiveCorrect && pointCorrect && sharedCorrect ? "YES" : "NO")}");
            }
        }
    }
}

