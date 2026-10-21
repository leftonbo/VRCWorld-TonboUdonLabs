"""Validate and summarize the T174 Udon VM export (Python standard library only)."""
import csv
import hashlib
import json
import math
from pathlib import Path
import random
import statistics as stats

ROOT = Path(__file__).resolve().parent
NAMES = '''clock state_inline state_local_fields state_local_property state_local_methods
state_local_array state_local_list state_external_fields state_external_property state_external_methods
state_shared_array state_shared_list state_refetch_array state_refetch_list call_inline call_local_method
call_argument_return call_fields call_array call_list call_array_event call_list_event call_array_argument
call_list_argument call_array_batch setup_attach_array setup_attach_list setup_acquire_array
setup_acquire_list setup_allocate_array setup_allocate_list setup_baseline'''.split()


def percentile(values, p):
    values = sorted(values)
    return values[math.ceil(len(values) * p) - 1]


def main():
    rows = list(csv.DictReader((ROOT / 'raw-editor.csv').open(encoding='utf-8-sig', newline='')))
    assert len(rows) == 5760, len(rows)
    lookup = {}
    for row in rows:
        for key in ('config', 'run', 'order', 'batch', 'iterations', 'elapsed_ticks', 'frequency', 'checksum', 'expected', 'checksum_ok'):
            row[key] = int(row[key])
        c = NAMES.index(row['case'])
        n = (1, 128, 512)[row['config']]
        expected = 0 if c == 0 else (n if c >= 25 else (n * n if c < 14 else 2 * n * n - n))
        assert row['iterations'] == n
        assert row['checksum'] == row['expected'] == expected and row['checksum_ok'] == 1, row
        assert row['elapsed_ticks'] >= 0 and row['frequency'] > 0
        assert row['environment'] == 'Editor-UdonVM' and row['experiment_id'] == 'UDON-INTERDATA-T174'
        # Warmups=5, then phases 5..34; reverse order every phase and every run.
        assert row['order'] == (c if (row['batch'] + 5 + row['run']) % 2 == 0 else 31 - c)
        key = row['config'], row['run'], row['batch'], row['case']
        assert key not in lookup, key
        row['us_op'] = row['elapsed_ticks'] * 1e6 / row['frequency'] / n
        lookup[key] = row
    assert len({r['frequency'] for r in rows}) == 1
    summary = []
    for config in range(3):
        for name in NAMES:
            selected = [lookup[config, run, batch, name] for run in range(2) for batch in range(30)]
            values = [r['us_op'] for r in selected]
            baseline = 'clock' if name == 'clock' else ('setup_baseline' if name.startswith('setup_') else ('state_inline' if name.startswith('state_') else 'call_inline'))
            delta = [r['us_op'] - lookup[config, r['run'], r['batch'], baseline]['us_op'] for r in selected]
            summary.append(dict(config=config, iterations=(1, 128, 512)[config], case=name, samples=len(values),
                                median_us_op=stats.median(values), p95_us_op=percentile(values, .95),
                                min_us_op=min(values), max_us_op=max(values), baseline=baseline,
                                paired_delta_us_op=stats.median(delta),
                                run0_median_us_op=stats.median([r['us_op'] for r in selected if r['run'] == 0]),
                                run1_median_us_op=stats.median([r['us_op'] for r in selected if r['run'] == 1])))
    with (ROOT / 'summary-editor.csv').open('w', encoding='utf-8', newline='') as out:
        writer = csv.DictWriter(out, fieldnames=list(summary[0])); writer.writeheader(); writer.writerows(summary)
    comparisons = [
        ('state_shared_array', 'state_external_fields'), ('state_shared_list', 'state_external_fields'),
        ('state_shared_array', 'state_external_property'), ('state_shared_list', 'state_external_property'),
        ('state_shared_array', 'state_external_methods'), ('state_shared_list', 'state_external_methods'),
        ('state_shared_array', 'state_local_array'), ('state_shared_list', 'state_local_list'),
        ('state_refetch_array', 'state_shared_array'), ('state_refetch_list', 'state_shared_list'),
        ('call_array', 'call_argument_return'), ('call_list', 'call_argument_return'),
        ('call_array_event', 'call_array'), ('call_list_event', 'call_list'),
        ('call_array_argument', 'call_array'), ('call_list_argument', 'call_list'),
        ('call_array_batch', 'call_array'), ('call_array_batch', 'call_argument_return'),
    ]
    ratios = []
    rng = random.Random(174)
    for config in (1, 2):
        for candidate, reference in comparisons:
            pairs = [(lookup[config, run, batch, candidate]['us_op'], lookup[config, run, batch, reference]['us_op']) for run in range(2) for batch in range(30)]
            samples = []
            for _ in range(2000):
                chosen = rng.choices(pairs, k=len(pairs))
                samples.append(stats.median([a for a, _ in chosen]) / stats.median([b for _, b in chosen]))
            ratios.append(dict(config=config, candidate=candidate, reference=reference,
                               median_time_ratio=stats.median([a for a, _ in pairs]) / stats.median([b for _, b in pairs]),
                               bootstrap_low=percentile(samples, .025), bootstrap_high=percentile(samples, .975),
                               run0_time_ratio=stats.median([a for a, _ in pairs[:30]]) / stats.median([b for _, b in pairs[:30]]),
                               run1_time_ratio=stats.median([a for a, _ in pairs[30:]]) / stats.median([b for _, b in pairs[30:]])))
    with (ROOT / 'ratios-editor.csv').open('w', encoding='utf-8', newline='') as out:
        writer = csv.DictWriter(out, fieldnames=list(ratios[0])); writer.writeheader(); writer.writerows(ratios)
    print(json.dumps({'rows': len(rows), 'checksums': 'all pass', 'missing_duplicate_order': 'all pass',
                      'frequency': rows[0]['frequency'], 'raw_sha256': hashlib.sha256((ROOT / 'raw-editor.csv').read_bytes()).hexdigest()}, indent=2))
    for row in summary:
        if row['config'] == 2:
            print(f"{row['case']:28} {row['median_us_op']:8.3f} p95={row['p95_us_op']:.3f} delta={row['paired_delta_us_op']:.3f}")
    for row in ratios:
        if row['config'] == 2:
            print(f"{row['candidate']} / {row['reference']}: {row['median_time_ratio']:.3f} [{row['bootstrap_low']:.3f}, {row['bootstrap_high']:.3f}]")


if __name__ == '__main__':
    main()
