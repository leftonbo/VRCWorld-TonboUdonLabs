"""Validate the full T228 grid and summarize raw Udon timings (stdlib only)."""
import csv
import hashlib
import itertools
import json
import math
import random
import statistics as st
import sys
from collections import defaultdict
from pathlib import Path

ROOT = Path(__file__).resolve().parent
SOURCE = ROOT / (sys.argv[1] if len(sys.argv) > 1 else "raw-editor.csv")
SUFFIX = "" if SOURCE.name == "raw-editor.csv" else "-" + SOURCE.stem.removeprefix("raw-")
NAMES = ["baseline", "plus_expression", "plus_steps", "sb_new_split", "sb_new_chain",
         "sb_capacity_split", "sb_capacity_chain", "sb_reuse_split", "sb_reuse_chain", "clock"]


def percentile(values, fraction):
    values = sorted(values)
    index = (len(values) - 1) * fraction
    lo = math.floor(index)
    return values[lo] + (values[math.ceil(index)] - values[lo]) * (index - lo)


def write_csv(name, rows):
    with (ROOT / (name + SUFFIX + ".csv")).open("w", newline="", encoding="utf-8") as output:
        writer = csv.DictWriter(output, fieldnames=list(rows[0]))
        writer.writeheader()
        writer.writerows(rows)


def main():
    rows = list(csv.DictReader(SOURCE.open(encoding="utf-8-sig")))
    assert len(rows) == 16 * 2 * 30 * len(NAMES), len(rows)
    groups, keyed = defaultdict(list), {}
    for row in rows:
        config, run, batch, order, n = (int(row[k]) for k in ("config", "run", "batch", "order", "iterations"))
        condition = config % 8
        workload = condition if condition < 2 else (2 if condition < 5 else 3)
        lines = 1 if condition < 2 else (8, 32, 128)[(condition - 2) % 3]
        expected_n = (64 if condition < 2 else {8: 16, 32: 4, 128: 2}[lines]) * (4 if config >= 8 else 1)
        output = "".join(f"Token{j}|" for j in range(8)) if workload < 2 else "".join(f"Stat{j}: {37+j*17}\n" for j in range(lines))
        assert row["experiment_id"] == "UDON-STRING-T228"
        assert row["environment"] in ("Editor-UdonVM", "VRChat-client")
        assert 0 <= config < 16 and 0 <= run < 2 and 0 <= batch < 30 and 0 <= order < len(NAMES)
        assert n == expected_n and int(row["workload"]) == workload and int(row["lines"]) == lines
        c = NAMES.index(row["case"])
        expected_c = (order + batch + 5) % len(NAMES)
        if run == 1:
            expected_c = len(NAMES) - 1 - expected_c
        assert c == expected_c, row
        expected = 0 if row["case"] == "clock" else n * len(output)
        assert int(row["checksum"]) == int(row["expected"]) == expected and row["exact_equal"] == "1"
        assert int(row["capacity"]) >= len(output)
        key = (config, run, batch, row["case"])
        assert key not in keyed
        ticks, frequency = int(row["elapsed_ticks"]), int(row["frequency"])
        assert ticks >= 0 and frequency > 0
        row["ms"] = ticks / frequency * 1000
        keyed[key] = row
        groups[(config, row["case"])].append(row)
    assert set(keyed) == set(itertools.product(range(16), range(2), range(30), NAMES))
    semantic_rows = None
    if SOURCE.name == "raw-editor.csv":
        semantics = list(csv.DictReader((ROOT / "semantics-editor.csv").open(encoding="utf-8-sig")))
        assert len(semantics) == 2 * 16 * 9
        assert {(int(r["input"]), int(r["config"]), r["case"]) for r in semantics} == set(itertools.product(range(2), range(16), NAMES[:-1]))
        assert all(r["exact_equal"] == "1" and r["length"] == r["expected_length"] for r in semantics)
        semantic_rows = len(semantics)
        assert (ROOT / "sample-output.txt").read_text(encoding="utf-8") == "".join(f"Stat{j}: {37+j*17}\n" for j in range(128))
    summaries = []
    for (config, case), samples in sorted(groups.items()):
        times = [r["ms"] for r in samples]
        n = int(samples[0]["iterations"])
        baseline = [keyed[(config, int(r["run"]), int(r["batch"]), "baseline")]["ms"] for r in samples]
        summaries.append(dict(config=config, workload=samples[0]["workload"], lines=samples[0]["lines"],
                              case=case, iterations=n, samples=len(times), median_ms=st.median(times),
                              p05_ms=percentile(times, .05), p95_ms=percentile(times, .95), min_ms=min(times), max_ms=max(times),
                              mad_ms=st.median(abs(t-st.median(times)) for t in times),
                              raw_us_per_document=st.median(times)*1000/n,
                              paired_baseline_delta_us=st.median(t-b for t,b in zip(times, baseline))*1000/n,
                              run0_median_ms=st.median(r["ms"] for r in samples if r["run"] == "0"),
                              run1_median_ms=st.median(r["ms"] for r in samples if r["run"] == "1")))
    comparisons, rng = [], random.Random(228)
    pairs_to_compare = [("sb_new_chain", "sb_new_split"), ("sb_capacity_chain", "sb_capacity_split"),
                        ("sb_reuse_chain", "sb_reuse_split"), ("sb_reuse_split", "plus_expression"),
                        ("sb_new_split", "plus_expression"), ("sb_capacity_split", "sb_new_split"),
                        ("sb_reuse_split", "sb_capacity_split"), ("plus_steps", "plus_expression")]
    for config in range(16):
        for a,b in pairs_to_compare:
            samples = [(keyed[(config, run, batch, a)], keyed[(config, run, batch, b)]) for run in range(2) for batch in range(30)]
            n = int(samples[0][0]["iterations"])
            deltas = [(x["ms"]-y["ms"])*1000/n for x,y in samples]
            boot = sorted(st.median(rng.choices(deltas, k=len(deltas))) for _ in range(2000))
            comparisons.append(dict(config=config, comparison=a+"_over_"+b,
                                    ratio_of_medians=st.median(x["ms"] for x,y in samples)/st.median(y["ms"] for x,y in samples),
                                    paired_delta_us=st.median(deltas), delta_p05_us=percentile(deltas,.05), delta_p95_us=percentile(deltas,.95),
                                    bootstrap_ci025_us=percentile(boot,.025), bootstrap_ci975_us=percentile(boot,.975),
                                    run0_ratio=st.median(x["ms"] for x,y in samples[:30])/st.median(y["ms"] for x,y in samples[:30]),
                                    run1_ratio=st.median(x["ms"] for x,y in samples[30:])/st.median(y["ms"] for x,y in samples[30:])))
    write_csv("summary", summaries)
    write_csv("comparisons", comparisons)
    result = dict(rows=len(rows), semantic_rows=semantic_rows, checksums=True, complete_grid=True, unique_keys=True,
                  order=True, excluded_rows=0, environments=sorted({r["environment"] for r in rows}),
                  frequencies=sorted({int(r["frequency"]) for r in rows}),
                  zero_ticks=sum(int(r["elapsed_ticks"]) == 0 for r in rows),
                  max_batch_ms=max(r["ms"] for r in rows), sha256=hashlib.sha256(SOURCE.read_bytes()).hexdigest(),
                  bootstrap="2000 paired resamples, seed 228; descriptive within-session CI; no multiplicity correction")
    (ROOT / ("validation"+SUFFIX+".json")).write_text(json.dumps(result, indent=2)+"\n", encoding="utf-8")
    print(json.dumps(result, indent=2))
    for row in summaries:
        if row["config"] >= 8 and row["case"] not in ("baseline", "clock"):
            print(row["config"], row["case"], round(row["raw_us_per_document"],3))


if __name__ == "__main__":
    main()
