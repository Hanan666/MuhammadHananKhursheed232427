#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <mpi.h>

#ifndef WIDTH
#define WIDTH 1200
#endif
#ifndef HEIGHT
#define HEIGHT 800
#endif
#ifndef MAXIT
#define MAXIT 1000
#endif
#ifndef CHUNK
#define CHUNK 4 /* rows per work unit */
#endif
#define TAG_REQUEST 1
#define TAG_WORK    2

static long long compute_rows(int first, int last) /* checksum of the iteration counts */
{
    long long total = 0;
    for (int py = first; py < last; py++) {
        double ci = -1.2 + py * 2.4 / HEIGHT;
        for (int px = 0; px < WIDTH; px++) {
            double cr = -2.2 + px * 3.2 / WIDTH, zr = 0, zi = 0;
            int it = 0;
            while (it < MAXIT && zr * zr + zi * zi <= 4.0) {
                double t = zr * zr - zi * zi + cr;
                zi = 2.0 * zr * zi + ci;
                zr = t;
                it++;
            }
            total += it;
        }
    }
    return total;
}

int main(int argc, char **argv)
{
    MPI_Init(&argc, &argv);
    int rank, size;
    MPI_Comm_rank(MPI_COMM_WORLD, &rank);
    MPI_Comm_size(MPI_COMM_WORLD, &size);
    if (size < 3) {
        if (rank == 0) fprintf(stderr, "run with at least 3 ranks (1 master + 2 workers)\n");
        MPI_Finalize();
        return 1;
    }
    int workers = size - 1;
    long long reference = 0;
    double serial_ms = 0;
    if (rank == 0) {
        double s = MPI_Wtime();
        reference = compute_rows(0, HEIGHT);
        serial_ms = (MPI_Wtime() - s) * 1000.0;
    }

    for (int pass = 0; pass < 2; pass++) { /* pass 0: static, pass 1: dynamic */
        const char *name = pass == 0 ? "mpi-static" : "mpi-dynamic";
        long long checksum = 0;
        long units = 0;
        double finish = 0;

        MPI_Barrier(MPI_COMM_WORLD);
        double t0 = MPI_Wtime();

        if (pass == 0) {
            if (rank >= 1) {
                int w = rank - 1;
                int first = (int)((long)w * HEIGHT / workers);
                int last  = (int)((long)(w + 1) * HEIGHT / workers);
                checksum = compute_rows(first, last);
                units = last - first;
            }
            finish = (MPI_Wtime() - t0) * 1000.0;
        } else if (rank == 0) { /* the master hands out units on demand */
            int next = 0, stopped = 0;
            while (stopped < workers) {
                long long msg;
                MPI_Status st;
                MPI_Recv(&msg, 1, MPI_LONG_LONG, MPI_ANY_SOURCE, TAG_REQUEST,
                         MPI_COMM_WORLD, &st);
                checksum += msg;
                
                int reply;
                if (next < HEIGHT) {
                    reply = next;
                    next += CHUNK;
                } else {
                    reply = -1;
                    stopped++;
                }
                MPI_Send(&reply, 1, MPI_INT, st.MPI_SOURCE, TAG_WORK, MPI_COMM_WORLD);
            }
        } else {
            long long msg = 0;
            for (;;) { /* a worker asks, computes, asks again */
                int first;
                MPI_Send(&msg, 1, MPI_LONG_LONG, 0, TAG_REQUEST, MPI_COMM_WORLD);
                MPI_Recv(&first, 1, MPI_INT, 0, TAG_WORK, MPI_COMM_WORLD, MPI_STATUS_IGNORE);
                if (first < 0) break;
                int last = first + CHUNK < HEIGHT ? first + CHUNK : HEIGHT;
                msg = compute_rows(first, last);
                units++;
            }
            finish = (MPI_Wtime() - t0) * 1000.0;
        }

        long long total = 0;
        if (pass == 0) MPI_Reduce(&checksum, &total, 1, MPI_LONG_LONG, MPI_SUM, 0, MPI_COMM_WORLD);
        else total = checksum; /* the master added up the results */

        double *fin = rank == 0 ? malloc(size * sizeof(double)) : NULL;
        long *un = rank == 0 ? malloc(size * sizeof(long)) : NULL;
        MPI_Gather(&finish, 1, MPI_DOUBLE, fin, 1, MPI_DOUBLE, 0, MPI_COMM_WORLD);
        MPI_Gather(&units, 1, MPI_LONG, un, 1, MPI_LONG, 0, MPI_COMM_WORLD);

        if (rank == 0) {
            double mx = fin[1], mn = fin[1];
            for (int r = 2; r < size; r++) {
                if (fin[r] > mx) mx = fin[r];
                if (fin[r] < mn) mn = fin[r];
            }
            double wall = mx; /* the slowest worker ends the run */
            if (pass == 0) {
                printf("workload=mandelbrot %dx%d maxit=%d ranks=%d (1 master, %d workers) "
                       "serial=%.1f (times in ms)\n", WIDTH, HEIGHT, MAXIT, size,
                       workers, serial_ms);
                printf("csv_header,policy,worker,units,finish_ms\n");
            }
            printf("[%-11s] wall=%.1f imbalance=%.2f efficiency=%.0f%% correct=%s\n",
                   name, wall, mn > 1e-9 ? mx / mn : 0.0,
                   serial_ms / (workers * wall) * 100.0, total == reference ? "YES" : "NO");
            for (int r = 1; r < size; r++)
                printf("csv,%s,%d,%ld,%.2f\n", name, r - 1, un[r], fin[r]);
            free(fin); free(un);
        }
    }
    MPI_Finalize();
    return 0;
}