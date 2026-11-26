#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <limits.h>
#include <omp.h>

#ifndef N
#define N 20000000
#endif
#ifndef REPS
#define REPS 3
#endif
#define BINS 256

typedef struct { long long sum; int mn, mx; long hist[BINS]; } stats_t;
static int *data;

static void serial_version(stats_t *r) /* the reference */
{
    memset(r, 0, sizeof *r);
    r->mn = INT_MAX; r->mx = INT_MIN;
    for (int i = 0; i < N; i++) {
        r->sum += data[i];
        if (data[i] < r->mn) r->mn = data[i];
        if (data[i] > r->mx) r->mx = data[i];
        r->hist[data[i] % BINS]++;
    }
}

/* data-parallel: every thread does ALL the work on its own share of the data */
static void data_parallel(stats_t *r)
{
    long long sum = 0;
    int mn = INT_MAX, mx = INT_MIN;
    long hist[BINS] = { 0 };

    #pragma omp parallel for reduction(+:sum) reduction(min:mn) reduction(max:mx) reduction(+:hist[:BINS])
    for (int i = 0; i < N; i++) {
        sum += data[i];
        if (data[i] < mn) mn = data[i];
        if (data[i] > mx) mx = data[i];
        hist[data[i] % BINS]++;
    }
    r->sum = sum; r->mn = mn; r->mx = mx;
    memcpy(r->hist, hist, sizeof hist);
}

/* task-parallel: every thread does a DIFFERENT job on ALL of the data */
static void task_parallel(stats_t *r)
{
    long long sum = 0;
    int mn = INT_MAX, mx = INT_MIN;
    long hist[BINS] = { 0 };

    #pragma omp parallel sections
    {
        #pragma omp section /* task A */
        {
            for (int i = 0; i < N; i++) sum += data[i];
        }
        #pragma omp section /* task B */
        {
            for (int i = 0; i < N; i++) {
                if (data[i] < mn) mn = data[i];
                if (data[i] > mx) mx = data[i];
            }
        }
        #pragma omp section /* task C */
        {
            for (int i = 0; i < N; i++) hist[data[i] % BINS]++;
        }
    }
    r->sum = sum; r->mn = mn; r->mx = mx;
    memcpy(r->hist, hist, sizeof hist);
}

static int same(const stats_t *a, const stats_t *b)
{
    return a->sum == b->sum && a->mn == b->mn && a->mx == b->mx &&
           memcmp(a->hist, b->hist, sizeof a->hist) == 0;
}

int main(void)
{
    data = malloc((size_t)N * sizeof(int));
    for (int i = 0; i < N; i++) data[i] = (int)(((long long)i * 7919) % 100003);

    stats_t ref, out;
    double t0 = omp_get_wtime();
    serial_version(&ref);
    double serial_ms = (omp_get_wtime() - t0) * 1000.0;

    printf("threads=%d n=%d reps=%d serial=%.1f ms\n",
           omp_get_max_threads(), N, REPS, serial_ms);

    void (*fn[2])(stats_t*) = { data_parallel, task_parallel };
    const char *name[2] = { "data-parallel", "task-parallel" };

    for (int v = 0; v < 2; v++) {
        int ok = 1;
        double total = 0;
        for (int r = 0; r < REPS; r++) {
            memset(&out, 0, sizeof out);
            t0 = omp_get_wtime();
            fn[v](&out);
            total += omp_get_wtime() - t0;
            if (!same(&out, &ref)) ok = 0;
        }
        double ms = total / REPS * 1000.0;
        printf("[%-13s] correct=%s avg_time=%.2f ms speedup=%.2f\n",
               name[v], ok ? "YES" : "NO", ms, serial_ms / ms);
    }
    free(data);
    return 0;
}