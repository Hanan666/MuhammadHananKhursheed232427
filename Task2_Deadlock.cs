
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

            if (comm.Size != 2)
            {
                if (rank == 0)
                    Console.WriteLine("Run this program with exactly 2 ranks.");

                return;
            }

            int messageSize = 8 * 1024 * 1024; // 8 MiB

            byte[] data = new byte[messageSize];

            // Fill the message with data
            for (int i = 0; i < messageSize; i++)
            {
                data[i] = (byte)(i % 256);
            }

            int otherRank;

            if (rank == 0)
                otherRank = 1;
            else
                otherRank = 0;

            Console.WriteLine("Rank " + rank + " is ready.");
            Console.WriteLine("Rank " + rank + " sending 8 MiB message...");

            // STEP 1: Both ranks send first
            comm.Send<byte[]>(data, otherRank, 0);

            Console.WriteLine("Rank " + rank + " send completed.");

            // STEP 2: Both ranks receive after sending
            Console.WriteLine("Rank " + rank + " waiting to receive...");

            byte[] received = comm.Receive<byte[]>(otherRank, 0);

            Console.WriteLine(
                "Rank " + rank + " received " +
                received.Length + " bytes."
            );

            Console.WriteLine("Rank " + rank + " completed.");
        }
    }
}
