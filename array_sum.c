#include <stdio.h>
#include <stdlib.h>
#include <limits.h>
#include <mpi.h>
#ifndef N
#define N 10000003 /* not divisible by most rank counts */
#endif

int main(int argc, char **argv)
{
    MPI_Init(&argc, &argv);
    int rank, size;
    MPI_Comm_rank(MPI_COMM_WORLD, &rank);
    MPI_Comm_size(MPI_COMM_WORLD, &size);

    int chunk = (N + size - 1) / size;          /* elements per rank, rounded up */
    long long padded = (long long)chunk * size; /* array padded with zeros to fit */

    int *data = NULL; /* exists only on rank 0 */
    long long ref_sum = 0;
    int ref_max = INT_MIN;

    if (rank == 0) {
        // Allocate zero-initialized padded array so extra elements don't affect sum or max
        data = calloc((size_t)padded, sizeof(int));
        for (int i = 0; i < N; i++) {
            data[i] = (int)(((long long)i * 7919) % 100003);
            /* sequential reference */
            ref_sum += data[i];
            if (data[i] > ref_max) ref_max = data[i];
        }
    }

    int *mine = malloc((size_t)chunk * sizeof(int));

    MPI_Barrier(MPI_COMM_WORLD);
    double t0 = MPI_Wtime();

    long long total = 0;
    int gmax = INT_MIN;

    // 1. Distribute equal chunks of size 'chunk' from root (rank 0) to all ranks
    MPI_Scatter(data, chunk, MPI_INT, mine, chunk, MPI_INT, 0, MPI_COMM_WORLD);

    // 2. Each rank computes its own local sum and local maximum
    long long local_sum = 0;
    int local_max = INT_MIN;
    for (int i = 0; i < chunk; i++) {
        local_sum += mine[i];
        if (mine[i] > local_max) {
            local_max = mine[i];
        }
    }

    // 3. Combine local sums and local maximums back onto rank 0
    MPI_Reduce(&local_sum, &total, 1, MPI_LONG_LONG, MPI_SUM, 0, MPI_COMM_WORLD);
    MPI_Reduce(&local_max, &gmax, 1, MPI_INT, MPI_MAX, 0, MPI_COMM_WORLD);

    MPI_Barrier(MPI_COMM_WORLD);
    double ms = (MPI_Wtime() - t0) * 1000.0;

    if (rank == 0) {
        printf("[array-sum] ranks=%d n=%d chunk=%d padding=%lld\n",
               size, N, chunk, padded - N);
        printf("sum=%lld reference=%lld max=%d reference_max=%d correct=%s time=%.2f ms\n",
               total, ref_sum, gmax, ref_max,
               (total == ref_sum && gmax == ref_max) ? "YES" : "NO", ms);
        free(data);
    }

    free(mine);
    MPI_Finalize();
    return 0;
}
