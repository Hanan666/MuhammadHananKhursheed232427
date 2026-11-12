
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

            int rank = comm.Rank;

            if (comm.Size != 2)
            {
                if (rank == 0)
                    Console.WriteLine("Run with exactly 2 ranks.");

                return;
            }

            int messageSize = 8 * 1024 * 1024; // 8 MiB

            byte[] data = new byte[messageSize];

            // Fill the message
            for (int i = 0; i < messageSize; i++)
            {
                data[i] = (byte)(i % 256);
            }

            byte[] received = new byte[messageSize];

            Stopwatch watch = new Stopwatch();

            comm.Barrier();

            watch.Start();

            if (rank == 0)
            {
                Console.WriteLine("Rank 0: Sending message...");

                // Rank 0 sends first
                comm.Send<byte[]>(data, 1, 0);

                Console.WriteLine("Rank 0: Message sent.");

                // Rank 0 receives after sending
                received = comm.Receive<byte[]>(1, 0);

                Console.WriteLine("Rank 0: Message received.");
            }
            else if (rank == 1)
            {
                Console.WriteLine("Rank 1: Waiting to receive...");

                // Rank 1 receives first
                received = comm.Receive<byte[]>(0, 0);

                Console.WriteLine("Rank 1: Message received.");

                // Rank 1 sends after receiving
                comm.Send<byte[]>(data, 0, 0);

                Console.WriteLine("Rank 1: Message sent.");
            }

            watch.Stop();

            // Verify received data
            bool correct = true;

            if (received.Length != messageSize)
            {
                correct = false;
            }
            else
            {
                for (int i = 0; i < messageSize; i++)
                {
                    if (received[i] != data[i])
                    {
                        correct = false;
                        break;
                    }
                }
            }

            Console.WriteLine("--------------------------------");

            Console.WriteLine("Rank: " + rank);
            Console.WriteLine("Message Size: " + messageSize + " bytes");
            Console.WriteLine("Completion Time: " +
                watch.Elapsed.TotalMilliseconds.ToString("F3") + " ms");
            Console.WriteLine("Data Correct: " + (correct ? "Yes" : "No"));

            comm.Barrier();

            if (rank == 0)
            {
                Console.WriteLine("--------------------------------");
                Console.WriteLine("Task 3A Completed Successfully!");
            }
        }
    }
}
