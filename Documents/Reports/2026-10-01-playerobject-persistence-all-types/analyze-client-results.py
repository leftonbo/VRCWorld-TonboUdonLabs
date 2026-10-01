"""Reproduce this export's observations; the 128 model is a fit, not a format specification."""
import csv
from pathlib import Path

ROOT = Path(__file__).resolve().parent
WIDTH = {
    "bool": 1, "byte": 1, "sbyte": 1,
    "short": 2, "ushort": 2, "char": 2,
    "int": 4, "uint": 4, "float": 4, "Color32": 4,
    "long": 8, "ulong": 8, "double": 8, "Vector2": 8,
    "Vector3": 12, "Vector4": 16, "Quaternion": 16, "Color": 16,
}


def read(name):
    lines = (ROOT / name).read_text(encoding="utf-8-sig").splitlines()
    return list(csv.DictReader((line for line in lines if line and not line.startswith("#")), delimiter="\t"))


if __name__ == "__main__":
    rows = read("raw-client-20261001-142619.tsv")
    expected = {row["case"]: row for row in read("raw-editor-written.tsv")}
    assert len(rows) == len({row["case"] for row in rows}) == 111
    assert {row["case"] for row in rows} == set(expected)
    assert len({row["value_type"] for row in rows}) == 20
    retained = fixed = 0
    for row in rows:
        count = int(row["count"])
        restored = int(row["restored_matches"])
        assert restored in (0, count), row["case"]
        assert int(row["current_matches"]) == count, row["case"]
        assert row["post_success"] == "True" and int(row["sent_bytes"]) > 0, row["case"]
        assert row["current_values"] == expected[row["case"]]["current_values"], row["case"]
        retained += restored == count
        if row["value_type"] in WIDTH and row["case"] != "int[27] 配列":
            fixed += 1
            predicted = (count - 1) * (WIDTH[row["value_type"]] + 1) < 128
            assert predicted == (restored == count), row["case"]
    assert retained == 58 and fixed == 102
    print("PASS 111 cases: restored 58, zero matches 53; current patterns and sends 111/111.")
    print("FIT ONLY: 128 model matches all 102 fixed-width scalar cases; internal cause unverified.")
    immediate = read("raw-client-restored-20261001-192458.tsv")
    previous = {row["case"]: row for row in rows}
    assert len(immediate) == 111 and {row["case"] for row in immediate} == set(previous)
    defaults = {"bool": "False", "char": "U+0000", "Vector2": "0,0", "Vector3": "0,0,0"}
    defaults.update(dict.fromkeys(("Vector4", "Quaternion", "Color", "Color32"), "0,0,0,0"))
    for row in immediate:
        old = previous[row["case"]]
        for key in ("value_type", "count", "nominal_bytes", "restored_matches"):
            assert row[key] == old[key], (row["case"], key)
        assert row["restored_matches"] == row["current_matches"], row["case"]
        assert row["post_success"] == row["sent_bytes"] == "unavailable", row["case"]
        if row["restored_matches"] == row["count"]:
            assert row["current_values"] == old["current_values"], row["case"]
        else:
            values = row["current_values"].split("|")
            assert len(values) == int(row["count"]), row["case"]
            assert all(value.split("=", 1)[1] == defaults.get(row["value_type"], "0") for value in values), row["case"]
    print("PASS immediate re-entry: same 58 retained / 53 failed; failed fields all default, no partial values; send results unavailable.")
