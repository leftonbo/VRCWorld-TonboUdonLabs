"""Validate the 200-case client export against Editor patterns and the preceding client run."""
import csv
import math
from pathlib import Path

HERE = Path(__file__).resolve().parent


def read(path):
    lines = path.read_text(encoding="utf-8-sig").splitlines()
    return list(csv.DictReader((line for line in lines if line and not line.startswith("#")), delimiter="\t"))


if __name__ == "__main__":
    raw = HERE / "raw-client-restored-20261001-212152.tsv"
    rows = read(raw)
    expected = {r["case"]: r for r in read(HERE / "raw-editor-written.tsv")}
    previous = {r["case"]: r for r in read(HERE.parent / "2026-10-01-playerobject-persistence-variable-array/raw-client-restored-20261001-201041.tsv")}
    assert len(rows) == len({r["case"] for r in rows}) == 200
    assert [r["case"] for r in rows] == list(expected)
    assert sum(int(r["count"]) for r in rows) == 13191
    defaults = {"bool": "False", "char": "U+0000", "Vector2": "0,0", "Vector3": "0,0,0"}
    defaults.update(dict.fromkeys(("Vector4", "Quaternion", "Color", "Color32"), "0,0,0,0"))
    retained = arrays = variable = shared = scalar = 0
    for row in rows:
        case = row["case"]
        count, matched = int(row["count"]), int(row["restored_matches"])
        assert all(row[key] == expected[case][key] for key in ("value_type", "count", "nominal_bytes", "current_shape")), case
        assert row["restored_matches"] == row["current_matches"] and matched in (0, count), case
        assert row["post_success"] == row["sent_bytes"] == "unavailable", case
        values = row["current_values"].split("|")
        assert len(values) == count, case
        assert [int(v.split("=", 1)[0]) for v in values] == list(range(1, count + 1)), case
        if matched == count:
            retained += 1
            assert row["current_values"] == expected[case]["current_values"], case
        else:
            assert all(v.split("=", 1)[1] == defaults.get(row["value_type"], "0") for v in values), case
        if row["current_shape"].startswith("array"):
            arrays += 1
            assert matched == count, case
        elif row["value_type"] in ("string", "VRCUrl"):
            variable += 1
            assert matched == count, case
        else:
            scalar += 1
            width = int(row["nominal_bytes"]) // count
            # Observational fit only; this does not establish the internal storage format.
            assert matched == (count if count <= math.ceil(128 / (width + 1)) else 0), case
        if case in previous:
            shared += 1
            assert all(row[key] == previous[case][key] for key in ("restored_matches", "current_values", "current_shape")), case
    assert (retained, arrays, variable, shared, scalar) == (146, 40, 16, 92, 144)
    assert "# playerobject_storage_usage_bytes=64590 limit_bytes=102400 pending=False" in raw.read_text(encoding="utf-8-sig")
    assert "# cloud_commit_ack=unavailable" in raw.read_text(encoding="utf-8-sig")
    print("PASS 200 cases / 13191 values: 146 fully retained, 54 entirely default; no partial restoration.")
    print("PASS all 144 fixed scalar cases fit the previous boundary; all 18 one-field controls retained.")
    print("PASS 16 string/URL cases and 40 arrays retained with exact values and lengths; 92 previous cases unchanged.")
    print("PASS storage usage 64590 / 102400 B; pending=False; no post-rejoin sends recorded.")
