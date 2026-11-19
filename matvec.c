#include <stdio.h>
#include <stdlib.h>
#include <mpi.h>

#ifndef N
#define N 2048 /* A is N x N */
#endif

/* Task 3: every phase starts and ends with a barrier, so all ranks measure the same interval */
static double phase_start(void)
{
#ifndef NO_BARRIER
    // Synchronize all ranks before starting the timer
    MPI_Barrier(MPI_COMM_WORLD);
#endif
    return MPI_Wtime();
}

static double phase_end(double t0)
{
#ifndef NO_BARRIER
    // Wait for the slowest rank to finish before stopping the timer
    MPI_Barrier(MPI_COMM_WORLD);
#endif
    return (MPI_Wtime() - t0) * 1000.0; /* milliseconds */
}

int main(int argc, char **argv)
{
    int rank, size;
    MPI_Init(&argc, &argv);
    MPI_Comm_rank(MPI_COMM_WORLD, &rank);
    MPI_Comm_size(MPI_COMM_WORLD, &size);

    int rows = (N + size - 1) / size; /* rows per rank, rounded up */
    int padded_rows = rows * size;

    double *A = NULL, *y = NULL, *y_ref = NULL; /* only on rank 0 */
    double *x = malloc(N * sizeof(double));    /* every rank needs the whole vector */
    double *a_local = malloc((size_t)rows * N * sizeof(double));
    double *y_local = malloc(rows * sizeof(double));

    if (rank == 0) {
        /* build the problem (not timed) */
        A = calloc((size_t)padded_rows * N, sizeof(double));
        y = malloc(padded_rows * sizeof(double));
        y_ref = malloc(N * sizeof(double));

        for (int i = 0; i < N; i++)
            for (int j = 0; j < N; j++)
                A[(size_t)i * N + j] = (i + j) % 10 + 1;

        for (int j = 0; j < N; j++)
            x[j] = j % 7 + 1;

        for (int i = 0; i < N; i++) { /* sequential reference */
            double s = 0;
            for (int j = 0; j < N; j++)
                s += A[(size_t)i * N + j] * x[j];
            y_ref[i] = s;
        }
    }

    /* phase 1: distribute the data */
    double t = phase_start();

    // Broadcast the entire vector x from rank 0 to all ranks
    MPI_Bcast(x, N, MPI_DOUBLE, 0, MPI_COMM_WORLD);

    // Scatter blocks of 'rows' rows (rows * N doubles) of matrix A to each rank
    MPI_Scatter(A, rows * N, MPI_DOUBLE, a_local, rows * N, MPI_DOUBLE, 0, MPI_COMM_WORLD);

    double t_distribute = phase_end(t);

    /* phase 2: local computation */
    t = phase_start();
    for (int i = 0; i < rows; i++) {
        double s = 0;
        for (int j = 0; j < N; j++)
            s += a_local[(size_t)i * N + j] * x[j];
        y_local[i] = s;
    }
    double t_compute = phase_end(t);

    /* phase 3: collect the result */
    t = phase_start();

    // Gather each rank's 'rows' computed elements into y on rank 0 in rank order
    MPI_Gather(y_local, rows, MPI_DOUBLE, y, rows, MPI_DOUBLE, 0, MPI_COMM_WORLD);

    double t_gather = phase_end(t);

    // Find the slowest (maximum) time across all ranks for each phase
    double mine[3] = { t_distribute, t_compute, t_gather }, worst[3];
    MPI_Reduce(mine, worst, 3, MPI_DOUBLE, MPI_MAX, 0, MPI_COMM_WORLD);

    if (rank == 0) {
        long wrong = 0;
        for (int i = 0; i < N; i++)
            if (y[i] != y_ref[i]) wrong++;

        printf("[matvec] ranks=%d n=%d rows_per_rank=%d correct=%s\n",
               size, N, rows, wrong == 0 ? "YES" : "NO");
        printf("phases (ms): distribute=%.2f compute=%.2f gather=%.2f total=%.2f\n",
               worst[0], worst[1], worst[2], worst[0] + worst[1] + worst[2]);

        free(A);
        free(y);
        free(y_ref);
    }

    free(x);
    free(a_local);
    free(y_local);
    MPI_Finalize();
    return 0;
}
