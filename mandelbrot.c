/* mandelbrot.c - Tasks 2, 3 and 4: irregular workload, static vs dynamic load balancing */
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <omp.h>

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
#define CHUNK 4 /* rows per work unit (dynamic version) */
#endif
#ifndef REPS
#define REPS 3
#endif
#define MAX_THREADS 64

static int image[HEIGHT][WIDTH], ref_image[HEIGHT][WIDTH];

static long long compute_row(int py, int *dst) /* iteration counts of one row of pixels */
{
    long long total = 0;
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
        dst[px] = it;
        total += it;
    }
    return total;
}

static int units_done[MAX_THREADS];      /* rows or units per worker */
static double finish_ms[MAX_THREADS];    /* when each worker finished its share */
static int next_row;                     /* shared work-queue counter */

static void run_static(int nt)
{
    double start = omp_get_wtime();
    #pragma omp parallel num_threads(nt)
    {
        int id = omp_get_thread_num();
        int first = (int)((long)id * HEIGHT / nt);
        int last  = (int)((long)(id + 1) * HEIGHT / nt);
        
        for (int py = first; py < last; py++) {
            compute_row(py, image[py]);
        }
        units_done[id] = last - first;
        finish_ms[id] = (omp_get_wtime() - start) * 1000.0;
    }
}

static void run_dynamic(int nt)
{
    double start = omp_get_wtime();
    next_row = 0;
    #pragma omp parallel num_threads(nt)
    {
        int id = omp_get_thread_num();
        units_done[id] = 0;
        
        while (1) {
            int first;
            #pragma omp atomic capture
            {
                first = next_row;
                next_row += CHUNK;
            }
            if (first >= HEIGHT) break;
            
            int last = (first + CHUNK < HEIGHT) ? (first + CHUNK) : HEIGHT;
            for (int py = first; py < last; py++) {
                compute_row(py, image[py]);
            }
            units_done[id]++;
        }
        finish_ms[id] = (omp_get_wtime() - start) * 1000.0;
    }
}

static void report(const char *name, int nt, double serial_ms, int rows_per_unit)
{
    double best = 1e30, w[MAX_THREADS], u[MAX_THREADS];
    int ok = 1;
    void (*fn)(int) = strcmp(name, "static") == 0 ? run_static : run_dynamic;

    for (int r = 0; r < REPS; r++) {
        memset(image, 0, sizeof image);
        double t0 = omp_get_wtime();
        fn(nt);
        double wall = (omp_get_wtime() - t0) * 1000.0;
        if (memcmp(image, ref_image, sizeof image) != 0) ok = 0;
        if (wall < best) {
            best = wall;
            for (int t = 0; t < nt; t++) { w[t] = finish_ms[t]; u[t] = units_done[t]; }
        }
    }
    double mx = w[0], mn = w[0];
    for (int t = 1; t < nt; t++) { if (w[t] > mx) mx = w[t]; if (w[t] < mn) mn = w[t]; }

    printf("[%-7s] %s wall=%.1f imbalance=%.2f efficiency=%.0f%% correct=%s\n", name,
           rows_per_unit > 0 ? "units of rows" : "row blocks   ", best,
           mn > 1e-9 ? mx / mn : 0.0, serial_ms / (nt * best) * 100.0, ok ? "YES" : "NO");
    for (int t = 0; t < nt; t++)
        printf("csv,%s,%d,%.0f,%.2f\n", name, t, u[t], w[t]);
}

int main(void)
{
    omp_set_dynamic(0);
    int nt = omp_get_max_threads();
    if (nt > MAX_THREADS) nt = MAX_THREADS;

    double t0 = omp_get_wtime();
    long long checksum = 0;
    for (int py = 0; py < HEIGHT; py++) checksum += compute_row(py, ref_image[py]);
    double serial_ms = (omp_get_wtime() - t0) * 1000.0;

    printf("workload=mandelbrot %dx%d maxit=%d threads=%d serial=%.1f checksum=%lld (times in ms)\n",
           WIDTH, HEIGHT, MAXIT, nt, serial_ms, checksum);

#ifdef WRITE_PGM
    FILE *f = fopen("mandelbrot.pgm", "wb");
    fprintf(f, "P5\n%d %d\n255\n", WIDTH, HEIGHT);
    for (int y = 0; y < HEIGHT; y++)
        for (int x = 0; x < WIDTH; x++) fputc(ref_image[y][x] * 255 / MAXIT, f);
    fclose(f);
#endif

    printf("csv_header,policy,worker,units,finish_ms\n");
    report("static", nt, serial_ms, 0);
    report("dynamic", nt, serial_ms, CHUNK);
    return 0;
}