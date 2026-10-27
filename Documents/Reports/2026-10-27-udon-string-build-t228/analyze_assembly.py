"""Static evidence only: count emitted instructions per method, including all branches."""
import csv
import json
import re
from collections import Counter
from pathlib import Path

root = Path(__file__).resolve().parent
text = (root / "StringBuildBenchmark-editor-measured.uasm").read_text(encoding="utf-8-sig")
matches = list(re.finditer(r"^# App\.StringBuildBenchmark\.(\w+)\([^\n]*\)", text, re.M))
rows = []
for i, match in enumerate(matches):
    body = text[match.end():matches[i+1].start() if i+1 < len(matches) else len(text)]
    ops = Counter(re.findall(r"^\s+(PUSH|COPY|JUMP|JUMP_IF_FALSE|JUMP_INDIRECT|EXTERN|POP|NOP)(?:,|\s|$)", body, re.M))
    externs = Counter(re.findall(r'EXTERN, "([^"]+)"', body))
    rows.append(dict(method=match[1], instructions=sum(ops.values()), **{k:ops[k] for k in ("PUSH","COPY","JUMP","JUMP_IF_FALSE","JUMP_INDIRECT","EXTERN")},
                     append_string=externs["SystemTextStringBuilder.__Append__SystemString__SystemTextStringBuilder"],
                     append_int=externs["SystemTextStringBuilder.__Append__SystemInt32__SystemTextStringBuilder"],
                     string_concat=sum(n for key,n in externs.items() if key.startswith("SystemString.") and ("__Concat__" in key or "__op_Addition__" in key))))
with (root / "assembly-summary.csv").open("w", encoding="utf-8", newline="") as f:
    writer = csv.DictWriter(f, fieldnames=list(rows[0]))
    writer.writeheader(); writer.writerows(rows)
for row in rows:
    if row["method"].startswith(("Plus", "Sb")):
        print(row)

# Client-log export was added after Editor measurement. Check the final Editor
# compile kept the measured methods' opcode/extern sequences (operands/addresses
# deliberately excluded; this is structural evidence, not bytecode identity).
current = root / "StringBuildBenchmark.uasm"
if current.exists():
    def sequences(source):
        starts = list(re.finditer(r"^# App\.StringBuildBenchmark\.(\w+)\([^\n]*\)", source, re.M))
        found = {}
        for index, start in enumerate(starts):
            body = source[start.end():starts[index+1].start() if index+1 < len(starts) else len(source)]
            found[start[1]] = re.findall(r'^\s+(EXTERN, "[^"]+"|PUSH|COPY|JUMP_IF_FALSE|JUMP_INDIRECT|JUMP|POP|NOP)(?:,|\s|$)', body, re.M)
        return found
    before, after = sequences(text), sequences(current.read_text(encoding="utf-8-sig"))
    names = ["Run", "Baseline", "PlusExpression", "PlusSteps", "SbNewSplit", "SbNewChain",
             "SbCapacitySplit", "SbCapacityChain", "SbReuseSplit", "SbReuseChain"]
    evidence = {name: before[name] == after[name] for name in names}
    (root / "timing-method-structure.json").write_text(json.dumps(evidence, indent=2)+"\n", encoding="utf-8")
    print("Measured/final opcode+extern sequences:", evidence)
