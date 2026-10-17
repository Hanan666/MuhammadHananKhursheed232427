using System;
using System.Threading;
using System.Threading.Tasks;

class Program
{
    const int N = 100_000;
    static int[] input = new int[N];
    static int[] output = new int[N];

    static void Main(string[] args)
    {
        string mode = args.Length > 0 ? args[0] : "bug";
        int maxDegree = args.Length > 1 ? int.Parse(args[1]) : 4;
        var options = new ParallelOptions { MaxDegreeOfParallelism = maxDegree };

        for (int i = 0; i < N; i++) input[i] = i + 1;

        if (mode == "bug")
        {
            int tmp = 0;
            Parallel.For(0, N, options, i =>
            {
                tmp = input[i] * 3;
                Thread.SpinWait(20);
                output[i] = tmp + 7;
            });
        }
        else if (mode == "threadlocal")
        {
            Parallel.For<int>(0, N, options,
                () => 0,
                (i, loopState, localTmp) =>
                {
                    localTmp = input[i] * 3;
                    Thread.SpinWait(20);
                    output[i] = localTmp + 7;
                    return localTmp;
                },
                localTmp => { }
            );
        }
        else if (mode == "local")
        {
            Parallel.For(0, N, options, static i =>
            {
                int tmp = input[i] * 3;
                Thread.SpinWait(20);
                output[i] = tmp + 7;
            });
        }

        int wrongCount = 0;
        for (int i = 0; i < N; i++)
        {
            int expected = (input[i] * 3) + 7;
            if (output[i] != expected) wrongCount++;
        }

        Console.WriteLine($"Mode: {mode} | Degree: {maxDegree} | Wrong elements: {wrongCount}");
    }
}