#!/usr/bin/env python3
"""Builds the demo and counts what the compiler and the ancplua-fprog analyzers report, file by file.

Usage:  python3 count.py            build, print the table, exit 1 when a file differs from its expectation
        python3 count.py --no-build read the SARIF logs of the last build
        python3 count.py --json     print the counts as JSON
        python3 count.py --expected print only what the "// expect:" comments ask for, without building

Expectation: a line ending in "// expect: FP0302" (several IDs separated by commas) must get exactly these
diagnostics; every other line must get none. HappyPath and KnownGaps have no such comment, so they must be clean.
"""
import json
import os
import re
import subprocess
import sys
from collections import Counter, defaultdict
from urllib.parse import unquote, urlparse

HERE = os.path.dirname(os.path.abspath(__file__))
PROJECTS = ["HappyPath", "UnhappyPath", "KnownGaps"]
EXPECT = re.compile(r"//\s*expect:\s*([A-Z0-9, ]+?)\s*$")
LEVELS = {"error": "errors", "warning": "warnings", "note": "infos"}
BUILD_COUNT = re.compile(r"^(\d+) (Warning|Error)\(s\)$")


def build():
    command = ["dotnet", "build", os.path.join(HERE, "Demo.slnx"), "-c", "Release", "--no-incremental", "-v", "quiet", "-nologo"]
    result = subprocess.run(command, capture_output=True, text=True)
    if result.returncode != 0:
        sys.stderr.write(result.stdout)
        sys.stderr.write(result.stderr)
        raise SystemExit(f"dotnet build failed (exit {result.returncode}).")
    tail = [line for line in result.stdout.splitlines() if "Warning(s)" in line or "Error(s)" in line]
    return [line.strip() for line in tail]


def expected(project):
    marks = defaultdict(Counter)
    folder = os.path.join(HERE, project)
    for name in sorted(os.listdir(folder)):
        if not name.endswith(".cs"):
            continue
        marks[name]  # every file gets a row
        with open(os.path.join(folder, name), encoding="utf-8") as source:
            for number, line in enumerate(source, start=1):
                match = EXPECT.search(line)
                if match:
                    for rule in match.group(1).replace(" ", "").split(","):
                        marks[name][(number, rule)] += 1
    return marks


def reported(project):
    path = os.path.join(HERE, "artifacts", project + ".sarif")
    found = defaultdict(Counter)
    levels = defaultdict(Counter)
    if not os.path.exists(path):
        raise SystemExit(f"{path} is missing: build the demo first (run without --no-build).")
    with open(path, encoding="utf-8") as log:
        sarif = json.load(log)
    for run in sarif.get("runs", []):
        for result in run.get("results", []):
            locations = result.get("locations") or []
            if not locations:
                continue
            physical = locations[0].get("physicalLocation", {})
            uri = physical.get("artifactLocation", {}).get("uri", "")
            file_path = unquote(urlparse(uri).path) if uri.startswith("file:") else uri
            if os.path.basename(os.path.dirname(file_path)) != project:
                continue
            name = os.path.basename(file_path)
            line = physical.get("region", {}).get("startLine", 0)
            rule = result.get("ruleId", "?")
            if result.get("suppressions"):
                continue
            found[name][(line, rule)] += 1
            levels[name][LEVELS.get(result.get("level", "warning"), "warnings")] += 1
    return found, levels


def print_expected():
    total = 0
    for project in PROJECTS:
        marks = expected(project)
        count = sum(sum(file_marks.values()) for file_marks in marks.values())
        total += count
        print(f"{project}: 0 errors, {count} warnings, 0 infos expected")
        for name, file_marks in sorted(marks.items()):
            ids = Counter(rule for (_, rule), number in file_marks.items() for _ in range(number))
            listed = ", ".join(f"{rule} x{number}" for rule, number in sorted(ids.items())) or "-"
            print(f"  {name:<48} W{sum(ids.values())}  {listed}")
    print(f"\nExpected line of `dotnet build Demo.slnx`: {total} Warning(s), 0 Error(s)")


def main():
    arguments = set(sys.argv[1:])
    if "--expected" in arguments:
        print_expected()
        return 0
    build_lines = []
    if "--no-build" not in arguments:
        build_lines = build()

    summary = {"build": build_lines, "projects": {}}
    all_as_expected = True
    for project in PROJECTS:
        marks = expected(project)
        found, levels = reported(project)
        rows = []
        for name in sorted(set(marks) | set(found)):
            want, got = marks.get(name, Counter()), found.get(name, Counter())
            missing = sorted((want - got).elements())
            unexpected = sorted((got - want).elements())
            rows.append({
                "file": name,
                "errors": levels[name]["errors"],
                "warnings": levels[name]["warnings"],
                "infos": levels[name]["infos"],
                "ids": dict(sorted(Counter(rule for (_, rule), count in got.items() for _ in range(count)).items())),
                "missing": [f"{rule} line {line}" for line, rule in missing],
                "unexpected": [f"{rule} line {line}" for line, rule in unexpected],
                "as_expected": not missing and not unexpected,
            })
            all_as_expected &= rows[-1]["as_expected"]
        summary["projects"][project] = {
            "errors": sum(row["errors"] for row in rows),
            "warnings": sum(row["warnings"] for row in rows),
            "infos": sum(row["infos"] for row in rows),
            "files": rows,
        }
    build_mismatches = []
    for line in build_lines:
        match = BUILD_COUNT.fullmatch(line)
        if match:
            level = match[2].lower() + "s"
            displayed = sum(project[level] for project in summary["projects"].values())
            if displayed != int(match[1]):
                build_mismatches.append(f"Build reports {match[1]} {level}. The demo files account for {displayed}.")

    all_as_expected &= not build_mismatches
    summary["build_mismatches"] = build_mismatches
    summary["as_expected"] = all_as_expected

    if "--json" in arguments:
        print(json.dumps(summary, indent=2))
    else:
        for line in build_lines:
            print("dotnet build:", line)
        for mismatch in build_mismatches:
            print(mismatch)
        for project, data in summary["projects"].items():
            print(f"\n{project}: {data['errors']} errors, {data['warnings']} warnings, {data['infos']} infos")
            for row in data["files"]:
                ids = ", ".join(f"{rule} x{count}" for rule, count in row["ids"].items()) or "-"
                state = "ok      " if row["as_expected"] else "MISMATCH"
                print(f"  {state} {row['file']:<48} E{row['errors']} W{row['warnings']} I{row['infos']}  {ids}")
                for item in row["missing"]:
                    print(f"           expected but not reported: {item}")
                for item in row["unexpected"]:
                    print(f"           reported but not expected: {item}")
        print("\nRESULT:", "everything as expected" if all_as_expected else "MISMATCH, see above")
    return 0 if all_as_expected else 1


if __name__ == "__main__":
    sys.exit(main())
