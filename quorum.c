#include <stdio.h>
#include <stdlib.h>
#include <mpi.h>

#define KEYS 3
#define OP_STOP 0
#define OP_WRITE 1
#define OP_READ 2

static void pick_subset(int n, int k, int *out)
{
    /* k distinct replicas out of n (1..n) */
    int idx[64];
    for (int i = 0; i < n; i++) idx[i] = i + 1;
    for (int i = n - 1; i > 0; i--) { /* Fisher-Yates shuffle */
        int j = rand() % (i + 1), t = idx[i];
        idx[i] = idx[j];
        idx[j] = t;
    }
    for (int i = 0; i < k; i++) out[i] = idx[i];
}

int main(int argc, char **argv)
{
    MPI_Init(&argc, &argv);
    int rank, size;
    MPI_Comm_rank(MPI_COMM_WORLD, &rank);
    MPI_Comm_size(MPI_COMM_WORLD, &size);

    int n = size - 1; /* N replicas = ranks 1..size-1 */
    if (n < 2 || n > 60) {
        if (rank == 0) fprintf(stderr, "run with 3 to 61 ranks (rank 0 is the coordinator)\n");
        MPI_Finalize();
        return 1;
    }

    int w = argc > 1 ? atoi(argv[1]) : (n / 2 + 1); /* write quorum */
    int r = argc > 2 ? atoi(argv[2]) : (n / 2 + 1); /* read quorum */
    int trials = argc > 3 ? atoi(argv[3]) : 300;

    if (w < 1 || w > n || r < 1 || r > n) {
        if (rank == 0) fprintf(stderr, "W and R must be between 1 and N=%d\n", n);
        MPI_Finalize();
        return 1;
    }

    if (rank != 0) {
        /* ---- a replica ---- */
        int value[KEYS] = { 0 }, version[KEYS] = { 0 };
        for (;;) {
            int req[4], reply[2]; /* req = {op, key, value, version} */
            MPI_Recv(req, 4, MPI_INT, 0, 0, MPI_COMM_WORLD, MPI_STATUS_IGNORE);
            if (req[0] == OP_STOP) break;

            if (req[0] == OP_WRITE && req[3] > version[req[1]]) {
                /* keep only newer versions */
                value[req[1]] = req[2];
                version[req[1]] = req[3];
            }
            reply[0] = value[req[1]];   /* write: an ack; read: the data */
            reply[1] = version[req[1]];
            MPI_Send(reply, 2, MPI_INT, 0, 1, MPI_COMM_WORLD);
        }
    } else {
        /* ---- the coordinator ---- */
        srand(12345);
        int latest_ver[KEYS] = { 0 }, counter = 0, stale = 0, fresh = 0;
        int set[64];

        for (int t = 0; t < trials; t++) {
            int key = t % KEYS, ver = ++counter, val = 1000 + t;

            pick_subset(n, w, set); /* only these W replicas get the write */
            // Send write request to W chosen replicas and wait for all W acks
            int write_req[4] = { OP_WRITE, key, val, ver };
            int ack[2];
            for (int i = 0; i < w; i++) {
                MPI_Send(write_req, 4, MPI_INT, set[i], 0, MPI_COMM_WORLD);
            }
            for (int i = 0; i < w; i++) {
                MPI_Recv(ack, 2, MPI_INT, set[i], 1, MPI_COMM_WORLD, MPI_STATUS_IGNORE);
            }

            latest_ver[key] = ver;

            pick_subset(n, r, set); /* the read asks R replicas */
            int got_ver = -1;
            // Query R chosen replicas and pick the highest version returned
            int read_req[4] = { OP_READ, key, 0, 0 };
            int read_reply[2];
            for (int i = 0; i < r; i++) {
                MPI_Send(read_req, 4, MPI_INT, set[i], 0, MPI_COMM_WORLD);
            }
            for (int i = 0; i < r; i++) {
                MPI_Recv(read_reply, 2, MPI_INT, set[i], 1, MPI_COMM_WORLD, MPI_STATUS_IGNORE);
                if (read_reply[1] > got_ver) {
                    got_ver = read_reply[1];
                }
            }

            if (got_ver == latest_ver[key]) fresh++;
            else stale++;
        }

        int stop[4] = { OP_STOP, 0, 0, 0 };
        for (int i = 1; i <= n; i++)
            MPI_Send(stop, 4, MPI_INT, i, 0, MPI_COMM_WORLD);

        printf("[quorum] N=%d W=%d R=%d W+R=%d %s N\n", n, w, r, w + r, w + r > n ? ">" : "<=");
        printf("trials=%d fresh_reads=%d stale_reads=%d guarantee_holds=%s\n",
               trials, fresh, stale, stale == 0 ? "YES" : "NO");
    }

    MPI_Finalize();
    return 0;
}


