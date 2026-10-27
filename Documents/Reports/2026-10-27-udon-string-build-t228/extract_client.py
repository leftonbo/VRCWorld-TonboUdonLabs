"""Extract only T228 messages from a client log; never copy the full personal log."""
import argparse
import re
from pathlib import Path

parser = argparse.ArgumentParser()
parser.add_argument("log", type=Path)
args = parser.parse_args()
text = args.log.read_bytes().decode("utf-8-sig", errors="replace")
chunks = list(re.finditer(r"\[StringBuildCSV:(\d+)\]", text))
assert chunks, "No T228 CSV chunks yet"
end = re.findall(r"\[StringBuildCSV-END\] chars=(\d+) rows=(\d+) failures=(\d+)", text)
assert end, "Client export has not completed"
chars, rows, failures = map(int, end[-1])
offset, joined = 0, []
for match in chunks:
    position = int(match[1])
    assert position == offset, f"Missing, duplicate or reordered chunk at {offset}: {position}"
    # Read exact payload length, including the CRs in the first measured export.
    # This also accepts the revised exporter which normalizes CRLF before chunking.
    length = min(1800, chars - position)
    value = text[match.end():match.end()+length]
    assert "\n" not in value and len(value) == length, f"Truncated chunk at {position}"
    assert text[match.end()+length:].startswith(("\r\n", "\n")), f"Bad chunk boundary at {position}"
    joined.append(value)
    offset += len(value)
assert chars == offset and rows == 9600 and failures == 0
result = "".join(joined).replace("~", "\n")
assert len(result.splitlines()) == rows + 1
root = Path(__file__).resolve().parent
(root / "raw-client.csv").write_text(result, encoding="utf-8", newline="\n")
evidence = [line for line in text.splitlines() if "[StringBuildMeta]" in line or "[StringBuild]" in line or "[StringBuildCSV-END]" in line]
versions = [line.strip() for line in text.splitlines() if re.match(r"\s*(?:VRChat (?:Build|Version)|Unity Version|Operating System|Processor Type|Processor Count|Graphics Device|Screen Resolution|Resolution):", line)]
(root / "client-runtime-evidence.txt").write_text("\n".join(versions[:8] + evidence) + "\n", encoding="utf-8")
print(f"Extracted {rows} rows, {len(chunks)} contiguous chunks, {chars} chars; failures={failures}")
