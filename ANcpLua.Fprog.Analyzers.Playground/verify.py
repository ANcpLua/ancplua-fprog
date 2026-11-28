#!/usr/bin/env python3
"""Build the paired C# fixtures using only the requested NuGet analyzer package."""
import argparse
import base64
from collections import Counter
import hashlib
import json
import os
from pathlib import Path
import re
import runpy
import shutil
import subprocess
import sys
import tempfile
from urllib.parse import unquote
import xml.etree.ElementTree as ET
import zipfile

HERE = Path(__file__).resolve().parent
ROOT = HERE.parent
PACKAGE = "ANcpLua.Fprog.Analyzers"
PROJECT = HERE.name + ".csproj"
ANALYZER = "analyzers/dotnet/cs/ancplua-fprog.dll"
MARKER = "contentFiles/cs/any/Fprog/FprogAttributes.cs"
OFFICIAL = "https://api.nuget.org/v3/index.json"
CASES = ("HappyPath", "UnhappyPath")
VERSION = re.compile(r"[0-9]+\.[0-9]+\.[0-9]+(?:-[0-9A-Za-z.-]+)?")


def require(condition, message):
    if not condition:
        raise ValueError(message)


def run(args, cwd, env, success=True):
    result = subprocess.run(args, cwd=cwd, env=env, stdout=subprocess.PIPE,
                            stderr=subprocess.STDOUT, text=True, timeout=300)
    if success and result.returncode:
        raise ValueError(f"{' '.join(map(str, args))} failed ({result.returncode})\n{result.stdout[-8000:]}")
    return result


def inspect_package(archive, expected_commit):
    with zipfile.ZipFile(archive) as package:
        names = package.namelist()
        require(len(names) == len(set(names)), "Package contains duplicate paths")
        specs = [name for name in names if name.endswith(".nuspec")]
        require(len(specs) == 1, "Expected exactly one nuspec")
        metadata = ET.fromstring(package.read(specs[0])).find("{*}metadata")
        require(metadata is not None and metadata.findtext("{*}id") == PACKAGE, "Unexpected package ID")
        version = metadata.findtext("{*}version") or ""
        require(VERSION.fullmatch(version), f"Expected an exact package version, got {version!r}")
        require(not metadata.findall(".//{*}dependency"), "The analyzer must have no NuGet dependencies")
        require(metadata.findtext("{*}developmentDependency") == "true", "Expected a build-time development dependency")
        repository = metadata.find("{*}repository")
        require(repository is not None and repository.get("url") == "https://github.com/ANcpLua/ancplua-fprog",
                "Missing canonical repository in nuspec")
        commit = repository.get("commit", "")
        if expected_commit:
            require(commit == expected_commit, f"Expected repository commit {expected_commit}, got {commit!r}")
        # NuGet.org adds a repository signature. Compare the contents that define
        # the product, including its nuspec, independently of ZIP timestamps and OPC metadata.
        payload = sorted(name for name in names if not name.endswith("/") and not (
            name in {".signature.p7s", "[Content_Types].xml", "_rels/.rels"} or
            name.startswith("package/services/metadata/core-properties/")))
        require(set(payload) == {specs[0], ANALYZER, MARKER, "README.md", "LICENSE"},
                f"Unexpected package payload: {payload}")
        digest = hashlib.sha256()
        for name in payload:
            for field in (name.encode(), package.read(name)):
                digest.update(len(field).to_bytes(8, "big"))
                digest.update(field)
        return version, commit, digest.hexdigest()


def configured_feed(path, source):
    root = ET.Element("configuration")
    sources = ET.SubElement(root, "packageSources")
    ET.SubElement(sources, "clear")
    ET.SubElement(sources, "add", key="playground", value=source)
    ET.ElementTree(root).write(path, encoding="utf-8", xml_declaration=True)


def stage(workspace):
    shutil.copy2(ROOT / "global.json", workspace)
    project = workspace / HERE.name
    project.mkdir()
    shutil.copy2(HERE / PROJECT, project)
    shutil.copy2(HERE / "Directory.Build.props", project)
    (project / "ReferencedTypes").mkdir()
    shutil.copy2(HERE / "ReferencedTypes/ReferencedTypes.csproj", project / "ReferencedTypes")
    demo = workspace / "demo"
    demo.mkdir()
    for file in (".globalconfig", "count.py"):
        shutil.copy2(ROOT / "demo" / file, demo)
    for group in (*CASES, "ReferencedTypes"):
        folder = demo / group
        folder.mkdir()
        for file in (ROOT / "demo" / group).glob("*.cs"):
            shutil.copy2(file, folder)
    return project


def active_rules():
    active = set()
    for name in ("Shipped", "Unshipped"):
        table = ROOT / "ancplua-fprog" / f"AnalyzerReleases.{name}.md"
        section = None
        for line in table.read_text(encoding="utf-8").splitlines():
            if line.startswith("### "):
                section = line[4:].strip()
            match = re.match(r"\s*(FP\d{4})\s*\|", line)
            if match:
                if section == "Removed Rules":
                    active.discard(match[1])
                elif section in {"New Rules", "Changed Rules"}:
                    active.add(match[1])
                else:
                    raise ValueError(f"Unexpected rule section in {table}: {section}")
    require(active, "No active analyzer IDs found")
    return active


def expected_rows(demo, case):
    # Use the same // expect: parser as the source-project tests, on the actual copied inputs.
    marked = runpy.run_path(str(demo / "count.py"))["expected"](case)
    return Counter({(case, name, line, rule): count
                    for name, entries in marked.items() for (line, rule), count in entries.items()})


def read_sarif(path):
    require(path.is_file(), f"The compiler did not produce {path.name}")
    found = Counter()
    data = json.loads(path.read_text(encoding="utf-8-sig"))
    require(data.get("runs"), f"No compiler runs in {path.name}")
    for report in data["runs"]:
        for item in report.get("results", []):
            code = item.get("ruleId", "<missing ID>")
            if item.get("suppressions") or (not code.startswith("FP") and item.get("level") not in {"warning", "error"}):
                continue
            require(item.get("level") == "error", f"{code} was not enforced as an error")
            locations = item.get("locations", [])
            require(locations, f"{code} produced no source location in {path.name}")
            location = locations[0]["physicalLocation"]
            parts = unquote(location["artifactLocation"]["uri"]).replace("\\", "/").split("/")
            found[(parts[-2] if len(parts) > 1 else "", parts[-1], location["region"]["startLine"], code)] += 1
    return found


def verify_restore(workspace, project, version, config, props, env, source, candidate):
    run(["dotnet", "restore", project / PROJECT, "--configfile", config, "--no-http-cache", "--disable-build-servers", *props], workspace, env)
    install = workspace / "packages" / PACKAGE.lower() / version.lower()
    archive = install / f"{PACKAGE.lower()}.{version.lower()}.nupkg"
    require(archive.is_file(), f"Missing exact restored archive: {archive}")
    metadata = json.loads((install / ".nupkg.metadata").read_text(encoding="utf-8"))
    recorded_source = metadata["source"]
    raw_hash = base64.b64encode(hashlib.sha512(archive.read_bytes()).digest()).decode()
    transport_hash = archive.with_suffix(".nupkg.sha512").read_text(encoding="utf-8").strip()
    require(raw_hash == transport_hash, "NuGet transport checksum does not match the restored archive")
    if candidate:
        require(Path(recorded_source).resolve() == Path(source).resolve(), f"Unexpected package source {recorded_source}")
        require(archive.read_bytes() == candidate.read_bytes(), "The restored nupkg differs from the local candidate")
    else:
        require(recorded_source == OFFICIAL, f"Unexpected package source {recorded_source}")
        run(["dotnet", "nuget", "verify", "--all", archive], workspace, env)
    for folder in (project, project / "ReferencedTypes"):
        assets = json.loads((folder / "obj/project.assets.json").read_text(encoding="utf-8"))
        key = f"{PACKAGE}/{version}"
        target = assets["targets"]["net11.0"][key]
        require(not target.get("compile") and not target.get("runtime"), "Analyzer unexpectedly supplies runtime/compile assets")
        require(ANALYZER in target.get("analyzers", {}), "No recorded analyzer asset for the exact restored version")
        require(target["contentFiles"][MARKER]["buildAction"] == "Compile", "Package did not inject its marker source")
        # NuGet's package content hash omits the repository-signing envelope.
        require(assets["libraries"][key]["sha512"] == metadata["contentHash"],
                "project.assets.json does not match NuGet's restored package content hash")
    with zipfile.ZipFile(archive) as package:
        for name in (ANALYZER, MARKER):
            require((install / name).read_bytes() == package.read(name), f"Restored {name} differs from packaged bytes")
    resolved = run(["dotnet", "msbuild", project / PROJECT, "-nologo", "-nodeReuse:false",
                    "-t:ResolveLockFileAnalyzers;RunProduceContentAssets", "-getItem:Analyzer,Compile", *props],
                   project, env)
    items = json.loads(resolved.stdout)["Items"]
    for kind, file in (("Analyzer", ANALYZER), ("Compile", MARKER)):
        paths = [Path(item["FullPath"]).resolve() for item in items[kind]]
        require(paths.count((install / file).resolve()) == 1, f"MSBuild did not select the packaged {kind} item: {file}")
    return archive


def build_case(project, case, props, env, expected):
    result = run(["dotnet", "build", project / PROJECT, "-c", "Release", "--no-restore", "--no-incremental",
                  "--disable-build-servers", "-v", "quiet", *props, f"-p:PlaygroundCase={case}"], project, env, success=False)
    consumer_log = project / "obj" / f"{case}.sarif"
    helper_log = project / "ReferencedTypes" / "obj" / "ReferencedTypes.sarif"
    if not consumer_log.is_file() or not helper_log.is_file():
        raise ValueError(f"{case}: build exited {result.returncode} before all compiler logs were written\n{result.stdout[-4000:]}")
    helper = read_sarif(helper_log)
    require(not helper, f"Unexpected diagnostic(s) building the metadata fixture: {list(helper.elements())}")
    got = read_sarif(consumer_log)
    # Non-compiler MSBuild/NuGet errors may never enter a SARIF log.
    accepted = {rule for _, _, _, rule in expected}
    extra_codes = set(re.findall(r"\b(?:warning|error) ([A-Z]+\d+)\s*:", result.stdout)) - accepted
    if result.returncode != (1 if expected else 0) or got != expected or extra_codes:
        raise ValueError(f"{case}: build exit {result.returncode}, unexpected build codes {sorted(extra_codes)}\n"
                         f"Missing: {list((expected - got).elements())}\n"
                         f"Unexpected: {list((got - expected).elements())}\n{result.stdout[-4000:]}")
    print(f"{case}: build exit {result.returncode}, {sum(got.values())} exact diagnostics", flush=True)


def verify(args, workspace):
    project = stage(workspace)
    child_temp = workspace / "tmp"
    child_temp.mkdir()
    env = dict(os.environ, TMPDIR=str(child_temp), TMP=str(child_temp), TEMP=str(child_temp), NUGET_PACKAGES=str(workspace / "packages"), NUGET_HTTP_CACHE_PATH=str(workspace / "http"),
               NUGET_PLUGINS_CACHE_PATH=str(workspace / "plugin-cache"), NUGET_SCRATCH=str(workspace / "nuget-scratch"),
               DOTNET_CLI_HOME=str(workspace / "cli"), DOTNET_CLI_TELEMETRY_OPTOUT="1", DOTNET_NOLOGO="1",
               DOTNET_CLI_UI_LANGUAGE="en-US", DOTNET_SKIP_FIRST_TIME_EXPERIENCE="1", MSBUILDDISABLENODEREUSE="1")
    sdk = run(["dotnet", "--version"], workspace, env).stdout.strip()
    require(sdk == json.loads((workspace / "global.json").read_text())["sdk"]["version"], "Unexpected consumer SDK")
    if args.package:
        candidate = args.package.resolve(strict=True)
        version, _, _ = inspect_package(candidate, args.expected_commit)
        feed = workspace / "feed"
        feed.mkdir()
        shutil.copyfile(candidate, feed / f"{PACKAGE}.{version}.nupkg")
        source = str(feed)
    else:
        candidate = None
        version = args.nuget
        require(VERSION.fullmatch(version), f"Expected an exact NuGet version, got {version!r}")
        source = OFFICIAL
    cfg = workspace / "NuGet.Config"
    configured_feed(cfg, source)
    props = [f"-p:PlaygroundVersion={version}", "-p:NuGetAudit=false"]
    archive = verify_restore(workspace, project, version, cfg, props, env, source, candidate)
    restored, commit, payload = inspect_package(archive, args.expected_commit)
    require(restored == version, f"Expected {version}, restored {restored}")
    if args.expected_payload_sha256:
        require(payload == args.expected_payload_sha256, "Package payload SHA256 does not match the verified candidate")
    print(f"{PACKAGE} {version}, SDK {sdk}, repository commit {commit}, source {source}", flush=True)
    names = [{file.name for file in (workspace / "demo" / case).glob("*.cs")} for case in CASES]
    require(names[0] == names[1], f"Missing paired fixture(s): {sorted(names[0] ^ names[1])}")
    happy = expected_rows(workspace / "demo", CASES[0])
    unhappy = expected_rows(workspace / "demo", CASES[1])
    require(not happy, "HappyPath fixtures may not expect diagnostics")
    rules = active_rules()
    covered = {rule for _, _, _, rule in unhappy if rule.startswith("FP")}
    require(covered == rules, f"Expected examples for exactly the active rules; missing={sorted(rules-covered)}, extra={sorted(covered-rules)}")
    build_case(project, CASES[0], props, env, happy)
    build_case(project, CASES[1], props, env, unhappy)
    for rule in sorted(rules):
        matches = [(name, line, count) for (_, name, line, code), count in unhappy.items() if code == rule]
        controls = sorted((name, line) for name, line, _ in matches if name.upper().startswith(rule + "_"))
        require(controls, f"{rule} has no dedicated example with its paired clean counterpart")
        name, line = controls[0]
        print(f"{rule}  clean=0  expected={sum(count for _, _, count in matches)}  verified={name}:{line}")
    print(f"PLAYGROUND_PAYLOAD_SHA256={payload}")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    src = parser.add_mutually_exclusive_group(required=True)
    src.add_argument("--package", type=Path, help="Exact local release candidate nupkg")
    src.add_argument("--nuget", metavar="VERSION", help="Exact published stable/prerelease version on NuGet.org")
    parser.add_argument("--expected-commit", help="Require this exact nuspec repository commit")
    parser.add_argument("--expected-payload-sha256", help="Require this exact normalized payload digest")
    args = parser.parse_args()
    try:
        # Short paths also leave sufficient room for SDK Unix-domain sockets on macOS.
        with tempfile.TemporaryDirectory(prefix="fprog-playground-", dir="/tmp" if sys.platform == "darwin" else None) as temp:
            verify(args, Path(temp))
    except (OSError, ValueError, KeyError, zipfile.BadZipFile, ET.ParseError, subprocess.SubprocessError) as e:
        parser.exit(1, f"Playground failed: {e}\n")


if __name__ == "__main__":
    main()
