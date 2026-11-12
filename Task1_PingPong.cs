
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

            int[] messageSizes =
            {
                0, 1, 8, 64, 256,
                1024, 4096, 16384, 65536, 262144
            };

            int[] iterations =
            {
                1000, 1000, 1000, 1000, 1000,
                500, 200, 100, 50, 20
            };

            int repetitions = 5;

            if (comm.Size != 2)
            {
                if (rank == 0)
                    Console.WriteLine("Please run with exactly 2 ranks.");

                return;
            }

            if (rank == 0)
            {
                Console.WriteLine("MPI PING-PONG BENCHMARK");
                Console.WriteLine("-----------------------------------------------");
                Console.WriteLine("Size(bytes)\tIterations\tMean RTT(us)\tRTT/2(us)\tCorrect");

                for (int s = 0; s < messageSizes.Length; s++)
                {
                    int size = messageSizes[s];
                    int count = iterations[s];

                    byte[] payload = new byte[size];

                    // Fill message with data
                    for (int i = 0; i < size; i++)
                    {
                        payload[i] = (byte)(i % 256);
                    }

                    // Warm-up communication
                    comm.Send<byte[]>(payload, 1, 0);
                    byte[] warmup = comm.Receive<byte[]>(1, 0);

                    comm.Barrier();

                    double totalRTT = 0;

                    // Five measurement repetitions
                    for (int r = 0; r < repetitions; r++)
                    {
                        comm.Barrier();

                        Stopwatch watch = Stopwatch.StartNew();

                        for (int i = 0; i < count; i++)
                        {
                            comm.Send<byte[]>(payload, 1, 0);

                            byte[] received = comm.Receive<byte[]>(1, 0);

                            if (received.Length != size)
                            {
                                Console.WriteLine("Incorrect message length!");
                            }
                        }

                        watch.Stop();

                        double rtt = (watch.Elapsed.TotalMilliseconds * 1000) / count;

                        totalRTT += rtt;
                    }

                    // Check returned data
                    comm.Send<byte[]>(payload, 1, 0);

                    byte[] returned = comm.Receive<byte[]>(1, 0);

                    bool correct = true;

                    if (returned.Length != size)
                    {
                        correct = false;
                    }
                    else
                    {
                        for (int i = 0; i < size; i++)
                        {
                            if (returned[i] != payload[i])
                            {
                                correct = false;
                                break;
                            }
                        }
                    }

                    double meanRTT = totalRTT / repetitions;
                    double oneWay = meanRTT / 2;

                    Console.WriteLine(
                        $"{size}\t\t{count}\t\t{meanRTT:F3}\t\t{oneWay:F3}\t\t{(correct ? "Yes" : "No")}"
                    );
                }

                comm.Barrier();

                Console.WriteLine("-----------------------------------------------");
                Console.WriteLine("Benchmark completed.");
            }
            else if (rank == 1)
            {
                for (int s = 0; s < messageSizes.Length; s++)
                {
                    int count = iterations[s];

                    // Warm-up communication
                    byte[] warmup = comm.Receive<byte[]>(0, 0);
                    comm.Send<byte[]>(warmup, 0, 0);

                    comm.Barrier();

                    // Five measurement repetitions
                    for (int r = 0; r < repetitions; r++)
                    {
                        comm.Barrier();

                        for (int i = 0; i < count; i++)
                        {
                            byte[] received = comm.Receive<byte[]>(0, 0);

                            comm.Send<byte[]>(received, 0, 0);
                        }
                    }

                    // Final correctness check
                    byte[] receivedData = comm.Receive<byte[]>(0, 0);

                    comm.Send<byte[]>(receivedData, 0, 0);
                }

                comm.Barrier();
            }
        }
    }
}
