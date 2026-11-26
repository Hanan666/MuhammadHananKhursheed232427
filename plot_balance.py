# plot_balance.py - bar chart of per-worker completion times for one or more policies
import sys
from collections import defaultdict
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt

files, out = [], "balance.png"
args = sys.argv[1:]
while args:
    a = args.pop(0)
    if a == "-o":
        out = args.pop(0)
    else:
        files.append(a)

data = defaultdict(dict)
for name in files:
    with open(name) as f:
        for line in f:
            parts = line.strip().split(",")
            if len(parts) == 5 and parts[0] == "csv":
                data[parts[1]][int(parts[2])] = float(parts[4])

policies = list(data)
fig, ax = plt.subplots(figsize=(8, 4.5))
width = 0.8 / max(len(policies), 1)
for k, p in enumerate(policies):
    workers = sorted(data[p])
    xs = [w + k * width for w in workers]
    ax.bar(xs, [data[p][w] for w in workers], width, label=p)
    times = list(data[p].values())
    print(f"{p:12s} slowest {max(times):8.1f} ms  fastest {min(times):8.1f} ms  "
          f"imbalance {max(times) / min(times):5.2f}")

ax.set_xlabel("worker")
ax.set_ylabel("completion time (ms)")
ax.set_title("Per-worker completion time")
ax.legend()
ax.grid(True, axis="y", alpha=0.3)
fig.tight_layout()
fig.savefig(out, dpi=150)
print("wrote", out)