
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

            int next = (rank + 1) % size;
            int previous = (rank - 1 + size) % size;

            // Each process sends its own rank as a token
            int token = rank;

            // Start non-blocking receive
            ReceiveRequest receiveRequest =
                comm.ImmediateReceive<int>(previous, 0);

            // Start non-blocking send
            Request sendRequest =
                comm.ImmediateSend<int>(token, next, 0);

            // Wait for both operations
            receiveRequest.Wait();
            sendRequest.Wait();

            // Get received token
            int received = (int)receiveRequest.GetValue();

            // Verify token
            if (received == previous)
            {
                Console.WriteLine(
                    $"Rank {rank}: Received token {received} from Rank {previous} - PASS");
            }
            else
            {
                Console.WriteLine(
                    $"Rank {rank}: Expected {previous}, Received {received} - FAIL");
            }

            comm.Barrier();

            if (rank == 0)
            {
                Console.WriteLine("Ring communication completed.");
            }
        }
    }
}
