"""Revalidate T209 raw data and emit statistics; Python standard library only."""
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

ROOT = Path(sys.argv[1]).resolve() if len(sys.argv) > 1 else Path(__file__).resolve().parent
NAMES = ["clock", "baseline_bool"] + [
    f"{kind}_{method}"
    for kind in ("gameobject", "material", "transform", "udonbehaviour", "object")
    for method in ("ne", "valid", "implicit")
]


def percentile(values, fraction):
    values = sorted(values)
    index = (len(values) - 1) * fraction
    lo = math.floor(index)
    return values[lo] + (values[math.ceil(index)] - values[lo]) * (index - lo)


def write_csv(name, rows):
    with (ROOT / name).open("w", newline="", encoding="utf-8") as output:
        writer = csv.DictWriter(output, fieldnames=list(rows[0]))
        writer.writeheader()
        writer.writerows(rows)


def main():
    source = ROOT / "raw-editor.csv"
    rows = list(csv.DictReader(source.open(encoding="utf-8-sig")))
    assert len(rows) == 8 * 2 * 30 * 17, len(rows)
    groups = defaultdict(list)
    keyed = {}
    for row in rows:
        config, run, batch, order, n = (
            int(row[k]) for k in ("config", "run", "batch", "order", "iterations")
        )
        assert row["experiment_id"] == "UDON-NULL-T209"
        assert row["environment"] == "Editor-UdonVM"
        assert 0 <= config < 8 and 0 <= run < 2 and 0 <= batch < 30
        assert n == (128 if config < 4 else 1024)
        assert int(row["input"]) == config % 4
        c = NAMES.index(row["case"])
        expected_c = (order + batch + 5) % 17
        if run == 1:
            expected_c = 16 - expected_c
        assert c == expected_c, row
        expected = 0 if c == 0 else (n if config % 4 == 0 else n // 2 if config % 4 == 2 else 0)
        assert int(row["checksum"]) == int(row["expected"]) == expected
        assert row["checksum_ok"] == "1"
        key = (config, run, batch, row["case"])
        assert key not in keyed, key
        ticks, frequency = int(row["elapsed_ticks"]), int(row["frequency"])
        assert ticks >= 0 and frequency > 0
        row["ms"] = ticks / frequency * 1000
        keyed[key] = row
        groups[(config, row["case"])].append(row)
    assert set(keyed) == set(itertools.product(range(8), range(2), range(30), NAMES))
    semantics = list(csv.DictReader((ROOT / "semantics-editor.csv").open(encoding="utf-8-sig")))
    assert len(semantics) == 64
    assert {(int(r["input"]), r["case"]) for r in semantics} == set(itertools.product(range(4), NAMES[1:]))
    assert all(r["ok"] == "1" and r["checksum"] == r["expected"] for r in semantics)
    summaries = []
    for (config, case), samples in sorted(groups.items()):
        times = [r["ms"] for r in samples]
        n = int(samples[0]["iterations"])
        base = [keyed[(config, int(r["run"]), int(r["batch"]), "baseline_bool")]["ms"] for r in samples]
        summaries.append(dict(
            config=config, input=config % 4, case=case, iterations=n, samples=len(times),
            median_ms=st.median(times), p05_ms=percentile(times, .05),
            p95_ms=percentile(times, .95), min_ms=min(times), max_ms=max(times),
            mad_ms=st.median(abs(t - st.median(times)) for t in times),
            raw_ns_per_op=st.median(times) * 1e6 / n,
            paired_baseline_delta_ns_per_op=st.median(t - b for t, b in zip(times, base)) * 1e6 / n,
            run0_median_ms=st.median(r["ms"] for r in samples if r["run"] == "0"),
            run1_median_ms=st.median(r["ms"] for r in samples if r["run"] == "1"),
        ))
    comparisons = []
    rng = random.Random(209)
    for config in range(8):
        n = 128 if config < 4 else 1024
        for kind in ("gameobject", "material", "transform", "udonbehaviour", "object"):
            for a, b in (("implicit", "ne"), ("implicit", "valid"), ("valid", "ne")):
                pairs = [(keyed[(config, run, batch, kind + "_" + a)]["ms"],
                          keyed[(config, run, batch, kind + "_" + b)]["ms"])
                         for run in range(2) for batch in range(30)]
                deltas = [(x - y) * 1e6 / n for x, y in pairs]
                boot = sorted(st.median(rng.choices(deltas, k=len(deltas))) for _ in range(2000))
                comparisons.append(dict(
                    config=config, input=config % 4, iterations=n, type=kind, comparison=a + "_over_" + b,
                    ratio_of_medians=st.median(x for x, y in pairs) / st.median(y for x, y in pairs),
                    paired_median_ratio=st.median(x / y for x, y in pairs),
                    paired_delta_ns=st.median(deltas), delta_p05_ns=percentile(deltas, .05),
                    delta_p95_ns=percentile(deltas, .95), bootstrap_ci025_ns=percentile(boot, .025),
                    bootstrap_ci975_ns=percentile(boot, .975),
                    run0_ratio=st.median(x for x, y in pairs[:30]) / st.median(y for x, y in pairs[:30]),
                    run1_ratio=st.median(x for x, y in pairs[30:]) / st.median(y for x, y in pairs[30:]),
                ))
    write_csv("summary.csv", summaries)
    write_csv("comparisons.csv", comparisons)
    result = dict(rows=len(rows), semantic_rows=len(semantics), checksums=True,
                  complete_grid=True, unique_keys=True, order=True, excluded_rows=0,
                  frequencies=sorted({int(r["frequency"]) for r in rows}),
                  zero_ticks=sum(int(r["elapsed_ticks"]) == 0 for r in rows),
                  sha256=hashlib.sha256(source.read_bytes()).hexdigest(),
                  bootstrap="2000 paired-sample resamples, seed 209; descriptive within-session CI, no multiplicity correction")
    (ROOT / "validation.json").write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(result, indent=2))
    for row in comparisons:
        if row["iterations"] == 1024 and row["input"] == 0:
            print(row)


if __name__ == "__main__":
    main()
