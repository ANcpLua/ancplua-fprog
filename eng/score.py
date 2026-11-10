"""Score a run per rule: hidden tests (TRX) and false positives in the clean corpus (SARIF).

A rule scores 1 when it has at least one hidden test, all of its hidden tests passed,
and the clean corpus contains no diagnostic with its ID. Test names start with the rule ID,
for example FP0301_ThrowInCore_Reports.
"""

import json
import re
import sys
import xml.etree.ElementTree as ET
from collections import defaultdict
from pathlib import Path

BINDING = [
    "FP0101", "FP0102", "FP0103", "FP0104",
    "FP0201", "FP0202", "FP0203",
    "FP0301", "FP0302", "FP0303",
    "FP0401", "FP0402", "FP0403",
    "FP0501", "FP0502", "FPS001",
    "FP0601", "FP0701", "FP0801", "FP0802",
]
TRX_NS = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}
RULE_PREFIX = re.compile(r"^(FPS?\d{3,4})")


def read_tests(trx_dir: Path) -> dict[str, list[str]]:
    outcomes: dict[str, list[str]] = defaultdict(list)
    for trx in sorted(trx_dir.rglob("*.trx")):
        for result in ET.parse(trx).getroot().iterfind(".//t:UnitTestResult", TRX_NS):
            match = RULE_PREFIX.match(result.get("testName", ""))
            if match:
                outcomes[match.group(1)].append(result.get("outcome", "Unknown"))
    return outcomes


NOT_LOADED = {"CS8032", "CS9057"}  # analyzer could not be created / built against a newer compiler


def read_corpus(sarif_dir: Path) -> tuple[dict[str, int], int, int]:
    false_positives: dict[str, int] = defaultdict(int)
    crashes = 0
    not_loaded = 0
    for sarif in sorted(sarif_dir.rglob("*.sarif")):
        for run in json.loads(sarif.read_text(encoding="utf-8-sig")).get("runs", []):
            for result in run.get("results", []):
                rule = result.get("ruleId", "")
                if rule == "AD0001":
                    crashes += 1
                elif rule in NOT_LOADED:
                    not_loaded += 1
                elif RULE_PREFIX.match(rule):
                    false_positives[rule] += 1
    return false_positives, crashes, not_loaded


def main() -> int:
    if len(sys.argv) != 3:
        print("usage: score.py <trx-dir> <corpus-sarif-dir>", file=sys.stderr)
        return 2
    tests = read_tests(Path(sys.argv[1]))
    corpus_dir = Path(sys.argv[2])
    if not corpus_dir.is_dir():
        print(f"corpus SARIF directory missing: {corpus_dir}", file=sys.stderr)
        return 2
    false_positives, crashes, not_loaded = read_corpus(corpus_dir)
    if not_loaded:
        print("The compiler did not load the analyzer in the corpus build (CS8032/CS9057), so the "
              "corpus result is meaningless. Pin Microsoft.CodeAnalysis.CSharp to the SDK's compiler.",
              file=sys.stderr)
        return 2
    if not any(corpus_dir.rglob("*.sarif")):
        print(f"no SARIF files in {corpus_dir}; did the corpus build run?", file=sys.stderr)
        return 2

    score = 0
    print(f"{'rule':<8} {'tests':>7} {'passed':>7} {'corpus FP':>10}  result")
    for rule in BINDING:
        outcomes = tests.get(rule, [])
        passed = sum(outcome == "Passed" for outcome in outcomes)
        ok = bool(outcomes) and passed == len(outcomes) and false_positives.get(rule, 0) == 0
        score += ok
        print(f"{rule:<8} {len(outcomes):>7} {passed:>7} {false_positives.get(rule, 0):>10}  {'ok' if ok else '-'}")
    print(f"\nscore: {score}/{len(BINDING)}   analyzer crashes in corpus (AD0001): {crashes}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
