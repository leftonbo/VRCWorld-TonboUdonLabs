"""Extract the emitted predicate only, excluding array access/branch/loop."""
import csv
import hashlib
import re
from pathlib import Path

root = Path(__file__).resolve().parent
source = root / "NullCheckBenchmark.uasm"
lines = source.read_text(encoding="utf-8").splitlines()
start = next(i for i, line in enumerate(lines) if line == "# App.NullCheckBenchmark.Run(int)")
checks = [i for i in range(start, len(lines)) if re.search(
    r'EXTERN, "(UnityEngineObject.__op_(Inequality|Implicit)|VRCSDKBaseUtilities.__IsValid)', lines[i])]
assert len(checks) == 15
rows, snippets = [], []
for number, index in enumerate(checks):
    kind = ("gameobject", "material", "transform", "udonbehaviour", "object")[number // 3]
    method = ("ne", "valid", "implicit")[number % 3]
    getter = max(i for i in range(start, index) if 'EXTERN, "SystemObjectArray.__Get__' in lines[i])
    code = [line.strip() for line in lines[getter + 1:index + 1] if line.strip()]
    ops = [line.split(",")[0] for line in code]
    row = dict(type=kind, method=method, first_line=getter + 2, last_line=index + 1,
               instructions=len(ops), push=ops.count("PUSH"), copy=ops.count("COPY"),
               extern=ops.count("EXTERN"))
    assert row["extern"] == 1
    rows.append(row)
    snippets.append(f"{kind}/{method} lines {getter + 2}-{index + 1}\n" + "\n".join(code))
for offset in range(0, 15, 3):
    assert rows[offset + 1]["instructions"] == rows[offset + 2]["instructions"] == 3
    assert rows[offset]["instructions"] == (7 if offset == 12 else 10)
with (root / "assembly-summary.csv").open("w", newline="", encoding="utf-8") as output:
    writer = csv.DictWriter(output, fieldnames=list(rows[0]))
    writer.writeheader()
    writer.writerows(rows)
(root / "predicate-snippets.txt").write_text("\n\n".join(snippets) + "\n", encoding="utf-8")
print("15 predicates verified; ne=10 (base Object=7), valid=3, implicit=3.")
print("UASM SHA256=" + hashlib.sha256(source.read_bytes()).hexdigest())
