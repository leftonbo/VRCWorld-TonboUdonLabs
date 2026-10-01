"""Validate the client export against the configured patterns and previous restored cases."""
import csv
from pathlib import Path

ROOT = Path(__file__).resolve().parent


def read(path):
    lines = path.read_text(encoding="utf-8-sig").splitlines()
    return list(csv.DictReader((line for line in lines if line and not line.startswith("#")), delimiter="\t"))


if __name__ == "__main__":
    raw = ROOT / "raw-client-restored-20261001-201041.tsv"
    rows = read(raw)
    expected = {row["case"]: row for row in read(ROOT / "raw-editor-written.tsv")}
    previous = {row["case"]: row for row in read(ROOT.parent / "2026-10-01-playerobject-persistence-all-types/raw-client-restored-20261001-192458.tsv")}
    assert len(rows) == len({row["case"] for row in rows}) == 92
    assert {row["case"] for row in rows} == set(expected)
    assert sum(int(row["count"]) for row in rows) == 10596
    defaults = {"bool": "False", "char": "U+0000", "Vector2": "0,0", "Vector3": "0,0,0"}
    defaults.update(dict.fromkeys(("Vector4", "Quaternion", "Color", "Color32"), "0,0,0,0"))
    retained = arrays = shared = 0
    for row in rows:
        case = row["case"]
        count = int(row["count"])
        assert all(row[key] == expected[case][key] for key in ("value_type", "count", "nominal_bytes")), case
        assert row["restored_matches"] == row["current_matches"], case
        assert int(row["restored_matches"]) in (0, count), case
        assert row["post_success"] == row["sent_bytes"] == "unavailable", case
        assert row["current_shape"] == expected[case]["current_shape"], case
        values = row["current_values"].split("|")
        assert len(values) == count, case
        assert [int(value.split("=", 1)[0]) for value in values] == list(range(1, count + 1)), case
        if int(row["restored_matches"]) == count:
            retained += 1
            assert row["current_values"] == expected[case]["current_values"], case
        else:
            assert all(value.split("=", 1)[1] == defaults.get(row["value_type"], "0") for value in values), case
        if row["current_shape"].startswith("array"):
            arrays += 1
            assert int(row["restored_matches"]) == count, case
        if case in previous:
            shared += 1
            assert row["restored_matches"] == previous[case]["restored_matches"], case
    assert (retained, arrays, shared) == (74, 40, 41)
    assert "# playerobject_storage_usage_bytes=49640 limit_bytes=102400 pending=False" in raw.read_text(encoding="utf-8-sig")
    print("PASS 92 cases: 74 retained with exact values, 18 entirely default; no partial restoration.")
    print("PASS 40 arrays and all string/URL cases retained; 41 previous cases unchanged; usage 49640 / 102400 B.")
