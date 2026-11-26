#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <omp.h>

#ifndef LOGN
#define LOGN 22
#endif
#define N (1L << LOGN)

static long long *in, *seq, *a, *b;

static void sequential_scan(void) /* inclusive and exclusive reference */
{
    long long s = 0;
    for (long i = 0; i < N; i++) { seq[i] = s; s += in[i]; } /* seq = exclusive scan */
}

/* Hillis-Steele: log2(N) steps, every element adds the element 'd' places to its left */
/* produces the INCLUSIVE scan */
static void hillis_steele(long long *out)
{
    memcpy(a, in, N * sizeof(long long));
    for (long d = 1; d < N; d *= 2) {
        #pragma omp parallel for
        for (long i = 0; i < N; i++) {
            if (i >= d)
                b[i] = a[i] + a[i - d];
            else
                b[i] = a[i];
        }
        long long *t = a; a = b; b = t; /* double buffering */
    }
    memcpy(out, a, N * sizeof(long long));
}

/* Blelloch: up-sweep (reduce) then down-sweep, 2N additions, produces the EXCLUSIVE scan */
static void blelloch(long long *out)
{
    memcpy(out, in, N * sizeof(long long));
    for (long d = 1; d < N; d *= 2) { /* up-sweep */
        #pragma omp parallel for
        for (long k = 0; k < N / (2 * d); k++) {
            long i = (k + 1) * 2 * d - 1;
            out[i] += out[i - d];
        }
    }
    out[N - 1] = 0; /* clear the root */
    for (long d = N / 2; d >= 1; d /= 2) { /* down-sweep */
        #pragma omp parallel for
        for (long k = 0; k < N / (2 * d); k++) {
            long i = (k + 1) * 2 * d - 1;
            long long t = out[i - d];
            out[i - d] = out[i];
            out[i] += t;
        }
    }
}

int main(void)
{
    in  = malloc(N * sizeof(long long)); seq = malloc(N * sizeof(long long));
    a   = malloc(N * sizeof(long long)); b   = malloc(N * sizeof(long long));
    long long *out = malloc(N * sizeof(long long));
    for (long i = 0; i < N; i++) in[i] = i % 10 + 1;

    double t0 = omp_get_wtime();
    sequential_scan();
    double seq_ms = (omp_get_wtime() - t0) * 1000.0;
    printf("n=2^%d threads=%d sequential=%.2f ms\n", LOGN, omp_get_max_threads(), seq_ms);

    t0 = omp_get_wtime();
    hillis_steele(out);
    double hs_ms = (omp_get_wtime() - t0) * 1000.0;
    long bad = 0;
    for (long i = 0; i < N; i++) if (out[i] != seq[i] + in[i]) bad++; /* inclusive = exclusive + element */
    printf("[hillis-steele] correct=%s time=%.2f ms additions=%ld steps=%d\n",
           bad == 0 ? "YES" : "NO", hs_ms, (long)LOGN * N, LOGN);

    t0 = omp_get_wtime();
    blelloch(out);
    double bl_ms = (omp_get_wtime() - t0) * 1000.0;
    bad = 0;
    for (long i = 0; i < N; i++) if (out[i] != seq[i]) bad++;
    printf("[blelloch     ] correct=%s time=%.2f ms additions=%ld steps=%d\n",
           bad == 0 ? "YES" : "NO", bl_ms, 2 * (N - 1), 2 * LOGN);
    return 0;
}