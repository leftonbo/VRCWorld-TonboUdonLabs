"""Validate and summarize the Udon CSV. Stdlib only; rejects partial/failed runs.
Usage: python analyze.py raw-editor.csv [raw-client.csv]
"""
import csv
import math
import statistics as st
import sys
from collections import defaultdict
from pathlib import Path

HEADER = 'experiment_id,environment,config,run,case,order,batch,iterations,entries,operations,elapsed_ticks,frequency,checksum,expected,checksum_ok'


def baseline(case):
    if case == 'clock':
        return 'clock'
    if case.startswith('comm_'):
        return 'comm_inline'
    if case in ('dict_string_build', 'dict_int_build', 'dict_build_baseline'):
        return 'dict_build_baseline'
    return 'dict_baseline'


def analyze(path):
    rows = list(csv.DictReader(path.open(encoding='utf-8-sig')))
    assert len(rows) == 4 * 2 * 30 * 31, ('incomplete run', len(rows))
    groups = defaultdict(list)
    indexed = {}
    for row in rows:
        for name in HEADER.split(','):
            if name not in ('experiment_id', 'environment', 'case'):
                row[name] = int(row[name])
        assert row['checksum_ok'] == 1 and row['checksum'] == row['expected'], row
        assert row['elapsed_ticks'] >= 0 and row['frequency'] > 0, row
        assert row['iterations'] == (256 if row['config'] % 2 == 0 else 512)
        assert row['entries'] == (128 if row['config'] < 2 else 1024)
        key = (row['config'], row['run'], row['batch'], row['case'])
        assert key not in indexed, ('duplicate', key)
        indexed[key] = row
        row['ms'] = row['elapsed_ticks'] * 1000 / row['frequency']
        groups[row['config'], row['run'], row['case']].append(row)
    assert len({r['environment'] for r in rows}) == 1
    assert {r['config'] for r in rows} == set(range(4))
    assert {r['run'] for r in rows} == {0, 1}
    assert len(groups) == 4 * 2 * 31
    summary = []
    for (config, run, case), items in sorted(groups.items()):
        assert len(items) == 30
        assert {x['batch'] for x in items} == set(range(30))
        times = sorted(x['ms'] for x in items)
        ops = items[0]['operations']
        delta = []
        for x in items:
            b = indexed[config, run, x['batch'], baseline(case)]
            delta.append(x['ms']/ops - b['ms']/b['operations'])
        # Key generation has a different checksum workload: arithmetic baseline is descriptive only.
        summary.append(dict(config=config, run=run, case=case, n=items[0]['iterations'], entries=items[0]['entries'],
            samples=len(items), median_ms=st.median(times), p95_ms=times[math.ceil(.95*len(times))-1],
            min_ms=times[0], max_ms=times[-1], raw_ms_op=st.median(times)/ops, raw_us_op=st.median(times)*1000/ops,
            paired_baseline_delta_ms_op='' if case == 'key_string_generate' else st.median(delta),
            paired_baseline_delta_us_op='' if case == 'key_string_generate' else st.median(delta)*1000,
            baseline='not-equivalent' if case == 'key_string_generate' else baseline(case)))
    output = path.with_name(path.stem.replace('raw-', 'summary-')+'.csv')
    with output.open('w', newline='', encoding='utf-8') as f:
        writer = csv.DictWriter(f, summary[0].keys())
        writer.writeheader(); writer.writerows(summary)
    print(path.name, 'PASS', len(rows), 'rows;', len(groups), 'groups;', output.name)
    for case in ('comm_inline','comm_argument_return','comm_fields_direct','comm_array_direct','comm_list_direct',
                 'comm_array_event','comm_list_event','comm_array_batch','dict_string_get','dict_int_get',
                 'dict_string_update','dict_int_update','dict_string_build','dict_int_build','dict_string_to_id_get',
                 'key_string_generate','key_int_generate'):
        selected = [x for x in summary if x['case'] == case]
        print(case, 'us/op range', round(min(x['raw_us_op'] for x in selected),4), round(max(x['raw_us_op'] for x in selected),4))
    return summary


def extract(log, output):
    lines = log.read_text(encoding='utf-8', errors='replace').splitlines()
    end = [i for i, line in enumerate(lines) if '[SharedData] EXPORT_END rows=7440' in line]
    assert end, 'No complete export marker'
    end = end[-1]
    rows = [line.split('[SharedDataCSV] ',1)[1] for line in lines[:end] if '[SharedDataCSV] ' in line]
    assert len(rows) >= 7440
    output.write_text(HEADER+'\n'+'\n'.join(rows[-7440:])+'\n', encoding='utf-8')
    analyze(output)


if __name__ == '__main__':
    if len(sys.argv) == 4 and sys.argv[1] == '--extract':
        extract(Path(sys.argv[2]), Path(sys.argv[3]))
    else:
        assert len(sys.argv) > 1, __doc__
        for arg in sys.argv[1:]:
            analyze(Path(arg))
