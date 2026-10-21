using System;
using System.Diagnostics;
using MPI;

class Program
{
    static void Main(string[] args)
    {
        using (new MPI.Environment(ref args))
        {
            Intracommunicator comm = Communicator.world;

            if (comm.Size < 2)
            {
                if (comm.Rank == 0)
                    Console.WriteLine("Use at least 2 ranks.");

                return;
            }

            string mode = args.Length > 0
                ? args[0].ToLowerInvariant()
                : "master";

            if (mode == "master")
                TaskFarm(comm);
            else if (mode == "ring")
                TokenRing(comm);
            else if (comm.Rank == 0)
                Console.WriteLine("Use master or ring.");
        }
    }

    static void TaskFarm(Intracommunicator comm)
    {
        const int taskCount = 100;
        const int taskTag = 100;
        const int workerTag = 101;
        const int resultTag = 102;

        int rank = comm.Rank;

        comm.Barrier();

        if (rank == 0)
        {
            int nextTask = 1;
            int completed = 0;
            int[] served = new int[comm.Size];
            long total = 0;

            for (int worker = 1; worker < comm.Size; worker++)
            {
                int task = nextTask <= taskCount ? nextTask++ : -1;
                comm.Send(task, worker, taskTag);
            }

            while (completed < taskCount)
            {
                int worker = comm.Receive<int>(
                    Communicator.anySource, workerTag);

                long result = comm.Receive<long>(worker, resultTag);

                total += result;
                served[worker]++;
                completed++;

                int task = nextTask <= taskCount ? nextTask++ : -1;
                comm.Send(task, worker, taskTag);
            }

            long expected = (long)taskCount *
                (taskCount + 1) * (2 * taskCount + 1) / 6;

            Console.WriteLine("TASK FARM");
            Console.WriteLine($"Ranks: {comm.Size}");
            Console.WriteLine($"Workers: {comm.Size - 1}");
            Console.WriteLine($"Tasks: {taskCount}");

            for (int worker = 1; worker < comm.Size; worker++)
                Console.WriteLine(
                    $"Worker {worker}: {served[worker]} tasks");

            Console.WriteLine($"Completed tasks: {completed}");
            Console.WriteLine($"Result: {total}");
            Console.WriteLine($"Expected: {expected}");
            Console.WriteLine(
                $"Correct: {(completed == taskCount && total == expected ? "YES" : "NO")}");
        }
        else
        {
            while (true)
            {
                int task = comm.Receive<int>(0, taskTag);

                if (task == -1)
                    break;

                long result = (long)task * task;

                comm.Send(rank, 0, workerTag);
                comm.Send(result, 0, resultTag);
            }
        }

        comm.Barrier();
    }

    static void TokenRing(Intracommunicator comm)
    {
        const int rounds = 1000;
        const int tag = 200;

        int rank = comm.Rank;
        int previous = (rank - 1 + comm.Size) % comm.Size;
        int next = (rank + 1) % comm.Size;

        long token = 0;

        comm.Barrier();
        Stopwatch timer = Stopwatch.StartNew();

        for (int round = 0; round < rounds; round++)
        {
            if (rank == 0)
            {
                comm.Send(token, next, tag);
                token = comm.Receive<long>(previous, tag);
                token++;
            }
            else
            {
                token = comm.Receive<long>(previous, tag);
                token++;
                comm.Send(token, next, tag);
            }
        }

        timer.Stop();

        if (rank == 0)
        {
            long messages = (long)rounds * comm.Size;
            long expected = messages;
            double elapsed = timer.Elapsed.TotalMilliseconds;

            Console.WriteLine("TOKEN RING");
            Console.WriteLine($"Ranks: {comm.Size}");
            Console.WriteLine($"Rounds: {rounds}");
            Console.WriteLine($"Messages: {messages}");
            Console.WriteLine($"Time: {elapsed:F3} ms");
            Console.WriteLine(
                $"Time per message: {elapsed / messages:F6} ms");
            Console.WriteLine($"Final token: {token}");
            Console.WriteLine($"Expected: {expected}");
            Console.WriteLine(
                $"Correct: {(token == expected ? "YES" : "NO")}");
        }

        comm.Barrier();
    }
}
