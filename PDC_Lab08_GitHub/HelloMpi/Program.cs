using System;
using MPI;

class Program
{
    static void Main(string[] args)
    {
        using (new MPI.Environment(ref args))
        {
            Intracommunicator comm = Communicator.world;

            int rank = comm.Rank;
            int size = comm.Size;
            string processor = MPI.Environment.ProcessorName;

            string report = $"Rank {rank} of {size}, processor: {processor}";

            if (rank == 0)
            {
                Console.WriteLine($"Logical cores: {System.Environment.ProcessorCount}");
                Console.WriteLine("UNORDERED REPORT");
                Console.Out.Flush();
            }

            comm.Barrier();

            Console.WriteLine(report);
            Console.Out.Flush();

            comm.Barrier();

            if (rank == 0)
            {
                Console.WriteLine();
                Console.WriteLine("ORDERED REPORT");
                Console.WriteLine(report);

                for (int source = 1; source < size; source++)
                {
                    string received = comm.Receive<string>(source, 100);
                    Console.WriteLine(received);
                }
            }
            else
            {
                comm.Send(report, 0, 100);
            }

            comm.Barrier();
        }
    }
}
