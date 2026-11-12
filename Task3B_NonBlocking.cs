
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
            byte[] received = new byte[messageSize];

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

            comm.Barrier();

            Stopwatch watch = Stopwatch.StartNew();

            Console.WriteLine("Rank " + rank + ": Starting non-blocking communication.");

            // STEP 1: Post non-blocking receive
            ReceiveRequest receiveRequest =
                comm.ImmediateReceive<byte>(otherRank, 0, received);

            Console.WriteLine("Rank " + rank + ": Receive request posted.");

            // STEP 2: Post non-blocking send
            Request sendRequest =
                comm.ImmediateSend<byte>(data, otherRank, 0);

            Console.WriteLine("Rank " + rank + ": Send request posted.");

            // STEP 3: Wait for receive to complete
            receiveRequest.Wait();

            Console.WriteLine("Rank " + rank + ": Receive completed.");

            // STEP 4: Wait for send to complete
            sendRequest.Wait();

            Console.WriteLine("Rank " + rank + ": Send completed.");

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

            Console.WriteLine(
                "Completion Time: " +
                watch.Elapsed.TotalMilliseconds.ToString("F3") + " ms"
            );

            Console.WriteLine("Data Correct: " + (correct ? "Yes" : "No"));

            comm.Barrier();

            if (rank == 0)
            {
                Console.WriteLine("--------------------------------");
                Console.WriteLine("Task 3B Completed Successfully!");
            }
        }
    }
}
