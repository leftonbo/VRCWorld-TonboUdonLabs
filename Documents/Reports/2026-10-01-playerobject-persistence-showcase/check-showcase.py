"""Check the recorded showcase matrix, retained identities and Editor output."""
import csv
import re
from pathlib import Path

HERE = Path(__file__).resolve().parent
ARCHIVED = HERE.parents[1] / "Archived"
raw = (HERE / "raw-editor-written.tsv").read_text(encoding="utf-8-sig")
rows = list(csv.DictReader((line for line in raw.splitlines() if not line.startswith("#")), delimiter="\t"))
types = ["int", "long", "float", "double", "bool", "string", "short", "byte", "sbyte", "ushort", "uint", "ulong", "char", "Vector2", "Vector3", "Vector4", "Quaternion", "Color", "Color32", "VRCUrl"]
cases = [(r["case"], int(r["count"]), types.index(r["value_type"]), int(r["nominal_bytes"]),
          r["case"], r["case"].startswith("JSON"), r["current_shape"].startswith("array")) for r in rows]
assert len(cases) == 200 and len({c[0] for c in cases}) == 200
assert sum(c[1] for c in cases) == 13191
widths = {4: 1, 7: 1, 8: 1, 6: 2, 9: 2, 12: 2, 0: 4, 10: 4, 2: 4, 18: 4,
          1: 8, 11: 8, 3: 8, 13: 8, 14: 12, 15: 16, 16: 16, 17: 16}
maximum = {1: 64, 2: 43, 4: 26, 8: 15, 12: 10, 16: 8}
fixed = cases[:144]
assert all(not c[6] and c[2] in widths and c[3] == c[1] * widths[c[2]] for c in fixed)
assert [widths[c[2]] for c in fixed] == sorted(widths[c[2]] for c in fixed)
for kind, width in widths.items():
    boundary = maximum[width]
    assert [c[1] for c in fixed if c[2] == kind] == [1, *range(boundary - 3, boundary + 4)]
assert all(c[2] in (5, 19) and not c[6] for c in cases[144:160])
elements = [1, 15, 16, 26, 27, 28, 31, 32, 33, 63, 64, 65, 127, 128, 129, 255, 256, 257, 512, 1024]
for start, kind in ((160, 0), (180, 1)):
    assert [(c[1], c[2], c[6]) for c in cases[start:start + 20]] == [(n, kind, True) for n in elements]

def schema(path):
    rows = re.findall(r'^(.+) fields=(\d+) values=(\d+) nominalBytes=(-?\d+) networkId=(\d+) programGuid=([0-9a-f]+)$',
                      path.read_text(encoding="utf-8-sig"), re.M)
    return {row[0]: tuple(row[1:]) for row in rows}

current = schema(HERE / "editor-validation.txt")
old = schema(ARCHIVED / "2026-10-01-playerobject-persistence-variable-array/editor-validation.txt")
assert len(current) == 200 and len(old) == 92
assert all(current[label] == values for label, values in old.items()), "Retained schema/identity changed"
assert len({row[3] for row in current.values()}) == 200
for _, count, _, size, label, _, array in cases:
    row = current[label]
    assert tuple(map(int, row[:3])) == (1 if array else count, count, size)
new_ids = {row[3] for label, row in current.items() if label not in old}
historical_ids = set()
for path in ARCHIVED.glob("*playerobject*/editor-validation.txt"):
    historical_ids.update(row[3] for row in schema(path).values())
assert not new_ids.intersection(historical_ids), "A historical Network ID was reused"

assert len(rows) == 200 and [r["case"] for r in rows] == [c[4] for c in cases]
for row, case in zip(rows, cases):
    assert int(row["count"]) == case[1]
    assert int(row["restored_matches"]) == 0 and int(row["current_matches"]) == case[1]
    assert row["post_success"].lower() == "true"
assert "cloud_commit_ack=unavailable" in raw
print("PASS 200 cases / 13191 values; fixed 1 + maximum +/-3; byte-width order; variable/array tail;")
print("PASS 92 retained schemas, Network IDs and Program GUIDs; 108 new IDs avoid historical IDs;")
print("PASS Editor TSV order, initial snapshots, all current matches and post-send success (not client persistence).")
